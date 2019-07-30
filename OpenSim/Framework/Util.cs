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
using System.Drawing;
using System.Drawing.Imaging;
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
using log4net.Appender;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Amib.Threading;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Web;

namespace OpenSim.Framework
{
    [Flags]
    public enum PermissionMask : uint
    {
        None = 0,

        // folded perms
        FoldedTransfer = 1,
        FoldedModify = 1 << 1,
        FoldedCopy = 1 << 2,
        FoldedExport = 1 << 3,

        // DO NOT USE THIS FOR NEW WORK. IT IS DEPRECATED AND
        // EXISTS ONLY TO REACT TO EXISTING OBJECTS HAVING IT.
        // NEW CODE SHOULD NEVER SET THIS BIT!
        // Use InventoryItemFlags.ObjectSlamPerm in the Flags field of
        // this legacy slam bit. It comes from prior incomplete
        // understanding of the code and the prohibition on
        // reading viewer code that used to be in place.
        Slam = (1 << 4),

        FoldedMask = 0x0f,

        FoldingShift = 13 ,  // number of bit shifts from normal perm to folded or back (same as Transfer shift below)
                             // when doing as a block

        Transfer = 1 << 13, // 0x02000
        Modify = 1 << 14,   // 0x04000
        Copy = 1 << 15,     // 0x08000
        Export = 1 << 16,   // 0x10000
        Move = 1 << 19,     // 0x80000
        Damage = 1 << 20,   // 0x100000 does not seem to be in use
        // All does not contain Export, which is special and must be
        // explicitly given
        All = 0x8e000,
        AllAndExport = 0x9e000,
        AllAndExportNoMod = 0x9a000,
        AllEffective = 0x9e000,
        UnfoldedMask = 0x1e000
    }

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
        QueueUserWorkItem,
        SmartThreadPool,
        Thread,
    }

    /// <summary>
    /// Class for delivering SmartThreadPool statistical information
    /// </summary>
    /// <remarks>
    /// We do it this way so that we do not directly expose STP.
    /// </remarks>
    public class STPInfo
    {
        public string Name { get; set; }
        public STPStartInfo STPStartInfo { get; set; }
        public WIGStartInfo WIGStartInfo { get; set; }
        public bool IsIdle { get; set; }
        public bool IsShuttingDown { get; set; }
        public int MaxThreads { get; set; }
        public int MinThreads { get; set; }
        public int InUseThreads { get; set; }
        public int ActiveThreads { get; set; }
        public int WaitingCallbacks { get; set; }
        public int MaxConcurrentWorkItems { get; set; }
    }

    /// <summary>
    /// Miscellaneous utility functions
    /// </summary>
    public static class Util
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Log-level for the thread pool:
        /// 0 = no logging
        /// 1 = only first line of stack trace; don't log common threads
        /// 2 = full stack trace; don't log common threads
        /// 3 = full stack trace, including common threads
        /// </summary>
        public static int LogThreadPool { get; set; }
        public static bool LogOverloads { get; set; }

        public static readonly int MAX_THREADPOOL_LEVEL = 3;

        public static double TimeStampClockPeriodMS;
        public static double TimeStampClockPeriod;

        static Util()
        {
            LogThreadPool = 0;
            LogOverloads = true;
            TimeStampClockPeriod = 1.0D/ (double)Stopwatch.Frequency;
            TimeStampClockPeriodMS = 1e3 * TimeStampClockPeriod;
            m_log.InfoFormat("[UTIL] TimeStamp clock with period of {0}ms", Math.Round(TimeStampClockPeriodMS,6,MidpointRounding.AwayFromZero));
        }

        private static uint nextXferID = 5000;
        private static Random randomClass = new ThreadSafeRandom();

        // Get a list of invalid file characters (OS dependent)
        private static string regexInvalidFileChars = "[" + new String(Path.GetInvalidFileNameChars()) + "]";
        private static string regexInvalidPathChars = "[" + new String(Path.GetInvalidPathChars()) + "]";
        private static object XferLock = new object();

        /// <summary>
        /// Thread pool used for Util.FireAndForget if FireAndForgetMethod.SmartThreadPool is used
        /// </summary>
        private static SmartThreadPool m_ThreadPool;

        // Watchdog timer that aborts threads that have timed-out
        private static Timer m_threadPoolWatchdog;

        // Unix-epoch starts at January 1st 1970, 00:00:00 UTC. And all our times in the server are (or at least should be) in UTC.
        public static readonly DateTime UnixEpoch =
            DateTime.ParseExact("1970-01-01 00:00:00 +0", "yyyy-MM-dd hh:mm:ss z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime();

        private static readonly string rawUUIDPattern
            = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
        public static readonly Regex PermissiveUUIDPattern = new Regex(rawUUIDPattern);
        public static readonly Regex UUIDPattern = new Regex(string.Format("^{0}$", rawUUIDPattern));

        public static FireAndForgetMethod DefaultFireAndForgetMethod = FireAndForgetMethod.SmartThreadPool;
        public static FireAndForgetMethod FireAndForgetMethod = DefaultFireAndForgetMethod;

        public static bool IsPlatformMono
        {
            get { return Type.GetType("Mono.Runtime") != null; }
        }

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
            return Vector3.Distance(a,b);
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
            return Vector3.DistanceSquared(a,b) < (amount * amount);
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

        public static Vector3 GetNormalizedVector(Vector3 a)
        {
            Vector3 v = new Vector3(a.X, a.Y, a.Z);
            v.Normalize();
            return v;
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

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ulong UIntsToLong(uint X, uint Y)
        {
            return ((ulong)X << 32) | (ulong)Y;
        }

        // Regions are identified with a 'handle' made up of its world coordinates packed into a ulong.
        // Region handles are based on the coordinate of the region corner with lower X and Y
        // var regions need more work than this to get that right corner from a generic world position
        // this corner must be on a grid point
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ulong RegionWorldLocToHandle(uint X, uint Y)
        {
           ulong handle = X & 0xffffff00; // make sure it matchs grid coord points.
           handle <<= 32; // to higher half
           handle |= (Y & 0xffffff00);
           return handle;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ulong RegionGridLocToHandle(uint X, uint Y)
        {
            ulong handle = X;
            handle <<= 40; // shift to higher half and mult by 256)
            handle |= (Y << 8);  // mult by 256)
            return handle;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void RegionHandleToWorldLoc(ulong handle, out uint X, out uint Y)
        {
            X = (uint)(handle >> 32);
            Y = (uint)(handle & 0xfffffffful);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void RegionHandleToRegionLoc(ulong handle, out uint X, out uint Y)
        {
            X = (uint)(handle >> 40) & 0x00ffffffu; //  bring from higher half, divide by 256 and clean
            Y = (uint)(handle >> 8) & 0x00ffffffu; // divide by 256 and clean
            // if you trust the uint cast then the clean can be removed.
        }

        // A region location can be 'world coordinates' (meters) or 'region grid coordinates'
        // grid coordinates have a fixed step of 256m as defined by viewers
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static uint WorldToRegionLoc(uint worldCoord)
        {
            return worldCoord >> 8;
        }

        // Convert a region's 'region grid coordinate' to its 'world coordinate'.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static uint RegionToWorldLoc(uint regionCoord)
        {
            return regionCoord << 8;
        }


        public static bool checkServiceURI(string uristr, out string serviceURI)
        {
            serviceURI = string.Empty;
            try
            {
                Uri  uri = new Uri(uristr);
                serviceURI = uri.AbsoluteUri;
                if(uri.Port == 80)
                    serviceURI = serviceURI.Trim(new char[] { '/', ' ' }) +":80/";
                else if(uri.Port == 443)
                    serviceURI = serviceURI.Trim(new char[] { '/', ' ' }) +":443/";
                return true;
            }
            catch
            {
                serviceURI = string.Empty;
            }
            return false;
        }

        public static bool buildHGRegionURI(string inputName, out string serverURI, out string regionName)
        {
            serverURI = string.Empty;
            regionName = string.Empty;

            inputName = inputName.Trim();

            if (!inputName.StartsWith("http") && !inputName.StartsWith("https"))
            {
                // Formats: grid.example.com:8002:region name
                //          grid.example.com:region name
                //          grid.example.com:8002
                //          grid.example.com

                string host;
                uint port = 80;

                string[] parts = inputName.Split(new char[] { ':' });
                int indx;
                if(parts.Length == 0)
                    return false;
                if (parts.Length == 1)
                {
                    indx = inputName.IndexOf('/');
                    if (indx < 0)
                        serverURI = "http://"+ inputName + "/";
                    else
                    {
                        serverURI = "http://"+ inputName.Substring(0,indx + 1);
                        if(indx + 2 < inputName.Length)
                            regionName = inputName.Substring(indx + 1);
                    }
                }
                else
                {
                    host = parts[0];

                    if (parts.Length >= 2)
                    {
                        indx = parts[1].IndexOf('/');
                        if(indx < 0)
                        {
                            // If it's a number then assume it's a port. Otherwise, it's a region name.
                            if (!UInt32.TryParse(parts[1], out port))
                            {
                                port = 80;
                                regionName = parts[1];
                            }
                        }
                        else
                        {
                            string portstr = parts[1].Substring(0, indx);
                            if(indx + 2 < parts[1].Length)
                                regionName = parts[1].Substring(indx + 1);
                            if (!UInt32.TryParse(portstr, out port))
                                port = 80;
                        }
                    }
                    // always take the last one
                    if (parts.Length >= 3)
                    {
                       regionName = parts[2];
                    }

                    serverURI = "http://"+ host +":"+ port.ToString() + "/";
                }
            }
            else
            {
                // Formats: http://grid.example.com region name
                //          http://grid.example.com "region name"
                //          http://grid.example.com

                string[] parts = inputName.Split(new char[] { ' ' });

                if (parts.Length == 0)
                    return false;

                serverURI = parts[0];

                int indx = serverURI.LastIndexOf('/');
                if(indx > 10)
                {
                    if(indx + 2 < inputName.Length)
                        regionName = inputName.Substring(indx + 1);
                    serverURI = inputName.Substring(0, indx + 1);
                }
                else if (parts.Length >= 2)
                {
                    regionName = inputName.Substring(serverURI.Length);
                }
            }

            // use better code for sanity check
            Uri uri;
            try
            {
                    uri = new Uri(serverURI);
            }
            catch
            {
                return false;
            }

            if(!string.IsNullOrEmpty(regionName))
                regionName = regionName.Trim(new char[] { '"', ' ' });
            serverURI = uri.AbsoluteUri;
            if(uri.Port == 80)
                serverURI = serverURI.Trim(new char[] { '/', ' ' }) +":80/";
            else if(uri.Port == 443)
                serverURI = serverURI.Trim(new char[] { '/', ' ' }) +":443/";
            return true;
        }

        public static T Clamp<T>(T x, T min, T max)
            where T : IComparable<T>
        {
            return x.CompareTo(max) > 0 ? max :
                x.CompareTo(min) < 0 ? min :
                x;
        }

        // Clamp the maximum magnitude of a vector
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampV(Vector3 x, float max)
        {
            float lenSq = x.LengthSquared();
            if (lenSq > (max * max))
            {
                lenSq = max / (float)Math.Sqrt(lenSq);
                x = x * lenSq;
            }
            return x;
        }

        /// <summary>
        /// Check if any of the values in a Vector3 are NaN or Infinity
        /// </summary>
        /// <param name="v">Vector3 to check</param>
        /// <returns></returns>
        public static bool IsNanOrInfinity(Vector3 v)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                return true;

            if (float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsNaN(v.Z))
                return true;

            return false;
        }

        // Inclusive, within range test (true if equal to the endpoints)
        public static bool InRange<T>(T x, T min, T max)
            where T : IComparable<T>
        {
            return x.CompareTo(max) <= 0 && x.CompareTo(min) >= 0;
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

        public static byte[] DocToBytes(XmlDocument doc)
        {
            using (MemoryStream ms = new MemoryStream())
            using (XmlTextWriter xw = new XmlTextWriter(ms, null))
            {
                xw.Formatting = Formatting.Indented;
                doc.WriteTo(xw);
                xw.Flush();

                return ms.ToArray();
            }
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
            return LoadArchSpecificWindowsDll(libraryName, string.Empty);
        }

        public static bool LoadArchSpecificWindowsDll(string libraryName, string path)
        {
            // We do this so that OpenSimulator on Windows loads the correct native library depending on whether
            // it's running as a 32-bit process or a 64-bit one.  By invoking LoadLibary here, later DLLImports
            // will find it already loaded later on.
            //
            // This isn't necessary for other platforms (e.g. Mac OSX and Linux) since the DLL used can be
            // controlled in config files.
            string nativeLibraryPath;

            if (Util.Is64BitProcess())
                nativeLibraryPath = Path.Combine(Path.Combine(path, "lib64"), libraryName);
            else
                nativeLibraryPath = Path.Combine(Path.Combine(path, "lib32"), libraryName);

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
            TimeSpan t = stamp.ToUniversalTime() - UnixEpoch;
            return (int)t.TotalSeconds;
        }

        public static DateTime ToDateTime(ulong seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        public static DateTime ToDateTime(int seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        /// <summary>
        /// Return an md5 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>

        public static string Md5Hash(string data)
        {
            return Md5Hash(data, Encoding.Default);
        }

        public static string Md5Hash(string data, Encoding encoding)
        {
            byte[] dataMd5 = ComputeMD5Hash(data, encoding);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dataMd5.Length; i++)
                sb.AppendFormat("{0:x2}", dataMd5[i]);
            return sb.ToString();
        }

        private static byte[] ComputeMD5Hash(string data, Encoding encoding)
        {
            using(MD5 md5 = MD5.Create())
                return md5.ComputeHash(encoding.GetBytes(data));
        }

        /// <summary>
        /// Return an SHA1 hash
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>

        public static string SHA1Hash(string data, Encoding enc)
        {
            return SHA1Hash(enc.GetBytes(data));
        }

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
            byte[] ret;
            using (SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider())
                ret = SHA1.ComputeHash(src);
            return ret;
        }

        public static UUID ComputeSHA1UUID(string src)
        {
            return ComputeSHA1UUID(Encoding.Default.GetBytes(src));
        }

        public static UUID ComputeSHA1UUID(byte[] src)
        {
            byte[] ret;
            using (SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider())
                ret = SHA1.ComputeHash(src);
            return new UUID(ret, 2);
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
            return v.X >= min.X && v.Y >= min.Y && v.Z >= min.Z
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
        public static bool IsOutsideView(float drawdist, uint oldx, uint newx, uint oldy, uint newy,
            int oldsizex, int oldsizey, int newsizex, int newsizey)
        {
            // we still need to make sure we see new region  1stNeighbors
            drawdist--;
            oldx *= Constants.RegionSize;
            newx *= Constants.RegionSize;
            if (oldx + oldsizex + drawdist < newx)
                return true;
            if (newx + newsizex + drawdist < oldx)
                return true;

            oldy *= Constants.RegionSize;
            newy *= Constants.RegionSize;
            if (oldy + oldsizey + drawdist < newy)
                return true;
            if (newy + newsizey + drawdist < oldy)
                return true;

            return false;
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

        private static ExpiringCache<string,IPAddress> dnscache = new ExpiringCache<string, IPAddress>();

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
            if(String.IsNullOrWhiteSpace(dnsAddress))
                return null;

            IPAddress ia = null;
            if(dnscache.TryGetValue(dnsAddress, out ia) && ia != null)
            {
                dnscache.AddOrUpdate(dnsAddress, ia, 300);
                return ia;
            }

            ia = null;
            // If it is already an IP, don't let GetHostEntry see it
            if (IPAddress.TryParse(dnsAddress, out ia) && ia != null)
            {
                if (ia.Equals(IPAddress.Any) || ia.Equals(IPAddress.IPv6Any))
                    return null;
                dnscache.AddOrUpdate(dnsAddress, ia, 300);
                return ia;
            }

            IPHostEntry IPH;
            try
            {
                IPH = Dns.GetHostEntry(dnsAddress);
            }
            catch // (SocketException e)
            {
                return null;
            }

            if(IPH == null || IPH.AddressList.Length == 0)
                return null;

            ia = null;
            foreach (IPAddress Adr in IPH.AddressList)
            {
                if (ia == null)
                    ia = Adr;

                if (Adr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ia = Adr;
                    break;
                }
            }
            if(ia != null)
                dnscache.AddOrUpdate(dnsAddress, ia, 300);
            return ia;
        }

        public static IPEndPoint getEndPoint(IPAddress ia, int port)
        {
            if(ia == null)
                return null;

            IPEndPoint newEP = null;
            try
            {
                newEP = new IPEndPoint(ia, port);
            }
            catch
            {
                newEP = null;
            }
            return newEP;
        }

        public static IPEndPoint getEndPoint(string hostname, int port)
        {
            if(String.IsNullOrWhiteSpace(hostname))
                return null;
            
            IPAddress ia = null;
            if(dnscache.TryGetValue(hostname, out ia) && ia != null)
            {
                dnscache.AddOrUpdate(hostname, ia, 300);
                return getEndPoint(ia, port);
            }

            ia = null;

            // If it is already an IP, don't let GetHostEntry see it
            if (IPAddress.TryParse(hostname, out ia) && ia != null)
            {
                if (ia.Equals(IPAddress.Any) || ia.Equals(IPAddress.IPv6Any))
                    return null;

                dnscache.AddOrUpdate(hostname, ia, 300);
                return getEndPoint(ia, port);
            }


            IPHostEntry IPH;
            try
            {
                IPH = Dns.GetHostEntry(hostname);
            }
            catch // (SocketException e)
            {
                return null;
            }

            if(IPH == null || IPH.AddressList.Length == 0)
                return null;

            ia = null;
            foreach (IPAddress Adr in IPH.AddressList)
            {
                if (ia == null)
                    ia = Adr;

                if (Adr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ia = Adr;
                    break;
                }
            }

            if(ia != null)
                dnscache.AddOrUpdate(hostname, ia, 300);

            return getEndPoint(ia,port);
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
        /// Parses a foreign asset ID.
        /// </summary>
        /// <param name="id">A possibly-foreign asset ID: http://grid.example.com:8002/00000000-0000-0000-0000-000000000000 </param>
        /// <param name="url">The URL: http://grid.example.com:8002</param>
        /// <param name="assetID">The asset ID: 00000000-0000-0000-0000-000000000000. Returned even if 'id' isn't foreign.</param>
        /// <returns>True: this is a foreign asset ID; False: it isn't</returns>
        public static bool ParseForeignAssetID(string id, out string url, out string assetID)
        {
            url = String.Empty;
            assetID = String.Empty;

            UUID uuid;
            if (UUID.TryParse(id, out uuid))
            {
                assetID = uuid.ToString();
                return false;
            }

            if ((id.Length == 0) || (id[0] != 'h' && id[0] != 'H'))
                return false;

            Uri assetUri;
            if (!Uri.TryCreate(id, UriKind.Absolute, out assetUri) || assetUri.Scheme != Uri.UriSchemeHttp)
                return false;

            // Simian
            if (assetUri.Query != string.Empty)
            {
                NameValueCollection qscoll = HttpUtility.ParseQueryString(assetUri.Query);
                assetID = qscoll["id"];
                if (assetID != null)
                    url = id.Replace(assetID, ""); // Malformed again, as simian expects
                else
                    url = id; // !!! best effort
            }
            else // robust
            {
                url = "http://" + assetUri.Authority;
                assetID = assetUri.LocalPath.Trim(new char[] { '/' });
            }

            if (!UUID.TryParse(assetID, out uuid))
                return false;

            return true;
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

        public static string logFile()
        {
            foreach (IAppender appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender is FileAppender && appender.Name == "LogFileAppender")
                {
                    return ((FileAppender)appender).File;
                }
            }

            return "./OpenSim.log";
        }

        public static string statsLogFile()
        {
            foreach (IAppender appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender is FileAppender && appender.Name == "StatsLogFileAppender")
                {
                    return ((FileAppender)appender).File;
                }
            }

            return "./OpenSimStats.log";
        }

        public static string logDir()
        {
            return Path.GetDirectoryName(logFile());
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

        #region Nini (config) related Methods

        public static IConfigSource ConvertDataRowToXMLConfig(DataRow row, string fileName)
        {
            if (!File.Exists(fileName))
            {
                // create new file
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

        public static string GetConfigVarWithDefaultSection(IConfigSource config, string varname, string section)
        {
            // First, check the Startup section, the default section
            IConfig cnf = config.Configs["Startup"];
            if (cnf == null)
                return string.Empty;
            string val = cnf.GetString(varname, string.Empty);

            // Then check for an overwrite of the default in the given section
            if (!string.IsNullOrEmpty(section))
            {
                cnf = config.Configs[section];
                if (cnf != null)
                    val = cnf.GetString(varname, val);
            }

            return val;
        }

        /// <summary>
        /// Gets the value of a configuration variable by looking into
        /// multiple sections in order. The latter sections overwrite
        /// any values previously found.
        /// </summary>
        /// <typeparam name="T">Type of the variable</typeparam>
        /// <param name="config">The configuration object</param>
        /// <param name="varname">The configuration variable</param>
        /// <param name="sections">Ordered sequence of sections to look at</param>
        /// <returns></returns>
        public static T GetConfigVarFromSections<T>(IConfigSource config, string varname, string[] sections)
        {
            return GetConfigVarFromSections<T>(config, varname, sections, default(T));
        }

        /// <summary>
        /// Gets the value of a configuration variable by looking into
        /// multiple sections in order. The latter sections overwrite
        /// any values previously found.
        /// </summary>
        /// <remarks>
        /// If no value is found then the given default value is returned
        /// </remarks>
        /// <typeparam name="T">Type of the variable</typeparam>
        /// <param name="config">The configuration object</param>
        /// <param name="varname">The configuration variable</param>
        /// <param name="sections">Ordered sequence of sections to look at</param>
        /// <param name="val">Default value</param>
        /// <returns></returns>
        public static T GetConfigVarFromSections<T>(IConfigSource config, string varname, string[] sections, object val)
        {
            foreach (string section in sections)
            {
                IConfig cnf = config.Configs[section];
                if (cnf == null)
                    continue;

                if (typeof(T) == typeof(String))
                    val = cnf.GetString(varname, (string)val);
                else if (typeof(T) == typeof(Boolean))
                    val = cnf.GetBoolean(varname, (bool)val);
                else if (typeof(T) == typeof(Int32))
                    val = cnf.GetInt(varname, (int)val);
                else if (typeof(T) == typeof(float))
                    val = cnf.GetFloat(varname, (float)val);
                else
                    m_log.ErrorFormat("[UTIL]: Unhandled type {0}", typeof(T));
            }

            return (T)val;
        }

        public static void MergeEnvironmentToConfig(IConfigSource ConfigSource)
        {
            IConfig enVars = ConfigSource.Configs["Environment"];
            // if section does not exist then user isn't expecting them, so don't bother.
            if( enVars != null )
            {
                // load the values from the environment
                EnvConfigSource envConfigSource = new EnvConfigSource();
                // add the requested keys
                string[] env_keys = enVars.GetKeys();
                foreach ( string key in env_keys )
                {
                    envConfigSource.AddEnv(key, string.Empty);
                }
                // load the values from environment
                envConfigSource.LoadEnv();
                // add them in to the master
                ConfigSource.Merge(envConfigSource);
                ConfigSource.ExpandKeyValues();
            }
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

        /// <summary>
        /// Reads a configuration file, configFile, merging it with the main configuration, config.
        /// If the file doesn't exist, it copies a given exampleConfigFile onto configFile, and then
        /// merges it.
        /// </summary>
        /// <param name="config">The main configuration data</param>
        /// <param name="configFileName">The name of a configuration file in ConfigDirectory variable, no path</param>
        /// <param name="exampleConfigFile">Full path to an example configuration file</param>
        /// <param name="configFilePath">Full path ConfigDirectory/configFileName</param>
        /// <param name="created">True if the file was created in ConfigDirectory, false if it existed</param>
        /// <returns>True if success</returns>
        public static bool MergeConfigurationFile(IConfigSource config, string configFileName, string exampleConfigFile, out string configFilePath, out bool created)
        {
            created = false;
            configFilePath = string.Empty;

            IConfig cnf = config.Configs["Startup"];
            if (cnf == null)
            {
                m_log.WarnFormat("[UTILS]: Startup section doesn't exist");
                return false;
            }

            string configDirectory = cnf.GetString("ConfigDirectory", ".");
            string configFile = Path.Combine(configDirectory, configFileName);

            if (!File.Exists(configFile) && !string.IsNullOrEmpty(exampleConfigFile))
            {
                // We need to copy the example onto it

                if (!Directory.Exists(configDirectory))
                    Directory.CreateDirectory(configDirectory);

                try
                {
                    File.Copy(exampleConfigFile, configFile);
                    created = true;
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[UTILS]: Exception copying configuration file {0} to {1}: {2}", configFile, exampleConfigFile, e.Message);
                    return false;
                }
            }

            if (File.Exists(configFile))
            {
                // Merge
                config.Merge(new IniConfigSource(configFile));
                config.ExpandKeyValues();
                configFilePath = configFile;
                return true;
            }
            else
                return false;
        }

        #endregion

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
                (byte)(regionHandle >> 32), (byte)(regionHandle >> 40), (byte)(regionHandle >> 48), (byte)(regionHandle >> 56),
                (byte)x, (byte)(x >> 8), 0, 0,
                (byte)y, (byte)(y >> 8), 0, 0 };
            return new UUID(bytes, 0);
        }

        public static UUID BuildFakeParcelID(ulong regionHandle, uint x, uint y, uint z)
        {
            byte[] bytes =
            {
                (byte)regionHandle, (byte)(regionHandle >> 8), (byte)(regionHandle >> 16), (byte)(regionHandle >> 24),
                (byte)(regionHandle >> 32), (byte)(regionHandle >> 40), (byte)(regionHandle >> 48), (byte)(regionHandle >> 56),
                (byte)x, (byte)(x >> 8), (byte)z, (byte)(z >> 8),
                (byte)y, (byte)(y >> 8), 0, 0 };
            return new UUID(bytes, 0);
        }

        public static bool ParseFakeParcelID(UUID parcelID, out ulong regionHandle, out uint x, out uint y)
        {
            byte[] bytes = parcelID.GetBytes();
            regionHandle = Utils.BytesToUInt64(bytes);
            x = Utils.BytesToUInt(bytes, 8) & 0xffff;
            y = Utils.BytesToUInt(bytes, 12) & 0xffff;
            // validation may fail, just reducing the odds of using a real UUID as encoded parcel
            return  ( bytes[0] == 0 && bytes[4] == 0 && // handler x,y multiples of 256
                         bytes[9] < 64 && bytes[13] < 64 && // positions < 16km
                         bytes[14] == 0 && bytes[15] == 0);
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

//            if (Environment.OSVersion.Platform != PlatformID.Unix)
//            {
//                os = Environment.OSVersion.ToString();
//            }
//            else
//            {
//                os = ReadEtcIssue();
//            }
//
//            if (os.Length > 45)
//            {
//                os = os.Substring(0, 45);
//            }

            return os;
        }

        public static string GetRuntimeInformation()
        {
            string ru = String.Empty;

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
               ru = "Unix/Mono";
            }
            else
                if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ru = "OSX/Mono";
                else
                {
                    if (IsPlatformMono)
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
            if (passPosition == -1)
                return connectionString;
            passPosition = connectionString.IndexOf("=", passPosition);
            if (passPosition < connectionString.Length)
                passPosition += 1;
            passEndPosition = connectionString.IndexOf(";", passPosition);

            displayConnectionString = connectionString.Substring(0, passPosition);
            displayConnectionString += "***";
            displayConnectionString += connectionString.Substring(passEndPosition, connectionString.Length - passEndPosition);

            return displayConnectionString;
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

        public static void BinaryToASCII(char[] chars)
        {
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (ch < 32 || ch > 127)
                    chars[i] = '.';
            }
        }

        public static string BinaryToASCII(string src)
        {
            char[] chars = src.ToCharArray();
            BinaryToASCII(chars);
            return new String(chars);
        }

        /// <summary>
        /// Reads a known number of bytes from a stream.
        /// Throws EndOfStreamException if the stream doesn't contain enough data.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        /// <param name="data">The array to write bytes into. The array
        /// will be completely filled from the stream, so an appropriate
        /// size must be given.</param>
        public static void ReadStream(Stream stream, byte[] data)
        {
            int offset = 0;
            int remaining = data.Length;

            while (remaining > 0)
            {
                int read = stream.Read(data, offset, remaining);
                if (read <= 0)
                    throw new EndOfStreamException(String.Format("End of stream reached with {0} bytes left to read", remaining));
                remaining -= read;
                offset += read;
            }
        }

        public static Guid GetHashGuid(string data, string salt)
        {
            byte[] hash = ComputeMD5Hash(data + salt, Encoding.Default);

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
            if (String.IsNullOrEmpty(str))
                return Utils.EmptyBytes;

            if (!str.EndsWith("\0"))
                str += "\0";

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte[] data = UTF8.GetBytes(str);

            if (data.Length > 255) //play safe
            {
                int cut = 254;
                if((data[cut] & 0x80 ) != 0 )
                    {
                    while(cut > 0 && (data[cut] & 0xc0) != 0xc0)
                        cut--;
                    }
                Array.Resize<byte>(ref data, cut + 1);
                data[cut] = 0;
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
            if (String.IsNullOrEmpty(str))
                return Utils.EmptyBytes;

            if (!str.EndsWith("\0"))
                 str += "\0";

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte[] data = UTF8.GetBytes(str);

            if (data.Length > 1024)
            {
                int cut = 1023;
                if((data[cut] & 0x80 ) != 0 )
                    {
                    while(cut > 0 && (data[cut] & 0xc0) != 0xc0)
                        cut--;
                    }
                Array.Resize<byte>(ref data, cut + 1);
                data[cut] = 0;
            }

            return data;
        }

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to MaxLength bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <param name="args">
        /// Arguments to substitute into the string via the {} mechanism.
        /// </param>
        /// <returns></returns>
        public static byte[] StringToBytes(string str, int MaxLength, params object[] args)
        {
            return StringToBytes1024(string.Format(str, args), MaxLength);
        }

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to MaxLength bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
        public static byte[] StringToBytes(string str, int MaxLength)
        {
            if (String.IsNullOrEmpty(str))
                return Utils.EmptyBytes;

            if (!str.EndsWith("\0"))
                 str += "\0";

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte[] data = UTF8.GetBytes(str);

            if (data.Length > MaxLength)
            {
                int cut = MaxLength - 1 ;
                if((data[cut] & 0x80 ) != 0 )
                    {
                    while(cut > 0 && (data[cut] & 0xc0) != 0xc0)
                        cut--;
                    }
                Array.Resize<byte>(ref data, cut + 1);
                data[cut] = 0;
            }

            return data;
        }
        /// <summary>
        /// Pretty format the hashtable contents to a single line.
        /// </summary>
        /// <remarks>
        /// Used for debugging output.
        /// </remarks>
        /// <param name='ht'></param>
        public static string PrettyFormatToSingleLine(Hashtable ht)
        {
            StringBuilder sb = new StringBuilder();

            int i = 0;

            foreach (string key in ht.Keys)
            {
                sb.AppendFormat("{0}:{1}", key, ht[key]);

                if (++i < ht.Count)
                    sb.AppendFormat(", ");
            }

            return sb.ToString();
        }

        public static bool TryParseHttpRange(string header, out int start, out int end)
        {
            start = end = 0;

            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');

                if (rangeValues.Length == 2)
                {
                    string rawStart = rangeValues[0].Trim();
                    if (rawStart != "" && !Int32.TryParse(rawStart, out start))
                        return false;

                    if (start < 0)
                        return false;

                    string rawEnd = rangeValues[1].Trim();
                    if (rawEnd == "")
                    {
                        end = -1;
                        return true;
                    }
                    else if (Int32.TryParse(rawEnd, out end))
                        return end > 0;
                }
            }

            start = end = 0;
            return false;
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

        public static void InitThreadPool(int minThreads, int maxThreads)
        {
            if (maxThreads < 2)
                throw new ArgumentOutOfRangeException("maxThreads", "maxThreads must be greater than 2");

            if (minThreads > maxThreads || minThreads < 2)
                throw new ArgumentOutOfRangeException("minThreads", "minThreads must be greater than 2 and less than or equal to maxThreads");

            if (m_ThreadPool != null)
            {
                m_log.Warn("SmartThreadPool is already initialized.  Ignoring request.");
                return;
            }

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.ThreadPoolName = "Util";
            startInfo.IdleTimeout = 20000;
            startInfo.MaxWorkerThreads = maxThreads;
            startInfo.MinWorkerThreads = minThreads;

            m_ThreadPool = new SmartThreadPool(startInfo);
            m_threadPoolWatchdog = new Timer(ThreadPoolWatchdog, null, 0, 1000);
        }

        public static int FireAndForgetCount()
        {
            const int MAX_SYSTEM_THREADS = 200;

            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.QueueUserWorkItem:
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

        /// <summary>
        /// Additional information about threads in the main thread pool. Used to time how long the
        /// thread has been running, and abort it if it has timed-out.
        /// </summary>
        private class ThreadInfo
        {
            public long ThreadFuncNum { get; set; }
            public string StackTrace { get; set; }
            private string context;
            public bool LogThread { get; set; }

            public IWorkItemResult WorkItem { get; set; }
            public Thread Thread { get; set; }
            public bool Running { get; set; }
            public bool Aborted { get; set; }
            private int started;
            public bool DoTimeout;

            public ThreadInfo(long threadFuncNum, string context, bool dotimeout = true)
            {
                ThreadFuncNum = threadFuncNum;
                this.context = context;
                LogThread = false;
                Thread = null;
                Running = false;
                Aborted = false;
                DoTimeout = dotimeout;
            }

            public void Started()
            {
                Thread = Thread.CurrentThread;
                started = EnvironmentTickCount();
                Running = true;
            }

            public void Ended()
            {
                Running = false;
            }

            public int Elapsed()
            {
                return EnvironmentTickCountSubtract(started);
            }

            public void Abort()
            {
                Aborted = true;
                WorkItem.Cancel(true);
            }

            /// <summary>
            /// Returns the thread's stack trace.
            /// </summary>
            /// <remarks>
            /// May return one of two stack traces. First, tries to get the thread's active stack
            /// trace. But this can fail, so as a fallback this method will return the stack
            /// trace that was active when the task was queued.
            /// </remarks>
            public string GetStackTrace()
            {
                string ret = (context == null) ? "" : ("(" + context + ") ");

                StackTrace activeStackTrace = Util.GetStackTrace(Thread);
                if (activeStackTrace != null)
                    ret += activeStackTrace.ToString();
                else if (StackTrace != null)
                    ret += "(Stack trace when queued) " + StackTrace;
                // else, no stack trace available

                return ret;
            }
        }

        private static long nextThreadFuncNum = 0;
        private static long numQueuedThreadFuncs = 0;
        private static long numRunningThreadFuncs = 0;
        private static long numTotalThreadFuncsCalled = 0;
        private static Int32 threadFuncOverloadMode = 0;

        public static long TotalQueuedFireAndForgetCalls { get { return numQueuedThreadFuncs; } }
        public static long TotalRunningFireAndForgetCalls { get { return numRunningThreadFuncs; } }

        // Maps (ThreadFunc number -> Thread)
        private static ConcurrentDictionary<long, ThreadInfo> activeThreads = new ConcurrentDictionary<long, ThreadInfo>();

        private static readonly int THREAD_TIMEOUT = 10 * 60 * 1000;    // 10 minutes

        /// <summary>
        /// Finds threads in the main thread pool that have timed-out, and aborts them.
        /// </summary>
        private static void ThreadPoolWatchdog(object state)
        {
            foreach (KeyValuePair<long, ThreadInfo> entry in activeThreads)
            {
                ThreadInfo t = entry.Value;
                if (t.DoTimeout && t.Running && !t.Aborted && (t.Elapsed() >= THREAD_TIMEOUT))
                {
                    m_log.WarnFormat("Timeout in threadfunc {0} ({1}) {2}", t.ThreadFuncNum, t.Thread.Name, t.GetStackTrace());
                    t.Abort();

                    ThreadInfo dummy;
                    activeThreads.TryRemove(entry.Key, out dummy);

                    // It's possible that the thread won't abort. To make sure the thread pool isn't
                    // depleted, increase the pool size.
//                    m_ThreadPool.MaxThreads++;
                }
            }
        }

        public static long TotalFireAndForgetCallsMade { get { return numTotalThreadFuncsCalled; } }

        public static Dictionary<string, int> GetFireAndForgetCallsMade()
        {
            return new Dictionary<string, int>(m_fireAndForgetCallsMade);
        }

        private static Dictionary<string, int> m_fireAndForgetCallsMade = new Dictionary<string, int>();

        public static Dictionary<string, int> GetFireAndForgetCallsInProgress()
        {
            return new Dictionary<string, int>(m_fireAndForgetCallsInProgress);
        }

        private static Dictionary<string, int> m_fireAndForgetCallsInProgress = new Dictionary<string, int>();

        public static void FireAndForget(System.Threading.WaitCallback callback)
        {
            FireAndForget(callback, null, null);
        }

        public static void FireAndForget(System.Threading.WaitCallback callback, object obj)
        {
            FireAndForget(callback, obj, null);
        }

        public static void FireAndForget(System.Threading.WaitCallback callback, object obj, string context, bool dotimeout = true)
        {
            Interlocked.Increment(ref numTotalThreadFuncsCalled);
            WaitCallback realCallback;

            bool loggingEnabled = LogThreadPool > 0;

            long threadFuncNum = Interlocked.Increment(ref nextThreadFuncNum);
            ThreadInfo threadInfo = new ThreadInfo(threadFuncNum, context, dotimeout);

            if (FireAndForgetMethod == FireAndForgetMethod.RegressionTest)
            {
                // If we're running regression tests, then we want any exceptions to rise up to the test code.
                realCallback =
                    o =>
                    {
                        Culture.SetCurrentCulture();
                        callback(o);
                    };
            }
            else
            {
                // When OpenSim interacts with a database or sends data over the wire, it must send this in en_US culture
                // so that we don't encounter problems where, for instance, data is saved with a culture that uses commas
                // for decimals places but is read by a culture that treats commas as number seperators.
                realCallback = o =>
                {
                    long numQueued1 = Interlocked.Decrement(ref numQueuedThreadFuncs);
                    long numRunning1 = Interlocked.Increment(ref numRunningThreadFuncs);
                    threadInfo.Started();
                    activeThreads[threadFuncNum] = threadInfo;

                    try
                    {
                        if ((loggingEnabled || (threadFuncOverloadMode == 1)) && threadInfo.LogThread)
                            m_log.DebugFormat("Run threadfunc {0} (Queued {1}, Running {2})", threadFuncNum, numQueued1, numRunning1);

                        Culture.SetCurrentCulture();
                        callback(o);
                    }
                    catch (ThreadAbortException)
                    {
                    }
                    catch (Exception e)
                    {
                        m_log.Error(string.Format("[UTIL]: Util STP threadfunc {0} terminated with error ", threadFuncNum), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref numRunningThreadFuncs);
                        threadInfo.Ended();
                        ThreadInfo dummy;
                        activeThreads.TryRemove(threadFuncNum, out dummy);
                        if ((loggingEnabled || (threadFuncOverloadMode == 1)) && threadInfo.LogThread)
                            m_log.DebugFormat("Exit threadfunc {0} ({1})", threadFuncNum, FormatDuration(threadInfo.Elapsed()));
                    }
                };
            }

            long numQueued = Interlocked.Increment(ref numQueuedThreadFuncs);
            try
            {
                threadInfo.LogThread = false;

                switch (FireAndForgetMethod)
                {
                    case FireAndForgetMethod.RegressionTest:
                    case FireAndForgetMethod.None:
                        realCallback.Invoke(obj);
                        break;
                    case FireAndForgetMethod.QueueUserWorkItem:
                        ThreadPool.QueueUserWorkItem(realCallback, obj);
                        break;
                    case FireAndForgetMethod.SmartThreadPool:
                        if (m_ThreadPool == null)
                            InitThreadPool(2, 15);
                        threadInfo.WorkItem = m_ThreadPool.QueueWorkItem((cb, o) => cb(o), realCallback, obj);
                        break;
                    case FireAndForgetMethod.Thread:
                        Thread thread = new Thread(delegate(object o) { realCallback(o); });
                        thread.Start(obj);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception)
            {
                Interlocked.Decrement(ref numQueuedThreadFuncs);
                ThreadInfo dummy;
                activeThreads.TryRemove(threadFuncNum, out dummy);
                throw;
            }
        }

        /// <summary>
        /// Returns whether the thread should be logged. Some very common threads aren't logged,
        /// to avoid filling up the log.
        /// </summary>
        /// <param name="stackTrace">A partial stack trace of where the thread was queued</param>
        /// <returns>Whether to log this thread</returns>
        private static bool ShouldLogThread(string stackTrace)
        {
            if (LogThreadPool < 3)
            {
                if (stackTrace.Contains("BeginFireQueueEmpty"))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a stack trace for a thread added using FireAndForget().
        /// </summary>
        /// <param name="full">Will contain the full stack trace</param>
        /// <param name="partial">Will contain only the first frame of the stack trace</param>
        private static void GetFireAndForgetStackTrace(out string full, out string partial)
        {
            string src = Environment.StackTrace;
            string[] lines = src.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            StringBuilder dest = new StringBuilder(src.Length);

            bool started = false;
            bool first = true;
            partial = "";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (!started)
                {
                    // Skip the initial stack frames, because they're of no interest for debugging
                    if (line.Contains("StackTrace") || line.Contains("FireAndForget"))
                        continue;
                    started = true;
                }

                if (first)
                {
                    line = line.TrimStart();
                    first = false;
                    partial = line;
                }

                bool last = (i == lines.Length - 1);
                if (last)
                    dest.Append(line);
                else
                    dest.AppendLine(line);
            }

            full = dest.ToString();
        }

#pragma warning disable 0618
        /// <summary>
        /// Return the stack trace of a different thread.
        /// </summary>
        /// <remarks>
        /// This is complicated because the thread needs to be paused in order to get its stack
        /// trace. And pausing another thread can cause a deadlock. This method attempts to
        /// avoid deadlock by using a short timeout (200ms), after which it gives up and
        /// returns 'null' instead of the stack trace.
        ///
        /// Take from: http://stackoverflow.com/a/14935378
        ///
        /// WARNING: this doesn't work in Mono. See https://bugzilla.novell.com/show_bug.cgi?id=571691
        ///
        /// </remarks>
        /// <returns>The stack trace, or null if failed to get it</returns>
        private static StackTrace GetStackTrace(Thread targetThread)
        {

            return null;
/*
        not only this does not work on mono but it is not longer recomended on windows.
        can cause deadlocks etc.

            if (IsPlatformMono)
            {
                // This doesn't work in Mono
                return null;
            }

            ManualResetEventSlim fallbackThreadReady = new ManualResetEventSlim();
            ManualResetEventSlim exitedSafely = new ManualResetEventSlim();

            try
            {
                new Thread(delegate()
                {
                    fallbackThreadReady.Set();
                    while (!exitedSafely.Wait(200))
                    {
                        try
                        {
                            targetThread.Resume();
                        }
                        catch (Exception)
                        {
                            // Whatever happens, do never stop to resume the main-thread regularly until the main-thread has exited safely.
                        }
                    }
                }).Start();

                fallbackThreadReady.Wait();
                // From here, you have about 200ms to get the stack-trace

                targetThread.Suspend();

                StackTrace trace = null;
                try
                {
                    trace = new StackTrace(targetThread, true);
                }
                catch (ThreadStateException)
                {
                    //failed to get stack trace, since the fallback-thread resumed the thread
                    //possible reasons:
                    //1.) This thread was just too slow
                    //2.) A deadlock ocurred
                    //Automatic retry seems too risky here, so just return null.
                }

                try
                {
                    targetThread.Resume();
                }
                catch (ThreadStateException)
                {
                    // Thread is running again already
                }

                return trace;
            }
            finally
            {
                // Signal the fallack-thread to stop
                exitedSafely.Set();
            }
*/
        }
#pragma warning restore 0618

        /// <summary>
        /// Get information about the current state of the smart thread pool.
        /// </summary>
        /// <returns>
        /// null if this isn't the pool being used for non-scriptengine threads.
        /// </returns>
        public static STPInfo GetSmartThreadPoolInfo()
        {
            if (m_ThreadPool == null)
                return null;

            STPInfo stpi = new STPInfo();
            stpi.Name = m_ThreadPool.Name;
            stpi.STPStartInfo = m_ThreadPool.STPStartInfo;
            stpi.IsIdle = m_ThreadPool.IsIdle;
            stpi.IsShuttingDown = m_ThreadPool.IsShuttingdown;
            stpi.MaxThreads = m_ThreadPool.MaxThreads;
            stpi.MinThreads = m_ThreadPool.MinThreads;
            stpi.InUseThreads = m_ThreadPool.InUseThreads;
            stpi.ActiveThreads = m_ThreadPool.ActiveThreads;
            stpi.WaitingCallbacks = m_ThreadPool.WaitingCallbacks;
            stpi.MaxConcurrentWorkItems = m_ThreadPool.Concurrency;

            return stpi;
        }

        public static void StopThreadPool()
        {
            if (m_ThreadPool == null)
                return;
            SmartThreadPool pool = m_ThreadPool;
            m_ThreadPool = null;

            try { pool.Shutdown(); } catch {}          
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

        public static long GetPhysicalMemUse()
        {
            return System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
        }

        // returns a timestamp in ms as double
        // using the time resolution avaiable to StopWatch
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]  
        public static double GetTimeStamp()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriod;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double GetTimeStampMS()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriodMS;
        }

        // doing math in ticks is usefull to avoid loss of resolution
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStampTicks()
        {
            return Stopwatch.GetTimestamp();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double TimeStampTicksToMS(long ticks)
        {
            return ticks * TimeStampClockPeriodMS;
        }

        /// <summary>
        /// Formats a duration (given in milliseconds).
        /// </summary>
        public static string FormatDuration(int ms)
        {
            TimeSpan span = new TimeSpan(ms * TimeSpan.TicksPerMillisecond);

            string str = "";
            string suffix = null;

            int hours = (int)span.TotalHours;
            if (hours > 0)
            {
                str += hours.ToString(str.Length == 0 ? "0" : "00");
                suffix = "hours";
            }

            if ((hours > 0) || (span.Minutes > 0))
            {
                if (str.Length > 0)
                    str += ":";
                str += span.Minutes.ToString(str.Length == 0 ? "0" : "00");
                if (suffix == null)
                    suffix = "min";
            }

            if ((hours > 0) || (span.Minutes > 0) || (span.Seconds > 0))
            {
                if (str.Length > 0)
                    str += ":";
                str += span.Seconds.ToString(str.Length == 0 ? "0" : "00");
                if (suffix == null)
                    suffix = "sec";
            }

            if (suffix == null)
                suffix = "ms";

            if (span.TotalMinutes < 1)
            {
                int ms1 = span.Milliseconds;
                if (str.Length > 0)
                {
                    ms1 /= 100;
                    str += ".";
                }
                str += ms1.ToString("0");
            }

            str += " " + suffix;

            return str;
        }

        /// <summary>
        /// Prints the call stack at any given point. Useful for debugging.
        /// </summary>
        public static void PrintCallStack()
        {
            PrintCallStack(m_log.DebugFormat);
        }

        public delegate void DebugPrinter(string msg, params Object[] parm);
        public static void PrintCallStack(DebugPrinter printer)
        {
            StackTrace stackTrace = new StackTrace(true);           // get call stack
            StackFrame[] stackFrames = stackTrace.GetFrames();  // get method calls (frames)

            // write call stack method names
            foreach (StackFrame stackFrame in stackFrames)
            {
                MethodBase mb = stackFrame.GetMethod();
                printer("{0}.{1}:{2}", mb.DeclaringType, mb.Name, stackFrame.GetFileLineNumber()); // write method name
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
        public static bool ReadBoolean(XmlReader reader)
        {
            // AuroraSim uses "int" for some fields that are boolean in OpenSim, e.g. "PassCollisions". Don't fail because of this.
            reader.ReadStartElement();
            string val = reader.ReadContentAsString().ToLower();
            bool result = val.Equals("true") || val.Equals("1");
            reader.ReadEndElement();

            return result;
        }

        public static UUID ReadUUID(XmlReader reader, string name)
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

        public static Vector3 ReadVector(XmlReader reader, string name)
        {
            Vector3 vec;

            reader.ReadStartElement(name);
            vec.X = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // X or x
            vec.Y = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Y or y
            vec.Z = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Z or z
            reader.ReadEndElement();

            return vec;
        }

        public static Quaternion ReadQuaternion(XmlReader reader, string name)
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

        public static T ReadEnum<T>(XmlReader reader, string name)
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
        /// Attempts to parse a UUI into its components: UUID, name and URL.
        /// </summary>
        /// <param name="value">uuid[;endpoint[;first last[;secret]]]</param>
        /// <param name="uuid">the uuid part</param>
        /// <param name="url">the endpoint part (e.g. http://foo.com)</param>
        /// <param name="firstname">the first name part (e.g. Test)</param>
        /// <param name="lastname">the last name part (e.g User)</param>
        /// <param name="secret">the secret part</param>
        public static bool ParseUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname, out string secret)
        {
            uuid = UUID.Zero; url = string.Empty; firstname = "Unknown"; lastname = "UserUPUUI"; secret = string.Empty;

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
        /// For foreign avatars, extracts their original name and Server URL from their First Name and Last Name.
        /// </summary>
        public static bool ParseForeignAvatarName(string firstname, string lastname,
            out string realFirstName, out string realLastName, out string serverURI)
        {
            realFirstName = realLastName = serverURI = string.Empty;

            if (!lastname.Contains("@"))
                return false;

            if (!firstname.Contains("."))
                return false;

            realFirstName = firstname.Split('.')[0];
            realLastName = firstname.Split('.')[1];
            serverURI = new Uri("http://" + lastname.Replace("@", "")).ToString();

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
                    return CalcUniversalIdentifier(id, agentsURI, parts[0] + " " + parts[1]);
            }

            return CalcUniversalIdentifier(id, agentsURI, firstName + " " + lastName);
        }

        private static string CalcUniversalIdentifier(UUID id, string agentsURI, string name)
        {
            return id.ToString() + ";" + agentsURI + ";" + name;
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

        /// <summary>
        /// Escapes the special characters used in "LIKE".
        /// </summary>
        /// <remarks>
        /// For example: EscapeForLike("foo_bar%baz") = "foo\_bar\%baz"
        /// </remarks>
        public static string EscapeForLike(string str)
        {
            return str.Replace("_", "\\_").Replace("%", "\\%");
        }

        /// <summary>
        /// Returns the name of the user's viewer.
        /// </summary>
        /// <remarks>
        /// This method handles two ways that viewers specify their name:
        /// 1. Viewer = "Firestorm-Release 4.4.2.34167", Channel = "(don't care)" -> "Firestorm-Release 4.4.2.34167"
        /// 2. Viewer = "4.5.1.38838", Channel = "Firestorm-Beta" -> "Firestorm-Beta 4.5.1.38838"
        /// </remarks>
        public static string GetViewerName(AgentCircuitData agent)
        {
            string name = agent.Viewer;
            if (name == null)
                name = "";
            else
                name = name.Trim();

            // Check if 'Viewer' is just a version number. If it's *not*, then we
            // assume that it contains the real viewer name, and we return it.
            foreach (char c in name)
            {
                if (Char.IsLetter(c))
                    return name;
            }

            // The 'Viewer' string contains just a version number. If there's anything in
            // 'Channel' then assume that it's the viewer name.
            if ((agent.Channel != null) && (agent.Channel.Length > 0))
                name = agent.Channel.Trim() + " " + name;

            return name;
        }

        public static void LogFailedXML(string message, string xml)
        {
            int length = xml.Length;
            if (length > 2000)
                xml = xml.Substring(0, 2000) + "...";

            for (int i = 0 ; i < xml.Length ; i++)
            {
                if (xml[i] < 0x20)
                {
                    xml = "Unprintable binary data";
                    break;
                }
            }

            m_log.ErrorFormat("{0} Failed XML ({1} bytes) = {2}", message, length, xml);
        }

        /// <summary>
        /// Performs a high quality image resize
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        /// <returns>Resized image</returns>
        public static Bitmap ResizeImageSolid(Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            using (ImageAttributes atrib = new ImageAttributes())
            using (Graphics graphics = Graphics.FromImage(result))
            {
                atrib.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                atrib.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

                graphics.DrawImage(image,new Rectangle(0,0, result.Width, result.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, atrib);
            }

            return result;
        }

    }

/*  don't like this code
    public class DoubleQueue<T> where T:class
    {
        private Queue<T> m_lowQueue = new Queue<T>();
        private Queue<T> m_highQueue = new Queue<T>();

        private object m_syncRoot = new object();
        private Semaphore m_s = new Semaphore(0, 1);

        public DoubleQueue()
        {
        }

        public virtual int Count
        {
            get
            {
                lock (m_syncRoot)
                    return m_highQueue.Count + m_lowQueue.Count;
            }
        }

        public virtual void Enqueue(T data)
        {
            Enqueue(m_lowQueue, data);
        }

        public virtual void EnqueueLow(T data)
        {
            Enqueue(m_lowQueue, data);
        }

        public virtual void EnqueueHigh(T data)
        {
            Enqueue(m_highQueue, data);
        }

        private void Enqueue(Queue<T> q, T data)
        {
            lock (m_syncRoot)
            {
                q.Enqueue(data);
                m_s.WaitOne(0);
                m_s.Release();
            }
        }

        public virtual T Dequeue()
        {
            return Dequeue(Timeout.Infinite);
        }

        public virtual T Dequeue(int tmo)
        {
            return Dequeue(TimeSpan.FromMilliseconds(tmo));
        }

        public virtual T Dequeue(TimeSpan wait)
        {
            T res = null;

            if (!Dequeue(wait, ref res))
                return null;

            return res;
        }

        public bool Dequeue(int timeout, ref T res)
        {
            return Dequeue(TimeSpan.FromMilliseconds(timeout), ref res);
        }

        public bool Dequeue(TimeSpan wait, ref T res)
        {
            if (!m_s.WaitOne(wait))
                return false;

            lock (m_syncRoot)
            {
                if (m_highQueue.Count > 0)
                    res = m_highQueue.Dequeue();
                else if (m_lowQueue.Count > 0)
                    res = m_lowQueue.Dequeue();

                if (m_highQueue.Count == 0 && m_lowQueue.Count == 0)
                    return true;

                try
                {
                    m_s.Release();
                }
                catch
                {
                }

                return true;
            }
        }

        public virtual void Clear()
        {

            lock (m_syncRoot)
            {
                // Make sure sem count is 0
                m_s.WaitOne(0);

                m_lowQueue.Clear();
                m_highQueue.Clear();
            }
        }
    }
*/
    public class BetterRandom
    {
        private const int BufferSize = 1024;  // must be a multiple of 4
        private byte[] RandomBuffer;
        private int BufferOffset;
        private RNGCryptoServiceProvider rng;
        public BetterRandom()
        {
            RandomBuffer = new byte[BufferSize];
            rng = new RNGCryptoServiceProvider();
            BufferOffset = RandomBuffer.Length;
        }
        private void FillBuffer()
        {
            rng.GetBytes(RandomBuffer);
            BufferOffset = 0;
        }
        public int Next()
        {
            if (BufferOffset >= RandomBuffer.Length)
            {
                FillBuffer();
            }
            int val = BitConverter.ToInt32(RandomBuffer, BufferOffset) & 0x7fffffff;
            BufferOffset += sizeof(int);
            return val;
        }
        public int Next(int maxValue)
        {
            return Next() % maxValue;
        }
        public int Next(int minValue, int maxValue)
        {
            if (maxValue < minValue)
            {
                throw new ArgumentOutOfRangeException("maxValue must be greater than or equal to minValue");
            }
            int range = maxValue - minValue;
            return minValue + Next(range);
        }
        public double NextDouble()
        {
            int val = Next();
            return (double)val / int.MaxValue;
        }
        public void GetBytes(byte[] buff)
        {
            rng.GetBytes(buff);
        }

    }
}
