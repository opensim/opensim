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
using System.Threading.Tasks;

using OMV = OpenMetaverse;

namespace osWebRtcVoice
{
    /// <summary>
    /// This is the interface for the viewer session. It is used to store the
    /// state of the viewer session and to disconnect the session when needed.
    /// </summary>
    public interface IVoiceViewerSession
    {
        [Flags]
        public enum VFlags: uint
        {
            None = 0,
            IsParcel = 1,
            IsEstate = IsParcel | 2,
            IsAdmin = 4,
            IsChildAgent = 8,
        }

        // This ID is passed to and from the viewer to identify the session
        public string ViewerSessionID { get; set; }
        public IWebRtcVoiceService VoiceService { get; set; }
        // THis ID is passed between us and the voice service to idetify the session
        public string VoiceServiceSessionId { get; set; }
        // The UUID of the region that is being connected to
        public OMV.UUID RegionId { get; set; }

        // The simulator has a GUID to identify the user
        public OMV.UUID AgentId { get; set; }
        public VFlags Flags { get; set; }

        // Disconnect the connection to the voice service for this session
        public Task Shutdown();
    }
}
