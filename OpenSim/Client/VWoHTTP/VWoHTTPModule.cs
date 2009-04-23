using System;
using System.Collections.Generic;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Client.VWoHTTP.ClientStack;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.VWoHTTP
{
    class VWoHTTPModule : IRegionModule, IHttpAgentHandler
    {
        private bool m_disabled = true;

        private IHttpServer m_httpd;

        private readonly List<Scene> m_scenes = new List<Scene>();

        private Dictionary<UUID, VWHClientView> m_clients = new Dictionary<UUID, VWHClientView>();

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            if(m_disabled)
                return;

            m_scenes.Add(scene);

            m_httpd = scene.CommsManager.HttpServer;
        }

        public void PostInitialise()
        {
            if (m_disabled)
                return;

            m_httpd.AddAgentHandler("vwohttp", this);
        }

        public void Close()
        {
            if (m_disabled)
                return;

            m_httpd.RemoveAgentHandler("vwohttp", this);
        }

        public string Name
        {
            get { return "VWoHTTP ClientStack"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region Implementation of IHttpAgentHandler

        public bool Handle(OSHttpRequest req, OSHttpResponse resp)
        {
            string[] urlparts = req.Url.AbsolutePath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            if (urlparts.Length < 2)
                return false;

            if (urlparts[1] == "connect")
            {
                UUID sessID = UUID.Random();

                VWHClientView client = new VWHClientView(sessID, UUID.Random(), "VWoHTTPClient", m_scenes[0]);

                m_clients.Add(sessID, client);

                return true;
            }
            else
            {
                if (urlparts.Length < 3)
                    return false;

                UUID sessionID;
                if (!UUID.TryParse(urlparts[1], out sessionID))
                    return false;

                if (!m_clients.ContainsKey(sessionID))
                    return false;

                return m_clients[sessionID].ProcessInMsg(req, resp);
            }
        }

        public bool Match(OSHttpRequest req, OSHttpResponse resp)
        {
            return req.Url.ToString().Contains("vwohttp");
        }

        #endregion
    }
}
