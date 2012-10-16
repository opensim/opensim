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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Amib.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// The method used by Util.FireAndForget for asynchronously firing events
    /// </summary>
    /// <remarks>
    /// None is used to execute the method in the same thread that made the call.  It should only be used by regression
    /// test code that relies on predictable event ordering.
    /// RegressionTest is used by regression tests.  It fires the call synchronously and does not catch any exceptions.
    /// </remarks>
    public enum FireAndForgetMethod
    {
        None,
        RegressionTest,
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

        /// <summary>
        /// Thread pool used for Util.FireAndForget if FireAndForgetMethod.SmartThreadPool is used
        /// </summary>
        private static SmartThreadPool m_ThreadPool;

        // Unix-epoch starts at January 1st 1970, 00:00:00 UTC. And all our times in the server are (or at least should be) in UTC.
        private static readonly DateTime unixEpoch =
            DateTime.ParseExact("1970-01-01 00:00:00 +0", "yyyy-MM-dd hh:mm:ss z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime();

        private static readonly string rawUUIDPattern
            = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
        public static readonly Regex PermissiveUUIDPattern = new Regex(rawUUIDPattern);
        public static readonly Regex UUIDPattern = new Regex(string.Format("^{0}$", rawUUIDPattern));

        public static FireAndForgetMethod DefaultFireAndForgetMethod = FireAndForgetMethod.SmartThreadPool;
        public static FireAndForgetMethod FireAndForgetMethod = DefaultFireAndForgetMethod;

        /// <summary>
        /// Gets the name of the directory where the current running executable
        /// is located
        /// </summary>
        /// <returns>Filesystem path to the directory containing the current
        /// executable</returns>
        public static string ExecutingDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

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
        public static Encoding UTF8NoBomEncoding = new UTF8Encoding(false);

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
        /// Debug utility function to convert OSD into formatted XML for debugging purposes.
        /// </summary>
        /// <param name="osd">
        /// A <see cref="OSD"/>
        /// </param>
        /// <returns>
        /// A <see cref="System.String"/>
        /// </returns>
        public static string GetFormattedXml(OSD osd)
        {
            return GetFormattedXml(OSDParser.SerializeLLSDXmlString(osd));
        }

        /// <summary>
        /// Debug utility function to convert unbroken strings of XML into something human readable for occasional debugging purposes.
        /// </summary>
        /// <remarks>
        /// Please don't delete me even if I appear currently unused!
        /// </remarks>
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

        /// <summary>
        /// Is the platform Windows?
        /// </summary>
        /// <returns>true if so, false otherwise</returns>
        public static bool IsWindows()
        {
            PlatformID platformId = Environment.OSVersion.Platform;

            return (platformId == PlatformID.Win32NT
                || platformId == PlatformID.Win32S
                || platformId == PlatformID.Win32Windows
                || platformId == PlatformID.WinCE);
        }

        public static bool LoadArchSpecificWindowsDll(string libraryName)
        {
            // We do this so that OpenSimulator on Windows loads the correct native library depending on whether
            // it's running as a 32-bit process or a 64-bit one.  By invoking LoadLibary here, later DLLImports
            // will find it already loaded later on.
            //
            // This isn't necessary for other platforms (e.g. Mac OSX and Linux) since the DLL used can be
            // controlled in config files.
            string nativeLibraryPath;

            if (Util.Is64BitProcess())
                nativeLibraryPath = "lib64/" + libraryName;
            else
                nativeLibraryPath = "lib32/" + libraryName;

            m_log.DebugFormat("[UTIL]: Loading native Windows library at {0}", nativeLibraryPath);

            if (Util.LoadLibrary(nativeLibraryPath) == IntPtr.Zero)
            {
                m_log.ErrorFormat(
                    "[UTIL]: Couldn't find native Windows library at {0}", nativeLibraryPath);

                return false;
            }
            else
            {
                return true;
            }
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
        /// Return an SHA1 hash
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SHA1Hash(string data)
        {
            return SHA1Hash(Encoding.Default.GetBytes(data));
        }

        /// <summary>
        /// Return an SHA1 hash
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SHA1Hash(byte[] data)
        {
            byte[] hash = ComputeSHA1Hash(data);
            return BitConverter.ToString(hash).Replace("-", String.Empty);
        }

        private static byte[] ComputeSHA1Hash(byte[] src)
        {
            SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider();
            return SHA1.ComputeHash(src);
        }

        public static int fast_distance2d(int x, int y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);

            int min = Math.Min(x, y);

            return (x + y - (min >> 1) - (min >> 2) + (min >> 4));
        }

        /// <summary>
        /// Determines whether a point is inside a bounding box.
        /// </summary>
        /// <param name='v'></param>
        /// <param name='min'></param>
        /// <param name='max'></param>
        /// <returns></returns>
        public static bool IsInsideBox(Vector3 v, Vector3 min, Vector3 max)
        {
            return v.X >= min.X & v.Y >= min.Y && v.Z >= min.Z
                && v.X <= max.X && v.Y <= max.Y && v.Z <= max.Z;
        }

        /// <summary>
        /// Are the co-ordinates of the new region visible from the old region?
        /// </summary>
        /// <param name="oldx">Old region x-coord</param>
        /// <param name="newx">New region x-coord</param>
        /// <param name="oldy">Old region y-coord</param>
        /// <param name="newy">New region y-coord</param>
        /// <returns></returns>        
        public static bool IsOutsideView(float drawdist, uint oldx, uint newx, uint oldy, uint newy)
        {
            int dd = (int)((drawdist + Constants.RegionSize - 1) / Constants.RegionSize);

            int startX = (int)oldx - dd;
            int startY = (int)oldy - dd;

            int endX = (int)oldx + dd;
            int endY = (int)oldy + dd;

            return (newx < startX || endX < newx || newy < startY || endY < newy);
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
                m_log.WarnFormat("[UTIL]: An error occurred while resolving host name {0}, {1}", dnsAddress, e);

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
            IPAddress[] iplist = GetLocalHosts();

            if (iplist.Length == 0) // No accessible external interfaces
            {
                IPAddress[] loopback = Dns.GetHostAddresses("localhost");
                IPAddress localhost = loopback[0];

                return localhost;
            }

            foreach (IPAddress host in iplist)
            {
                if (!IPAddress.IsLoopback(host) && host.AddressFamily == AddressFamily.InterNetwork)
                {
                    return host;
                }
            }

            if (iplist.Length > 0)
            {
                foreach (IPAddress host in iplist)
                {
                    if (host.AddressFamily == AddressFamily.InterNetwork)
                        return host;
                }
                // Well all else failed...
                return iplist[0];
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

        public static Vector3 Clip(Vector3 vec, float min, float max)
        {
            return new Vector3(Clip(vec.X, min, max), Clip(vec.Y, min, max),
                Clip(vec.Z, min, max));
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

        /// <summary>
        /// Copy data from one stream to another, leaving the read position of both streams at the beginning.
        /// </summary>
        /// <param name='inputStream'>
        /// Input stream.  Must be seekable.
        /// </param>
        /// <exception cref='ArgumentException'>
        /// Thrown if the input stream is not seekable.
        /// </exception>
        public static Stream Copy(Stream inputStream)
        {
            if (!inputStream.CanSeek)
                throw new ArgumentException("Util.Copy(Stream inputStream) must receive an inputStream that can seek");

            const int readSize = 256;
            byte[] buffer = new byte[readSize];
            MemoryStream ms = new MemoryStream();
        
            int count = inputStream.Read(buffer, 0, readSize);

            while (count > 0)
            {
                ms.Write(buffer, 0, count);
                count = inputStream.Read(buffer, 0, readSize);
            }

            ms.Position = 0;
            inputStream.Position = 0;

            return ms;
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

        public static string GetRuntimeInformation()
        {
            string ru = String.Empty;

            if (Environment.OSVersion.Platform == PlatformID.Unix)
                ru = "Unix/Mono";
            else
                if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ru = "OSX/Mono";
                else
                {
                    if (Type.GetType("Mono.Runtime") != null)
                        ru = "Win/Mono";
                    else
                        ru = "Win/.NET";
                }

            return ru;
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
            Decoder utf8Decode = Encoding.UTF8.GetDecoder();

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

        public static uint ConvertAccessLevelToMaturity(byte maturity)
        {
            if (maturity <= 13)
                return 0;
            else if (maturity <= 21)
                return 1;
            else
                return 2;
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

        public static OSDMap GetOSDMap(string data)
        {
            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson(data);
                if (buffer.Type == OSDType.Map)
                {
                    args = (OSDMap)buffer;
                    return args;
                }
                else
                {
                    // uh?
                    m_log.Debug(("[UTILS]: Got OSD of unexpected type " + buffer.Type.ToString()));
                    return null;
                }
            }
            catch (Exception ex)
            {
                m_log.Debug("[UTILS]: exception on GetOSDMap " + ex.Message);
                return null;
            }
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

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 256 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <param name="args">
        /// Arguments to substitute into the string via the {} mechanism.
        /// </param>
        /// <returns></returns>
        public static byte[] StringToBytes256(string str, params object[] args)
        {
            return StringToBytes256(string.Format(str, args));
        }

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 256 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
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

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 1024 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <param name="args">
        /// Arguments to substitute into the string via the {} mechanism.
        /// </param>
        /// <returns></returns>
        public static byte[] StringToBytes1024(string str, params object[] args)
        {
            return StringToBytes1024(string.Format(str, args));
        }

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 1024 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
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

        /// <summary>
        /// Used to trigger an early library load on Windows systems.
        /// </summary>
        /// <remarks>
        /// Required to get 32-bit and 64-bit processes to automatically use the
        /// appropriate native library.
        /// </remarks>
        /// <param name="dllToLoad"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        /// <summary>
        /// Determine whether the current process is 64 bit
        /// </summary>
        /// <returns>true if so, false if not</returns>
        public static bool Is64BitProcess()
        {
            return IntPtr.Size == 8;
        }

        #region FireAndForget Threading Pattern

        /// <summary>
        /// Created to work around a limitation in Mono with nested delegates
        /// </summary>
        private sealed class FireAndForgetWrapper
        {
            private static volatile FireAndForgetWrapper instance;
            private static object syncRoot = new Object();

            public static FireAndForgetWrapper Instance {
                get {

                    if (instance == null)
                    {
                        lock (syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new FireAndForgetWrapper();
                            }
                        }
                    }

                    return instance;
                }
            }

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
            WaitCallback realCallback;

            if (FireAndForgetMethod == FireAndForgetMethod.RegressionTest)
            {
                // If we're running regression tests, then we want any exceptions to rise up to the test code.
                realCallback = o => { Culture.SetCurrentCulture(); callback(o); };
            }
            else
            {
                // When OpenSim interacts with a database or sends data over the wire, it must send this in en_US culture
                // so that we don't encounter problems where, for instance, data is saved with a culture that uses commas
                // for decimals places but is read by a culture that treats commas as number seperators.
                realCallback = o =>
                {
                    Culture.SetCurrentCulture();

                    try
                    {
                        callback(o);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[UTIL]: Continuing after async_call_method thread terminated with exception {0}{1}",
                            e.Message, e.StackTrace);
                    }
                };
            }

            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.RegressionTest:
                case FireAndForgetMethod.None:
                    realCallback.Invoke(obj);
                    break;
                case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                    ThreadPool.UnsafeQueueUserWorkItem(realCallback, obj);
                    break;
                case FireAndForgetMethod.QueueUserWorkItem:
                    ThreadPool.QueueUserWorkItem(realCallback, obj);
                    break;
                case FireAndForgetMethod.BeginInvoke:
                    FireAndForgetWrapper wrapper = FireAndForgetWrapper.Instance;
                    wrapper.FireAndForget(realCallback, obj);
                    break;
                case FireAndForgetMethod.SmartThreadPool:
                    if (m_ThreadPool == null)
                        m_ThreadPool = new SmartThreadPool(2000, 15, 2);
                    m_ThreadPool.QueueWorkItem(SmartThreadPoolCallback, new object[] { realCallback, obj });
                    break;
                case FireAndForgetMethod.Thread:
                    Thread thread = new Thread(delegate(object o) { realCallback(o); });
                    thread.Start(obj);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Get a thread pool report.
        /// </summary>
        /// <returns></returns>
        public static string GetThreadPoolReport()
        {
            string threadPoolUsed = null;
            int maxThreads = 0;
            int minThreads = 0;
            int allocatedThreads = 0;
            int inUseThreads = 0;
            int waitingCallbacks = 0;
            int completionPortThreads = 0;

            StringBuilder sb = new StringBuilder();
            if (FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool)
            {
                threadPoolUsed = "SmartThreadPool";
                maxThreads = m_ThreadPool.MaxThreads;
                minThreads = m_ThreadPool.MinThreads;
                inUseThreads = m_ThreadPool.InUseThreads;
                allocatedThreads = m_ThreadPool.ActiveThreads;
                waitingCallbacks = m_ThreadPool.WaitingCallbacks;
            }
            else if (
                FireAndForgetMethod == FireAndForgetMethod.UnsafeQueueUserWorkItem
                    || FireAndForgetMethod == FireAndForgetMethod.UnsafeQueueUserWorkItem)
            {
                threadPoolUsed = "BuiltInThreadPool";
                ThreadPool.GetMaxThreads(out maxThreads, out completionPortThreads);
                ThreadPool.GetMinThreads(out minThreads, out completionPortThreads);
                int availableThreads;
                ThreadPool.GetAvailableThreads(out availableThreads, out completionPortThreads);
                inUseThreads = maxThreads - availableThreads;
                allocatedThreads = -1;
                waitingCallbacks = -1;
            }

            if (threadPoolUsed != null)
            {
                sb.AppendFormat("Thread pool used           : {0}\n", threadPoolUsed);
                sb.AppendFormat("Max threads                : {0}\n", maxThreads);
                sb.AppendFormat("Min threads                : {0}\n", minThreads);
                sb.AppendFormat("Allocated threads          : {0}\n", allocatedThreads < 0 ? "not applicable" : allocatedThreads.ToString());
                sb.AppendFormat("In use threads             : {0}\n", inUseThreads);
                sb.AppendFormat("Work items waiting         : {0}\n", waitingCallbacks < 0 ? "not available" : waitingCallbacks.ToString());
            }
            else
            {
                sb.AppendFormat("Thread pool not used\n");
            }

            return sb.ToString();
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

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. This trims down TickCount so it doesn't wrap
        /// for the callers. 
        /// This trims it to a 12 day interval so don't let your frame time get too long.
        /// </summary>
        /// <returns></returns>
        public static Int32 EnvironmentTickCount()
        {
            return Environment.TickCount & EnvironmentTickCountMask;
        }
        const Int32 EnvironmentTickCountMask = 0x3fffffff;

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. Subtracts the passed value (previously fetched by
        /// 'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="prevValue"></param>
        /// <returns>subtraction of passed prevValue from current Environment.TickCount</returns>
        public static Int32 EnvironmentTickCountSubtract(Int32 newValue, Int32 prevValue)
        {
            Int32 diff = newValue - prevValue;
            return (diff >= 0) ? diff : (diff + EnvironmentTickCountMask + 1);
        }

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. Subtracts the passed value (previously fetched by
        /// 'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <returns>subtraction of passed prevValue from current Environment.TickCount</returns>
        public static Int32 EnvironmentTickCountSubtract(Int32 prevValue)
        {
            return EnvironmentTickCountSubtract(EnvironmentTickCount(), prevValue);
        }

        // Returns value of Tick Count A - TickCount B accounting for wrapping of TickCount
        // Assumes both tcA and tcB came from previous calls to Util.EnvironmentTickCount().
        // A positive return value indicates A occured later than B
        public static Int32 EnvironmentTickCountCompare(Int32 tcA, Int32 tcB)
        {
            // A, B and TC are all between 0 and 0x3fffffff
            int tc = EnvironmentTickCount();

            if (tc - tcA >= 0)
                tcA += EnvironmentTickCountMask + 1;

            if (tc - tcB >= 0)
                tcB += EnvironmentTickCountMask + 1;

            return tcA - tcB;
        }

        /// <summary>
        /// Prints the call stack at any given point. Useful for debugging.
        /// </summary>
        public static void PrintCallStack()
        {
            StackTrace stackTrace = new StackTrace(true);           // get call stack
            StackFrame[] stackFrames = stackTrace.GetFrames();  // get method calls (frames)

            // write call stack method names
            foreach (StackFrame stackFrame in stackFrames)
            {
                MethodBase mb = stackFrame.GetMethod();
                m_log.DebugFormat("{0}.{1}:{2}", mb.DeclaringType, mb.Name, stackFrame.GetFileLineNumber()); // write method name
            }
        }

        /// <summary>
        /// Gets the client IP address
        /// </summary>
        /// <param name="xff"></param>
        /// <returns></returns>
        public static IPEndPoint GetClientIPFromXFF(string xff)
        {
            if (xff == string.Empty)
                return null;

            string[] parts = xff.Split(new char[] { ',' });
            if (parts.Length > 0)
            {
                try
                {
                    return new IPEndPoint(IPAddress.Parse(parts[0]), 0);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[UTIL]: Exception parsing XFF header {0}: {1}", xff, e.Message);
                }
            }

            return null;
        }

        public static string GetCallerIP(Hashtable req)
        {
            if (req.ContainsKey("headers"))
            {
                try
                {
                    Hashtable headers = (Hashtable)req["headers"];
                    if (headers.ContainsKey("remote_addr") && headers["remote_addr"] != null)
                        return headers["remote_addr"].ToString();
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[UTIL]: exception in GetCallerIP: {0}", e.Message);
                }
            }
            return string.Empty;
        }

        #region Xml Serialization Utilities
        public static bool ReadBoolean(XmlTextReader reader)
        {
            reader.ReadStartElement();
            bool result = Boolean.Parse(reader.ReadContentAsString().ToLower());
            reader.ReadEndElement();

            return result;
        }

        public static UUID ReadUUID(XmlTextReader reader, string name)
        {
            UUID id;
            string idStr;

            reader.ReadStartElement(name);

            if (reader.Name == "Guid")
                idStr = reader.ReadElementString("Guid");
            else if (reader.Name == "UUID")
                idStr = reader.ReadElementString("UUID");
            else // no leading tag
                idStr = reader.ReadContentAsString();
            UUID.TryParse(idStr, out id);
            reader.ReadEndElement();

            return id;
        }

        public static Vector3 ReadVector(XmlTextReader reader, string name)
        {
            Vector3 vec;

            reader.ReadStartElement(name);
            vec.X = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // X or x
            vec.Y = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Y or y
            vec.Z = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Z or z
            reader.ReadEndElement();

            return vec;
        }

        public static Quaternion ReadQuaternion(XmlTextReader reader, string name)
        {
            Quaternion quat = new Quaternion();

            reader.ReadStartElement(name);
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                switch (reader.Name.ToLower())
                {
                    case "x":
                        quat.X = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
                        break;
                    case "y":
                        quat.Y = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
                        break;
                    case "z":
                        quat.Z = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
                        break;
                    case "w":
                        quat.W = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
                        break;
                }
            }

            reader.ReadEndElement();

            return quat;
        }

        public static T ReadEnum<T>(XmlTextReader reader, string name)
        {
            string value = reader.ReadElementContentAsString(name, String.Empty);
            // !!!!! to deal with flags without commas
            if (value.Contains(" ") && !value.Contains(","))
                value = value.Replace(" ", ", ");

            return (T)Enum.Parse(typeof(T), value); ;
        }
        #endregion

        #region Universal User Identifiers
       /// <summary>
        /// </summary>
        /// <param name="value">uuid[;endpoint[;first last[;secret]]]</param>
        /// <param name="uuid">the uuid part</param>
        /// <param name="url">the endpoint part (e.g. http://foo.com)</param>
        /// <param name="firstname">the first name part (e.g. Test)</param>
        /// <param name="lastname">the last name part (e.g User)</param>
        /// <param name="secret">the secret part</param>
        public static bool ParseUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname, out string secret)
        {
            uuid = UUID.Zero; url = string.Empty; firstname = "Unknown"; lastname = "User"; secret = string.Empty;

            string[] parts = value.Split(';');
            if (parts.Length >= 1)
                if (!UUID.TryParse(parts[0], out uuid))
                    return false;

            if (parts.Length >= 2)
                url = parts[1];

            if (parts.Length >= 3)
            {
                string[] name = parts[2].Split();
                if (name.Length == 2)
                {
                    firstname = name[0];
                    lastname = name[1];
                }
            }
            if (parts.Length >= 4)
                secret = parts[3];

            return true;
        }

        /// <summary>
        /// Produces a universal (HG) system-facing identifier given the information
        /// </summary>
        /// <param name="acircuit"></param>
        /// <returns>uuid[;homeURI[;first last]]</returns>
        public static string ProduceUserUniversalIdentifier(AgentCircuitData acircuit)
        {
            if (acircuit.ServiceURLs.ContainsKey("HomeURI"))
                return UniversalIdentifier(acircuit.AgentID, acircuit.firstname, acircuit.lastname, acircuit.ServiceURLs["HomeURI"].ToString());
            else
                return acircuit.AgentID.ToString();
        }

        /// <summary>
        /// Produces a universal (HG) system-facing identifier given the information
        /// </summary>
        /// <param name="id">UUID of the user</param>
        /// <param name="firstName">first name (e.g Test)</param>
        /// <param name="lastName">last name (e.g. User)</param>
        /// <param name="homeURI">homeURI (e.g. http://foo.com)</param>
        /// <returns>a string of the form uuid[;homeURI[;first last]]</returns>
        public static string UniversalIdentifier(UUID id, String firstName, String lastName, String homeURI)
        {
            string agentsURI = homeURI;
            if (!agentsURI.EndsWith("/"))
                agentsURI += "/";

            // This is ugly, but there's no other way, given that the name is changed
            // in the agent circuit data for foreigners
            if (lastName.Contains("@"))
            {
                string[] parts = firstName.Split(new char[] { '.' });
                if (parts.Length == 2)
                    return id.ToString() + ";" + agentsURI + ";" + parts[0] + " " + parts[1];
            }
            return id.ToString() + ";" + agentsURI + ";" + firstName + " " + lastName;

        }

        /// <summary>
        /// Produces a universal (HG) user-facing name given the information
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="homeURI"></param>
        /// <returns>string of the form first.last @foo.com or first last</returns>
        public static string UniversalName(String firstName, String lastName, String homeURI)
        {
            Uri uri = null;
            try
            {
                uri = new Uri(homeURI);
            }
            catch (UriFormatException)
            {
                return firstName + " " + lastName;
            }
            return firstName + "." + lastName + " " + "@" + uri.Authority;
        }
        #endregion
    }
}
