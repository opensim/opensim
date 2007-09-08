/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using libsecondlife;

using Nini.Config;

namespace OpenSim.Framework.Utilities
{
    public class Util
    {
        private static Random randomClass = new Random();
        private static uint nextXferID = 5000;
        private static object XferLock = new object();
        private static Dictionary<LLUUID, string> capsURLS = new Dictionary<LLUUID, string>();

        public static ulong UIntsToLong(uint X, uint Y)
        {
            return Helpers.UIntsToLong(X, Y);
        }

        public static Random RandomClass
        {
            get
            {
                return randomClass;
            }
        }

        public static uint GetNextXferID()
        {
            uint id = 0;
            lock(XferLock)
            {
                id = nextXferID;
                nextXferID++;
            }
            return id;
        }

        public Util()
        {

        }

        public static string GetFileName(string file)
        {
            // Return just the filename on UNIX platforms
            // TODO: this should be customisable with a prefix, but that's something to do later.
            if (System.Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return file;
            }

            // Return %APPDATA%/OpenSim/file for 2K/XP/NT/2K3/VISTA
            // TODO: Switch this to System.Enviroment.SpecialFolders.ApplicationData
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!System.IO.Directory.Exists("%APPDATA%\\OpenSim\\"))
                {
                    System.IO.Directory.CreateDirectory("%APPDATA%\\OpenSim");   
                }

                return "%APPDATA%\\OpenSim\\" + file;
            }

