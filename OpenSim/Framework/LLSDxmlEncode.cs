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

// a class for low level LLSD encoding into a provided StringBuilder
// for cases where we already need to know the low level detail
// and so using something like OSD or even protbuf is just a pure waste

using System;
using System.Globalization;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public static class LLSDxmlEncode
    {
        static readonly  DateTime depoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void AddStart(StringBuilder sb, bool addxmlversion = false)
        {
            if(addxmlversion)
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><llsd>"); // legacy llsd xml name still valid
            else
                sb.Append("<llsd>");
        }

        public const string LLSDEmpty = "<llsd><map /></llsd>";

        // got tired of creating a stringbuilder all the time;
        public static StringBuilder Start(int size = 256, bool addxmlversion = false)
        {
            StringBuilder sb = new StringBuilder(size);
            if(addxmlversion)
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><llsd>"); // legacy llsd xml name still valid
            else
                sb.Append("<llsd>");
            return sb;
        }

        public static void AddEnd(StringBuilder sb)
        {
            sb.Append("</llsd>");
        }

        public static string End(StringBuilder sb)
        {
            sb.Append("</llsd>");
            return sb.ToString();
        }

        // map == a list of key value pairs
        public static void AddMap(StringBuilder sb)
        {
            sb.Append("<map>");
        }

        public static void AddEndMap(StringBuilder sb)
        {
            sb.Append("</map>");
        }

        public static void AddEmptyMap(StringBuilder sb)
        {
            sb.Append("<map />");
        }

        // array == a list values
        public static void AddArray(StringBuilder sb)
        {
            sb.Append("<array>");
        }

        public static void AddEndArray(StringBuilder sb)
        {
            sb.Append("</array>");
        }

        public static void AddEndMapAndArray(StringBuilder sb)
        {
            sb.Append("</map></array>");
        }

        public static void AddEmptyArray(StringBuilder sb)
        {
            sb.Append("<array />");
        }

        // undefined or null
        public static void AddUnknownElem(StringBuilder sb)
        {
            sb.Append("<undef />");
        }

        public static void AddElem(bool e, StringBuilder sb)
        {
            if(e)
                sb.Append("<boolean>1</boolean>");
            else
                sb.Append("<boolean />");
        }

        public static void AddElem(byte e, StringBuilder sb)
        {
            if(e == 0)
                sb.Append("<integer />");
            else
            {
                sb.Append("<integer>");
                sb.Append(e.ToString());     
                sb.Append("</integer>");
            }
        }

        public static void AddElem(byte[] e, StringBuilder sb)
        {
            if(e == null || e.Length == 0)
                sb.Append("binary />");
            else
            {
                sb.Append("<binary>"); // encode64 is default
                sb.Append(Convert.ToBase64String(e,Base64FormattingOptions.None));     
                sb.Append("</binary>");
            }
        }

        public static void AddElem(int e, StringBuilder sb)
        {
            if(e == 0)
                sb.Append("<integer />");
            else
            {
                sb.Append("<integer>");
                sb.Append(e.ToString());
                sb.Append("</integer>");
            }
        }

        public static void AddElem(uint e, StringBuilder sb)
        {
            AddElem(uintToByteArray(e), sb);
        }

        public static void AddElem(ulong e, StringBuilder sb)
        {
            AddElem(ulongToByteArray(e), sb);
        }

        public static void AddElem(float e, StringBuilder sb)
        {
            if(e == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }
        }

        public static void AddElem(Vector2 e, StringBuilder sb)
        {
            sb.Append("<array>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(Vector3 e, StringBuilder sb)
        {
            sb.Append("<array>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Z == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Z.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(Quaternion e, StringBuilder sb)
        {
            sb.Append("<array><key>x</key>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }
            if(e.Z == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Z.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.W == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.W.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(double e, StringBuilder sb)
        {
            if(e == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }
        }

        public static void AddElem(UUID e, StringBuilder sb)
        {
            if(e == UUID.Zero)
                sb.Append("<uuid />");
            else
            {
                sb.Append("<uuid>");
                EscapeToXML(e.ToString(), sb);     
                sb.Append("</uuid>");
            }
        }

        public static void AddElem(string e, StringBuilder sb)
        {
            if(String.IsNullOrEmpty(e))
                sb.Append("<string />");
            else
            {
                sb.Append("<string>");
                EscapeToXML(e, sb);
                sb.Append("</string>");
            }
        }

        public static void AddRawElem(string e, StringBuilder sb)
        {
            if(String.IsNullOrEmpty(e))
                return;

            sb.Append(e);     
        }

        public static void AddElem(Uri e, StringBuilder sb)
        {
            if(e == null)
            {
                sb.Append("<uri />");
                return;
            }

            string s;
            if (e.IsAbsoluteUri)
                s = e.AbsoluteUri;
            else
                s = e.ToString();

            if(String.IsNullOrEmpty(s))
                sb.Append("<uri />");
            else
            {
                sb.Append("<uri>");
                sb.Append(s);     
                sb.Append("</uri>");
            }
        }

        public static void AddElem(DateTime e, StringBuilder sb)
        {
            DateTime u = e.ToUniversalTime();
            if(u == depoch)
            {
                sb.Append("<date />");
                return;
            }    
            string format;
            if(u.Hour == 0 && u.Minute == 0 && u.Second == 0)
                format = "yyyy-MM-dd";
            else if (u.Millisecond > 0)
                format = "yyyy-MM-ddTHH:mm:ss.ffZ";
            else
                format = "yyyy-MM-ddTHH:mm:ssZ";
            sb.Append("<date>");
            sb.Append(u.ToString(format,CultureInfo.InvariantCulture));
            sb.Append("</date>");
        }

//************ key value *******************
// assumes name is a valid llsd key

        public static void AddMap(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><map>");
        }

        public static void AddEmptyMap(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><map />");
        }

        // array == a list values
        public static void AddArray(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array>");
        }

        public static void AddArrayAndMap(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array><map>");
        }

        public static void AddEmptyArray(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array />");
        }

        // undefined or null
        public static void AddUnknownElem(string name, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><undef />");
        }

        public static void AddElem(string name, bool e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e)
                sb.Append("<boolean>1</boolean>");
            else
                sb.Append("<boolean />");
        }

        public static void AddElem(string name, byte e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == 0)
                sb.Append("<integer />");
            else
            {
                sb.Append("<integer>");
                sb.Append(e.ToString());
                sb.Append("</integer>");
            }
        }

        public static void AddElem(string name, byte[] e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == null || e.Length == 0)
                sb.Append("binary />");
            else
            {
                sb.Append("<binary>"); // encode64 is default
                sb.Append(Convert.ToBase64String(e,Base64FormattingOptions.None));
                sb.Append("</binary>");
            }
        }

        public static void AddElem(string name, int e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == 0)
                sb.Append("<integer />");
            else
            {
                sb.Append("<integer>");
                sb.Append(e.ToString());
                sb.Append("</integer>");
            }
        }

        public static void AddElem(string name, uint e, StringBuilder sb)
        {
            AddElem(name, uintToByteArray(e), sb);
        }

        public static void AddElem(string name, ulong e, StringBuilder sb)
        {
            AddElem(name, ulongToByteArray(e), sb);
        }

        public static void AddElem(string name, float e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.ToString(CultureInfo.InvariantCulture));     
                sb.Append("</real>");
            }
        }

        public static void AddElem(string name, Vector2 e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array>>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(string name, Vector3 e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Z == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Z.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(string name, Quaternion e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key><array>");

            if(e.X == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.Y == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }
            if(e.Z == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.Z.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }

            if(e.W == 0)
                sb.Append("<real /></array>");
            else
            {
                sb.Append("<real>");
                sb.Append(e.W.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real></array>");
            }
        }

        public static void AddElem(string name, double e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == 0)
                sb.Append("<real />");
            else
            {
                sb.Append("<real>");
                sb.Append(e.ToString(CultureInfo.InvariantCulture));
                sb.Append("</real>");
            }
        }

        public static void AddElem(string name, UUID e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == UUID.Zero)
                sb.Append("<uuid />");
            else
            {
                sb.Append("<uuid>");
                EscapeToXML(e.ToString(), sb);     
                sb.Append("</uuid>");
            }
        }

        public static void AddElem(string name, string e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(String.IsNullOrEmpty(e))
                sb.Append("<string />");
            else
            {
                sb.Append("<string>");
                EscapeToXML(e, sb);
                sb.Append("</string>");
            }
        }

        public static void AddRawElem(string name, string e, StringBuilder sb)
        {
            if (String.IsNullOrEmpty(e))
                return;

            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");
            sb.Append(e);
        }

        public static void AddElem(string name, Uri e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            if(e == null)
            {
                sb.Append("<uri />");
                return;
            }

            string s;
            if (e.IsAbsoluteUri)
                s = e.AbsoluteUri;
            else
                s = e.ToString();

            if(String.IsNullOrEmpty(s))
                sb.Append("<uri />");
            else
            {
                sb.Append("<uri>");
                sb.Append(s);     
                sb.Append("</uri>");
            }
        }

        public static void AddElem(string name, DateTime e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");

            DateTime u = e.ToUniversalTime();
            if(u == depoch)
            {
                sb.Append("<date />");
                return;
            }    
            string format;
            if(u.Hour == 0 && u.Minute == 0 && u.Second == 0)
                format = "yyyy-MM-dd";
            else if (u.Millisecond > 0)
                format = "yyyy-MM-ddTHH:mm:ss.ffZ";
            else
                format = "yyyy-MM-ddTHH:mm:ssZ";
            sb.Append("<date>");
            sb.Append(u.ToString(format,CultureInfo.InvariantCulture));
            sb.Append("</date>");
        }

        public static void AddLLSD(string e, StringBuilder sb)
        {
            sb.Append(e);
        }

        public static void AddLLSD(string name, string e, StringBuilder sb)
        {
            sb.Append("<key>");
            sb.Append(name);
            sb.Append("</key>");
            sb.Append(e);
        }

        public static void EscapeToXML(string s, StringBuilder sb)
        {
            int i;
            char c;
            int len = s.Length;

            for (i = 0; i < len; i++)
            {
                c = s[i];
                switch (c)
                {
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    case '\\':
                        sb.Append("&apos;");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }

        public static byte[] ulongToByteArray(ulong uLongValue)
        {
            return new byte[8]
            {
                (byte)(uLongValue >> 56),
                (byte)(uLongValue >> 48),
                (byte)(uLongValue >> 40),
                (byte)(uLongValue >> 32),
                (byte)(uLongValue >> 24),
                (byte)(uLongValue >> 16),
                (byte)(uLongValue >> 8),
                (byte)uLongValue
            };
        }

        public static byte[] uintToByteArray(uint value)
        {
            return new byte[4]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
        }
    }
}
