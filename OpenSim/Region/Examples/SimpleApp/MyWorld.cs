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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace SimpleApp
{
    public class MyWorld : Scene
    {
        private List<ScenePresence> m_avatars;

        public MyWorld(RegionInfo regionInfo, AgentCircuitManager authen, PermissionManager permissionManager, CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                       AssetCache assetCach, StorageManager storeMan, BaseHttpServer httpServer,
                       ModuleLoader moduleLoader, bool physicalPrim, bool ChildGetTasks)
            : base(regionInfo, authen, permissionManager, commsMan, sceneGridService, assetCach, storeMan, httpServer, moduleLoader, false, true, false)
        {
            m_avatars = new List<ScenePresence>();
        }

        public override void LoadWorldMap()
        {
            float[] map = new float[65536];

            for (int i = 0; i < 65536; i++)
            {
                map[i] = 25f;
            }

            Terrain.GetHeights1D(map);
            CreateTerrainTexture(true);
        }

        public override void AddNewClient(IClientAPI client, bool child)
        {
            SubscribeToClientEvents(client);

            ScenePresence avatar = CreateAndAddScenePresence(client, child);
            avatar.AbsolutePosition = new LLVector3(128, 128, 26);

            LLVector3 pos = new LLVector3(128, 128, 128);

            client.OnCompleteMovementToRegion +=
                delegate() { client.SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero); };
 
            client.SendRegionHandshake(m_regInfo);
        }
    }
}