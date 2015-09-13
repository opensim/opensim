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

namespace OpenSim.Server.Base
{
    public class ProtocolVersions
    {
        /// <value>
        /// This is the external protocol versions.  It is separate from the OpenSimulator project version.
        /// 
        /// These version numbers should be increased by 1 every time a code
        /// change in the Service.Connectors and Server.Handlers, espectively, 
        /// makes the previous OpenSimulator revision incompatible
        /// with the new revision. 
        /// 
        /// Changes which are compatible with an older revision (e.g. older revisions experience degraded functionality
        /// but not outright failure) do not need a version number increment.
        /// 
        /// Having this version number allows the grid service to reject connections from regions running a version
        /// of the code that is too old. 
        ///
        /// </value>
        
        // The range of acceptable servers for client-side connectors
        public readonly static int ClientProtocolVersionMin = 1;
        public readonly static int ClientProtocolVersionMax = 1;

        // The range of acceptable clients in server-side handlers
        public readonly static int ServerProtocolVersionMin = 1;
        public readonly static int ServerProtocolVersionMax = 1;
    }
}
