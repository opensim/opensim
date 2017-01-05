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

using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public struct mapItemReply
    {
        public uint x;
        public uint y;
        public UUID id;
        public int Extra;
        public int Extra2;
        public string name;

        public mapItemReply(uint pX, uint pY, UUID pId, string pName, int pExt1, int pExt2)
        {
            x = pX;
            y = pY;
            id = pId;
            name = pName;
            Extra = pExt1;
            Extra2 = pExt2;
        }

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["X"] = OSD.FromInteger((int)x);
            map["Y"] = OSD.FromInteger((int)y);
            map["ID"] = OSD.FromUUID(id);
            map["Name"] = OSD.FromString(name);
            map["Extra"] = OSD.FromInteger(Extra);
            map["Extra2"] = OSD.FromInteger(Extra2);
            return map;
        }

        public void FromOSD(OSDMap map)
        {
            x = (uint) map["X"].AsInteger();
            y = (uint) map["Y"].AsInteger();
            id = map["ID"].AsUUID();
            Extra = map["Extra"].AsInteger();
            Extra2 = map["Extra2"].AsInteger();
            name = map["Name"].AsString();
        }
    }
}
