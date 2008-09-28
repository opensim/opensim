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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.Environment
{
    public class EventQueueHelper
    {
        private EventQueueHelper() {} // no construction possible, it's an utility class

        private static byte[] regionHandleToByteArray(ulong regionHandle)
        {
            // Reverse endianness of RegionHandle
            return new byte[]
            {
                (byte)((regionHandle >> 56) % 256),
                (byte)((regionHandle >> 48) % 256),
                (byte)((regionHandle >> 40) % 256),
                (byte)((regionHandle >> 32) % 256),
                (byte)((regionHandle >> 24) % 256),
                (byte)((regionHandle >> 16) % 256),
                (byte)((regionHandle >> 8) % 256),
                (byte)(regionHandle % 256)
            };
        }

        private static LLSD buildEvent(string eventName, LLSD eventBody)
        {
            LLSDMap llsdEvent = new LLSDMap(2);
            llsdEvent.Add("message", new LLSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return llsdEvent;
        }

        public static LLSD EnableSimulator(ulong Handle, IPEndPoint endPoint)
        {
            LLSDMap llsdSimInfo = new LLSDMap(3);

            llsdSimInfo.Add("Handle", new LLSDBinary(regionHandleToByteArray(Handle)));
            llsdSimInfo.Add("IP", new LLSDBinary(endPoint.Address.GetAddressBytes()));
            llsdSimInfo.Add("Port", new LLSDInteger(endPoint.Port));

            LLSDArray arr = new LLSDArray(1);
            arr.Add(llsdSimInfo);

            LLSDMap llsdBody = new LLSDMap(1);
            llsdBody.Add("SimulatorInfo", arr);

            return buildEvent("EnableSimulator", llsdBody);
        }

        public static LLSD CrossRegion(ulong Handle, Vector3 pos, Vector3 lookAt,
                                       IPEndPoint newRegionExternalEndPoint,
                                       string capsURL, UUID AgentID, UUID SessionID)
        {
            LLSDArray LookAtArr = new LLSDArray(3);
            LookAtArr.Add(LLSD.FromReal(lookAt.X));
            LookAtArr.Add(LLSD.FromReal(lookAt.Y));
            LookAtArr.Add(LLSD.FromReal(lookAt.Z));

            LLSDArray PositionArr = new LLSDArray(3);
            PositionArr.Add(LLSD.FromReal(pos.X));
            PositionArr.Add(LLSD.FromReal(pos.Y));
            PositionArr.Add(LLSD.FromReal(pos.Z));

            LLSDMap InfoMap = new LLSDMap(2);
            InfoMap.Add("LookAt", LookAtArr);
            InfoMap.Add("Position", PositionArr);

            LLSDArray InfoArr = new LLSDArray(1);
            InfoArr.Add(InfoMap);

            LLSDMap AgentDataMap = new LLSDMap(2);
            AgentDataMap.Add("AgentID", LLSD.FromUUID(AgentID));
            AgentDataMap.Add("SessionID",  LLSD.FromUUID(SessionID));

            LLSDArray AgentDataArr = new LLSDArray(1);
            AgentDataArr.Add(AgentDataMap);

            LLSDMap RegionDataMap = new LLSDMap(4);
            RegionDataMap.Add("RegionHandle", LLSD.FromBinary(regionHandleToByteArray(Handle)));
            RegionDataMap.Add("SeedCapability", LLSD.FromString(capsURL));
            RegionDataMap.Add("SimIP", LLSD.FromBinary(newRegionExternalEndPoint.Address.GetAddressBytes()));
            RegionDataMap.Add("SimPort", LLSD.FromInteger(newRegionExternalEndPoint.Port));

            LLSDArray RegionDataArr = new LLSDArray(1);
            RegionDataArr.Add(RegionDataMap);

            LLSDMap llsdBody = new LLSDMap(3);
            llsdBody.Add("Info", InfoArr);
            llsdBody.Add("AgentData", AgentDataArr);
            llsdBody.Add("RegionData", RegionDataArr);

            return buildEvent("CrossedRegion", llsdBody);
        }

        public static LLSD TeleportFinishEvent(
            ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
            uint locationID, uint flags, string capsURL, UUID AgentID)
        {
            LLSDMap info = new LLSDMap();
            info.Add("AgentID", LLSD.FromUUID(AgentID));
            info.Add("LocationID", LLSD.FromInteger(4)); // TODO what is this?
            info.Add("RegionHandle", LLSD.FromBinary(regionHandleToByteArray(regionHandle)));
            info.Add("SeedCapability", LLSD.FromString(capsURL));
            info.Add("SimAccess", LLSD.FromInteger(simAccess));
            info.Add("SimIP", LLSD.FromBinary(regionExternalEndPoint.Address.GetAddressBytes()));
            info.Add("SimPort", LLSD.FromInteger(regionExternalEndPoint.Port));
            info.Add("TeleportFlags", LLSD.FromBinary(1L << 4)); // AgentManager.TeleportFlags.ViaLocation

            LLSDArray infoArr = new LLSDArray();
            infoArr.Add(info);

            LLSDMap body = new LLSDMap();
            body.Add("Info", infoArr);

            return buildEvent("TeleportFinish", body);
        }

        public static LLSD KeepAliveEvent()
        {
            return buildEvent("FAKEEVENT", new LLSDMap());
        }
    }
}
