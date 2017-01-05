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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Common
{
    public static class EntityTransferHelpers
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Set up correct handling of the InformClientOfNeighbour call from the source region that triggers the
        /// viewer to setup a connection with the destination region.
        /// </summary>
        /// <param name='tc'></param>
        /// <param name='neighbourTcs'>
        /// A list that will be populated with any TestClients set up in response to
        /// being informed about a destination region.
        /// </param>
        public static void SetupInformClientOfNeighbourTriggersNeighbourClientCreate(
            TestClient tc, List<TestClient> neighbourTcs)
        {
            // XXX: Confusingly, this is also used for non-neighbour notification (as in teleports that do not use the
            // event queue).

            tc.OnTestClientInformClientOfNeighbour += (neighbourHandle, neighbourExternalEndPoint) =>
            {
                uint x, y;
                Util.RegionHandleToRegionLoc(neighbourHandle, out x, out y);

                m_log.DebugFormat(
                    "[TEST CLIENT]: Processing inform client of neighbour located at {0},{1} at {2}",
                    x, y, neighbourExternalEndPoint);

                AgentCircuitData newAgent = tc.RequestClientInfo();

                Scene neighbourScene;
                SceneManager.Instance.TryGetScene(x, y, out neighbourScene);

                TestClient neighbourTc = new TestClient(newAgent, neighbourScene);
                neighbourTcs.Add(neighbourTc);
                neighbourScene.AddNewAgent(neighbourTc, PresenceType.User);
            };
        }

        /// <summary>
        /// Set up correct handling of the InformClientOfNeighbour call from the source region that triggers the
        /// viewer to setup a connection with the destination region.
        /// </summary>
        /// <param name='tc'></param>
        /// <param name='neighbourTcs'>
        /// A list that will be populated with any TestClients set up in response to
        /// being informed about a destination region.
        /// </param>
        public static void SetupSendRegionTeleportTriggersDestinationClientCreateAndCompleteMovement(
            TestClient client, List<TestClient> destinationClients)
        {
            client.OnTestClientSendRegionTeleport
                += (regionHandle, simAccess, regionExternalEndPoint, locationID, flags, capsURL) =>
            {
                uint x, y;
                Util.RegionHandleToRegionLoc(regionHandle, out x, out y);

                m_log.DebugFormat(
                    "[TEST CLIENT]: Processing send region teleport for destination at {0},{1} at {2}",
                    x, y, regionExternalEndPoint);

                AgentCircuitData newAgent = client.RequestClientInfo();

                Scene destinationScene;
                SceneManager.Instance.TryGetScene(x, y, out destinationScene);

                TestClient destinationClient = new TestClient(newAgent, destinationScene);
                destinationClients.Add(destinationClient);
                destinationScene.AddNewAgent(destinationClient, PresenceType.User);

                ThreadPool.UnsafeQueueUserWorkItem(o => destinationClient.CompleteMovement(), null);
            };
        }
    }
}
