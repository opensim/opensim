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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Xml;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Communications.Local
{
    public class LocalInterregionComms : IRegionModule, IInterregionCommsOut, IInterregionCommsIn
    {
        private bool m_enabled = false;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Scene> m_sceneList = new List<Scene>();

        #region Events
        public event ChildAgentUpdateReceived OnChildAgentUpdate;

        #endregion /* Events */

        #region IRegionModule

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (m_sceneList.Count == 0)
            {
                IConfig startupConfig = config.Configs["Communications"];

                if ((startupConfig != null) && (startupConfig.GetString("InterregionComms", "RESTCommms") == "LocalComms"))
                {
                    m_log.Debug("[LOCAL COMMS]: Enabling InterregionComms LocalComms module");
                    m_enabled = true;
                }
            }

            if (!m_enabled)
                return;

            Init(scene);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LocalInterregionCommsModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
        /// <summary>
        /// Can be called from other modules.
        /// </summary>
        /// <param name="scene"></param>
        public void Init(Scene scene)
        {
            if (!m_sceneList.Contains(scene))
            {
                lock (m_sceneList)
                {
                    m_sceneList.Add(scene);
                    if (m_enabled)
                        scene.RegisterModuleInterface<IInterregionCommsOut>(this);
                    scene.RegisterModuleInterface<IInterregionCommsIn>(this);
                }

            }
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

        /**
         * Agent-related communications 
         */

        public bool SendCreateChildAgent(ulong regionHandle, AgentCircuitData aCircuit)
        {            
            foreach (Scene s in m_sceneList)
            {                
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
//                    m_log.DebugFormat("[LOCAL COMMS]: Found region {0} to send SendCreateChildAgent", regionHandle);
                    s.NewUserConnection(aCircuit);
                    return true;
                }
            }
            
//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for SendCreateChildAgent", regionHandle);
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentData cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.DebugFormat(
                    //    "[LOCAL COMMS]: Found region {0} {1} to send ChildAgentUpdate", 
                    //    s.RegionInfo.RegionName, regionHandle);
                    
                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }
            
//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for ChildAgentUpdate", regionHandle);
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentPosition cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool SendReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //Console.WriteLine("\n >>> Local SendReleaseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendReleaseAgent");
                    return s.IncomingReleaseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendReleaseAgent");
            return false;
        }

        public bool SendCloseAgent(ulong regionHandle, UUID id)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //Console.WriteLine("\n >>> Local SendCloseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCloseAgent");
                    return s.IncomingCloseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendCloseAgent");
            return false;
        }

        /**
         * Object-related communications 
         */

        public bool SendCreateObject(ulong regionHandle, ISceneObject sog)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCreateObject");
                    ISceneObject sogClone = sog.CloneForNewScene();
                    return s.IncomingCreateObject(sogClone);
                }
            }
            return false;
        }

        #endregion /* IInterregionComms */

        #region Misc

        public UUID GetRegionID(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return s.RegionInfo.RegionID;
            }
            // ? weird. should not happen
            return m_sceneList[0].RegionInfo.RegionID;
        }
        #endregion
    }
}
