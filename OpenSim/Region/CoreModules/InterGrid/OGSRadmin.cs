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
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.InterGrid
{
    public class OGSRadmin : IRegionModule 
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<Scene> m_scenes = new List<Scene>();
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
            lock (m_scenes)
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
                MainServer.Instance.AddXmlRPCHandler("grid_message", GridWideMessage);
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

            // REFACTORING PROBLEM. This authorization needs to be replaced with some other
            //if ((!requestData.Contains("password") || (string)requestData["password"] != m_com.NetworkServersInfo.GridRecvKey))
            //{
            //    responseData["accepted"] = false;
            //    responseData["success"] = false;
            //    responseData["error"] = "Invalid Key";
            //    response.Value = responseData;
            //    return response;
            //}

            string message = (string)requestData["message"];
            string user = (string)requestData["user"];
            m_log.InfoFormat("[RADMIN]: Broadcasting: {0}", message);

            lock (m_scenes)
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
