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
using OpenMetaverse;

namespace OpenSim.Framework.Capabilities
{
    [OSDMap]
    public class LLSDEnvironmentRequest
    {
        public UUID messageID;
        public UUID regionID;
    }

    [OSDMap]
    public class LLSDEnvironmentSetResponse
    {
        public UUID regionID;
        public UUID messageID;
        public Boolean success;
        public String fail_reason;
    }

    public class EnvironmentSettings
    {
        /// <summary>
        /// generates a empty llsd settings response for viewer
        /// </summary>
        /// <param name="messageID">the message UUID</param>
        /// <param name="regionID">the region UUID</param>
        public static string EmptySettings(UUID messageID, UUID regionID)
        {
            OSDArray arr = new OSDArray();
            LLSDEnvironmentRequest msg = new LLSDEnvironmentRequest();
            msg.messageID = messageID;
            msg.regionID = regionID;
            arr.Array.Add(msg);
            return LLSDHelpers.SerialiseLLSDReply(arr);
        }
    }

}
