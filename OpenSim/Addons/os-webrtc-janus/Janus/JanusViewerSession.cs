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

using System.Reflection;
using System.Threading.Tasks;

using OMV = OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;

namespace osWebRtcVoice
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
            m_log.Debug($"{LogHeader} JanusViewerSession created {ViewerSessionID}");
        }
        public JanusViewerSession(string pViewerSessionID, IWebRtcVoiceService pVoiceService)
        {
            ViewerSessionID = pViewerSessionID;
            VoiceService = pVoiceService;
            m_log.Debug($"{LogHeader} JanusViewerSession created {ViewerSessionID}");
        }

        // Send the messages to the voice service to try and get rid of the session
        // IVoiceViewerSession.Shutdown
        public async Task Shutdown()
        {
            m_log.DebugFormat($"{LogHeader} JanusViewerSession shutdown {ViewerSessionID}");
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
                _ = await s.DestroySession().ConfigureAwait(false);
                s.Dispose();
            }
        }
    }
}
