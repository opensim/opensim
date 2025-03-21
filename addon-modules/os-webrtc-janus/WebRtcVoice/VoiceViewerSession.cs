// Copyright 2024 Robert Adams (misterblue@misterblue.com)
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Linq;
using System.Collections.Generic;

using OpenMetaverse;
using System.Threading.Tasks;

namespace WebRtcVoice
{
    public class VoiceViewerSession : IVoiceViewerSession
    {

        // A simple session structure that is used when the connection is actually in the
        //    remote service.
        public VoiceViewerSession(IWebRtcVoiceService pVoiceService, UUID pRegionId, UUID pAgentId)
        {
            RegionId = pRegionId;
            AgentId = pAgentId;
            ViewerSessionID = UUID.Random().ToString();
            VoiceService = pVoiceService;
            
        }
        public string ViewerSessionID { get; set; }
        public IWebRtcVoiceService VoiceService { get ; set; }
        public string VoiceServiceSessionId { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public UUID RegionId { get ; set; }
        public UUID AgentId { get ; set; }

        // =====================================================================
        // ViewerSessions hold the connection information for the client connection through to the voice service.
        // This collection is static and is simulator wide so there will be sessions for all regions and all clients.
        public static Dictionary<string, IVoiceViewerSession> ViewerSessions = new Dictionary<string, IVoiceViewerSession>();
        // Get a viewer session by the viewer session ID
        public static bool TryGetViewerSession(string pViewerSessionId, out IVoiceViewerSession pViewerSession)
        {
            lock (ViewerSessions)
            {
                return ViewerSessions.TryGetValue(pViewerSessionId, out pViewerSession);
            }
        }
        // public static bool TryGetViewerSessionByAgentId(UUID pAgentId, out IVoiceViewerSession pViewerSession)
        public static bool TryGetViewerSessionByAgentId(UUID pAgentId, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> pViewerSessions)
        {
            lock (ViewerSessions)
            {
                pViewerSessions = ViewerSessions.Where(v => v.Value.AgentId == pAgentId);
                return pViewerSessions.Count() > 0;
            }
        }
        // Get a viewer session by the VoiceService session ID
        public static bool TryGetViewerSessionByVSSessionId(string pVSSessionId, out IVoiceViewerSession pViewerSession)
        {
            lock (ViewerSessions)
            {
                var sessions = ViewerSessions.Where(v => v.Value.VoiceServiceSessionId == pVSSessionId);
                if (sessions.Count() > 0)
                {
                    pViewerSession = sessions.First().Value;
                    return true;
                }   
                pViewerSession = null;
                return false;
            }
        }
        public static void AddViewerSession(IVoiceViewerSession pSession)
        {
            lock (ViewerSessions)
            {
                ViewerSessions[pSession.ViewerSessionID] = pSession;
            }
        }
        public static void RemoveViewerSession(string pSessionId)
        {
            lock (ViewerSessions)
            {
                ViewerSessions.Remove(pSessionId);
            }
        }

        // Update a ViewSession from one ID to another.
        // Remove the old session ID from the ViewerSessions collection, update the
        //     sessionID value in  the IVoiceViewerSession, and add the session back to the
        //     collection.
        // This is used in the kludge to synchronize a region's ViewerSessionID with the
        //     remote VoiceService's session ID.
        public static void UpdateViewerSessionId(IVoiceViewerSession pSession, string pNewSessionId)
        {
            lock (ViewerSessions)
            {
                ViewerSessions.Remove(pSession.ViewerSessionID);
                pSession.ViewerSessionID = pNewSessionId;
                ViewerSessions[pSession.ViewerSessionID] = pSession;
            }
        }

        public Task Shutdown()
        {
            throw new System.NotImplementedException();
        }
    }
}

