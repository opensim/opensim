/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using BclExtras;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Amib.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// The method used by Util.FireAndForget for asynchronously firing events
    /// </summary>
    public enum FireAndForgetMethod
    {
        UnsafeQueueUserWorkItem,
        QueueUserWorkItem,
        BeginInvoke,
        SmartThreadPool,
        Thread,
    }

    /// <summary>
    /// Miscellaneous utility functions
    /// </summary>
    public class Util
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static uint nextXferID = 5000;
        private static Random randomClass = new Random();
        // Get a list of invalid file characters (OS dependent)
        private static string regexInvalidFileChars = "[" + new String(Path.GetInvalidFileNameChars()) + "]";
        private static string regexInvalidPathChars = "[" + new String(Path.GetInvalidPathChars()) + "]";
        private static object XferLock = new object();
        /// <summary>Thread pool used for Util.FireAndForget if
        /// FireAndForgetMethod.SmartThreadPool is used</summary>
        private static SmartThreadPool m_ThreadPool;

        // Unix-epoch starts at January 1st 1970, 00:00:00 UTC. And all our times in the server are (or at least should be) in UTC.
        private static readonly DateTime unixEpoch =
            DateTime.ParseExact("1970-01-01 00:00:00 +0", "yyyy-MM-dd hh:mm:ss z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime();

        public static readonly Regex UUIDPattern 
            = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        public static FireAndForgetMethod FireAndForgetMethod = FireAndForgetMethod.SmartThreadPool;

        /// <summary>
        /// Linear interpolates B<->C using percent A
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static double lerp(double a, double b, double c)
        {
            return (b*a) + (c*(1 - a));
        }

        /// <summary>
        /// Bilinear Interpolate, see Lerp but for 2D using 'percents' X & Y.
        /// Layout:
        ///     A B
        ///     C D
        /// A<->C = Y
        /// C<->D = X
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static double lerp2D(double x, double y, double a, double b, double c, double d)
        {
            return lerp(y, lerp(x, a, b), lerp(x, c, d));
        }


        public static Encoding UTF8 = Encoding.UTF8;

        /// <value>
        /// Well known UUID for the blank texture used in the Linden SL viewer version 1.20 (and hopefully onwards) 
        /// </value>
        public static UUID BLANK_TEXTURE_UUID = new UUID("5748decc-f629-461c-9a36-a35a221fe21f");

        #region Vector Equations

        /// <summary>
        /// Get the distance between two 3d vectors
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <param name="b">A 3d vector</param>
        /// <returns>The distance between the two vectors</returns>
        public static double GetDistanceTo(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Returns true if the distance beween A and B is less than amount. Significantly faster than GetDistanceTo since it eliminates the Sqrt.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool DistanceLessThan(Vector3 a, Vector3 b, double amount)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (dx*dx + dy*dy + dz*dz) < (amount*amount);
        }

        /// <summary>
        /// Get the magnitude of a 3d vector
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <returns>The magnitude of the vector</returns>
        public static double GetMagnitude(Vector3 a)
        {
            return Math.Sqrt((a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z));
        }

        /// <summary>
        /// Get a normalized form of a 3d vector
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <returns>A new vector which is normalized form of the vector</returns>
        /// <remarks>The vector paramater cannot be <0,0,0></remarks>
        public static Vector3 GetNormalizedVector(Vector3 a)
        {
            if (IsZeroVector(a))
                throw new ArgumentException("Vector paramater cannot be a zero vector.");

            float Mag = (float) GetMagnitude(a);
            return new Vector3(a.X / Mag, a.Y / Mag, a.Z / Mag);
        }

        /// <summary>
        /// Returns if a vector is a zero vector (has all zero components)
        /// </summary>
        /// <returns></returns>
        public static bool IsZeroVector(Vector3 v)
        {
            if (v.X == 0 && v.Y == 0 && v.Z == 0)
            {
                return true;
            }

            return false;
        }

        # endregion

        public static Quaternion Axes2Rot(Vector3 fwd, Vector3 left, Vector3 up)
        {
            float s;
            float tr = (float) (fwd.X + left.Y + up.Z + 1.0);

            if (tr >= 1.0)
            {
                s = (float) (0.5 / Math.Sqrt(tr));
                return new Quaternion(
                        (left.Z - up.Y) * s,
                        (up.X - fwd.Z) * s,
                        (fwd.Y - left.X) * s,
                        (float) 0.25 / s);
            }
            else
            {
                float max = (left.Y > up.Z) ? left.Y : up.Z;

                if (max < fwd.X)
                {
                    s = (float) (Math.Sqrt(fwd.X - (left.Y + up.Z) + 1.0));
                    float x = (float) (s * 0.5);
                    s = (float) (0.5 / s);
                    return new Quaternion(
                            x,
                            (fwd.Y + left.X) * s,
                            (up.X + fwd.Z) * s,
                            (left.Z - up.Y) * s);
                }
                else if (max == left.Y)
                {
                    s = (float) (Math.Sqrt(left.Y - (up.Z + fwd.X) + 1.0));
                    float y = (float) (s * 0.5);
                    s = (float) (0.5 / s);
                    return new Quaternion(
                            (fwd.Y + left.X) * s,
                            y,
                            (left.Z + up.Y) * s,
                            (up.X - fwd.Z) * s);
                }
                else
                {
                    s = (float) (Math.Sqrt(up.Z - (fwd.X + left.Y) + 1.0));
                    float z = (float) (s * 0.5);
                    s = (float) (0.5 / s);
                    return new Quaternion(
                            (up.X + fwd.Z) * s,
                            (left.Z + up.Y) * s,
                            z,
                            (fwd.Y - left.X) * s);
                }
            }
        }

        public static Random RandomClass
        {
            get { return randomClass; }
        }

        public static ulong UIntsToLong(uint X, uint Y)
        {
            return Utils.UIntsToLong(X, Y);
        }

        public static T Clamp<T>(T x, T min, T max)
            where T : IComparable<T>
        {
            return x.CompareTo(max) > 0 ? max :
                x.CompareTo(min) < 0 ? min :
                x;
        }

        public static uint GetNextXferID()
        {
            uint id = 0;
            lock (XferLock)
            {
                id = nextXferID;
                nextXferID++;
            }
            return id;
        }

        public static string GetFileName(string file)
        {
            // Return just the filename on UNIX platforms
            // TODO: this should be customisable with a prefix, but that's something to do later.
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return file;
            }

            // Return %APPDATA%/OpenSim/file for 2K/XP/NT/2K3/VISTA
            // TODO: Switch this to System.Enviroment.SpecialFolders.ApplicationData
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!Directory.Exists("%APPDATA%\\OpenSim\\"))
                {
                    Directory.CreateDirectory("%APPDATA%\\OpenSim");
                }

                return "%APPDATA%\\OpenSim\\" + file;
            }

            // Catch all - covers older windows versions
            // (but those probably wont work anyway)
            return file;
        }

        /// <summary>
        /// Debug utility function to convert unbroken strings of XML into something human readable for occasional debugging purposes.
        ///
        /// Please don't delete me even if I appear currently unused!
        /// </summary>
        /// <param name="rawXml"></param>
        /// <returns></returns>
        public static string GetFormattedXml(string rawXml)
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml(rawXml);

            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;

            try
            {
                xd.WriteTo(xtw);
            }
            finally
            {
                xtw.Close();
            }

            return sb.ToString();
        }

        public static bool IsEnvironmentSupported(ref string reason)
        {
            // Must have .NET 2.0 (Generics / libsl)
            if (Environment.Version.Major < 2)
            {
                reason = ".NET 1.0/1.1 lacks components that is used by OpenSim";
                return false;
            }

            // Windows 95/98/ME are unsupported
            if (Environment.OSVersion.Platform == PlatformID.Win32Windows &&
                Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                reason = "Windows 95/98/ME will not run OpenSim";
                return false;
            }

            // Windows 2000 / Pre-SP2 XP
            if (Environment.OSVersion.Version.Major == 5 &&
                Environment.OSVersion.Version.Minor == 0)
            {
                reason = "Please update to Windows XP Service Pack 2 or Server2003";
                return false;
            }

            return true;
        }

        public static int UnixTimeSinceEpoch()
        {
            return ToUnixTime(DateTime.UtcNow);
        }

        public static int ToUnixTime(DateTime stamp)
        {
            TimeSpan t = stamp.ToUniversalTime() - unixEpoch;
            return (int) t.TotalSeconds;
        }

        public static DateTime ToDateTime(ulong seconds)
        {
            DateTime epoch = unixEpoch;
            return epoch.AddSeconds(seconds);
        }

        public static DateTime ToDateTime(int seconds)
        {
            DateTime epoch = unixEpoch;
            return epoch.AddSeconds(seconds);
        }

        /// <summary>
        /// Return an md5 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Md5Hash(string data)
        {
            byte[] dataMd5 = ComputeMD5Hash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dataMd5.Length; i++)
                sb.AppendFormat("{0:x2}", dataMd5[i]);
            return sb.ToString();
        }

        private static byte[] ComputeMD5Hash(string data)
        {
            MD5 md5 = MD5.Create();
            return md5.ComputeHash(Encoding.Default.GetBytes(data));
        }

        /// <summary>
        /// Return an SHA1 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SHA1Hash(string data)
        {
            byte[] hash = ComputeSHA1Hash(data);
            return BitConverter.ToString(hash).Replace("-", String.Empty);
        }

        private static byte[] ComputeSHA1Hash(string src)
        {
            SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider();
            return SHA1.ComputeHash(Encoding.Default.GetBytes(src));
        }

        public static int fast_distance2d(int x, int y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);

            int min = Math.Min(x, y);

            return (x + y - (min >> 1) - (min >> 2) + (min >> 4));
        }

        public static bool IsOutsideView(uint oldx, uint newx, uint oldy, uint newy)
        {
            // Eventually this will be a function of the draw distance / camera position too.
            return (((int)Math.Abs((int)(oldx - newx)) > 1) || ((int)Math.Abs((int)(oldy - newy)) > 1));
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

                output.Append(CleanString(Util.UTF8.GetString(bytes, 0, bytes.Length - 1)));
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
                            output.Append((char) bytes[i + j]);
                        else
                            output.Append(".");
                    }
                }
            }

            return output.ToString();
        }

        /// <summary>
        /// Converts a URL to a IPAddress
        /// </summary>
        /// <param name="url">URL Standard Format</param>
        /// <returns>A resolved IP Address</returns>
        public static IPAddress GetHostFromURL(string url)
        {
            return GetHostFromDNS(url.Split(new char[] {'/', ':'})[3]);
        }

        /// <summary>
        /// Returns a IP address from a specified DNS, favouring IPv4 addresses.
        /// </summary>
        /// <param name="dnsAddress">DNS Hostname</param>
        /// <returns>An IP address, or null</returns>
        public static IPAddress GetHostFromDNS(string dnsAddress)
        {
            // Is it already a valid IP? No need to look it up.
            IPAddress ipa;
            if (IPAddress.TryParse(dnsAddress, out ipa))
                return ipa;

            IPAddress[] hosts = null;

            // Not an IP, lookup required
            try
            {
                hosts = Dns.GetHostEntry(dnsAddress).AddressList;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[UTIL]: An error occurred while resolving {0}, {1}", dnsAddress, e);

                // Still going to throw the exception on for now, since this was what was happening in the first place
                throw e;
            }

            foreach (IPAddress host in hosts)
            {
                if (host.AddressFamily == AddressFamily.InterNetwork)
                {
                    return host;
                }
            }

            if (hosts.Length > 0)
                return hosts[0];

            return null;
        }

        public static Uri GetURI(string protocol, string hostname, int port, string path)
        {
            return new UriBuilder(protocol, hostname, port, path).Uri;
        }

        /// <summary>
        /// Gets a list of all local system IP addresses
        /// </summary>
        /// <returns></returns>
        public static IPAddress[] GetLocalHosts()
        {
            return Dns.GetHostAddresses(Dns.GetHostName());
        }

        public static IPAddress GetLocalHost()
        {
            string dnsAddress = "localhost";

            IPAddress[] hosts = Dns.GetHostEntry(dnsAddress).AddressList;

            foreach (IPAddress host in hosts)
            {
                if (!IPAddress.IsLoopback(host) && host.AddressFamily == AddressFamily.InterNetwork)
                {
                    return host;
                }
            }

            if (hosts.Length > 0)
            {
                foreach (IPAddress host in hosts)
                {
                    if (host.AddressFamily == AddressFamily.InterNetwork)
                        return host;
                }
                // Well all else failed...
                return hosts[0];
            }

            return null;
        }

        /// <summary>
        /// Removes all invalid path chars (OS dependent)
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>safe path</returns>
        public static string safePath(string path)
        {
            return Regex.Replace(path, regexInvalidPathChars, String.Empty);
        }

        /// <summary>
        /// Removes all invalid filename chars (OS dependent)
        /// </summary>
        /// <param name="path">filename</param>
        /// <returns>safe filename</returns>
        public static string safeFileName(string filename)
        {
            return Regex.Replace(filename, regexInvalidFileChars, String.Empty);
            ;
        }

        //
        // directory locations
        //

        public static string homeDir()
        {
            string temp;
            //            string personal=(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            //            temp = Path.Combine(personal,".OpenSim");
            temp = ".";
            return temp;
        }

        public static string assetsDir()
        {
            return Path.Combine(configDir(), "assets");
        }

        public static string inventoryDir()
        {
            return Path.Combine(configDir(), "inventory");
        }

        public static string configDir()
        {
            return ".";
        }

        public static string dataDir()
        {
            return ".";
        }

        public static string logDir()
        {
            return ".";
        }

        // From: http://coercedcode.blogspot.com/2008/03/c-generate-unique-filenames-within.html
        public static string GetUniqueFilename(string FileName)
        {
            int count = 0;
            string Name;

            if (File.Exists(FileName))
            {
                FileInfo f = new FileInfo(FileName);

                if (!String.IsNullOrEmpty(f.Extension))
                {
                    Name = f.FullName.Substring(0, f.FullName.LastIndexOf('.'));
                }
                else
                {
                    Name = f.FullName;
                }

                while (File.Exists(FileName))
                {
                    count++;
                    FileName = Name + count + f.Extension;
                }
            }
            return FileName;
        }

        // Nini (config) related Methods
        public static IConfigSource ConvertDataRowToXMLConfig(DataRow row, string fileName)
        {
            if (!File.Exists(fileName))
            {
                //create new file
            }
            XmlConfigSource config = new XmlConfigSource(fileName);
            AddDataRowToConfig(config, row);
            config.Save();

            return config;
        }

        public static void AddDataRowToConfig(IConfigSource config, DataRow row)
        {
            config.Configs.Add((string) row[0]);
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                config.Configs[(string) row[0]].Set(row.Table.Columns[i].ColumnName, row[i]);
            }
        }

        public static float Clip(float x, float min, float max)
        {
            return Math.Min(Math.Max(x, min), max);
        }

        public static int Clip(int x, int min, int max)
        {
            return Math.Min(Math.Max(x, min), max);
        }

        /// <summary>
        /// Convert an UUID to a raw uuid string.  Right now this is a string without hyphens.
        /// </summary>
        /// <param name="UUID"></param>
        /// <returns></returns>
        public static String ToRawUuidString(UUID UUID)
        {
            return UUID.Guid.ToString("n");
        }

        public static string CleanString(string input)
        {
            if (input.Length == 0)
                return input;

            int clip = input.Length;

            // Test for ++ string terminator
            int pos = input.IndexOf("\0");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Test for CR
            pos = input.IndexOf("\r");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Test for LF
            pos = input.IndexOf("\n");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Truncate string before first end-of-line character found
            return input.Substring(0, clip);
        }

        /// <summary>
        /// returns the contents of /etc/issue on Unix Systems
        /// Use this for where it's absolutely necessary to implement platform specific stuff
        /// </summary>
        /// <returns></returns>
        public static string ReadEtcIssue()
        {
            try
            {
                StreamReader sr = new StreamReader("/etc/issue.net");
                string issue = sr.ReadToEnd();
                sr.Close();
                return issue;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static void SerializeToFile(string filename, Object obj)
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = null;

            try
            {
                stream = new FileStream(
                    filename, FileMode.Create,
                    FileAccess.Write, FileShare.None);

                formatter.Serialize(stream, obj);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }

        public static Object DeserializeFromFile(string filename)
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = null;
            Object ret = null;

            try
            {
                stream = new FileStream(
                    filename, FileMode.Open,
                    FileAccess.Read, FileShare.None);

                ret = formatter.Deserialize(stream);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            return ret;
        }

        public static string Compress(string text)
        {
            byte[] buffer = Util.UTF8.GetBytes(text);
            MemoryStream memory = new MemoryStream();
            using (GZipStream compressor = new GZipStream(memory, CompressionMode.Compress, true))
            {
                compressor.Write(buffer, 0, buffer.Length);
            }

            memory.Position = 0;
           
            byte[] compressed = new byte[memory.Length];
            memory.Read(compressed, 0, compressed.Length);

            byte[] compressedBuffer = new byte[compressed.Length + 4];
            Buffer.BlockCopy(compressed, 0, compressedBuffer, 4, compressed.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, compressedBuffer, 0, 4);
            return Convert.ToBase64String(compressedBuffer);
        }

        public static string Decompress(string compressedText)
        {
            byte[] compressedBuffer = Convert.FromBase64String(compressedText);
            using (MemoryStream memory = new MemoryStream())
            {
                int msgLength = BitConverter.ToInt32(compressedBuffer, 0);
                memory.Write(compressedBuffer, 4, compressedBuffer.Length - 4);

                byte[] buffer = new byte[msgLength];

                memory.Position = 0;
                using (GZipStream decompressor = new GZipStream(memory, CompressionMode.Decompress))
                {
                    decompressor.Read(buffer, 0, buffer.Length);
                }

                return Util.UTF8.GetString(buffer);
            }
        }

        public static XmlRpcResponse XmlRpcCommand(string url, string methodName, params object[] args)
        {
            return SendXmlRpcCommand(url, methodName, args);
        }

        public static XmlRpcResponse SendXmlRpcCommand(string url, string methodName, object[] args)
        {
            XmlRpcRequest client = new XmlRpcRequest(methodName, args);
            return client.Send(url, 6000);
        }

        /// <summary>
        /// Returns an error message that the user could not be found in the database
        /// </summary>
        /// <returns>XML string consisting of a error element containing individual error(s)</returns>
        public static XmlRpcResponse CreateUnknownUserErrorResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            responseData["error_type"] = "unknown_user";
            responseData["error_desc"] = "The user requested is not in the database";

            response.Value = responseData;
            return response;
        }

        /// <summary>
        /// Converts a byte array in big endian order into an ulong.
        /// </summary>
        /// <param name="bytes">
        /// The array of bytes
        /// </param>
        /// <returns>
        /// The extracted ulong
        /// </returns>
        public static ulong BytesToUInt64Big(byte[] bytes)
        {
            if (bytes.Length < 8) return 0;
            return ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32) |
                ((ulong)bytes[4] << 24) | ((ulong)bytes[5] << 16) | ((ulong)bytes[6] << 8) | (ulong)bytes[7];
        }

        // used for RemoteParcelRequest (for "About Landmark")
        public static UUID BuildFakeParcelID(ulong regionHandle, uint x, uint y)
        {
            byte[] bytes =
            {
                (byte)regionHandle, (byte)(regionHandle >> 8), (byte)(regionHandle >> 16), (byte)(regionHandle >> 24),
                (byte)(regionHandle >> 32), (byte)(regionHandle >> 40), (byte)(regionHandle >> 48), (byte)(regionHandle << 56),
                (byte)x, (byte)(x >> 8), 0, 0,
                (byte)y, (byte)(y >> 8), 0, 0 };
            return new UUID(bytes, 0);
        }

        public static UUID BuildFakeParcelID(ulong regionHandle, uint x, uint y, uint z)
        {
            byte[] bytes =
            {
                (byte)regionHandle, (byte)(regionHandle >> 8), (byte)(regionHandle >> 16), (byte)(regionHandle >> 24),
                (byte)(regionHandle >> 32), (byte)(regionHandle >> 40), (byte)(regionHandle >> 48), (byte)(regionHandle << 56),
                (byte)x, (byte)(x >> 8), (byte)z, (byte)(z >> 8),
                (byte)y, (byte)(y >> 8), 0, 0 };
            return new UUID(bytes, 0);
        }

        public static void ParseFakeParcelID(UUID parcelID, out ulong regionHandle, out uint x, out uint y)
        {
            byte[] bytes = parcelID.GetBytes();
            regionHandle = Utils.BytesToUInt64(bytes);
            x = Utils.BytesToUInt(bytes, 8) & 0xffff;
            y = Utils.BytesToUInt(bytes, 12) & 0xffff;
        }

        public static void ParseFakeParcelID(UUID parcelID, out ulong regionHandle, out uint x, out uint y, out uint z)
        {
            byte[] bytes = parcelID.GetBytes();
            regionHandle = Utils.BytesToUInt64(bytes);
            x = Utils.BytesToUInt(bytes, 8) & 0xffff;
            z = (Utils.BytesToUInt(bytes, 8) & 0xffff0000) >> 16;
            y = Utils.BytesToUInt(bytes, 12) & 0xffff;
        }

        public static void FakeParcelIDToGlobalPosition(UUID parcelID, out uint x, out uint y)
        {
            ulong regionHandle;
            uint rx, ry;

            ParseFakeParcelID(parcelID, out regionHandle, out x, out y);
            Utils.LongToUInts(regionHandle, out rx, out ry);

            x += rx;
            y += ry;
        }
        
        /// <summary>
        /// Get operating system information if available.  Returns only the first 45 characters of information
        /// </summary>
        /// <returns>
        /// Operating system information.  Returns an empty string if none was available.
        /// </returns>
        public static string GetOperatingSystemInformation()
        {
            string os = String.Empty;

            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                os = Environment.OSVersion.ToString();
            }
            else
            {
                os = ReadEtcIssue();
            }
                      
            if (os.Length > 45)
            {
                os = os.Substring(0, 45);
            }
            
            return os;
        }

        /// <summary>
        /// Is the given string a UUID?
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool isUUID(string s)
        {
            return UUIDPattern.IsMatch(s);
        }

        public static string GetDisplayConnectionString(string connectionString)
        {
            int passPosition = 0;
            int passEndPosition = 0;
            string displayConnectionString = null;

            // hide the password in the connection string
            passPosition = connectionString.IndexOf("password", StringComparison.OrdinalIgnoreCase);
            passPosition = connectionString.IndexOf("=", passPosition);
            if (passPosition < connectionString.Length)
                passPosition += 1;
            passEndPosition = connectionString.IndexOf(";", passPosition);

            displayConnectionString = connectionString.Substring(0, passPosition);
            displayConnectionString += "***";
            displayConnectionString += connectionString.Substring(passEndPosition, connectionString.Length - passEndPosition);

            return displayConnectionString;
        }

        public static T ReadSettingsFromIniFile<T>(IConfig config, T settingsClass)
        {
            Type settingsType = settingsClass.GetType();

            FieldInfo[] fieldInfos = settingsType.GetFields();
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                if (!fieldInfo.IsStatic)
                {
                    if (fieldInfo.FieldType == typeof(System.String))
                    {
                        fieldInfo.SetValue(settingsClass, config.Get(fieldInfo.Name, (string)fieldInfo.GetValue(settingsClass)));
                    }
                    else if (fieldInfo.FieldType == typeof(System.Boolean))
                    {
                        fieldInfo.SetValue(settingsClass, config.GetBoolean(fieldInfo.Name, (bool)fieldInfo.GetValue(settingsClass)));
                    }
                    else if (fieldInfo.FieldType == typeof(System.Int32))
                    {
                        fieldInfo.SetValue(settingsClass, config.GetInt(fieldInfo.Name, (int)fieldInfo.GetValue(settingsClass)));
                    }
                    else if (fieldInfo.FieldType == typeof(System.Single))
                    {
                        fieldInfo.SetValue(settingsClass, config.GetFloat(fieldInfo.Name, (float)fieldInfo.GetValue(settingsClass)));
                    }
                    else if (fieldInfo.FieldType == typeof(System.UInt32))
                    {
                        fieldInfo.SetValue(settingsClass, Convert.ToUInt32(config.Get(fieldInfo.Name, ((uint)fieldInfo.GetValue(settingsClass)).ToString())));
                    }
                }
            }

            PropertyInfo[] propertyInfos = settingsType.GetProperties();
            foreach (PropertyInfo propInfo in propertyInfos)
            {
                if ((propInfo.CanRead) && (propInfo.CanWrite))
                {
                    if (propInfo.PropertyType == typeof(System.String))
                    {
                        propInfo.SetValue(settingsClass, config.Get(propInfo.Name, (string)propInfo.GetValue(settingsClass, null)), null);
                    }
                    else if (propInfo.PropertyType == typeof(System.Boolean))
                    {
                        propInfo.SetValue(settingsClass, config.GetBoolean(propInfo.Name, (bool)propInfo.GetValue(settingsClass, null)), null);
                    }
                    else if (propInfo.PropertyType == typeof(System.Int32))
                    {
                        propInfo.SetValue(settingsClass, config.GetInt(propInfo.Name, (int)propInfo.GetValue(settingsClass, null)), null);
                    }
                    else if (propInfo.PropertyType == typeof(System.Single))
                    {
                        propInfo.SetValue(settingsClass, config.GetFloat(propInfo.Name, (float)propInfo.GetValue(settingsClass, null)), null);
                    }
                    if (propInfo.PropertyType == typeof(System.UInt32))
                    {
                        propInfo.SetValue(settingsClass, Convert.ToUInt32(config.Get(propInfo.Name, ((uint)propInfo.GetValue(settingsClass, null)).ToString())), null);
                    }
                }
            }

            return settingsClass;
        }

        public static string Base64ToString(string str)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            Decoder utf8Decode = encoder.GetDecoder();

            byte[] todecode_byte = Convert.FromBase64String(str);
            int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount];
            utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            string result = new String(decoded_char);
            return result;
        }

        public static Guid GetHashGuid(string data, string salt)
        {
            byte[] hash = ComputeMD5Hash(data + salt);

            //string s = BitConverter.ToString(hash);

            Guid guid = new Guid(hash);

            return guid;
        }

        public static byte ConvertMaturityToAccessLevel(uint maturity)
        {
            byte retVal = 0;
            switch (maturity)
            {
                case 0: //PG
                    retVal = 13;
                    break;
                case 1: //Mature
                    retVal = 21;
                    break;
                case 2: // Adult
                    retVal = 42;
                    break;
            }

            return retVal;

        }

        /// <summary>
        /// Produces an OSDMap from its string representation on a stream
        /// </summary>
        /// <param name="data">The stream</param>
        /// <param name="length">The size of the data on the stream</param>
        /// <returns>The OSDMap or an exception</returns>
        public static OSDMap GetOSDMap(Stream stream, int length)
        {
            byte[] data = new byte[length];
            stream.Read(data, 0, length);
            string strdata = Util.UTF8.GetString(data);
            OSDMap args = null;
            OSD buffer;
            buffer = OSDParser.DeserializeJson(strdata);
            if (buffer.Type == OSDType.Map)
            {
                args = (OSDMap)buffer;
                return args;
            }
            return null;
        }

        public static string[] Glob(string path)
        {
            string vol=String.Empty;

            if (Path.VolumeSeparatorChar != Path.DirectorySeparatorChar)
            {
                string[] vcomps = path.Split(new char[] {Path.VolumeSeparatorChar}, 2, StringSplitOptions.RemoveEmptyEntries);

                if (vcomps.Length > 1)
                {
                    path = vcomps[1];
                    vol = vcomps[0];
                }
            }
            
            string[] comps = path.Split(new char[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);

            // Glob

            path = vol;
            if (vol != String.Empty)
                path += new String(new char[] {Path.VolumeSeparatorChar, Path.DirectorySeparatorChar});
            else
                path = new String(new char[] {Path.DirectorySeparatorChar});

            List<string> paths = new List<string>();
            List<string> found = new List<string>();
            paths.Add(path);

            int compIndex = -1;
            foreach (string c in comps)
            {
                compIndex++;

                List<string> addpaths = new List<string>();
                foreach (string p in paths)
                {
                    string[] dirs = Directory.GetDirectories(p, c);

                    if (dirs.Length != 0)
                    {
                        foreach (string dir in dirs)
                            addpaths.Add(Path.Combine(path, dir));
                    }

                    // Only add files if that is the last path component
                    if (compIndex == comps.Length - 1)
                    {
                        string[] files = Directory.GetFiles(p, c);
                        foreach (string f in files)
                            found.Add(f);
                    }
                }
                paths = addpaths;
            }

            return found.ToArray();
        }

        public static string ServerURI(string uri)
        {
            if (uri == string.Empty)
                return string.Empty;

            // Get rid of eventual slashes at the end
            uri = uri.TrimEnd('/');

            IPAddress ipaddr1 = null;
            string port1 = "";
            try
            {
                ipaddr1 = Util.GetHostFromURL(uri);
            }
            catch { }

            try
            {
                port1 = uri.Split(new char[] { ':' })[2];
            }
            catch { }

            // We tried our best to convert the domain names to IP addresses
            return (ipaddr1 != null) ? "http://" + ipaddr1.ToString() + ":" + port1 : uri;
        }

        public static byte[] StringToBytes256(string str)
        {
            if (String.IsNullOrEmpty(str)) { return Utils.EmptyBytes; }
            if (str.Length > 254) str = str.Remove(254);
            if (!str.EndsWith("\0")) { str += "\0"; }
            
            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte[] data = UTF8.GetBytes(str);
            if (data.Length > 256)
            {
                Array.Resize<byte>(ref data, 256);
                data[255] = 0;
            }

            return data;
        }

        public static byte[] StringToBytes1024(string str)
        {
            if (String.IsNullOrEmpty(str)) { return Utils.EmptyBytes; }
            if (str.Length > 1023) str = str.Remove(1023);
            if (!str.EndsWith("\0")) { str += "\0"; }

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte[] data = UTF8.GetBytes(str);
            if (data.Length > 1024)
            {
                Array.Resize<byte>(ref data, 1024);
                data[1023] = 0;
            }

            return data;
        }

        #region FireAndForget Threading Pattern

        /// <summary>
        /// Created to work around a limitation in Mono with nested delegates
        /// </summary>
        private class FireAndForgetWrapper
        {
            public void FireAndForget(System.Threading.WaitCallback callback)
            {
                callback.BeginInvoke(null, EndFireAndForget, callback);
            }

            public void FireAndForget(System.Threading.WaitCallback callback, object obj)
            {
                callback.BeginInvoke(obj, EndFireAndForget, callback);
            }

            private static void EndFireAndForget(IAsyncResult ar)
            {
                System.Threading.WaitCallback callback = (System.Threading.WaitCallback)ar.AsyncState;

                try { callback.EndInvoke(ar); }
                catch (Exception ex) { m_log.Error("[UTIL]: Asynchronous method threw an exception: " + ex.Message, ex); }

                ar.AsyncWaitHandle.Close();
            }
        }

        public static void FireAndForget(System.Threading.WaitCallback callback)
        {
            FireAndForget(callback, null);
        }

        public static void InitThreadPool(int maxThreads)
        {
            if (maxThreads < 2)
                throw new ArgumentOutOfRangeException("maxThreads", "maxThreads must be greater than 2");
            if (m_ThreadPool != null)
                throw new InvalidOperationException("SmartThreadPool is already initialized");

            m_ThreadPool = new SmartThreadPool(2000, maxThreads, 2);
        }

        public static int FireAndForgetCount()
        {
            const int MAX_SYSTEM_THREADS = 200;

            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                case FireAndForgetMethod.QueueUserWorkItem:
                case FireAndForgetMethod.BeginInvoke:
                    int workerThreads, iocpThreads;
                    ThreadPool.GetAvailableThreads(out workerThreads, out iocpThreads);
                    return workerThreads;
                case FireAndForgetMethod.SmartThreadPool:
                    return m_ThreadPool.MaxThreads - m_ThreadPool.InUseThreads;
                case FireAndForgetMethod.Thread:
                    return MAX_SYSTEM_THREADS - System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
                default:
                    throw new NotImplementedException();
            }
        }

        public static void FireAndForget(System.Threading.WaitCallback callback, object obj)
        {
            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                    ThreadPool.UnsafeQueueUserWorkItem(callback, obj);
                    break;
                case FireAndForgetMethod.QueueUserWorkItem:
                    ThreadPool.QueueUserWorkItem(callback, obj);
                    break;
                case FireAndForgetMethod.BeginInvoke:
                    FireAndForgetWrapper wrapper = Singleton.GetInstance<FireAndForgetWrapper>();
                    wrapper.FireAndForget(callback, obj);
                    break;
                case FireAndForgetMethod.SmartThreadPool:
                    if (m_ThreadPool == null)
                        m_ThreadPool = new SmartThreadPool(2000, 15, 2);
                    m_ThreadPool.QueueWorkItem(SmartThreadPoolCallback, new object[] { callback, obj });
                    break;
                case FireAndForgetMethod.Thread:
                    Thread thread = new Thread(delegate(object o) { callback(o); });
                    thread.Start(obj);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static object SmartThreadPoolCallback(object o)
        {
            object[] array = (object[])o;
            WaitCallback callback = (WaitCallback)array[0];
            object obj = array[1];

            callback(obj);
            return null;
        }

        #endregion FireAndForget Threading Pattern
    }
}
