using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using log4net;

namespace OpenSim.Region.Framework.Scenes
{
    public class LinksetData : ICloneable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public const int LINKSETDATA_MAX = 131072;

        private static readonly object linksetDataLock = new object();

        public SortedList<string, LinksetDataEntry> Data { get; set; }

        public int BytesFree { get; set; }

        /* Make BytesUsed required so we can fail on an old format serialization.  
         * If we see that we fall back to deserializing the Collection and calculating
         * size.  This will require it be present in the data going forward 
         */
        [JsonRequired]
        public int BytesUsed { get; set; }

        public LinksetData()
        {
            Data = new();
            BytesFree = LINKSETDATA_MAX;
            BytesUsed = 0;
        }

        public object Clone()
        {
            return LinksetData.DeserializeLinksetData(this.SerializeLinksetData());
        }

        public void Clear()
        {
            lock(linksetDataLock)
            {
                Data.Clear();
                BytesFree = LINKSETDATA_MAX;
                BytesUsed = 0;
            }
        }

        public int Count()
        {
            lock (linksetDataLock)
            {
                return Data.Count;
            }
        }

        public string SerializeLinksetData()
        {
            lock (linksetDataLock)
            {
                return ((Data is null) || (Data.Count <= 0)) ? 
                    null : JsonSerializer.Serialize<LinksetData>(this);
            }
        }
   
        public static LinksetData DeserializeLinksetData(string data)
        {
            LinksetData lsd = null;
            
            if (string.IsNullOrWhiteSpace(data) is false)
            {           
                try
                {
                    lsd = JsonSerializer.Deserialize<LinksetData>(data);                  
                }
                catch (JsonException jse)
                {
                    try
                    {
                        m_log.Debug($"Exception deserializing LinkSetData, trying original format: {jse.Message}");
                        var listData = JsonSerializer.Deserialize<SortedList<string, LinksetDataEntry>>(data);
                        lsd = new LinksetData { Data = listData }; 
                    }
                    catch (Exception e)
                    {
                        m_log.Error($"Exception deserializing LinkSetData new and original format: {data}", e);
                    }
                    finally
                    {
                        // if we got data but no costs calculate that
                        if (lsd?.BytesUsed <= 0)
                        {
                            var cost = 0;

                            foreach (var kvp in lsd?.Data)
                            {
                                cost += kvp.Value.GetCost(kvp.Key);
                            }

                            lsd.BytesUsed = cost;
                            lsd.BytesFree = LINKSETDATA_MAX - cost;
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error($"General Exception deserializing LinkSetData", e);
                }
            }

            return lsd;
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

                if (string.IsNullOrWhiteSpace(key))
                    return 2;

                if (Data.TryGetValue(key, out LinksetDataEntry entry))
                {
                    if (!entry.CheckPassword(pass))
                        return -1;

                    if (entry.Value == value)
                        return 2;

                    // Subtract out the old entry
                    LinksetDataAccountingDelta(-entry.GetCost(key));
                }

                // Add New or Update handled here.
                var newEntry = new LinksetDataEntry(value, pass);
                Data[key] = newEntry;

                int cost = newEntry.GetCost(key);
                LinksetDataAccountingDelta(cost);

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
            if (string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return -1;

                LinksetDataEntry entry;
                if (Data.TryGetValue(key, out entry) is false)
                    return -1;

                if (entry.CheckPassword(pass) is false)
                    return 1;

                Data.Remove(key);

                LinksetDataAccountingDelta(-entry.GetCost(key));

                return 0;
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
            lock (linksetDataLock)
            {
                return (BytesFree <= 0);
            }
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
            if (otherLinksetData is null)
                return;

            lock (linksetDataLock)
            {
                foreach (var kvp in otherLinksetData.Data)
                {
                    // Shouldn't happen but we wont add invalid kvps
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                        continue;

                    // If its already present skip it
                    if (Data.ContainsKey(kvp.Key))
                        continue;

                    // Do we have space for another entry?
                    if (LinksetDataOverLimit())
                        break;

                    LinksetDataEntry val = (LinksetDataEntry)kvp.Value.Clone();
                    Data[kvp.Key] = val;

                    LinksetDataAccountingDelta(val.GetCost(kvp.Key));
                }

                // Clear the LinksetData entries from the "other" SOG
                otherLinksetData.Data.Clear();

                otherLinksetData.BytesFree = LINKSETDATA_MAX;
                otherLinksetData.BytesUsed = 0;
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

                if (Data.TryGetValue(key, out LinksetDataEntry entry))
                {
                    return entry.CheckPasswordAndGetValue(pass);
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Add/Subtract an integer value from the current data allocated for the Linkset.
        /// Assumes a lock is held from the caller.
        /// </summary>
        /// <param name="delta">An integer value, positive adds, negative subtracts delta bytes.</param>
        private void LinksetDataAccountingDelta(int delta)
        {
            BytesUsed += delta;
            BytesFree = LINKSETDATA_MAX - BytesUsed;

            if (BytesFree < 0)
                BytesFree = 0;
        }
    }

    public class LinksetDataEntry : ICloneable
    {
        public LinksetDataEntry()
        {
        }

        public LinksetDataEntry(string value, string password)
        {
            Value = value;
            Password = password;
        }

        public object Clone()
        {
            return JsonSerializer.Deserialize<LinksetDataEntry>(
                JsonSerializer.Serialize<LinksetDataEntry>(this));
        }

        public string Password { get; set; } 

        public string Value { get; set; }

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

        public bool IsProtected()
        {
            return !string.IsNullOrEmpty(Password);
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

            if (string.IsNullOrWhiteSpace(key) is false)
            {
                cost += Encoding.UTF8.GetBytes(key).Length;

                if (string.IsNullOrWhiteSpace(this.Value) is false)
                {
                    cost += Encoding.UTF8.GetBytes(this.Value).Length;
                }

                if (string.IsNullOrEmpty(this.Password) is false)
                {
                    // For parity, the password adds 32 bytes regardless of the length. See LL caveats
                    cost += Math.Max(Encoding.UTF8.GetBytes(this.Password).Length, 32);
                }
            }

            return cost;
        }
    }
}