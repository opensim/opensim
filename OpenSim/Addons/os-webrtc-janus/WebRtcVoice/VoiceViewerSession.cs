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

using System.Linq;
using System.Collections.Generic;

using OpenMetaverse;
using System.Threading.Tasks;

namespace osWebRtcVoice
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
        public IWebRtcVoiceService VoiceService { get; set; }
        public string VoiceServiceSessionId { get; set; }
        public UUID RegionId { get; set; }
        public UUID AgentId { get; set; }
        public IVoiceViewerSession.VFlags Flags { get; set; }

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
        public static bool TryGetViewerSessionsByAgentId(UUID pAgentId, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> pViewerSessions)
        {
            lock (ViewerSessions)
            {
                pViewerSessions = ViewerSessions.Where(v => v.Value.AgentId == pAgentId);
                return pViewerSessions.Count() > 0;
            }
        }

        public static bool TryGetViewerSessionByAgentId(UUID pAgentId, out IVoiceViewerSession pViewerSession)
        {
            lock (ViewerSessions)
            {
                IEnumerable<KeyValuePair<string,IVoiceViewerSession>> sessions = ViewerSessions.Where(v => v.Value.AgentId == pAgentId);
                if(sessions.Count() > 0)
                {
                    pViewerSession = sessions.First().Value;
                    return true;
                }
                pViewerSession = null;
                return false;
            }
        }

        // Get a viewer session by the VoiceService session ID
        public static bool TryGetViewerSessionByVSSessionId(string pVSSessionId, out IVoiceViewerSession pViewerSession)
        {
            lock (ViewerSessions)
            {
                IEnumerable<KeyValuePair<string,IVoiceViewerSession>> sessions = ViewerSessions.Where(v => v.Value.VoiceServiceSessionId == pVSSessionId);
                if (sessions.Count() > 0)
                {
                    pViewerSession = sessions.First().Value;
                    return true;
                }
                pViewerSession = null;
                return false;
            }
        }

        public static bool TryGetViewerSessionByAgentAndRegion(UUID pAgentId, UUID pRegionId, out IVoiceViewerSession pViewerSession)
        {
            lock (ViewerSessions)
            {
                IVoiceViewerSession session = ViewerSessions.Values.FirstOrDefault(v => v.AgentId == pAgentId && v.RegionId == pRegionId);
                if (session is not null)
                {
                    pViewerSession = session;
                    return true;
                }
                pViewerSession = null;
                return false;
            }
        }

        public static bool TryGetViewerSessionsByAgentAndRegion(UUID pAgentId, UUID pRegionId, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> pViewerSessions)
        {
            lock (ViewerSessions)
            {
                pViewerSessions = ViewerSessions.Where(v => v.Value.AgentId == pAgentId && v.Value.RegionId == pRegionId);
                return pViewerSessions.Count() > 0;
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
            if (!string.IsNullOrEmpty(ViewerSessionID))
            {
                RemoveViewerSession(ViewerSessionID);
            }
            return Task.CompletedTask;        }
    }
}

