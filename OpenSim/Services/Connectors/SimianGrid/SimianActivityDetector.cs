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
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using log4net;

namespace OpenSim.Services.Connectors.SimianGrid
{
    public class SimianActivityDetector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IGridUserService m_GridUserService;

        public SimianActivityDetector(IGridUserService guService)
        {
            m_GridUserService = guService;
            m_log.DebugFormat("[SIMIAN ACTIVITY DETECTOR]: Started");
        }

        public void AddRegion(Scene scene)
        {
            // For now the only events we listen to are these
            // But we could trigger the position update more often
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnAvatarEnteringNewParcel += OnEnteringNewParcel;
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnAvatarEnteringNewParcel -= OnEnteringNewParcel;
        }

        public void OnMakeRootAgent(ScenePresence sp)
        {
            m_log.DebugFormat("[SIMIAN ACTIVITY DETECTOR]: Detected root presence {0} in {1}", sp.UUID, sp.Scene.RegionInfo.RegionName);
            Util.FireAndForget(delegate(object o)
            {
                m_GridUserService.SetLastPosition(sp.UUID.ToString(), sp.ControllingClient.SessionId, sp.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
            }, null, "SimianActivityDetector.SetLastPositionOnMakeRootAgent");
        }

        public void OnNewClient(IClientAPI client)
        {
            client.OnConnectionClosed += OnConnectionClose;
        }

        public void OnConnectionClose(IClientAPI client)
        {
            if (client.SceneAgent.IsChildAgent)
                return;

//            m_log.DebugFormat("[ACTIVITY DETECTOR]: Detected client logout {0} in {1}", client.AgentId, client.Scene.RegionInfo.RegionName);
            m_GridUserService.LoggedOut(
                client.AgentId.ToString(), client.SessionId, client.Scene.RegionInfo.RegionID,
                client.SceneAgent.AbsolutePosition, client.SceneAgent.Lookat);
        }

        void OnEnteringNewParcel(ScenePresence sp, int localLandID, UUID regionID)
        {
            // Asynchronously update the position stored in the session table for this agent
            Util.FireAndForget(delegate(object o)
            {
                m_GridUserService.SetLastPosition(sp.UUID.ToString(), sp.ControllingClient.SessionId, sp.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
            }, null, "SimianActivityDetector.SetLastPositionOnEnteringNewParcel");
        }
    }
}