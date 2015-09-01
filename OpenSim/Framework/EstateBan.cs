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
using System.Collections.Generic;
using System.Reflection;

using OpenMetaverse;

namespace OpenSim.Framework
{
    public class EstateBan
    {
        private uint m_estateID = 1;
        /// <summary>
        /// ID of the estate this ban limits access to.
        /// </summary>
        public uint EstateID
        {
            get
            {
                return m_estateID; 
            }
            set
            {
                m_estateID = value;
            }
        }

        private UUID m_bannedUserID = UUID.Zero;
        /// <summary>
        /// ID of the banned user.
        /// </summary>
        public UUID BannedUserID
        {
            get
            {
                return m_bannedUserID;
            }
            set
            {
                m_bannedUserID = value;
            }
        }

        private string m_bannedHostAddress = string.Empty;
        /// <summary>
        /// IP address or domain name of the banned client.
        /// </summary>
        public string BannedHostAddress
        {
            get
            {
                return m_bannedHostAddress;
            }
            set
            {
                m_bannedHostAddress = value;
            }
        }

        private string m_bannedHostIPMask = string.Empty;
        /// <summary>
        /// IP address mask for banning group of client hosts.
        /// </summary>
        public string BannedHostIPMask
        {
           get
            {
                return m_bannedHostIPMask;
            }
            set
            {
                m_bannedHostIPMask = value;
            }
        }

        private string m_bannedHostNameMask = string.Empty;
        /// <summary>
        /// Domain name mask for banning group of client hosts.
        /// </summary>
        public string BannedHostNameMask
        {
            get
            {
                return m_bannedHostNameMask;
            }
            set
            {
                m_bannedHostNameMask = value;
            }
        }

        public EstateBan() { }

        public Dictionary<string, object> ToMap()
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            PropertyInfo[] properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo p in properties)
                map[p.Name] = p.GetValue(this, null);

            return map;
        }

        public EstateBan(Dictionary<string, object> map)
        {
            foreach (KeyValuePair<string, object> kvp in map)
            {
                PropertyInfo p = this.GetType().GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (p == null)
                    continue;
                object value = p.GetValue(this, null);
                if (value is String)
                    p.SetValue(this, map[p.Name], null);
                else if (value is UInt32)
                    p.SetValue(this, UInt32.Parse((string)map[p.Name]), null);
                else if (value is Boolean)
                    p.SetValue(this, Boolean.Parse((string)map[p.Name]), null);
                else if (value is UUID)
                    p.SetValue(this, UUID.Parse((string)map[p.Name]), null);
            }
        }


        /// <summary>
        ///  For debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            Dictionary<string, object> map = ToMap();
            string result = string.Empty;
            foreach (KeyValuePair<string, object> kvp in map)
                result += string.Format("{0}: {1} {2}", kvp.Key, kvp.Value, Environment.NewLine);

            return result;
        }
    }
}
