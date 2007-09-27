using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.UserManagement
{
    public class CAPSService
    {
        private BaseHttpServer m_server;

        public CAPSService(BaseHttpServer httpServer)
        {
            m_server = httpServer;
            this.AddCapsSeedHandler("/CapsSeed/", CapsRequest);
        }

        private void AddCapsSeedHandler(string path, RestMethod restMethod)
        {
            m_server.AddStreamHandler(new RestStreamHandler("POST",  path, restMethod));
        }

        public string CapsRequest(string request, string path, string param)
        {
            System.Console.WriteLine("new caps request " + request +" from path "+ path);
            return "";
        }
    }
}
