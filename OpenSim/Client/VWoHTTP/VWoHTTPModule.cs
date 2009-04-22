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

        private IHttpServer m_httpd;

        private readonly List<Scene> m_scenes = new List<Scene>();

        private Dictionary<UUID, VWHClientView> m_clients = new Dictionary<UUID, VWHClientView>();

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scenes.Add(scene);

            m_httpd = scene.CommsManager.HttpServer;
        }

        public void PostInitialise()
        {
            m_httpd.AddAgentHandler("vwohttp", this);
        }

        public void Close()
        {
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
            
            return false;
        }

        public bool Match(OSHttpRequest req, OSHttpResponse resp)
        {
            return req.Url.ToString().Contains("vwohttp");
        }

        #endregion
    }
}
