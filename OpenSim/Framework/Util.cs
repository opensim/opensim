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
using System.Runtime.CompilerServices;
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
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

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

        FoldingShift = 13,  // number of bit shifts from normal perm to folded or back (same as Transfer shift below)
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
        Thread
    }

    /// <summary>
    /// Class for delivering SmartThreadPool statistical information
    /// </summary>
    /// <remarks>
    /// We do it this way so that we do not directly expose STP.
    /// </remarks>
    public class STPInfo
    {
        public string Name;
        public bool IsIdle;
        public bool IsShuttingDown;
        public int MaxThreads;
        public int MinThreads;
        public int InUseThreads;
        public int ActiveThreads;
        public int WaitingCallbacks;
        public int MaxConcurrentWorkItems;
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
            TimeStampClockPeriod = 1.0D / (double)Stopwatch.Frequency;
            TimeStampClockPeriodMS = 1e3 * TimeStampClockPeriod;
            m_log.Info($"[UTIL] TimeStamp clock with period of {Math.Round(TimeStampClockPeriodMS, 6, MidpointRounding.AwayFromZero)}ms");
        }

        private static uint nextXferID = 5000;

        // Get a list of invalid file characters (OS dependent)
        private static readonly string regexInvalidFileChars = $"[{new String(Path.GetInvalidFileNameChars())}]";
        private static readonly string regexInvalidPathChars = $"[{new String(Path.GetInvalidPathChars())}]";
        private static readonly object XferLock = new();

        public static readonly char[] SplitCommaArray = [','];
        public static readonly char[] SplitDotArray = ['.'];
        public static readonly char[] SplitColonArray = [':'];
        public static readonly char[] SplitSemicolonArray = [';'];
        public static readonly char[] SplitSlashArray = ['/'];
        public static readonly char[] SplitSpaceArray = [' '];
        public static readonly char[] SplitSlashSpaceArray = ['/', ' '];
        public static readonly char[] SplitDoubleQuoteSpaceArray = ['"', ' '];
        public static readonly char[] SplitSlashColonArray = ['/', ':'];

        public static readonly XmlReaderSettings SharedXmlReaderSettings = new()
        {
            IgnoreWhitespace = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Ignore,
            MaxCharactersInDocument = 10_000_000
        };

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
        public static readonly Regex PermissiveUUIDPattern = new(rawUUIDPattern);
        public static readonly Regex UUIDPattern = new(string.Format("^{0}$", rawUUIDPattern));

        public static FireAndForgetMethod DefaultFireAndForgetMethod = FireAndForgetMethod.SmartThreadPool;
        public static FireAndForgetMethod FireAndForgetMethod = DefaultFireAndForgetMethod;

        public static readonly string UUIDZeroString = UUID.Zero.ToString();

        public const bool IsPlatformMono = false;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberBits(uint n)
        {
            return System.Numerics.BitOperations.Log2(n) + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int intLog2(uint n)
        {
            return System.Numerics.BitOperations.Log2(n);
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
            return (b * a) + (c * (1 - a));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] UTF8Getbytes(string s)
        {
            return UTF8.GetBytes(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] UTF8NBGetbytes(string s)
        {
            return UTF8NoBomEncoding.GetBytes(s);
        }

        /// <value>
        /// Well known UUID for the blank texture used in the Linden SL viewer version 1.20 (and hopefully onwards)
        /// </value>
        public static UUID BLANK_TEXTURE_UUID = new("5748decc-f629-461c-9a36-a35a221fe21f");

        #region Vector Equations

        /// <summary>
        /// Get the distance between two 3d vectors
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <param name="b">A 3d vector</param>
        /// <returns>The distance between the two vectors</returns>
        public static double GetDistanceTo(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
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
            return Vector3.DistanceSquared(a, b) < (amount * amount);
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
            Vector3 v = new(a.X, a.Y, a.Z);
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
            float tr = (fwd.X + left.Y + up.Z + 1.0f);

            if (tr >= 1.0)
            {
                s = 0.5f / MathF.Sqrt(tr);
                return new Quaternion(
                        (left.Z - up.Y) * s,
                        (up.X - fwd.Z) * s,
                        (fwd.Y - left.X) * s,
                        (float)0.25 / s);
            }
            else
            {
                float max = (left.Y > up.Z) ? left.Y : up.Z;

                if (max < fwd.X)
                {
                    s = MathF.Sqrt(fwd.X - (left.Y + up.Z) + 1.0f);
                    float x = s * 0.5f;
                    s = 0.5f / s;
                    return new Quaternion(
                            x,
                            (fwd.Y + left.X) * s,
                            (up.X + fwd.Z) * s,
                            (left.Z - up.Y) * s);
                }
                else if (max == left.Y)
                {
                    s = MathF.Sqrt(left.Y - (up.Z + fwd.X) + 1.0f);
                    float y = s * 0.5f;
                    s = 0.5f / s;
                    return new Quaternion(
                            (fwd.Y + left.X) * s,
                            y,
                            (left.Z + up.Y) * s,
                            (up.X - fwd.Z) * s);
                }
                else
                {
                    s = MathF.Sqrt(up.Z - (fwd.X + left.Y) + 1.0f);
                    float z = s * 0.5f;
                    s = 0.5f / s;
                    return new Quaternion(
                            (up.X + fwd.Z) * s,
                            (left.Z + up.Y) * s,
                            z,
                            (fwd.Y - left.X) * s);
                }
            }
        }

        // legacy, do not use
        public static Random RandomClass
        {
            get {  return Random.Shared;}
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong UIntsToLong(uint X, uint Y)
        {
            return ((ulong)X << 32) | (ulong)Y;
        }

        // Regions are identified with a 'handle' made up of its world coordinates packed into a ulong.
        // Region handles are based on the coordinate of the region corner with lower X and Y
        // var regions need more work than this to get that right corner from a generic world position
        // this corner must be on a grid point
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RegionWorldLocToHandle(uint X, uint Y)
        {
            ulong handle = X & 0xffffff00; // make sure it matches grid coord points.
            handle <<= 32; // to higher half
            handle |= (Y & 0xffffff00);
            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RegionGridLocToHandle(uint X, uint Y)
        {
            ulong handle = X;
            handle <<= 40; // shift to higher half and mult by 256)
            handle |= (Y << 8);  // mult by 256)
            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegionHandleToWorldLoc(ulong handle, out uint X, out uint Y)
        {
            X = (uint)(handle >> 32);
            Y = (uint)(handle & 0xfffffffful);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegionHandleToRegionLoc(ulong handle, out uint X, out uint Y)
        {
            X = (uint)(handle >> 40) & 0x00ffffffu; //  bring from higher half, divide by 256 and clean
            Y = (uint)(handle >> 8) & 0x00ffffffu; // divide by 256 and clean
            // if you trust the uint cast then the clean can be removed.
        }

        // A region location can be 'world coordinates' (meters) or 'region grid coordinates'
        // grid coordinates have a fixed step of 256m as defined by viewers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint WorldToRegionLoc(uint worldCoord)
        {
            return worldCoord >> 8;
        }

        // Convert a region's 'region grid coordinate' to its 'world coordinate'.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RegionToWorldLoc(uint regionCoord)
        {
            return regionCoord << 8;
        }

        public static bool CompareRegionHandles(ulong handle, Vector3 handleOffset, RegionInfo region, out Vector3 regionOffset)
        {
            RegionHandleToWorldLoc(handle, out uint uhX, out uint uhY);
            double px = uhX - region.WorldLocX + (double)handleOffset.X;
            double py = uhY - region.WorldLocY + (double)handleOffset.Y;
            if (px >= 0 && px < region.RegionSizeX && py >= 0 && py < region.RegionSizeY)
            {
                regionOffset = new Vector3((float)px, (float)py, handleOffset.Z);
                return true;
            }
            regionOffset = Vector3.Zero;
            return false;
        }

        public static bool CompareRegionHandles(ulong handle, Vector3 handleOffset, ulong regionhandle, int regionSizeX, int regionSizeY, out Vector3 regionOffset)
        {
            RegionHandleToWorldLoc(handle, out uint uhX, out uint uhY);
            RegionHandleToWorldLoc(regionhandle, out uint urX, out uint urY);
            double px = uhX - urX + (double)handleOffset.X;
            double py = uhY - urY + (double)handleOffset.Y;
            if (px >= 0 && px < regionSizeX && py >= 0 && py < regionSizeY)
            {
                regionOffset = new Vector3((float)px, (float)py, handleOffset.Z);
                return true;
            }
            regionOffset = Vector3.Zero;
            return false;
        }

        public static bool CompareRegionHandles(ulong handle, Vector3 handleOffset, int regionX, int regionY, int regionSizeX, int regionSizeY, out Vector3 regionOffset)
        {
            RegionHandleToWorldLoc(handle, out uint uhX, out uint uhY);
            double px = uhX - regionX + (double)handleOffset.X;
            double py = uhY - regionY + (double)handleOffset.Y;
            if (px >= 0 && px < regionSizeX && py >= 0 && py < regionSizeY)
            {
                regionOffset = new Vector3((float)px, (float)py, handleOffset.Z);
                return true;
            }
            regionOffset = Vector3.Zero;
            return false;
        }

        public static bool checkServiceURI(string uristr, out string serviceURI, out string serviceHost, out string serviceIPstr)
        {
            serviceHost = string.Empty;
            serviceIPstr = string.Empty;
            try
            {
                Uri uri = new(uristr);
                serviceURI = uri.AbsoluteUri;
                if (uri.Port == 80)
                    serviceURI = $"{serviceURI.Trim(SplitSlashSpaceArray)}:80/";
                else if (uri.Port == 443)
                    serviceURI = $"{serviceURI.Trim(SplitSlashSpaceArray)}:443/";
                serviceHost = uri.Host;

                IPEndPoint ep = Util.getEndPoint(serviceHost, uri.Port);
                if (ep == null)
                    return false;

                serviceIPstr = ep.Address.ToString();
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
                int port = 80;

                string[] parts = inputName.Split(Util.SplitColonArray);
                int indx;
                if (parts.Length == 0)
                    return false;
                if (parts.Length == 1)
                {
                    indx = inputName.IndexOf('/');
                    if (indx < 0)
                        serverURI = $"http://{inputName}/";
                    else
                    {
                        serverURI = $"http://{inputName[..(indx + 1)]}";
                        if (indx + 2 < inputName.Length)
                            regionName = inputName[(indx + 1)..];
                    }
                }
                else
                {
                    host = parts[0];

                    if (parts.Length >= 2)
                    {
                        indx = parts[1].IndexOf('/');
                        if (indx < 0)
                        {
                            // If it's a number then assume it's a port. Otherwise, it's a region name.
                            if (!int.TryParse(parts[1], out port))
                            {
                                port = 80;
                                regionName = parts[1];
                            }
                        }
                        else
                        {
                            string portstr = parts[1][..indx];
                            if (indx + 2 < parts[1].Length)
                                regionName = parts[1][(indx + 1)..];
                            if (!int.TryParse(portstr, out port))
                                port = 80;
                        }
                    }
                    // always take the last one
                    if (parts.Length >= 3)
                    {
                        regionName = parts[2];
                    }

                    serverURI = $"http://{host}:{port}/";
                }
            }
            else
            {
                // Formats: http://grid.example.com region name
                //          http://grid.example.com "region name"
                //          http://grid.example.com

                string[] parts = inputName.Split();

                if (parts.Length == 0)
                    return false;

                serverURI = parts[0];

                int indx = serverURI.LastIndexOf('/');
                if (indx > 10)
                {
                    if (indx + 2 < inputName.Length)
                        regionName = inputName[(indx + 1)..];
                    serverURI = inputName[..(indx + 1)];
                }
                else if (parts.Length >= 2)
                {
                    regionName = inputName[serverURI.Length..];
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

            if (!string.IsNullOrEmpty(regionName))
                regionName = regionName.Trim(SplitDoubleQuoteSpaceArray);
            serverURI = uri.AbsoluteUri;
            if (uri.Port == 80)
                serverURI = $"{serverURI.Trim(SplitSlashSpaceArray)}:80/";
            else if (uri.Port == 443)
                serverURI = $"{serverURI.Trim(SplitSlashSpaceArray)}:443/";
            return true;
        }

        //obsolete  use Math.Clamp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Clamp<T>(T x, T min, T max)
            where T : IComparable<T>
        {
            if (x.CompareTo(max) > 0)
                return max;

            if (x.CompareTo(min) < 0)
                return min;
            return x;
        }

        // Clamp the maximum magnitude of a vector
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampV(Vector3 x, float max)
        {
            float lenSq = x.LengthSquared();
            if (lenSq > (max * max))
            {
                lenSq = max / MathF.Sqrt(lenSq);
                x *= lenSq;
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
            XmlDocument xd = new();

            xd.LoadXml(rawXml);

            StringBuilder sb = new();
            StringWriter sw = new(sb);

            XmlTextWriter xtw = new(sw)
            {
                Formatting = Formatting.Indented
            };

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

        // helper for services responses.
        // they send identical messages, but each own chars case 
        public static byte[] sucessResultSuccess = osUTF8.GetASCIIBytes("<?xml version =\"1.0\"?><ServerResponse><Result>Success</Result></ServerResponse>");

        public static byte[] ResultFailureMessageStart = osUTF8.GetASCIIBytes("<?xml version =\"1.0\"?><ServerResponse><Result>Failure</Result><Message>");
        public static byte[] ResultFailureMessageEnd = osUTF8.GetASCIIBytes("</Message></ServerResponse>");
        public static byte[] ResultFailureMessage(string message)
        {
            osUTF8 res = new(ResultFailureMessageStart.Length + ResultFailureMessageEnd.Length + message.Length);
            res.Append(ResultFailureMessageStart);
            res.Append(message);
            res.Append(ResultFailureMessageEnd);
            return res.ToArray();
        }

        public static byte[] DocToBytes(XmlDocument doc)
        {
            using MemoryStream ms = new();
            using XmlTextWriter xw = new(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsHexa(char c)
        {
            if (c >= '0' && c <= '9')
                return true;
            if (c >= 'a' && c <= 'f')
                return true;
            if (c >= 'A' && c <= 'F')
                return true;

            return false;
        }

        public static List<UUID> GetUUIDsOnString(ref string s, int indx, int len)
        {
            var ids = new List<UUID>();

            int endA = indx + len;
            if (endA > s.Length)
                endA = s.Length;
            if (endA - indx < 36)
                return ids;

            int endB = endA - 26;
            endA -= 35;

            int idbase;
            int next;
            int retry;

            while (indx < endA)
            {
                for (; indx < endA; ++indx)
                {
                    if (IsHexa(s[indx]))
                        break;
                }
                if (indx == endA)
                    break;

                idbase = indx;
                for (; indx < endB; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                    if (indx - idbase >= 8)
                        ++idbase;
                }

                if (s[indx] != '-')
                    continue;

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }
                ++indx;
                //retry = indx;

                next = indx + 12;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (UUID.TryParse(s.AsSpan(idbase, 36), out UUID u))
                {
                    ids.Add(u);
                }
                ++indx;
            }

            return ids;
        }
        public static List<UUID> GetUUIDsOnString(ReadOnlySpan<char> s)
        {
            var ids = new List<UUID>();
            if (s.Length < 36)
                return ids;

            int indx = 8;
            while (indx < s.Length - 28)
            {
                if (s[indx] == '-')
                {
                    if (UUID.TryParse(s.Slice(indx - 8, 36), out UUID id))
                    {
                        if (id.IsNotZero())
                            ids.Add(id);
                        indx += 37;
                    }
                    else
                        indx += 9;
                }
                else
                    indx++;
            }
            return ids;
        }

        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsHexa(byte c)
        {
            if (c >= '0' && c <= '9')
                return true;
            if (c >= 'a' && c <= 'f')
                return true;
            if (c >= 'A' && c <= 'F')
                return true;

            return false;
        }

        public static List<UUID> GetUUIDsOnData(byte[] s, int indx, int len)
        {
            var ids = new List<UUID>();

            int endA = indx + len;
            if (endA > s.Length)
                endA = s.Length;
            if (endA - indx < 36)
                return ids;

            int endB = endA - 26;
            endA -= 35;

            int idbase;
            int next;
            int retry;

            while (indx < endA)
            {
                for (; indx < endA; ++indx)
                {
                    if (IsHexa(s[indx]))
                        break;
                }
                if (indx == endA)
                    break;

                idbase = indx;
                for (; indx < endB; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                    if (indx - idbase >= 8)
                        ++idbase;
                }

                if (s[indx] != '-')
                    continue;

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }

                ++indx;
                retry = indx;
                next = indx + 4;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (s[indx] != '-')
                {
                    indx = retry;
                    continue;
                }
                ++indx;
                //retry = indx;

                next = indx + 12;
                for (; indx < next; ++indx)
                {
                    if (!IsHexa(s[indx]))
                        break;
                }
                if (indx != next)
                    continue;

                if (UUID.TryParse(Encoding.ASCII.GetString(s, idbase, 36), out UUID u))
                {
                    ids.Add(u);
                }
                ++indx;
            }

            return ids;
        }
        */

        /// <summary>
        /// Is the platform Windows?
        /// </summary>
        /// <returns>true if so, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            /*
            PlatformID platformId = Environment.OSVersion.Platform;

            return (platformId == PlatformID.Win32NT
                || platformId == PlatformID.Win32S
                || platformId == PlatformID.Win32Windows
                || platformId == PlatformID.WinCE);
            */
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

            if (Environment.Is64BitProcess)
                nativeLibraryPath = Path.Combine(Path.Combine(path, "lib64"), libraryName);
            else
                nativeLibraryPath = Path.Combine(Path.Combine(path, "lib32"), libraryName);

            m_log.Debug($"[UTIL]: Loading native Windows library at {nativeLibraryPath}");

            if (!NativeLibrary.TryLoad(nativeLibraryPath, out _))
            {
                m_log.Error($"[UTIL]: Couldn't find native Windows library at {nativeLibraryPath}");
                return false;
            }
            return true;
        }

        public static bool IsEnvironmentSupported(ref string reason)
        {
            if (Environment.Version.Major < 8)
            {
                reason = "Dotnet 8.0 is required";
                return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double UnixTimeSinceEpochSecs()
        {
            TimeSpan t = DateTime.UtcNow - UnixEpoch;
            return t.TotalSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnixTimeSinceEpoch()
        {
            TimeSpan t = DateTime.UtcNow - UnixEpoch;
            return (int)t.TotalSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong UnixTimeSinceEpoch_uS()
        {
            TimeSpan t = DateTime.UtcNow - UnixEpoch;
            return (ulong)(t.TotalMilliseconds * 1000);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToUnixTime(DateTime stamp)
        {
            TimeSpan t = stamp.ToUniversalTime() - UnixEpoch;
            return (int)t.TotalSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ToDateTime(ulong seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ToDateTime(int seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        /// <summary>
        /// Return an md5 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Md5Hash(string data)
        {
            return Md5Hash(data, Encoding.Default);
        }

        public static string Md5Hash(string data, Encoding encoding)
        {
            byte[] dataMd5 = ComputeMD5Hash(data, encoding);
            StringBuilder sb = new();
            for (int i = 0; i < dataMd5.Length; i++)
                sb.AppendFormat("{0:x2}", dataMd5[i]);
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] ComputeMD5Hash(string data, Encoding encoding)
        {
            return MD5.HashData(encoding.GetBytes(data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UUID ComputeASCIIMD5UUID(string data)
        {
            byte[] bytes = MD5.HashData(Encoding.ASCII.GetBytes(data));
            UUID uuid = new(bytes, 2);
            uuid.c &= 0x0fff;
            uuid.c |= 0x3000;
            uuid.d &= 0x3f;
            uuid.d |= 0x80;

            return uuid;
        }

        /// <summary>
        /// Return an SHA1 hash
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char LowNibbleToHexByteCharLowcaps(byte b)
        {
            b &= 0x0f;
            return (char)(b > 9 ? b + 0x57 : b + '0');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char HighNibbleToHexByteCharLowcaps(byte b)
        {
            b >>= 4;
            return (char)(b > 9 ? b + 0x57 : b + '0');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char LowNibbleToHexByteCharHighcaps(byte b)
        {
            b &= 0x0f;
            return (char)(b > 9 ? b + 0x37 : b + '0');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char HighNibbleToHexByteCharHighcaps(byte b)
        {
            b >>= 4;
            return (char)(b > 9 ? b + 0x37 : b + '0');
        }

        public static unsafe string bytesToHexString(byte[] bytes, bool lowerCaps)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return string.Create(2 * bytes.Length, bytes, (chars, bytes) =>
            {
                fixed (char* dstb = chars)
                fixed (byte* srcb = bytes)
                {
                    char* dst = dstb;
                    if (lowerCaps)
                    {
                        for (int i = 0; i < bytes.Length; ++i)
                        {
                            byte b = srcb[i];
                            *dst++ = HighNibbleToHexByteCharLowcaps(b);
                            *dst++ = LowNibbleToHexByteCharLowcaps(b);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < bytes.Length; ++i)
                        {
                            byte b = srcb[i];
                            *dst++ = HighNibbleToHexByteCharLowcaps(b);
                            *dst++ = LowNibbleToHexByteCharLowcaps(b);
                        }
                    }
                }
            });
        }

        public static unsafe string bytesToLowcaseHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return string.Create(2 * bytes.Length, bytes, (chars, bytes) =>
            {
                fixed (char* dstb = chars)
                fixed (byte* srcb = bytes)
                {
                    char* dst = dstb;
                    for (int i = 0; i < bytes.Length; ++i)
                    {
                        byte b = srcb[i];
                        *dst++ = HighNibbleToHexByteCharLowcaps(b);
                        *dst++ = LowNibbleToHexByteCharLowcaps(b);
                    }
                }
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SHA1Hash(string data, Encoding enc)
        {
            return SHA1Hash(enc.GetBytes(data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SHA1Hash(string data)
        {
            return SHA1Hash(Encoding.Default.GetBytes(data));
        }

        /// <summary>
        /// Return an SHA1 hash
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SHA1Hash(byte[] data)
        {
            byte[] hash = ComputeSHA1Hash(data);
            return bytesToHexString(hash, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] ComputeSHA1Hash(byte[] src)
        {
            return SHA1.HashData(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SHA256Hash(byte[] data)
        {
            return SHA256Hash(data.AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SHA256Hash(ReadOnlySpan<byte> data)
        {
            byte[] hash = SHA256.HashData(data);
            return bytesToHexString(hash, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UUID ComputeSHA1UUID(string src)
        {
            return ComputeSHA1UUID(Encoding.Default.GetBytes(src));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UUID ComputeASCIISHA1UUID(string src)
        {
            return ComputeSHA1UUID(Encoding.ASCII.GetBytes(src));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UUID ComputeSHA1UUID(byte[] src)
        {
            byte[] ret = SHA1.HashData(src);
            UUID uuid = new(ret, 2);
            uuid.c &= 0x0fff;
            uuid.c |= 0x5000;
            uuid.d &= 0x3f;
            uuid.d |= 0x80;
            return uuid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AESEncrypt(ReadOnlySpan<char> secret, ReadOnlySpan<char> plainText)
        {
            return AESEncryptString(secret, plainText, string.Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AESEncryptTo(ReadOnlySpan<char> secret, ReadOnlySpan<char> plainText, ReadOnlySpan<char> ivString)
        {
            return AESEncryptString(secret, plainText, ivString);
        }
        /// <summary>
        /// AES Encrypt a string using a password and a random or custom Initialization 
        /// Vector
        /// </summary>
        /// <param name="secret">The secret encryption password or key.</param>
        /// <param name="plainText">The string or text to encrypt.</param>
        /// <param name="ivString">(optional) A string used to generate the Initialization Vector eg; an avatarID, a SecureSessionID, an object or script 
        /// ID...</param>
        /// <returns>A string composed by the Initialization Vector bytes and the 
        /// encrypted text bytes converted to lower case HexString and separated by " : " </returns>
        private static string AESEncryptString(ReadOnlySpan<char> secret, ReadOnlySpan<char> plainText, ReadOnlySpan<char> ivString)
        {
            if(secret.Length == 0 || plainText.Length == 0)
                return string.Empty;

            byte[] iv = ivString.Length == 0 ?
                    MD5.Create().ComputeHash(UUID.Random().GetBytes()) :
                    MD5.Create().ComputeHash(Utils.StringToBytesNoTerm(ivString)); 
            byte[] aesKey = SHA256.Create().ComputeHash(Utils.StringToBytesNoTerm(secret));
            byte[] encryptedText;
 
            using (Aes aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new())
                {
                    using (CryptoStream cryptoStream = new(memoryStream, encryptor, CryptoStreamMode.Write))
                        using (StreamWriter streamWriter = new(cryptoStream))
                            streamWriter.Write(plainText);
                    encryptedText = memoryStream.ToArray();
                }
            }

            return $"{Convert.ToHexString(iv)}:{Convert.ToHexString(encryptedText).ToLower()}";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> AESDecrypt(ReadOnlySpan<char> secret, ReadOnlySpan<char> encryptedText)
        {
            return AESDecryptString(secret, encryptedText, new ReadOnlySpan<char>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> AESDecryptFrom(ReadOnlySpan<char> secret, ReadOnlySpan<char> encryptedText, ReadOnlySpan<char> ivString)
        {
            return AESDecryptString(secret, encryptedText, ivString);
        }

        /// <summary>
        /// AES Decrypt the string encrypted by AESEncryptString with the same password 
        /// and ivString used in the encryption.
        /// </summary>
        /// <param name="secret">The secret decryption password or key.</param>
        /// <param name="encryptedText">The encrypted string or text.</param>
        /// <param name="ivString">The string used to generate the Initialization Vector 
        /// if used in the encription. eg; an avatarID, a SecureSessionID, an object or 
        /// script ID...</param>
        /// <returns>The decrypted string.</returns>
        private static ReadOnlySpan<char> AESDecryptString(ReadOnlySpan<char> secret, ReadOnlySpan<char> encryptedText, ReadOnlySpan<char> ivString)
        {
            if(secret.Length == 0 || encryptedText.Length == 0)
                return string.Empty;

            int sep = encryptedText.IndexOf(':');
            if(sep < 0)
                return string.Empty;

            byte[] iv;
            byte[] buffer;
            try
            {
                iv = ivString.Length == 0 ?
                    Convert.FromHexString(encryptedText[..sep]):
                    MD5.HashData(Utils.StringToBytesNoTerm(ivString));
                buffer = Convert.FromHexString(encryptedText[(sep + 1)..]);
            }
            catch
            {
                return string.Empty;
            }

            byte[] aesKey = SHA256.HashData(Utils.StringToBytesNoTerm(secret));

            using Aes aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using MemoryStream memoryStream = new(buffer);
            using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
            using StreamReader streamReader = new(cryptoStream);
          
            return streamReader.ReadToEnd();
        }

        private static readonly string pathSSLRsaPriv = Path.Combine("SSL","src");
        private static readonly string pathSSLcerts = Path.Combine("SSL","ssl");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateOrUpdateSelfsignedCert(string certFileName, string certHostName, string certHostIp, string certPassword)
        {
            CreateOrUpdateSelfsignedCertificate(certFileName, certHostName, certHostIp, certPassword);
        }

        /// <summary>
        /// Create or renew an SSL selfsigned certificate using the parameters set in the startup section of OpenSim.ini
        /// </summary>
        /// <param name="certFileName">The certificate file name.</param>
        /// <param name="certHostName">The certificate host DNS name (CN).</param>
        /// <param name="certHostIp">The certificate host IP address.</param>
        /// <param name="certPassword">The certificate password.</param>
        private static void CreateOrUpdateSelfsignedCertificate(string certFileName, string certHostName, string certHostIp, string certPassword)
        {
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName(certHostName);
            san.AddIpAddress(IPAddress.Parse(certHostIp));

            // What OpenSim check (CN).
            X500DistinguishedName dn = new($"CN={certHostName}");

            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new(dn, rsa, HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);

                // (Optional)...
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature , false));

                // (Optional) SSL Server Authentication...
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

                request.CertificateExtensions.Add(san.Build());

                X509Certificate2 certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));

                string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks);

                // Create the SSL folder and sub folders if not exists.
                if (!Directory.Exists(pathSSLRsaPriv))
                    Directory.CreateDirectory(pathSSLRsaPriv);
                if (!Directory.Exists(pathSSLcerts))
                    Directory.CreateDirectory(pathSSLcerts);

                // Store the RSA key in SSL\src\
                File.WriteAllText(Path.Combine(pathSSLRsaPriv, certFileName) + ".txt", privateKey);
                // Export and store the .pfx and .p12 certificates in SSL\ssl\.
                // Note: Pfx is a Pkcs12 certificate and both files work for OpenSim.
                string sslFileNames = Path.Combine(pathSSLcerts, certFileName);
                byte[] pfxCertBytes = string.IsNullOrEmpty(certPassword) 
                                    ? certificate.Export(X509ContentType.Pfx) 
                                    : certificate.Export(X509ContentType.Pfx, certPassword);
                File.WriteAllBytes(sslFileNames + ".pfx", pfxCertBytes);

                byte[] p12CertBytes = string.IsNullOrEmpty(certPassword) 
                                    ? certificate.Export(X509ContentType.Pkcs12) 
                                    : certificate.Export(X509ContentType.Pkcs12, certPassword);
                File.WriteAllBytes(sslFileNames + ".p12", p12CertBytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertPemToPKCS12(string certFileName, string fullChainPath, string privateKeyPath)
        {
            ConvertPemToPKCS12Certificate(certFileName, fullChainPath, privateKeyPath, null);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertPemToPKCS12(string certFileName, string fullChainPath, string privateKeyPath, string outputPassword)
        {
            ConvertPemToPKCS12Certificate(certFileName, fullChainPath, privateKeyPath, outputPassword);
        }

        /// <summary>
        /// Convert or renew .pem certificate to PKCS12 .pfx and .p12 usable by OpenSim.
        /// the parameters are set in the startup section of OpenSim.ini
        /// </summary>
        /// <param name="certFileName">The output certificate file name.</param>
        /// <param name="certPath">The path of fullchain.pem. If your CA don't provide 
        /// the fullchain file, you can set the cert.pem instead.</param>
        /// <param name="keyPath">The path of the private key (privkey.pem).</param>
        /// <param name="outputPassword">The output certificates password.</param>
        private static void ConvertPemToPKCS12Certificate(string certFileName, string certPath, string keyPath, string outputPassword)
        {
            if(string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(keyPath)){
                m_log.Error($"[UTIL PemToPKCS12]: Missing fullchain.pem or privkey.pem path!.");
                return;
            }

            // Convert .pem (like Let's Encrypt files) to X509Certificate2 certificate.
            X509Certificate2 certificate;
            try
            {
                certificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            }
            catch(CryptographicException e)
            {
                m_log.Error($"[UTIL PemToPKCS12]: {e.Message}" );
                return;
            }

            // Create the SSL folder and ssl sub folder if not exists.
            if (!Directory.Exists(pathSSLcerts))
                Directory.CreateDirectory(pathSSLcerts);

            string sslFileNames = System.IO.Path.Combine(pathSSLcerts, certFileName);
            // Export and store the .pfx and .p12 certificates in SSL\ssl\.
            byte[] pfxCertBytes = string.IsNullOrEmpty(outputPassword)
                                ? certificate.Export(X509ContentType.Pfx)
                                : certificate.Export(X509ContentType.Pfx, outputPassword);
            File.WriteAllBytes(sslFileNames + ".pfx", pfxCertBytes);

            byte[] p12CertBytes = string.IsNullOrEmpty(outputPassword) 
                                ? certificate.Export(X509ContentType.Pkcs12) 
                                : certificate.Export(X509ContentType.Pkcs12, outputPassword);
            File.WriteAllBytes(sslFileNames + ".p12", p12CertBytes);
            
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FieldToString(byte[] bytes)
        {
            return FieldToString(bytes, String.Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FieldToASCIIString(byte[] bytes, int limit)
        {
            return CleanString(Encoding.ASCII.GetString(bytes, 0, limit < bytes.Length ? limit : bytes.Length));
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

            StringBuilder output = new();
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
                            output.Append($"{bytes[i + j]:X2} ");
                        else
                            output.Append("   ");
                    }

                    for (int j = 0; j < 16 && (i + j) < bytes.Length; j++)
                    {
                        if (bytes[i + j] >= 0x20 && bytes[i + j] < 0x7E)
                            output.Append((char)bytes[i + j]);
                        else
                            output.Append('.');
                    }
                }
            }

            return output.ToString();
        }

        private static readonly IPEndPoint dummyIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        private static readonly ExpiringCacheOS<string, IPAddress> dnscache = new(30000);
        private static readonly ExpiringCacheOS<SocketAddress, EndPoint> EndpointsCache = new(300000);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EndPoint GetEndPoint(SocketAddress sckaddr)
        {
            if (!EndpointsCache.TryGetValue(sckaddr, 300000, out EndPoint ep))
            {
                ep = dummyIPEndPoint.Create(sckaddr);
                EndpointsCache.AddOrUpdate(sckaddr, ep, 300);
            }
            return ep;
        }


        /// <summary>
        /// Converts a URL to a IPAddress
        /// </summary>
        /// <param name="url">URL Standard Format</param>
        /// <returns>A resolved IP Address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPAddress GetHostFromURL(string url)
        {
            return GetHostFromDNS(url.Split(SplitSlashColonArray)[3]);
        }

        /// <summary>
        /// Returns a IP address from a specified DNS, favouring IPv4 addresses.
        /// </summary>
        /// <param name="dnsAddress">DNS Hostname</param>
        /// <returns>An IP address, or null</returns>
        public static IPAddress GetHostFromDNS(string dnsAddress)
        {
            if (String.IsNullOrWhiteSpace(dnsAddress))
                return null;

            if (dnscache.TryGetValue(dnsAddress, 300000, out IPAddress ia) && ia != null)
                return ia;

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

            if (IPH == null || IPH.AddressList.Length == 0)
                return null;

            ia = null;
            foreach (IPAddress Adr in IPH.AddressList)
            {
                ia ??= Adr;

                if (Adr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ia = Adr;
                    break;
                }
            }
            if (ia != null)
                dnscache.AddOrUpdate(dnsAddress, ia, 300);
            return ia;
        }

        public static IPEndPoint getEndPoint(IPAddress ia, int port)
        {
            if (ia == null)
                return null;

            try
            {
                return  new IPEndPoint(ia, port);
            }
            catch
            {
                return null;
            }
        }

        public static IPEndPoint getEndPoint(string hostname, int port)
        {
            if (String.IsNullOrWhiteSpace(hostname))
                return null;

            if (dnscache.TryGetValue(hostname, 300000, out IPAddress ia) && ia != null)
                return getEndPoint(ia, port);

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

            if (IPH == null || IPH.AddressList.Length == 0)
                return null;

            ia = null;
            foreach (IPAddress Adr in IPH.AddressList)
            {
                ia ??= Adr;

                if (Adr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ia = Adr;
                    break;
                }
            }

            if (ia != null)
                dnscache.AddOrUpdate(hostname, ia, 300);

            return getEndPoint(ia, port);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Uri GetURI(string protocol, string hostname, int port, string path)
        {
            return new UriBuilder(protocol, hostname, port, path).Uri;
        }

        /// <summary>
        /// Gets a list of all local system IP addresses
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public static int ParseForeignAssetID(string id, out string url, out string assetID)
        {
            url = string.Empty;
            assetID = string.Empty;

            if (id.Length == 0)
                return -1;

            if (id[0] != 'h' && id[0] != 'H')
            {
                if (UUID.TryParse(id, out _))
                {
                    assetID = id;
                    return 0;
                }
                return -1;
            }

            OSHTTPURI uri = new(id, true);
            if (uri.IsResolvedHost)
            {
                url = uri.URL;
                string tmp = uri.Path;
                if (tmp.Length < 36)
                    return -3;
                if (tmp[0] == '/')
                    tmp = tmp[1..];
                if (UUID.TryParse(tmp, out _))
                {
                    assetID = tmp;
                    return 1;
                }
                return -1;
            }
            return -2;
        }

        /// <summary>
        /// Removes all invalid path chars (OS dependent)
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>safe path</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SafePath(string path)
        {
            return Regex.Replace(path, regexInvalidPathChars, String.Empty);
        }

        /// <summary>
        /// Removes all invalid filename chars (OS dependent)
        /// </summary>
        /// <param name="path">filename</param>
        /// <returns>safe filename</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SafeFileName(string filename)
        {
            return Regex.Replace(filename, regexInvalidFileChars, String.Empty);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string assetsDir()
        {
            return Path.Combine(configDir(), "assets");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string inventoryDir()
        {
            return Path.Combine(configDir(), "inventory");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string configDir()
        {
            return ".";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string dataDir()
        {
            return ".";
        }

        public static string logFile()
        {
            foreach (IAppender appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender is FileAppender appender1 && appender1.Name == "LogFileAppender")
                {
                    return appender1.File;
                }
            }

            return "./OpenSim.log";
        }

        public static string StatsLogFile()
        {
            foreach (IAppender appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender is FileAppender appender1 && appender1.Name == "StatsLogFileAppender")
                {
                    return appender1.File;
                }
            }

            return "./OpenSimStats.log";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                FileInfo f = new(FileName);

                if (!String.IsNullOrEmpty(f.Extension))
                {
                    Name = f.FullName[..f.FullName.LastIndexOf('.')];
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
            XmlConfigSource config = new(fileName);
            AddDataRowToConfig(config, row);
            config.Save();

            return config;
        }

        public static void AddDataRowToConfig(IConfigSource config, DataRow row)
        {
            config.Configs.Add((string)row[0]);
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                config.Configs[(string)row[0]].Set(row.Table.Columns[i].ColumnName, row[i]);
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
            foreach (string section in sections.AsSpan())
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
            if (enVars != null)
            {
                // load the values from the environment
                EnvConfigSource envConfigSource = new();
                // add the requested keys
                string[] env_keys = enVars.GetKeys();
                foreach (string key in env_keys)
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
                m_log.Warn("[UTILS]: Startup section doesn't exist");
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
                    m_log.Warn($"[UTILS]: Exception copying configuration file {configFile} to {exampleConfigFile}: {e.Message}");
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
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\0' || input[i] == '\r' || input[i] == '\n')
                    return input[..i];
            }
            return input;
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
                using StreamReader sr = new("/etc/issue.net");
                string issue = sr.ReadToEnd();
                return issue;
            }
            catch
            {
                return "";
            }
        }

        public static void SerializeToFile(string filename, Object obj)
        {
            var formatter = new BinaryFormatter();
            try
            {
                using Stream stream = new FileStream(filename, FileMode.Create,FileAccess.Write, FileShare.None);
                formatter.Serialize(stream, obj);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        public static Object DeserializeFromFile(string filename)
        {
            try
            {
                using Stream stream = new FileStream(filename, FileMode.Open,FileAccess.Read, FileShare.None);
                var formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            return null;
        }

        public static string Compress(string text)
        {
            using MemoryStream memory = new();
            using GZipStream compressor = new(memory, CompressionMode.Compress, true);

            byte[] buffer = Util.UTF8.GetBytes(text);
            compressor.Write(buffer, 0, buffer.Length);

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
            int msgLength = BitConverter.ToInt32(compressedBuffer, 0);

            using MemoryStream memory = new();
            memory.Write(compressedBuffer, 4, compressedBuffer.Length - 4);

            byte[] buffer = new byte[msgLength];

            memory.Position = 0;
            using GZipStream decompressor = new(memory, CompressionMode.Decompress);
            decompressor.Read(buffer, 0, buffer.Length);

            return Util.UTF8.GetString(buffer);
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

            MemoryStream ms = new();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlRpcResponse XmlRpcCommand(string url, string methodName, params object[] args)
        {
            return SendXmlRpcCommand(url, methodName, args);
        }

        public static XmlRpcResponse SendXmlRpcCommand(string url, string methodName, object[] args)
        {
            XmlRpcRequest xmlclient = new(methodName, args);
            using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
            return xmlclient.Send(url, hclient);
        }

        /// <summary>
        /// Returns an error message that the user could not be found in the database
        /// </summary>
        /// <returns>XML string consisting of a error element containing individual error(s)</returns>
        public static XmlRpcResponse CreateUnknownUserErrorResponse()
        {
            Hashtable responseData = new()
            {
                ["error_type"] = "unknown_user",
                ["error_desc"] = "The user requested is not in the database"
            };

            XmlRpcResponse response = new()
            {
                Value = responseData
            };
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
                (byte)y, (byte)(y >> 8), 0, 0
            };

            return new UUID(bytes, 0);
        }

        public static UUID BuildFakeParcelID(ulong regionHandle, uint x, uint y, uint z)
        {
            byte[] bytes =
            {
                (byte)regionHandle, (byte)(regionHandle >> 8), (byte)(regionHandle >> 16), (byte)(regionHandle >> 24),
                (byte)(regionHandle >> 32), (byte)(regionHandle >> 40), (byte)(regionHandle >> 48), (byte)(regionHandle >> 56),
                (byte)x, (byte)(x >> 8), (byte)z, (byte)(z >> 8),
                (byte)y, (byte)(y >> 8), 0, 0
            };
            return new UUID(bytes, 0);
        }

        public static bool ParseFakeParcelID(UUID parcelID, out ulong regionHandle, out uint x, out uint y)
        {
            byte[] bytes = parcelID.GetBytes();
            regionHandle = Utils.BytesToUInt64(bytes);
            x = Utils.BytesToUInt(bytes, 8) & 0xffff;
            y = Utils.BytesToUInt(bytes, 12) & 0xffff;
            // validation may fail, just reducing the odds of using a real UUID as encoded parcel
            return (bytes[0] == 0 && bytes[4] == 0 && // handler x,y multiples of 256
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
            ParseFakeParcelID(parcelID, out ulong regionHandle, out x, out y);
            Utils.LongToUInts(regionHandle, out uint rx, out uint ry);

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
            return String.Empty;
            //string os;
            //if (Environment.OSVersion.Platform != PlatformID.Unix)
            //{
            //   os = Environment.OSVersion.ToString();
            //}
            //else
            //{
            //   os = ReadEtcIssue();
            //}

            //if (os.Length > 45)
            //{
            //   os = os.Substring(0, 45);
            //}

            //return os;
        }

         public static readonly string RuntimeInformationStr = RuntimeInformation.ProcessArchitecture.ToString() + "/" + Environment.OSVersion.Platform switch
            {
                PlatformID.MacOSX or PlatformID.Unix => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX/DotNet" : "Unix/DotNet",
                _ => "Win/DotNet"
            };

        public static readonly string RuntimePlatformStr = Environment.OSVersion.Platform switch
            {
                PlatformID.MacOSX or PlatformID.Unix => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX/DotNet" : "Unix/DotNet",
                _ => "Win/DotNet"
            };

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
            int passPosition;
            int passEndPosition;

            // hide the password in the connection string
            passPosition = connectionString.IndexOf("password", StringComparison.OrdinalIgnoreCase);
            if (passPosition == -1)
                return connectionString;
            passPosition = connectionString.IndexOf('=', passPosition);
            if (passPosition < connectionString.Length)
                passPosition += 1;
            passEndPosition = connectionString.IndexOf(';', passPosition);

            return $"{connectionString[..passPosition]}***{connectionString[passEndPosition..]}";
        }

        public static string Base64ToString(string str)
        {
            Decoder utf8Decode = Encoding.UTF8.GetDecoder();

            byte[] todecode_byte = Convert.FromBase64String(str);
            int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount];
            utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            string result = new(decoded_char);
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
            Guid guid = new(hash);
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
            OSDMap args;
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
            OSDMap args;
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
                    m_log.Debug($"[UTILS]: Got OSD of unexpected type {buffer.Type}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                m_log.Debug($"[UTILS]: exception on GetOSDMap {ex.Message}");
                return null;
            }
        }

        public static string[] Glob(string path)
        {
            string vol = String.Empty;

            if (Path.VolumeSeparatorChar != Path.DirectorySeparatorChar)
            {
                string[] vcomps = path.Split(new char[] { Path.VolumeSeparatorChar }, 2, StringSplitOptions.RemoveEmptyEntries);

                if (vcomps.Length > 1)
                {
                    path = vcomps[1];
                    vol = vcomps[0];
                }
            }

            string[] comps = path.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            // Glob

            path = vol;
            if (vol != String.Empty)
                path += new String(new char[] { Path.VolumeSeparatorChar, Path.DirectorySeparatorChar });
            else
                path = new String(new char[] { Path.DirectorySeparatorChar });

            List<string> paths = new();
            List<string> found = new();
            paths.Add(path);

            int compIndex = -1;
            foreach (string c in comps)
            {
                compIndex++;

                List<string> addpaths = new();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AppendEndSlash(string path)
        {
            int len = path.Length;
            --len;
            if (len > 0 && path[len] != '/')
                return path + '/';
            return path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TrimEndSlash(string path)
        {
            int len = path.Length;
            --len;
            if (len > 0 && path[len] == '/')
                return path[..len];
            return path;
        }

        public static string ServerURIasIP(string uri)
        {
            if (uri.Length == 0)
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
                port1 = uri.Split(Util.SplitColonArray)[2];
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
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes256(string str, params object[] args)
        {
            return Utils.StringToBytes(string.Format(str, args), 255);
        }
        */
        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 256 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes256(ReadOnlySpan<char> str)
        {
            return Utils.StringToBytes(str, 255);
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
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes1024(string str, params object[] args)
        {
            return Utils.StringToBytes(string.Format(str, args).AsSpan(), 1024);
        }
        */

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to 1024 bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes1024(ReadOnlySpan<char> str)
        {
            return Utils.StringToBytes(str, 1024);
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
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes(string str, int MaxLength, params object[] args)
        {
            return Utils.StringToBytes(string.Format(str, args).AsSpan(), MaxLength);
        }
        */

        /// <summary>
        /// Convert a string to a byte format suitable for transport in an LLUDP packet.  The output is truncated to MaxLength bytes if necessary.
        /// </summary>
        /// <param name="str">
        /// If null or empty, then an bytes[0] is returned.
        /// Using "\0" will return a conversion of the null character to a byte.  This is not the same as bytes[0]
        /// </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytes(ReadOnlySpan<char> str, int MaxLength)
        {
            return Utils.StringToBytes(str, MaxLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] StringToBytesNoTerm(ReadOnlySpan<char> str, int MaxLength)
        {
            return Utils.StringToBytesNoTerm(str, MaxLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int osUTF8Getbytes(ReadOnlySpan<char> srcstr, byte[] dstarray, int maxdstlen, bool NullTerm = true)
        {
            return osUTF8Getbytes(srcstr, dstarray, 0, maxdstlen, NullTerm);
        }

        public static unsafe int osUTF8Getbytes(ReadOnlySpan<char> srcstr, byte* dstarray, int maxdstlen, bool NullTerm = true)
        {
            if (srcstr.Length == 0)
                return 0;

            fixed (char* srcbase = srcstr)
            {
                return osUTF8Getbytes(srcbase, srcstr.Length, dstarray, maxdstlen, NullTerm);
            }
        }

        public static unsafe int osUTF8Getbytes(ReadOnlySpan<char> srcstr, byte[] dstarray, int pos, int maxdstlen, bool NullTerm = true)
        {
            if (srcstr.Length == 0)
                return 0;

            if (pos + maxdstlen > dstarray.Length)
                return 0;

            fixed (char* srcbase = srcstr)
            {
                fixed (byte* dstbase = &dstarray[pos])
                {
                    return osUTF8Getbytes(srcbase, srcstr.Length, dstbase, maxdstlen, NullTerm);
                }
            }
        }

        public static unsafe int osUTF8Getbytes(char* srcarray, int srclength, byte* dstarray, int maxdstlen, bool NullTerm = true)
        {
            int dstlen = NullTerm ? maxdstlen - 1 : maxdstlen;
            int srclen = srclength >= dstlen ? dstlen : srclength;

            char c;
            char* src = srcarray;
            char* srcend = src + srclen;
            byte* dst = dstarray;
            byte* dstend = dst + dstlen;

            while (src < srcend && dst < dstend)
            {
                c = *src;
                ++src;

                if (c <= 0x7f)
                {
                    *dst = (byte)c;
                    ++dst;
                    continue;
                }

                if (c < 0x800)
                {
                    if (dst + 1 >= dstend)
                        break;
                    *dst = (byte)(0xC0 | (c >> 6));
                    ++dst;
                    *dst = (byte)(0x80 | (c & 0x3F));
                    ++dst;
                    continue;
                }

                if (c >= 0xD800 && c < 0xE000)
                {
                    if (c >= 0xDC00)
                        continue; // ignore invalid
                    if (src >= srcend || dst + 3 >= dstend)
                        break;

                    int a = c;

                    c = *src;
                    ++src;
                    if (c < 0xDC00 || c > 0xDFFF)
                        continue; // ignore invalid

                    a = (a << 10) + c - 0x35fdc00;

                    *dst = (byte)(0xF0 | (a >> 18));
                    ++dst;
                    *dst = (byte)(0x80 | ((a >> 12) & 0x3f));
                    ++dst;
                    *dst = (byte)(0x80 | ((a >> 6) & 0x3f));
                    ++dst;
                    *dst = (byte)(0x80 | (a & 0x3f));
                    ++dst;
                    continue;
                }
                if (dst + 2 >= dstend)
                    break;

                *dst = (byte)(0xE0 | (c >> 12));
                ++dst;
                *dst = (byte)(0x80 | ((c >> 6) & 0x3f));
                ++dst;
                *dst = (byte)(0x80 | (c & 0x3f));
                ++dst;
            }

            int ret = (int)(dst - dstarray);
            if (NullTerm && ret > 0 && *(dst - 1) != 0)
            {
                *dst = 0;
                ++ret;
            }

            return ret;
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
            StringBuilder sb = new();

            int i = 0;

            foreach (string key in ht.Keys)
            {
                sb.Append($"{key}:{ht[key]}");

                if (++i < ht.Count)
                    sb.AppendFormat(", ");
            }

            return sb.ToString();
        }

        public static bool TryParseHttpRange(string header, out int start, out int end)
        {
            start = end = 0;
            if (string.IsNullOrWhiteSpace(header))
                return false;

            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header[6..].Split('-');

                if (rangeValues.Length == 2)
                {
                    string rawStart = rangeValues[0].Trim();
                    if (rawStart != "" && !Int32.TryParse(rawStart, out start))
                        return false;

                    if (start < 0)
                        return false;

                    string rawEnd = rangeValues[1].Trim();
                    if (rawEnd.Length == 0)
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

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TimeBeginPeriod(uint period)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                timeBeginPeriod(period);
        }

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TimeEndPeriod(uint period)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                timeEndPeriod(period);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThreadSleep(int period)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                timeBeginPeriod(1);
                Thread.Sleep(period);
                timeEndPeriod(1);
            }
            else
                Thread.Sleep(period);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass,
                    IntPtr ProcessInformation, UInt32 ProcessInformationSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        public static void DisableTimerThrottling()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int sz = Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE));
                PROCESS_POWER_THROTTLING_STATE PwrInfo = new()
                {
                    Version = 1,
                    ControlMask = 4,
                    StateMask = 0
                };  // disable that flag explicitly
                nint PwrInfoPtr = Marshal.AllocHGlobal(sz);
                Marshal.StructureToPtr(PwrInfo, PwrInfoPtr, false);
                IntPtr handle = Process.GetCurrentProcess().Handle;
                bool r = SetProcessInformation(handle, 4, PwrInfoPtr, (uint)sz);
                Marshal.FreeHGlobal(PwrInfoPtr);
            }
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

        #region FireAndForget Threading Pattern

        public static void InitThreadPool(int minThreads, int maxThreads)
        {
            if (maxThreads < 2)
                throw new ArgumentOutOfRangeException(nameof(maxThreads), "maxThreads must be greater than 2");

            if (minThreads > maxThreads || minThreads < 2)
                throw new ArgumentOutOfRangeException(nameof(minThreads), "minThreads must be greater than 2 and less than or equal to maxThreads");

            if (m_ThreadPool != null)
            {
                m_log.Warn("SmartThreadPool is already initialized.  Ignoring request.");
                return;
            }

            STPStartInfo startInfo = new()
            {
                ThreadPoolName = "Util",
                IdleTimeout = 20000,
                MaxWorkerThreads = maxThreads,
                MinWorkerThreads = minThreads,
                SuppressFlow = true
            };

            m_ThreadPool = new SmartThreadPool(startInfo);
            m_threadPoolWatchdog = new Timer(ThreadPoolWatchdog, null, 0, 1000);
        }

        public static int FireAndForgetCount()
        {
            const int MAX_SYSTEM_THREADS = 200;

            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.QueueUserWorkItem:
                    ThreadPool.GetAvailableThreads(out int workerThreads, out _);
                    return workerThreads;
                case FireAndForgetMethod.SmartThreadPool:
                    return m_ThreadPool.MaxThreads - m_ThreadPool.InUseThreads;
                case FireAndForgetMethod.Thread:
                    {
                        using Process p = System.Diagnostics.Process.GetCurrentProcess();
                        return MAX_SYSTEM_THREADS - p.Threads.Count;
                    }
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
            private readonly string context;
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

        public static long TotalQueuedFireAndForgetCalls { get { return numQueuedThreadFuncs; } }
        public static long TotalRunningFireAndForgetCalls { get { return numRunningThreadFuncs; } }

        // Maps (ThreadFunc number -> Thread)
        private static readonly ConcurrentDictionary<long, ThreadInfo> activeThreads = new();

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
                    m_log.Warn($"Timeout in threadfunc {t.ThreadFuncNum} ({t.Thread.Name}) {t.GetStackTrace()}");
                    t.Abort();
                    activeThreads.TryRemove(entry.Key, out _);

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

        private static readonly Dictionary<string, int> m_fireAndForgetCallsMade = new();

        public static Dictionary<string, int> GetFireAndForgetCallsInProgress()
        {
            return new Dictionary<string, int>(m_fireAndForgetCallsInProgress);
        }

        private static readonly Dictionary<string, int> m_fireAndForgetCallsInProgress = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FireAndForget(System.Threading.WaitCallback callback)
        {
            FireAndForget(callback, null, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            ThreadInfo threadInfo = new(threadFuncNum, context, dotimeout);

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
                        if (loggingEnabled && threadInfo.LogThread)
                            m_log.DebugFormat("Run threadfunc {0} (Queued {1}, Running {2})", threadFuncNum, numQueued1, numRunning1);

                        Culture.SetCurrentCulture();
                        callback(o);
                    }
                    catch (ThreadAbortException)
                    {
                    }
                    catch (Exception e)
                    {
                        m_log.Error($"[UTIL]: Util STP threadfunc {threadFuncNum} terminated with error {e.Message}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref numRunningThreadFuncs);
                        activeThreads.TryRemove(threadFuncNum, out _);
                        if (loggingEnabled && threadInfo.LogThread)
                            m_log.Debug($"Exit threadfunc {threadFuncNum} ({FormatDuration(threadInfo.Elapsed())}");
                        callback = null;
                        o = null;
                        threadInfo = null;
                    }
                };
            }

            Interlocked.Increment(ref numQueuedThreadFuncs);
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
                        ThreadPool.UnsafeQueueUserWorkItem(realCallback, obj);
                        break;
                    case FireAndForgetMethod.SmartThreadPool:
                        if (m_ThreadPool == null)
                            InitThreadPool(2, 15);
                        threadInfo.WorkItem = m_ThreadPool.QueueWorkItem(realCallback, obj);
                        break;
                    case FireAndForgetMethod.Thread:
                        Thread thread = new(delegate (object o) { realCallback(o); realCallback = null; });
                        thread.Start(obj);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception)
            {
                Interlocked.Decrement(ref numQueuedThreadFuncs);
                activeThreads.TryRemove(threadFuncNum, out _);
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

            StringBuilder dest = new(src.Length);

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

            return new STPInfo()
            {
                Name = m_ThreadPool.Name,
                IsIdle = m_ThreadPool.IsIdle,
                IsShuttingDown = m_ThreadPool.IsShuttingdown,
                MaxThreads = m_ThreadPool.MaxThreads,
                MinThreads = m_ThreadPool.MinThreads,
                InUseThreads = m_ThreadPool.InUseThreads,
                ActiveThreads = m_ThreadPool.ActiveThreads,
                WaitingCallbacks = m_ThreadPool.WaitingCallbacks,
                MaxConcurrentWorkItems = m_ThreadPool.Concurrency
            };
        }

        public static void StopThreadPool()
        {
            if (m_ThreadPool == null)
                return;
            SmartThreadPool pool = m_ThreadPool;
            m_ThreadPool = null;

            try { pool.Shutdown(); } catch { }
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
            using Process p = System.Diagnostics.Process.GetCurrentProcess();
            return p.WorkingSet64;
        }

        // returns a timestamp in seconds as double
        // using the time resolution avaiable to StopWatch
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetTimeStamp()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriod;
        }

        // returns a timestamp in ms as double
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetTimeStampMS()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriodMS;
        }

        // doing math in ticks is usefull to avoid loss of resolution
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStampTicks()
        {
            return Stopwatch.GetTimestamp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double TimeStampTicksToMS(long ticks)
        {
            return ticks * TimeStampClockPeriodMS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToGatheredIds(Dictionary<UUID, sbyte> uuids, UUID id, sbyte type)
        {
            if (id.IsZero())
                return;
            uuids[id] = type;
        }

        /// <summary>
        /// Formats a duration (given in milliseconds).
        /// </summary>
        public static string FormatDuration(int ms)
        {
            TimeSpan span = new(ms * TimeSpan.TicksPerMillisecond);

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
                suffix ??= "min";
            }

            if ((hours > 0) || (span.Minutes > 0) || (span.Seconds > 0))
            {
                if (str.Length > 0)
                    str += ":";
                str += span.Seconds.ToString(str.Length == 0 ? "0" : "00");
                suffix ??= "sec";
            }

            suffix ??= "ms";

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
            StackTrace stackTrace = new(true);           // get call stack
            StackFrame[] stackFrames = stackTrace.GetFrames();  // get method calls (frames)

            // write call stack method names
            foreach (StackFrame stackFrame in stackFrames)
            {
                MethodBase mb = stackFrame.GetMethod();
                printer($"{mb.DeclaringType}.{mb.Name}:{stackFrame.GetFileLineNumber()}"); // write method name
            }
        }

        /// <summary>
        /// Gets the client IP address
        /// </summary>
        /// <param name="xff"></param>
        /// <returns></returns>
        public static IPEndPoint GetClientIPFromXFF(string xff)
        {
            if (xff.Length == 0)
                return null;

            string[] parts = xff.Split(Util.SplitCommaArray);
            if (parts.Length > 0)
            {
                try
                {
                    return new IPEndPoint(IPAddress.Parse(parts[0]), 0);
                }
                catch (Exception e)
                {
                    m_log.Warn($"[UTIL]: Exception parsing XFF header {xff}: {e.Message}");
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
                    m_log.Warn($"[UTIL]: exception in GetCallerIP: {e.Message}");
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
            reader.ReadStartElement(name);
            string idStr = reader.Name switch
            {
                "Guid" => reader.ReadElementString("Guid"),
                "UUID" => reader.ReadElementString("UUID"),
                // no leading tag
                _ => reader.ReadContentAsString(),
            };
            reader.ReadEndElement();

            _ = UUID.TryParse(idStr, out UUID id);
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
            Quaternion quat = new();

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
            if (value.Contains(' ') && !value.Contains(','))
                value = value.Replace(" ", ", ");

            return (T)Enum.Parse(typeof(T), value);
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
        public static unsafe bool ParseUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname, out string secret)
        {
            secret = string.Empty;

            if (value == null || value.Length == 36)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return UUID.TryParse(value, out uuid);
            }

            if (value.Length < 38)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            ++indxB;
            if (indxB >= value.Length)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }
            string n;
            if (nseps == 2)
                n = value[indxB..].Trim();
            else
            {
                indxA = seps[2];
                n = value[indxB..indxA];
                ++indxA;
                if (indxA < value.Length)
                    secret = value[indxA..];
            }

            string[] name = n.Split(SplitSpaceArray, StringSplitOptions.RemoveEmptyEntries);
            if (name.Length == 0)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            firstname = name[0];
            lastname = (name.Length > 1) ? name[1] : string.Empty;

            return firstname.Length > 0;
        }

        public static unsafe bool ParseFullUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname, out string secret)
        {
            secret = string.Empty;

            if (value == null || value.Length < 38)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            ++indxB;
            if (indxB >= value.Length)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }
            string n;
            if (nseps == 2)
                n = value[indxB..].Trim();
            else
            {
                indxA = seps[2];
                n = value[indxB..indxA];
                ++indxA;
                if (indxA < value.Length)
                    secret = value[indxA..];
            }

            string[] name = n.Split(SplitSpaceArray, StringSplitOptions.RemoveEmptyEntries);
            if (name.Length == 0)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            firstname = name[0];
            lastname = (name.Length > 1) ? name[1] : string.Empty;

            return firstname.Length > 0;
        }

        public static unsafe bool ParseUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname)
        {
            if (value == null || value.Length == 36)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return UUID.TryParse(value, out uuid);
            }

            if (value.Length < 38)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            ++indxB;
            if (indxB >= value.Length)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }
            string n;
            if (nseps == 2)
                n = value[indxB..];
            else
                n = value[indxB..seps[2]];

            string[] name = n.Split(SplitSpaceArray, StringSplitOptions.RemoveEmptyEntries);
            if (name.Length == 0)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            firstname = name[0];
            lastname = (name.Length > 1) ? name[1] : string.Empty;

            return firstname.Length > 0;
        }

        public static unsafe bool ParseFullUniversalUserIdentifier(string value, out UUID uuid, out string url, out string firstname, out string lastname)
        {
            if (value == null || value.Length < 38)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            ++indxB;
            if (indxB >= value.Length)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }
            string n;
            if (nseps == 2)
                n = value[indxB..];
            else
                n = value[indxB..seps[2]];

            string[] name = n.Split(SplitSpaceArray, StringSplitOptions.RemoveEmptyEntries);
            if (name.Length == 0)
            {
                firstname = string.Empty;
                lastname = string.Empty;
                return false;
            }

            firstname = name[0];
            lastname = (name.Length > 1) ? name[1] : string.Empty;

            return firstname.Length > 0;
        }

        public static unsafe bool ParseFullUniversalUserIdentifier(string value, out UUID uuid, out string url)
        {
            if (value == null || value.Length < 38)
            {
                url = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            indxA = seps[1] + 3;
            indxB = nseps > 2 ? seps[2] : value.Length;

            return indxA < indxB;
        }

        public static unsafe bool ParseUniversalUserIdentifier(string value, out UUID uuid, out string url)
        {
            if (value == null || value.Length < 38)
            {
                url = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..36], out uuid))
            {
                url = string.Empty;
                return false;
            }

            int* seps = stackalloc int[3];
            int nseps = 0;
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }

            if (nseps < 2)
            {
                url = string.Empty;
                uuid = UUID.Zero;
                return false;
            }

            int indxA = seps[0] + 1;
            int indxB = seps[1];
            url = value[indxA..indxB].Trim().ToLower();

            return true;
        }

        public static unsafe bool ParseFullUniversalUserIdentifier(string value, out UUID uuid)
        {
            if (value == null || value.Length < 38)
            {
                uuid = UUID.Zero;
                return false;
            }

            int nseps = 0;
            int* seps = stackalloc int[3];
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }
            if (nseps < 2)
            {
                uuid = UUID.Zero;
                return false;
            }

            if (!UUID.TryParse(value[..seps[0]], out uuid))
                return false;

            int indxA = seps[1] + 3;
            int indxB = nseps > 2 ? seps[2] : value.Length;

            return indxA < indxB;
        }

        public static bool ParseUniversalUserIdentifier(string value, out UUID uuid)
        {
            if (value == null || value.Length < 36)
            {
                uuid = UUID.Zero;
                return false;
            }
            return (value.Length == 36) ? UUID.TryParse(value, out uuid) : UUID.TryParse(value[..36], out uuid);
        }

        public static unsafe string RemoveUniversalUserIdentifierSecret(string value)
        {
            if (value.Length < 39)
                return value;
            int nseps = 0;
            int* seps = stackalloc int[3];
            for (int i = 36; i < value.Length && nseps < 3; ++i)
            {
                if (value[i] == ';')
                    seps[nseps++] = i;
            }
            if (nseps < 3)
                return value;
            return value[..seps[3]];
        }

        /// <summary>
        /// For foreign avatars, extracts their original name and Server URL from their First Name and Last Name.
        /// </summary>
        public static bool ParseForeignAvatarName(string firstname, string lastname,
            out string realFirstName, out string realLastName, out string serverURI)
        {
            realFirstName = realLastName = serverURI = string.Empty;

            if (!lastname.Contains('@'))
                return false;

            string[] parts = firstname.Split('.');
            if (parts.Length != 2)
                return false;

            realFirstName = parts[0].Trim();
            realLastName = parts[1].Trim();
            lastname = lastname.Trim();
            serverURI = new Uri($"http://{lastname.Replace("@", "")}").ToString();

            return true;
        }

        public static int ParseAvatarName(string name, out string FirstName, out string LastName, out string serverURI)
        {
            FirstName = LastName = serverURI = string.Empty;

            if (string.IsNullOrWhiteSpace(name) || name.Length < 1)
                return 0;

            int i = 0;
            bool havedot = false;

            while (i < name.Length && name[i] == ' ') ++i;
            int start = i;

            while (i < name.Length)
            {
                char c = name[i];
                if (c == '@')
                    return 0;

                if (c == ' ')
                {
                    if (i >= name.Length - 1 || i == start)
                        return 0;
                    break;
                }
                if (c == '.')
                {
                    if (i >= name.Length - 1 || i == start)
                        return 0;
                    havedot = true;
                    break;
                }
                ++i;
            }

            FirstName = name[start..i];

            if (i >= name.Length - 1)
                return 1;

            ++i;
            while (i < name.Length && name[i] == ' ') ++i;
            if (i == name.Length)
                return 1;

            start = i;
            while (i < name.Length)
            {
                char c = name[i];
                if (c == '.')
                {
                    if (havedot || i >= name.Length - 1)
                        return 0;
                    else start = i + 1;
                }
                else if (c == '@')
                {
                    if (i >= name.Length - 1)
                        return 0;

                    int j = i;
                    while (j > start && name[j - 1] == ' ') --j;
                    if (j <= start)
                        return 0;

                    LastName = name[start..j];

                    ++i;
                    while (i < name.Length && name[i] == ' ') ++i;
                    if (i > name.Length - 3)
                        return 0;

                    serverURI = name[i..].TrimEnd();
                    return serverURI.Length == 0 ? 2 : 3;
                }
                ++i;
            }
            LastName = name[start..].TrimEnd();
            return LastName.Length == 0 ? 1 : 2;
        }

        /// <summary>
        /// Produces a universal (HG) system-facing identifier given the information
        /// </summary>
        /// <param name="acircuit"></param>
        /// <returns>uuid[;homeURI[;first last]]</returns>
        public static string ProduceUserUniversalIdentifier(AgentCircuitData acircuit)
        {
            if (acircuit.ServiceURLs.TryGetValue("HomeURI", out object homeobj))
                return UniversalIdentifier(acircuit.AgentID, acircuit.firstname, acircuit.lastname, homeobj.ToString());
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
            if (!agentsURI.EndsWith('/'))
                agentsURI += "/";

            // This is ugly, but there's no other way, given that the name is changed
            // in the agent circuit data for foreigners
            if (lastName.Contains('@'))
            {
                string[] parts = firstName.Split(Util.SplitDotArray);
                if (parts.Length == 2)
                    return CalcUniversalIdentifier(id, agentsURI, parts[0].Trim() + " " + parts[1].Trim());
            }

            return CalcUniversalIdentifier(id, agentsURI, firstName.Trim() + " " + lastName.Trim());
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
            Uri uri;
            try
            {
                uri = new Uri(homeURI);
            }
            catch (UriFormatException)
            {
                return $"{firstName.Trim()} {lastName.Trim()}";
            }
            return $"{firstName.Trim()}.{lastName.Trim()}@{uri.Authority}";
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
            if (length > 250)
                xml = xml[..250] + "...";

            for (int i = 0; i < xml.Length; i++)
            {
                if (xml[i] < 0x20)
                {
                    xml = "Unprintable binary data";
                    break;
                }
            }

            m_log.Error($"{message} Failed XML ({length} bytes) = {xml}");
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
            Bitmap result = new(width, height, PixelFormat.Format24bppRgb);

            using (ImageAttributes atrib = new())
            using (Graphics graphics = Graphics.FromImage(result))
            {
                atrib.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, new Rectangle(0, 0, result.Width, result.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, atrib);
            }

            return result;
        }

        public static void SaveAssetToFile(string filename, byte[] data)
        {
            string assetPath = "UserAssets";
            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            FileStream fs = File.Create(Path.Combine(assetPath, filename));
            BinaryWriter bw = new(fs);
            bw.Write(data);
            bw.Close();
            fs.Close();
        }

        //https://www.color.org/sRGB.pdf
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LinearTosRGB(float linear)
        {
            return linear <= 0.0031308f ? (linear * 12.92f) : (1.055f * MathF.Pow(linear, 0.4166667f) - 0.055f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float sRGBtoLinear(float rgb)
        {
            return (rgb  < 0.04045f) ? rgb * 0.07739938f :  MathF.Pow((rgb + 0.055f) / 1.055f, 2.4f);
        }
    }
}
