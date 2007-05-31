using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework;
using Nwc.XmlRpc;

namespace OpenSim
{
    public class AuthenticateSessionsRemote : AuthenticateSessionsBase
    {
        public AuthenticateSessionsRemote()
        {

        }

        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {
            return new XmlRpcResponse();
        }
    }
}
