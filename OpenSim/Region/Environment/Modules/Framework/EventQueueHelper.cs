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

        public static OSD buildEvent(string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("message", new OSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return llsdEvent;
        }

        public static OSD EnableSimulator(ulong Handle, IPEndPoint endPoint)
        {
            OSDMap llsdSimInfo = new OSDMap(3);

            llsdSimInfo.Add("Handle", new OSDBinary(regionHandleToByteArray(Handle)));
            llsdSimInfo.Add("IP", new OSDBinary(endPoint.Address.GetAddressBytes()));
            llsdSimInfo.Add("Port", new OSDInteger(endPoint.Port));

            OSDArray arr = new OSDArray(1);
            arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(1);
            llsdBody.Add("SimulatorInfo", arr);

            return buildEvent("EnableSimulator", llsdBody);
        }

        public static OSD DisableSimulator(ulong Handle)
        {
            //OSDMap llsdSimInfo = new OSDMap(1);

            //llsdSimInfo.Add("Handle", new OSDBinary(regionHandleToByteArray(Handle)));

            //OSDArray arr = new OSDArray(1);
            //arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(0);
            //llsdBody.Add("SimulatorInfo", arr);

            return buildEvent("DisableSimulator", llsdBody);
        }
        
        public static OSD CrossRegion(ulong Handle, Vector3 pos, Vector3 lookAt,
                                       IPEndPoint newRegionExternalEndPoint,
                                       string capsURL, UUID AgentID, UUID SessionID)
        {
            OSDArray LookAtArr = new OSDArray(3);
            LookAtArr.Add(OSD.FromReal(lookAt.X));
            LookAtArr.Add(OSD.FromReal(lookAt.Y));
            LookAtArr.Add(OSD.FromReal(lookAt.Z));

            OSDArray PositionArr = new OSDArray(3);
            PositionArr.Add(OSD.FromReal(pos.X));
            PositionArr.Add(OSD.FromReal(pos.Y));
            PositionArr.Add(OSD.FromReal(pos.Z));

            OSDMap InfoMap = new OSDMap(2);
            InfoMap.Add("LookAt", LookAtArr);
            InfoMap.Add("Position", PositionArr);

            OSDArray InfoArr = new OSDArray(1);
            InfoArr.Add(InfoMap);

            OSDMap AgentDataMap = new OSDMap(2);
            AgentDataMap.Add("AgentID", OSD.FromUUID(AgentID));
            AgentDataMap.Add("SessionID",  OSD.FromUUID(SessionID));

            OSDArray AgentDataArr = new OSDArray(1);
            AgentDataArr.Add(AgentDataMap);

            OSDMap RegionDataMap = new OSDMap(4);
            RegionDataMap.Add("RegionHandle", OSD.FromBinary(regionHandleToByteArray(Handle)));
            RegionDataMap.Add("SeedCapability", OSD.FromString(capsURL));
            RegionDataMap.Add("SimIP", OSD.FromBinary(newRegionExternalEndPoint.Address.GetAddressBytes()));
            RegionDataMap.Add("SimPort", OSD.FromInteger(newRegionExternalEndPoint.Port));

            OSDArray RegionDataArr = new OSDArray(1);
            RegionDataArr.Add(RegionDataMap);

            OSDMap llsdBody = new OSDMap(3);
            llsdBody.Add("Info", InfoArr);
            llsdBody.Add("AgentData", AgentDataArr);
            llsdBody.Add("RegionData", RegionDataArr);

            return buildEvent("CrossedRegion", llsdBody);
        }

        public static OSD TeleportFinishEvent(
            ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
            uint locationID, uint flags, string capsURL, UUID AgentID)
        {
            OSDMap info = new OSDMap();
            info.Add("AgentID", OSD.FromUUID(AgentID));
            info.Add("LocationID", OSD.FromInteger(4)); // TODO what is this?
            info.Add("RegionHandle", OSD.FromBinary(regionHandleToByteArray(regionHandle)));
            info.Add("SeedCapability", OSD.FromString(capsURL));
            info.Add("SimAccess", OSD.FromInteger(simAccess));
            info.Add("SimIP", OSD.FromBinary(regionExternalEndPoint.Address.GetAddressBytes()));
            info.Add("SimPort", OSD.FromInteger(regionExternalEndPoint.Port));
            info.Add("TeleportFlags", OSD.FromBinary(1L << 4)); // AgentManager.TeleportFlags.ViaLocation

            OSDArray infoArr = new OSDArray();
            infoArr.Add(info);

            OSDMap body = new OSDMap();
            body.Add("Info", infoArr);

            return buildEvent("TeleportFinish", body);
        }

        public static OSD ScriptRunningReplyEvent(UUID objectID, UUID itemID, bool running, bool mono)
        {
            OSDMap script = new OSDMap();
            script.Add("ObjectID", OSD.FromUUID(objectID));
            script.Add("ItemID", OSD.FromUUID(itemID));
            script.Add("Running", OSD.FromBoolean(running));
            script.Add("Mono", OSD.FromBoolean(mono));
            
            OSDArray scriptArr = new OSDArray();
            scriptArr.Add(script);
            
            OSDMap body = new OSDMap();
            body.Add("Script", scriptArr);
            
            return buildEvent("ScriptRunningReply", body);
        }

        public static OSD EstablishAgentCommunication(UUID agentID, string simIpAndPort, string seedcap)
        {
            OSDMap body = new OSDMap(3);
            body.Add("agent-id", new OSDUUID(agentID));
            body.Add("sim-ip-and-port", new OSDString(simIpAndPort));
            body.Add("seed-capability", new OSDString(seedcap));

            return buildEvent("EstablishAgentCommunication", body);
        }

        public static OSD KeepAliveEvent()
        {
            return buildEvent("FAKEEVENT", new OSDMap());
        }
    }
}
