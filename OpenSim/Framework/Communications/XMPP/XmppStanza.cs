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
using System.Xml.Serialization;

namespace OpenSim.Framework.Communications.XMPP
{
    public abstract class XmppStanza
    {
        /// <summary>
        /// counter used for generating ID
        /// </summary>
        [XmlIgnore]
        private static ulong _ctr = 0;

        /// <summary>
        /// recipient JID
        /// </summary>
        [XmlAttribute("to")]
        public string ToJid;

        /// <summary>
        /// sender JID
        /// </summary>
        [XmlAttribute("from")]
        public string FromJid;

        /// <summary>
        /// unique ID.
        /// </summary>
        [XmlAttribute("id")]
        public string MessageId;

        public XmppStanza()
        {
        }

        public XmppStanza(string fromJid, string toJid)
        {
            ToJid = toJid;
            FromJid = fromJid;
            MessageId = String.Format("OpenSim_{0}{1}", DateTime.UtcNow.Ticks, _ctr++);
        }
    }
}
