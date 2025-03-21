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

using System.Reflection;
using System.Threading.Tasks;

using OMV = OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;

namespace WebRtcVoice
{
    public class JanusViewerSession : IVoiceViewerSession
    {
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected static readonly string LogHeader = "[JANUS VIEWER SESSION]";

        // 'viewer_session' that is passed to and from the viewer
        // IVoiceViewerSession.ViewerSessionID
        public string ViewerSessionID { get; set; }
        // IVoiceViewerSession.VoiceService
        public IWebRtcVoiceService VoiceService { get; set; }
        // The Janus server keeps track of the user by this ID
        // IVoiceViewerSession.VoiceServiceSessionId
        public string VoiceServiceSessionId { get; set; }
        // IVoiceViewerSession.RegionId
        public OMV.UUID RegionId { get; set; }
        // IVoiceViewerSession.AgentId
        public OMV.UUID AgentId { get; set; }

        // Janus keeps track of the user by this ID
        public int ParticipantId { get; set; }

        // Connections to the Janus server
        public JanusSession Session { get; set; }
        public JanusAudioBridge AudioBridge { get; set; }
        public JanusRoom Room { get; set; }

        // This keeps copies of the offer/answer incase we need to resend
        public string OfferOrig { get; set; }
        public string Offer { get; set; }
        // Contains "type" and "sdp" fields
        public OSDMap Answer { get; set; }

        public JanusViewerSession(IWebRtcVoiceService pVoiceService)
        {
            ViewerSessionID = OMV.UUID.Random().ToString();
            VoiceService = pVoiceService;
            m_log.DebugFormat("{0} JanusViewerSession created {1}", LogHeader, ViewerSessionID);
        }
        public JanusViewerSession(string pViewerSessionID, IWebRtcVoiceService pVoiceService)
        {
            ViewerSessionID = pViewerSessionID;
            VoiceService = pVoiceService;
            m_log.DebugFormat("{0} JanusViewerSession created {1}", LogHeader, ViewerSessionID);
        }

        // Send the messages to the voice service to try and get rid of the session
        // IVoiceViewerSession.Shutdown
        public async Task Shutdown()
        {
            m_log.DebugFormat("{0} JanusViewerSession shutdown {1}", LogHeader, ViewerSessionID);
            if (Room is not null)
            {
                var rm = Room;
                Room = null;
                await rm.LeaveRoom(this);
            }
            if (AudioBridge is not null)
            {
                var ab = AudioBridge;
                AudioBridge = null;
                await ab.Detach();
            }   
            if (Session is not null)
            {
                var s = Session;
                Session = null;
                await s.DestroySession();
                s.Dispose();
            }
        }
    }
}