            // Catch all - covers older windows versions
            // (but those probably wont work anyway)
            return file;
        }

        public static bool IsEnvironmentSupported(ref string reason)
        {
            // Must have .NET 2.0 (Generics / libsl)
            if (System.Environment.Version.Major < 2)
            {
                reason = ".NET 1.0/1.1 lacks components that is used by OpenSim";
                return false;
            }

            // Windows 95/98/ME are unsupported
            if (System.Environment.OSVersion.Platform == PlatformID.Win32Windows &&
                System.Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                reason = "Windows 95/98/ME will not run OpenSim";
                return false;
            }

            // Windows 2000 / Pre-SP2 XP
            if (System.Environment.OSVersion.Version.Major == 5 && (
                System.Environment.OSVersion.Version.Minor == 0))
            {
                reason = "Please update to Windows XP Service Pack 2 or Server2003";
                return false;
            }

            return true;
        }

        public static int UnixTimeSinceEpoch()
        {
            TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            int timestamp = (int)t.TotalSeconds;
            return timestamp;
        }

        public static string Md5Hash(string pass)
        {
            MD5 md5 = MD5CryptoServiceProvider.Create();
            byte[] dataMd5 = md5.ComputeHash(Encoding.Default.GetBytes(pass));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dataMd5.Length; i++)
                sb.AppendFormat("{0:x2}", dataMd5[i]);
            return sb.ToString();
        }

        public static string GetRandomCapsPath()
        {
            LLUUID caps = LLUUID.Random();
            string capsPath = caps.ToStringHyphenated();
            capsPath = capsPath.Remove(capsPath.Length - 4, 4);
            return capsPath;
        }

        public static int fast_distance2d(int x, int y)
        {
            x = System.Math.Abs(x);
            y = System.Math.Abs(y);

            int min = System.Math.Min(x, y);

            return (x + y - (min >> 1) - (min >> 2) + (min >> 4));
        }

        public static string FieldToString(byte[] bytes)
        {
            return FieldToString(bytes, String.Empty);
        }

        /// <summary>
        /// Convert a variable length field (byte array) to a string, with a
        /// field name prepended to each line of the output
        /// </summary>
        /// <remarks>If the byte array has unprintable characters in it, a 
        /// hex dump will be put in the string instead</remarks>
        /// <param name="bytes">The byte array to convert to a string</param>
        /// <param name="fieldName">A field name to prepend to each line of output</param>
        /// <returns>An ASCII string or a string containing a hex dump, minus 
        /// the null terminator</returns>
        public static string FieldToString(byte[] bytes, string fieldName)
        {
            // Check for a common case
            if (bytes.Length == 0) return String.Empty;

            StringBuilder output = new StringBuilder();
            bool printable = true;

            for (int i = 0; i < bytes.Length; ++i)
            {
                // Check if there are any unprintable characters in the array
                if ((bytes[i] < 0x20 || bytes[i] > 0x7E) && bytes[i] != 0x09
                    && bytes[i] != 0x0D && bytes[i] != 0x0A && bytes[i] != 0x00)
                {
                    printable = false;
                    break;
                }
            }

            if (printable)
            {
                if (fieldName.Length > 0)
                {
                    output.Append(fieldName);
                    output.Append(": ");
                }

                if (bytes[bytes.Length - 1] == 0x00)
                    output.Append(UTF8Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1));
                else
                    output.Append(UTF8Encoding.UTF8.GetString(bytes));
            }
            else
            {
                for (int i = 0; i < bytes.Length; i += 16)
                {
                    if (i != 0)
                        output.Append(Environment.NewLine);
                    if (fieldName.Length > 0)
                    {
                        output.Append(fieldName);
                        output.Append(": ");
                    }

                    for (int j = 0; j < 16; j++)
                    {
                        if ((i + j) < bytes.Length)
                            output.Append(String.Format("{0:X2} ", bytes[i + j]));
                        else
                            output.Append("   ");
                    }

                    for (int j = 0; j < 16 && (i + j) < bytes.Length; j++)
                    {
                        if (bytes[i + j] >= 0x20 && bytes[i + j] < 0x7E)
                            output.Append((char)bytes[i + j]);
                        else
                            output.Append(".");
                    }
                }
            }

            return output.ToString();
        }

        /// <summary>
        /// Returns a IP address from a specified DNS, favouring IPv4 addresses.
        /// </summary>
        /// <param name="dnsAddress">DNS Hostname</param>
        /// <returns>An IP address, or null</returns>
        public static IPAddress GetHostFromDNS(string dnsAddress)
        {
            IPAddress[] hosts = Dns.GetHostEntry(dnsAddress).AddressList;

            foreach (IPAddress host in hosts)
            {
                if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return host;
                }
            }

            if (hosts.Length > 0)
                return hosts[0];

            return null;
        }

        public static IPAddress GetLocalHost()
        {
            string dnsAddress = "localhost";

            IPAddress[] hosts = Dns.GetHostEntry(dnsAddress).AddressList;

            foreach (IPAddress host in hosts)
            {
                if (!IPAddress.IsLoopback(host) && host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return host;
                }
            }

            if (hosts.Length > 0)
                return hosts[0];

            return null;
        }

        //
        // directory locations
        //
        public static string homeDir()
        {
            string temp;
//            string personal=(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
//            temp = Path.Combine(personal,".OpenSim");
            temp=".";
            return temp;
        }

        public static string configDir()
        {
            string temp;
            temp = ".";
            return temp;
        }

        public static string dataDir()
        {
            string temp;
            temp = ".";
            return temp;
        }

        public static string logDir()
        {
            string temp;
            temp = ".";
            return temp;
        }

        public static string GetCapsURL(LLUUID userID)
        {
            if (capsURLS.ContainsKey(userID))
            {
                return capsURLS[userID];
            }
            return "";
        }

        public static void SetCapsURL(LLUUID userID, string url)
        {
            if (capsURLS.ContainsKey(userID))
            {
                capsURLS[userID] = url;
            }
            else
            {
                capsURLS.Add(userID, url);
            }
        }

        // Nini (config) related Methods
        public static IConfigSource ConvertDataRowToXMLConfig(System.Data.DataRow row, string fileName)
        {
            if(!File.Exists(fileName))
            {
                //create new file
            }
            XmlConfigSource config = new XmlConfigSource(fileName);
            AddDataRowToConfig(config, row);
            config.Save();

            return config;
        }

        public static void AddDataRowToConfig(IConfigSource config, System.Data.DataRow row)
        {
            config.Configs.Add((string)row[0]);
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                config.Configs[(string)row[0]].Set(row.Table.Columns[i].ColumnName, row[i]);
            }
        }
    }
}
