using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.InterGrid
{
    public class OGSRadmin : IRegionModule 
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<Scene> m_scenes = new List<Scene>();
        private CommunicationsManager m_com;
        private IConfigSource m_settings;

        #region Implementation of IRegionModuleBase

        public string Name
        {
            get { return "OGS Supporting RAdmin"; }
        }


        public void Initialise(IConfigSource source)
        {
            m_settings = source;
        }

        public void Close()
        {

        }

        public void AddRegion(Scene scene)
        {
            lock(m_scenes)
                m_scenes.Add(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes)
                m_scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            
        }

        public void PostInitialise()
        {
            if (m_settings.Configs["Startup"].GetBoolean("gridmode", false))
            {
                m_com = m_scenes[0].CommsManager;
                m_com.HttpServer.AddXmlRPCHandler("grid_message", GridWideMessage);
            }
        }

        #endregion

        #region IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_settings = source;

            lock (m_scenes)
                m_scenes.Add(scene);
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public XmlRpcResponse GridWideMessage(XmlRpcRequest req, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            Hashtable requestData = (Hashtable)req.Params[0];

            if ((!requestData.Contains("password") || (string)requestData["password"] != m_com.NetworkServersInfo.GridRecvKey))
            {
                responseData["accepted"] = false;
                responseData["success"] = false;
                responseData["error"] = "Invalid Key";
                response.Value = responseData;
                return response;
            }

            string message = (string)requestData["message"];
            string user = (string)requestData["user"];
            m_log.InfoFormat("[RADMIN]: Broadcasting: {0}", message);

            lock(m_scenes)
                foreach (Scene scene in m_scenes)
                {
                    IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
                    if (dialogModule != null)
                        dialogModule.SendNotificationToUsersInRegion(UUID.Random(), user, message);
                }

            responseData["accepted"] = true;
            responseData["success"] = true;
            response.Value = responseData;

            return response;
        }
    }
}
