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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Tests.Common
{
    public class TestEventQueueGetModule : IEventQueue, INonSharedRegionModule
    {
        public class Event
        {
            public string Name { get; set; }
            public object[] Args { get; set; }

            public Event(string name, object[] args)
            {
                name = Name;
                args = Args;
            }
        }

        public Dictionary<UUID, List<Event>> Events { get; set; }

        public void Initialise(IConfigSource source) {}

        public void Close() {}

        public void AddRegion(Scene scene)
        {
            Events = new Dictionary<UUID, List<Event>>();
            scene.RegisterModuleInterface<IEventQueue>(this);
        }

        public void RemoveRegion (Scene scene) {}

        public void RegionLoaded (Scene scene) {}

        public string Name { get { return "TestEventQueueGetModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        private void AddEvent(UUID avatarID, string name, params object[] args)
        {
            Console.WriteLine("Adding event {0} for {1}", name, avatarID);

            List<Event> avEvents;

            if (!Events.ContainsKey(avatarID))
            {
                avEvents = new List<Event>();
                Events[avatarID] = avEvents;
            }
            else
            {
                avEvents = Events[avatarID];
            }

            avEvents.Add(new Event(name, args));
        }

        public void ClearEvents()
        {
            if (Events != null)
                Events.Clear();
        }

        public bool Enqueue(OSD o, UUID avatarID)
        {
            AddEvent(avatarID, "Enqueue", o);
            return true;
        }

        public void DisableSimulator(ulong handle, UUID avatarID)
        {
            AddEvent(avatarID, "DisableSimulator", handle);
        }

        public void EnableSimulator (ulong handle, IPEndPoint endPoint, UUID avatarID, int regionSizeX, int regionSizeY)
        {
            AddEvent(avatarID, "EnableSimulator", handle);
        }

        public void EstablishAgentCommunication (UUID avatarID, IPEndPoint endPoint, string capsPath,
                                ulong regionHandle, int regionSizeX, int regionSizeY)
        {
            AddEvent(avatarID, "EstablishAgentCommunication", endPoint, capsPath);
        }

        public void TeleportFinishEvent (ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
                    uint locationID, uint flags, string capsURL, UUID agentID, int regionSizeX, int regionSizeY)
        {
            AddEvent(agentID, "TeleportFinishEvent", regionHandle, simAccess, regionExternalEndPoint, locationID, flags, capsURL);
        }

        public void CrossRegion (ulong handle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint,
                                    string capsURL, UUID avatarID, UUID sessionID, int regionSizeX, int regionSizeY)
        {
            AddEvent(avatarID, "CrossRegion", handle, pos, lookAt, newRegionExternalEndPoint, capsURL, sessionID);
        }

        public void ChatterboxInvitation(
            UUID sessionID, string sessionName, UUID fromAgent, string message, UUID toAgent, string fromName,
            byte dialog, uint timeStamp, bool offline, int parentEstateID, Vector3 position, uint ttl,
            UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            AddEvent(
                toAgent, "ChatterboxInvitation", sessionID, sessionName, fromAgent, message, toAgent, fromName, dialog,
                timeStamp, offline, parentEstateID, position, ttl, transactionID, fromGroup, binaryBucket);
        }

        public void ChatterBoxSessionAgentListUpdates (UUID sessionID, UUID fromAgent, UUID toAgent, bool canVoiceChat, bool isModerator, bool textMute , bool isEnterorLeave)
        {
            AddEvent(toAgent, "ChatterBoxSessionAgentListUpdates", sessionID, fromAgent, canVoiceChat, isModerator, textMute, isEnterorLeave);
        }

        public void ChatterBoxForceClose (UUID toAgent, UUID sessionID, string reason)
        {
            AddEvent(toAgent, "ForceCloseChatterBoxSession", sessionID, reason);
        }

        public void ParcelProperties (OpenMetaverse.Messages.Linden.ParcelPropertiesMessage parcelPropertiesMessage, UUID avatarID)
        {
            AddEvent(avatarID, "ParcelProperties", parcelPropertiesMessage);
        }

        public void GroupMembershipData(UUID receiverAgent, GroupMembershipData[] data)
        {
            AddEvent(receiverAgent, "AgentGroupDataUpdate", data);
        }

        public OSD ScriptRunningEvent (UUID objectID, UUID itemID, bool running, bool mono)
        {
            Console.WriteLine("ONE");
            throw new System.NotImplementedException ();
        }

        public OSD BuildEvent(string eventName, OSD eventBody)
        {
            Console.WriteLine("TWO");
            throw new System.NotImplementedException ();
        }

        public void partPhysicsProperties (uint localID, byte physhapetype, float density, float friction, float bounce, float gravmod, UUID avatarID)
        {
            AddEvent(avatarID, "partPhysicsProperties", localID, physhapetype, density, friction, bounce, gravmod);
        }
    }
}