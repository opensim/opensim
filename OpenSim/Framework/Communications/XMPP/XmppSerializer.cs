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
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Communications.XMPP
{
    public class XmppSerializer
    {
        // private static readonly ILog _log = 
        //     LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // need to do it this way, as XmlSerializer(type, extratypes)
        // does not work on mono (at least).
        private Dictionary<Type, XmlSerializer> _serializerForType = new Dictionary<Type, XmlSerializer>();
        private Dictionary<string, XmlSerializer> _serializerForName = new Dictionary<string, XmlSerializer>();
        private XmlSerializerNamespaces _xmlNs;
        private string _defaultNS;

        public XmppSerializer(bool server)
        {
            _xmlNs = new XmlSerializerNamespaces();
            _xmlNs.Add(String.Empty, String.Empty);
            if (server) 
                _defaultNS = "jabber:server";
            else
                _defaultNS = "jabber:client";

            // TODO: do this via reflection
            _serializerForType[typeof(XmppMessageStanza)] = _serializerForName["message"] = 
                new XmlSerializer(typeof(XmppMessageStanza), _defaultNS);
        }

        public void Serialize(XmlWriter xw, object o)
        {
            if (!_serializerForType.ContainsKey(o.GetType())) 
                throw new ArgumentException(String.Format("no serializer available for type {0}", o.GetType()));

            _serializerForType[o.GetType()].Serialize(xw, o, _xmlNs);
        }

        public object Deserialize(XmlReader xr)
        {
            // position on next element
            xr.Read();
            if (!_serializerForName.ContainsKey(xr.LocalName))
                throw new ArgumentException(String.Format("no serializer available for name {0}", xr.LocalName));

            return _serializerForName[xr.LocalName].Deserialize(xr);
        }
    }
}
