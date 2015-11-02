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
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public class EntityTransferContext
    {
        public EntityTransferContext()
        {
            InboundVersion = VersionInfo.SimulationServiceVersionAcceptedMax;
            OutboundVersion = VersionInfo.SimulationServiceVersionSupportedMax;
            WearablesCount = -1;
        }

        public float InboundVersion { get; set; }
        public float OutboundVersion { get; set; }
        public int WearablesCount { get; set; }

        public OSD Pack()
        {
            OSDMap data = new OSDMap();
            data["InboundVersion"] = OSD.FromReal(InboundVersion);
            data["OutboundVersion"] = OSD.FromReal(OutboundVersion);
            data["WearablesCount"] = OSD.FromInteger(WearablesCount);

            return data;
        }

        public void Unpack(OSD data)
        {
            OSDMap map = (OSDMap)data;

            if (map.ContainsKey("InboundVersion"))
                InboundVersion = (float)map["InboundVersion"].AsReal();
            if (map.ContainsKey("OutboundVersion"))
                OutboundVersion = (float)map["OutboundVersion"].AsReal();
            if (map.ContainsKey("WearablesCount"))
                WearablesCount = map["WearablesCount"].AsInteger();
        }
    }
}
