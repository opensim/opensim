using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenSim.Region.Framework.Scenes
{
    public class LinksetData
    {
        public const int LINKSETDATA_MAX = 131072;

        private static readonly object linksetDataLock = new object();

        public LinksetData()
        {
            Data = new SortedList<string, LinksetDataEntry>();

            LinksetDataBytesFree = LINKSETDATA_MAX;
            LinksetDataBytesUsed = 0;
        }

        public SortedList<string, LinksetDataEntry> Data { get; private set; } = null;

        public int LinksetDataBytesFree { get; private set; } = LINKSETDATA_MAX;
        public int LinksetDataBytesUsed { get; private set; } = 0;

        // Deep Copy of Linkset Data
        public LinksetData Copy()
        {
            lock (linksetDataLock)
            {
                var copy = new LinksetData();
                foreach (var entry in Data)
                {
                    var key = String.Copy(entry.Key);
                    var val = entry.Value.Copy();
                    copy.Data.Add(key, val);
                    copy.LinksetDataAccountingDelta(val.GetCost(key));
                }

                return copy;
            }
        }

        /// <summary>
        /// Adds or updates a entry to linkset data
        /// </summary>
        /// <returns>
        /// -1 if the password did not match
        /// -1 is the data was protected
        /// 0 if the data was successfully added or updated
        /// 1 if the data could not be added or updated due to memory
        /// 2 if the data is unchanged
        /// </returns>
        public int AddOrUpdateLinksetDataKey(string key, string value, string pass)
        {
            lock (linksetDataLock)
            {
                if (LinksetDataOverLimit())
                    return 1;

                LinksetDataEntry entry = null;
                if (Data.TryGetValue(key, out entry))
                {
                    if (!entry.CheckPassword(pass))
                        return -1;

                    if (entry.Value == value)
                        return 2;

                    // Subtract out the old entry
                    LinksetDataAccountingDelta(-entry.GetCost(key));
                }

                // Add New or Update handled here.
                LinksetDataEntry newEntry = new LinksetDataEntry(value, pass);
                Data[key] = newEntry;

                // Add the cost for the newply created entry
                LinksetDataAccountingDelta(newEntry.GetCost(key));

                return 0;
            }
        }

        /// <summary>
        /// Deletes a named key from the key value store
        /// </summary>
        /// <param name="key">The key value we're removing</param>
        /// <param name="pass">The password for a protected field (or string.Empty if not protected)</param>
        /// <returns>
        /// 0 if successful.
        /// 1 if not due to the password.
        /// -1 if no such key was found
        /// </returns>
        public int DeleteLinksetDataKey(string key, string pass)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return -1;

                LinksetDataEntry entry;
                if (!Data.TryGetValue(key, out entry))
                    return -1;

                if (!entry.CheckPassword(pass))
                    return 1;

                Data.Remove(key);
                LinksetDataAccountingDelta(-entry.GetCost(key));

                return 0;
            }
        }

        public void DeserializeLinksetData(string data)
        {
            if (data == null || data.Length == 0)
                return;

            //? Need to adjust accounting
            lock (linksetDataLock)
            {
                Data = JsonSerializer.Deserialize<SortedList<string, LinksetDataEntry>>(data);
            }
        }

        /// <summary>
        /// FindLinksetDataKeys - Given a Regex pattern and start, count return the
        /// list of matchingkeys in the LinksetData store.
        /// </summary>
        /// <param name="pattern">A Regex pattern to match</param>
        /// <param name="start">starting offset into the list of keys</param>
        /// <param name="count">how many to return, < 1 means all keys</param>
        /// <returns></returns>
        public string[] FindLinksetDataKeys(string pattern, int start, int count)
        {
            List<string> all_keys = new List<string>(GetLinksetDataSubList(0, 0));
            Regex rx = new Regex(pattern, RegexOptions.CultureInvariant);

            if (count < 1)
                count = all_keys.Count;

            List<string> matches = new List<string>();
            foreach (var str in all_keys)
            {
                if (rx.IsMatch(str))
                    matches.Add(str);
            }

            return matches.Skip(start).Take(count).ToArray();
        }

        /// <summary>
        /// GetLinksetDataSubList - Return a subset of key values from start for count
        /// </summary>
        /// <param name="start">Offset in the list for the first key</param>
        /// <param name="count">How many keys to return, < 1 means all keys</param>
        /// <returns></returns>
        public string[] GetLinksetDataSubList(int start, int count)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return new string[0];

                if (count < 1)
                    count = Data.Count;

                List<string> ret = Data.Keys.Skip(start).Take(count).ToList();

                return ret.ToArray();
            }
        }

        public bool HasLinksetData()
        {
            return Data.Count > 0;
        }

        /// <summary>
        /// LinksetDataCountMatches - Return a count of the # of keys that match pattern.
        /// </summary>
        /// <param name="pattern">The Regex pattern to match</param>
        /// <returns>An integer count or zero if none</returns>
        public int LinksetDataCountMatches(string pattern)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return 0;

                Regex reg = new Regex(pattern, RegexOptions.CultureInvariant);

                int count = 0;
                foreach (var kvp in Data)
                {
                    if (reg.IsMatch(kvp.Key))
                        count++;
                }

                return count;
            }
        }

        public int LinksetDataKeys()
        {
            return Data.Count;
        }

        public string[] LinksetDataMultiDelete(string pattern, string pass, out int deleted, out int not_deleted)
        {
            lock (linksetDataLock)
            {
                deleted = 0;
                not_deleted = 0;

                if (Data.Count <= 0)
                    return new string[0];

                Regex reg = new Regex(pattern, RegexOptions.CultureInvariant);
                List<string> matches = new List<string>();

                // Take a copy so we can delete as we iterate
                foreach (var kvp in Data.ToArray())
                {
                    if (reg.IsMatch(kvp.Key))
                    {
                        if (kvp.Value.CheckPassword(pass))
                        {
                            Data.Remove(kvp.Key);
                            matches.Add(kvp.Key);

                            LinksetDataAccountingDelta(-kvp.Value.GetCost(kvp.Key));
                            deleted += 1;
                        }
                        else
                        {
                            not_deleted += 1;
                        }
                    }
                }

                return matches.ToArray();
            }
        }

        public bool LinksetDataOverLimit()
        {
            return (LinksetDataBytesFree <= 0);
        }

        /// <summary>
        /// Merge the linksetData present in another Linkset into this one.
        /// The current root will have the new linkset for the merged sog.
        /// If a key is present in our linksetData it wins, dont overide it.
        /// </summary>
        /// <param name="otherLinkset"></param>
        public void MergeLinksetData(LinksetData otherLinksetData)
        {
            // Nothing to merge?
            if (otherLinksetData == null)
                return;

            lock (linksetDataLock)
            {
                foreach (var kvp in otherLinksetData.Data)
                {
                    // If its already present skip it
                    if (Data.ContainsKey(kvp.Key))
                        continue;

                    // Do we have space for another entry?
                    if (LinksetDataOverLimit())
                        break;

                    var key = string.Copy(kvp.Key);
                    var value = kvp.Value.Copy();

                    Data.Add(key, value);
                    LinksetDataAccountingDelta(value.GetCost(key));
                }

                // Clear the LinksetData entries from the "other" SOG
                otherLinksetData.Data.Clear();

                otherLinksetData.LinksetDataBytesFree = LINKSETDATA_MAX;
                otherLinksetData.LinksetDataBytesUsed = 0;
            }
        }

        /// <summary>
        /// Reads a value from the key value pair
        /// </summary>
        /// <param name="key">The key value we're retrieving</param>
        /// <param name="pass">The password for a protected field (or string.Empty if not protected)</param>
        /// <returns>Blank if no key or pass mismatch. Value if no password or pass matches</returns>
        public string ReadLinksetData(string key, string pass)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return string.Empty;

                LinksetDataEntry entry;
                if (Data.TryGetValue(key, out entry))
                {
                    return entry.CheckPasswordAndGetValue(pass);
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// ResetLinksetData - clear the list and update the accounting.
        /// </summary>
        public void ResetLinksetData()
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return;

                Data.Clear();

                LinksetDataBytesFree = LINKSETDATA_MAX;
                LinksetDataBytesUsed = 0;
            }
        }

        public string SerializeLinksetData()
        {
            lock (linksetDataLock)
            {
                return JsonSerializer.Serialize<SortedList<string, LinksetDataEntry>>(Data);
            }
        }

        /// <summary>
        /// Add/Subtract an integer value from the current data allocated for the Linkset.
        /// </summary>
        /// <param name="delta">An integer value, positive adds, negative subtracts delta bytes.</param>
        private void LinksetDataAccountingDelta(int delta)
        {
            LinksetDataBytesUsed += delta;
            LinksetDataBytesFree = LINKSETDATA_MAX - LinksetDataBytesUsed;

            if (LinksetDataBytesFree < 0)
                LinksetDataBytesFree = 0;
        }
    }

    public class LinksetDataEntry
    {
        public LinksetDataEntry(string value, string password)
        {
            Value = value;
            Password = password;
        }

        public string Password { get; private set; } = string.Empty;

        public string Value { get; private set; }

        public bool CheckPassword(string pass)
        {
            // A undocumented caveat for LinksetData appears to be that even for unprotected values,
            // if a pass is provided, it is still treated as protected
            return string.IsNullOrEmpty(Password) || (Password == pass);
        }

        public string CheckPasswordAndGetValue(string pass)
        {
            return CheckPassword(pass) ? Value : string.Empty;
        }

        // Deep Copy of Current Entry
        public LinksetDataEntry Copy()
        {
            string value = String.IsNullOrEmpty(Value) ? null : string.Copy(Value);
            string password = String.IsNullOrEmpty(Password) ? null : string.Copy(Password);

            return new LinksetDataEntry(value, password);
        }

        /// <summary>
        /// Calculate the cost in bytes for this entry.  Adds in the passed in key and
        /// if a password is supplied uses 32 bytes minimum unless the password is longer.
        /// </summary>
        /// <param name="key">The string key value associated with this entry.</param>
        /// <returns>int - cost of this entry in bytes</returns>
        public int GetCost(string key)
        {
            int cost = 0;

            cost += Encoding.UTF8.GetBytes(key).Length;
            cost += Encoding.UTF8.GetBytes(this.Value).Length;

            if (!string.IsNullOrEmpty(this.Password))
            {
                // For parity, the pass adds 32 bytes regardless of the length. See LL caveats
                cost += Math.Max(Encoding.UTF8.GetBytes(this.Password).Length, 32);
            }

            return cost;
        }

        public bool IsProtected()
        {
            return !string.IsNullOrEmpty(Password);
        }
    }
}