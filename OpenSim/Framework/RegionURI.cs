/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class RegionURI
    {
        private static byte[] schemaSep = osUTF8.GetASCIIBytes("://");
        private static byte[] altschemaSep = osUTF8.GetASCIIBytes("|!!");
        private static byte[] nameSep = osUTF8.GetASCIIBytes(":/ ");
        private static byte[] altnameSep = osUTF8.GetASCIIBytes(":/ +|");
        private static byte[] escapePref = osUTF8.GetASCIIBytes("+%");
        private static byte[] altPortSepPref = osUTF8.GetASCIIBytes(":|");

        public enum URIFlags : int
        {
            None = 0,
            Valid = 1 << 0,
            HasHost = 1 << 1,
            HasResolvedHost = 1 << 2,
            HasUserName = 1 << 3,
            HasUserPass = 1 << 4,
            HasRegionName = 1 << 5,
            HasCoords = 1 << 6,

            IsLocalGrid = 1 << 7 // this must be set externally
        }

        public URIFlags Flags;
        public IPAddress IP;
        public string originalURI = string.Empty;
        public string Schema = "http://";
        public string Host = string.Empty;
        public int Port = 80;
        public string RegionName = string.Empty;
        public string Username = string.Empty;
        public string UserPass = string.Empty;
        public int X = 127;
        public int Y = 127;
        public int Z = 2;

        public RegionURI(string _originalURI)
        {
            originalURI = _originalURI;
            Parse(_originalURI);
            if (!HasHost)
                Flags |= URIFlags.IsLocalGrid;
        }

        public RegionURI(string _originalURI, GridInfo gi)
        {
            originalURI = _originalURI;
            Parse(_originalURI);

            if(!HasHost)
            {
                Flags |= URIFlags.IsLocalGrid;
                return;
            }
            if(gi == null)
                return;

            if (gi.IsLocalGrid(HostUrl) == 1)
            {
                Host = string.Empty;
                Flags &= ~URIFlags.HasHost;
                Flags |= URIFlags.IsLocalGrid;
                return;
            }
            if (!ResolveDNS())
            {
                Flags = URIFlags.None;
            }
        }

        public void Parse(string inputURI)
        {
            Flags = URIFlags.None;
            if (string.IsNullOrWhiteSpace(inputURI))
                return;

            osUTF8Slice input = new osUTF8Slice(inputURI);
            input.SelfTrimStart((byte)' ');
            input.SelfTrimStart((byte)'+');

            int firstDot = input.IndexOf((byte)'.');
            if (firstDot == 0)
                return;

            osUTF8Slice tmpSlice;

            int indx = input.IndexOf(schemaSep);
            if (indx == 0)
                return;
            if (indx < 0)
                indx = input.IndexOf(altschemaSep);
            if (indx == 0)
                return;
            if (indx > 0)
            {
                if (indx < 2 || input.Length < indx + 4 || (firstDot > 0 && indx > firstDot))
                    return;

                bool issecure = false;
                tmpSlice = input.SubUTF8(0, indx).Clone();
                tmpSlice.ToASCIILowerSelf();

                if (tmpSlice.EndsWith((byte)'s'))
                {
                    issecure = true;
                    tmpSlice.SelfTrimEnd((byte)'s');
                }

                switch (tmpSlice.ToString())
                {
                    case "http":
                    case "hg":
                    case "hop":
                    case "surl":
                    case "x-grid-info":
                        // only https has this defined
                        if (issecure)
                        {
                            Schema = "https://";
                            Port = 443;
                        }
                        break;
                    default:
                        return;
                }

                indx += 3;
                input.SubUTF8Self(indx);
                firstDot -= indx;
            }

            int namestart = 0;
            if (firstDot > 0)
            {
                int hostend = -1;
                osUTF8Slice hosttmp = input.Clone();
                indx = input.IndexOfAny(altPortSepPref);
                if (indx > 0)
                {
                    if (indx < firstDot)
                        return;

                    hostend = indx;
                    ++indx;
                    int tmpport = 0;
                    byte c;
                    while (indx < input.Length)
                    {
                        c = input[indx];
                        if (c < (byte)'0' || c > (byte)'9')
                            break;
                        tmpport *= 10;
                        tmpport += c - (byte)'0';
                        ++indx;
                    }
                    if (indx > hostend + 1)
                    {
                        if (tmpport > 64 * 1024)
                            return;
                        Port = tmpport;
                    }
                    input.SubUTF8Self(indx);
                    input.SelfTrimStart(altnameSep);
                }
                else
                {
                    indx = input.IndexOfAny(altnameSep);
                    if (indx < 0)
                    {
                        hostend = input.Length;
                        namestart = -1;
                    }
                    else
                    {
                        hostend = indx;
                        namestart = indx + 1;
                    }
                }

                if (hostend <= 0)
                    return;

                hosttmp.SubUTF8Self(0, hostend);
                indx = hosttmp.IndexOf((byte)'@');
                if (indx >= 0)
                {
                    if (indx > 0)
                    {
                        tmpSlice = hosttmp.SubUTF8(0, indx);
                        int indx2 = tmpSlice.IndexOfAny(escapePref);
                        if (indx2 >= 0)
                        {
                            Username = Uri.UnescapeDataString(tmpSlice.ToString());
                            Username = Username.Replace('+', ' ');
                        }
                        else
                            Username = tmpSlice.ToString();
                        if (Username.Length > 0)
                            Flags |= URIFlags.HasUserName;
                    }
                    ++indx;
                    hosttmp.SubUTF8Self(indx);
                }
                if (hosttmp.Length == 0)
                {
                    Flags = URIFlags.None;
                    return;
                }

                indx = hosttmp.IndexOfAny(escapePref);
                if (indx >= 0)
                {
                    string blabla = Uri.UnescapeDataString(hosttmp.ToString());
                    blabla = blabla.Replace('+', ' ');
                    hosttmp = new osUTF8Slice(blabla);
                }
                hosttmp.ToASCIILowerSelf();
                Host = hosttmp.ToString();
                UriHostNameType type = Uri.CheckHostName(Host);
                if (type == UriHostNameType.Unknown || type == UriHostNameType.Basic)
                {
                    Flags = URIFlags.None;
                    return;
                }

                Flags |= URIFlags.HasHost;
            }

            if (namestart < 0 || input.Length == 0)
                return;

            input.SubUTF8Self(namestart);
            input.SelfTrimStart((byte)' ');
            input.SelfTrimStart((byte)'+');

            int firstCoord = input.IndexOf((byte)'/');
            if (firstCoord == 0)
            {
                Flags = URIFlags.None;
                return;
            }
            if (firstCoord < 0)
                firstCoord = input.IndexOf((byte)'(');

            if (firstCoord > 0)
                tmpSlice = input.SubUTF8(0, firstCoord);
            else
                tmpSlice = input;
            indx = tmpSlice.IndexOfAny(escapePref);
            if (indx >= 0)
            {
                string blabla = Uri.UnescapeDataString(tmpSlice.ToString());
                blabla = blabla.Replace('+', ' ');
                tmpSlice = new osUTF8Slice(blabla);
            }

            tmpSlice.SelfTrimEnd((byte)' ');
            if (tmpSlice.Length <= 0)
                return;

            RegionName = tmpSlice.ToString();
            Flags |= URIFlags.HasRegionName;

            if (firstCoord > 0)
            {
                if (input[firstCoord] == (byte)'/')
                {
                    ++firstCoord;
                    int tmp = 0;
                    tmpSlice = input.SubUTF8(firstCoord);
                    int indx2 = 0;
                    while (indx2 < tmpSlice.Length)
                    {
                        byte c = tmpSlice[indx2];
                        if (c < (byte)'0' || c > (byte)'9')
                            break;
                        tmp *= 10;
                        tmp += c - (byte)'0';
                        ++indx2;
                    }
                    if (indx2 == 0)
                    {
                        Flags = URIFlags.None;
                        return;
                    }
                    X = tmp;
                    tmpSlice.SubUTF8Self(indx2);
                    indx = tmpSlice.IndexOf((byte)'/');
                    if (indx >= 0)
                    {
                        ++indx;
                        tmpSlice.SubUTF8Self(indx);
                        indx2 = 0;
                        tmp = 0;
                        while (indx2 < tmpSlice.Length)
                        {
                            byte c = tmpSlice[indx2];
                            if (c < (byte)'0' || c > (byte)'9')
                                break;
                            tmp *= 10;
                            tmp += c - (byte)'0';
                            ++indx2;
                        }
                        if (indx2 == 0)
                        {
                            Flags = URIFlags.None;
                            return;
                        }
                        Y = tmp;
                        tmpSlice.SubUTF8Self(indx2);
                        indx = tmpSlice.IndexOf((byte)'/');
                        if (indx >= 0)
                        {
                            ++indx;
                            tmpSlice.SubUTF8Self(indx);
                            indx2 = 0;
                            tmp = 0;
                            int sig = 1;
                            if (tmpSlice[indx2] == (byte)'-')
                            {
                                sig = -1;
                                indx2++;
                            }
                            while (indx2 < tmpSlice.Length)
                            {
                                byte c = tmpSlice[indx2];
                                if (c < (byte)'0' || c > (byte)'9')
                                    break;
                                tmp *= 10;
                                tmp += c - (byte)'0';
                                ++indx2;
                            }
                            if (indx2 == 0)
                            {
                                Flags = URIFlags.None;
                                return;
                            }
                            Z = sig * tmp;
                        }
                    }
                }
                else // (,,) case
                {
                    ++firstCoord;
                    int tmp = 0;
                    tmpSlice = input.SubUTF8(firstCoord);
                    int indx2 = tmpSlice.IndexOf((byte)')');
                    if (indx2 == 0)
                        return;
                    if (indx2 > 0)
                        tmpSlice.SubUTF8Self(0, indx2);

                    indx2 = 0;
                    tmpSlice.SelfTrimStart((byte)' ');
                    tmpSlice.SelfTrimStart((byte)'+');

                    while (indx2 < tmpSlice.Length)
                    {
                        byte c = tmpSlice[indx2];
                        if (c < (byte)'0' || c > (byte)'9')
                            break;
                        tmp *= 10;
                        tmp += c - (byte)'0';
                        ++indx2;
                    }
                    if (indx2 == 0)
                    {
                        Flags = URIFlags.None;
                        return;
                    }
                    X = tmp;
                    tmpSlice.SubUTF8Self(indx2);
                    indx = tmpSlice.IndexOf((byte)',');
                    if (indx >= 0)
                    {
                        ++indx;
                        tmpSlice.SubUTF8Self(indx);
                        tmpSlice.SelfTrimStart((byte)' ');
                        tmpSlice.SelfTrimStart((byte)'+');
                        indx2 = 0;
                        tmp = 0;
                        while (indx2 < tmpSlice.Length)
                        {
                            byte c = tmpSlice[indx2];
                            if (c < (byte)'0' || c > (byte)'9')
                                break;
                            tmp *= 10;
                            tmp += c - (byte)'0';
                            ++indx2;
                        }
                        if (indx2 == 0)
                        {
                            Flags = URIFlags.None;
                            return;
                        }
                        Y = tmp;
                        tmpSlice.SubUTF8Self(indx2);
                        indx = tmpSlice.IndexOf((byte)',');
                        if (indx >= 0)
                        {
                            ++indx;
                            tmpSlice.SubUTF8Self(indx);
                            tmpSlice.SelfTrimStart((byte)' ');
                            tmpSlice.SelfTrimStart((byte)'+');
                            indx2 = 0;
                            tmp = 0;
                            int sig = 1;
                            if (tmpSlice[indx2] == (byte)'-')
                            {
                                sig = -1;
                                indx2++;
                            }
                            while (indx2 < tmpSlice.Length)
                            {
                                byte c = tmpSlice[indx2];
                                if (c < (byte)'0' || c > (byte)'9')
                                    break;
                                tmp *= 10;
                                tmp += c - (byte)'0';
                                ++indx2;
                            }
                            if (indx2 == 0)
                            {
                                Flags = URIFlags.None;
                                return;
                            }
                            Z = sig * tmp;
                        }
                    }
                }
            }
            return;
        }

        public bool ResolveDNS()
        {
            if ((Flags & URIFlags.HasHost) != 0)
            {
                IPAddress ip = Util.GetHostFromDNS(Host);
                if (ip != null)
                {
                    IP = ip;
                    Flags |= URIFlags.HasResolvedHost;
                    return true;
                }
            }
            return false;
        }

        public bool IsValid
        {
            get { return (Flags & (URIFlags.HasHost | URIFlags.HasRegionName)) != 0; }
        }

        public bool HasHost
        {
            get { return (Flags & URIFlags.HasHost) != 0; }
        }

        public bool HasRegionName
        {
            get { return (Flags & URIFlags.HasRegionName) != 0; }
        }

        public string HostUrl
        {
            get { return (Flags & URIFlags.HasHost) != 0 ? (Schema + Host + ":" + Port) : ""; }
        }

        public string HostUrlEndSlash
        {
            get { return (Flags & URIFlags.HasHost) != 0 ? (Schema + Host + ":" + Port + "/") : ""; }
        }

        public string RegionUrlAndName
        {
            get
            {
                string ret = (Flags & URIFlags.HasHost) != 0 ? (Schema + Host + ":" + Port + "/") : "";
                if ((Flags & URIFlags.HasRegionName) != 0)
                    ret += RegionName;
                return ret;
            }
        }

        public string RegionHostPortSpaceName
        {
            get
            {
                string ret = (Flags & URIFlags.HasHost) != 0 ? (Host + ":" + Port + "/ ") : ""; // space needed for compatibility
                if ((Flags & URIFlags.HasRegionName) != 0)
                    ret += RegionName;
                return ret;
            }
        }

        // this needs to be set before get
        public bool IsLocalGrid
        {
            get { return (Flags & URIFlags.IsLocalGrid) != 0; }
            set
            {
                if(value)
                {
                    Host = string.Empty;
                    Flags &= ~URIFlags.HasHost;
                    Flags |= URIFlags.IsLocalGrid;
                }
            }
        }
    }
}