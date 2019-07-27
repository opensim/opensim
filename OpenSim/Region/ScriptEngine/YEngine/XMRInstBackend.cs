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
using System.Threading;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using log4net;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /****************************************************\
     *  This file contains routines called by scripts.  *
    \****************************************************/

    public class XMRLSL_Api: LSL_Api
    {
        public AsyncCommandManager acm;
        private XMRInstance inst;

        public void InitXMRLSLApi(XMRInstance i)
        {
            acm = AsyncCommands;
            inst = i;
        }

        protected override void ScriptSleep(int ms)
        {
            ms = (int)(ms * m_ScriptDelayFactor);
            if (ms < 10)
                return;

            inst.Sleep(ms);
        }

        public override void llSleep(double sec)
        {
            inst.Sleep((int)(sec * 1000.0));
        }

        public override void llDie()
        {
            inst.Die();
        }

        /**
         * @brief Seat avatar on prim.
         * @param owner = true: owner of prim script is running in
         *               false: avatar that has given ANIMATION permission on the prim
         * @returns 0: successful
         *         -1: no permission to animate
         *         -2: no av granted perms
         *         -3: av not in region
         */
        /* engines should not have own API
                public int xmrSeatAvatar (bool owner)
                {
                    // Get avatar to be seated and make sure they have given us ANIMATION permission

                    UUID avuuid;
                    if (owner) {
                        avuuid = inst.m_Part.OwnerID;
                    } else {
                        if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) == 0) {
                            return -1;
                        }
                        avuuid = m_item.PermsGranter;
                    }
                    if (avuuid == UUID.Zero) {
                        return -2;
                    }

                    ScenePresence presence = World.GetScenePresence (avuuid);
                    if (presence == null) {
                        return -3;
                    }

                    // remoteClient = not used by ScenePresence.HandleAgentRequestSit()
                    //      agentID = not used by ScenePresence.HandleAgentRequestSit()
                    //     targetID = UUID of prim to sit on
                    //       offset = offset of sitting position

                    presence.HandleAgentRequestSit (null, UUID.Zero, m_host.UUID, OpenMetaverse.Vector3.Zero);
                    return 0;
                }
        */
        /**
         * @brief llTeleportAgent() is broken in that if you pass it a landmark,
         *        it still subjects the position to spawn points, as it always
         *        calls RequestTeleportLocation() with TeleportFlags.ViaLocation.
         *        See llTeleportAgent() and CheckAndAdjustTelehub().
         *
         * @param agent    = what agent to teleport
         * @param landmark = inventory name or UUID of a landmark object
         * @param lookat   = looking direction after teleport
         */
        /* engines should not have own API
                public void xmrTeleportAgent2Landmark (string agent, string landmark, LSL_Vector lookat)
                {
                    // find out about agent to be teleported
                    UUID agentId;
                    if (!UUID.TryParse (agent, out agentId)) throw new ApplicationException ("bad agent uuid");

                    ScenePresence presence = World.GetScenePresence (agentId);
                    if (presence == null) throw new ApplicationException ("agent not present in scene");
                    if (presence.IsNPC) throw new ApplicationException ("agent is an NPC");
                    if (presence.IsGod) throw new ApplicationException ("agent is a god");

                    // prim must be owned by land owner or prim must be attached to agent
                    if (m_host.ParentGroup.AttachmentPoint == 0) {
                        if (m_host.OwnerID != World.LandChannel.GetLandObject (presence.AbsolutePosition).LandData.OwnerID) {
                            throw new ApplicationException ("prim not owned by land's owner");
                        }
                    } else {
                        if (m_host.OwnerID != presence.UUID) throw new ApplicationException ("prim not attached to agent");
                    }

                    // find landmark in inventory or by UUID
                    UUID assetID = ScriptUtils.GetAssetIdFromKeyOrItemName (m_host, landmark);
                    if (assetID == UUID.Zero) throw new ApplicationException ("no such landmark");

                    // read it in and make sure it is a landmark
                    AssetBase lma = World.AssetService.Get (assetID.ToString ());
                    if ((lma == null) || (lma.Type != (sbyte)AssetType.Landmark)) throw new ApplicationException ("not a landmark");

                    // parse the record
                    AssetLandmark lm = new AssetLandmark (lma);

                    // the regionhandle (based on region's world X,Y) might be out of date
                    // re-read the handle so we can pass it to RequestTeleportLocation()
                    var region = World.GridService.GetRegionByUUID (World.RegionInfo.ScopeID, lm.RegionID);
                    if (region == null) throw new ApplicationException ("no such region");

                    // finally ready to teleport
                    World.RequestTeleportLocation (presence.ControllingClient,
                                                   region.RegionHandle,
                                                   lm.Position,
                                                   lookat,
                                                   (uint)TeleportFlags.ViaLandmark);
                }
        */
        /**
         * @brief Allow any member of group given by config SetParcelMusicURLGroup to set music URL.
         *        Code modelled after llSetParcelMusicURL().
         * @param newurl = new URL to set (or "" to leave it alone)
         * @returns previous URL string
         */
        /* engines should not have own API
                public string xmrSetParcelMusicURLGroup (string newurl)
                {
                    string groupname = m_ScriptEngine.Config.GetString ("SetParcelMusicURLGroup", "");
                    if (groupname == "") throw new ApplicationException ("no SetParcelMusicURLGroup config param set");

                    IGroupsModule igm = World.RequestModuleInterface<IGroupsModule> ();
                    if (igm == null) throw new ApplicationException ("no GroupsModule loaded");

                    GroupRecord grouprec = igm.GetGroupRecord (groupname);
                    if (grouprec == null) throw new ApplicationException ("no such group " + groupname);

                    GroupMembershipData gmd = igm.GetMembershipData (grouprec.GroupID, m_host.OwnerID);
                    if (gmd == null) throw new ApplicationException ("not a member of group " + groupname);

                    ILandObject land = World.LandChannel.GetLandObject (m_host.AbsolutePosition);
                    if (land == null) throw new ApplicationException ("no land at " + m_host.AbsolutePosition.ToString ());
                    string oldurl = land.GetMusicUrl ();
                    if (oldurl == null) oldurl = "";
                    if ((newurl != null) && (newurl != "")) land.SetMusicUrl (newurl);
                    return oldurl;
                }
        */
    }

    public partial class XMRInstance
    {
        /**
         * @brief The script is calling llReset().
         *        We throw an exception to unwind the script out to its main
         *        causing all the finally's to execute and it will also set
         *        eventCode = None to indicate event handler has completed.
         */
        public void ApiReset()
        {
            // do not do llResetScript on entry
            if(eventCode == ScriptEventCode.state_entry && stateCode == 0)
                return;
            // do clear the events queue on reset
            ClearQueue();
            //ClearQueueExceptLinkMessages();
            throw new ScriptResetException();
        }

        /**
         * @brief The script is calling one of the llDetected...(int number)
         *        functions.  Return corresponding DetectParams pointer.
         */
        public DetectParams GetDetectParams(int number)
        {
            DetectParams dp = null;
            if((number >= 0) && (m_DetectParams != null) && (number < m_DetectParams.Length))
                dp = m_DetectParams[number];

            return dp;
        }

        /**
         * @brief Script is calling llDie, so flag the run loop to delete script
         *        once we are off the microthread stack, and throw an exception
         *        to unwind the stack asap.
         */
        public void Die()
        {
            // llDie doesn't work in attachments!
            if(m_Part.ParentGroup.IsAttachment || m_DetachQuantum > 0)
                return;

            throw new ScriptDieException();
        }

        /**
         * @brief Called by script to sleep for the given number of milliseconds.
         */
        public void Sleep(int ms)
        {
            lock(m_QueueLock)
            {
                 // Say how long to sleep.
                m_SleepUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(ms);

                 // Don't wake on any events.
                m_SleepEventMask1 = 0;
                m_SleepEventMask2 = 0;
            }

             // The compiler follows all calls to llSleep() with a call to CheckRun().
             // So tell CheckRun() to suspend the microthread.
            suspendOnCheckRunTemp = true;
        }

        /**
         * Block script execution until an event is queued or a timeout is reached.
         * @param timeout = maximum number of seconds to wait
         * @param returnMask = if event is queued that matches these mask bits,
         *                     the script is woken, that event is dequeued and
         *                     returned to the caller.  The event handler is not
         *                     executed.
         * @param backgroundMask = if any of these events are queued while waiting,
         *                         execute their event handlers.  When any such event
         *                         handler exits, continue waiting for events or the
         *                         timeout.
         * @returns empty list: no event was queued that matched returnMask and the timeout was reached
         *                      or a background event handler changed state (eg, via 'state' statement)
         *                else: list giving parameters of the event:
         *                      [0] = event code (integer)
         *                   [1..n] = call parameters to the event, if any
         * Notes:
         *   1) Scrips should use XMREVENTMASKn_<eventname> symbols for the mask arguments,
         *      where n is 1 or 2 for mask1 or mask2 arguments.
         *      The list[0] return argument can be decoded by using XMREVENTCODE_<eventname> symbols.
         *   2) If all masks are zero, the call ends up acting like llSleep.
         *   3) If an event is enabled in both returnMask and backgroundMask, the returnMask bit
         *      action takes precedence, ie, the event is returned.  This allows a simple specification
         *      of -1 for both backgroundMask arguments to indicate that all events not listed in
         *      the returnMask argumetns should be handled in the background.
         *   4) Any events not listed in either returnMask or backgroundMask arguments will be
         *      queued for later processing (subject to normal queue limits).
         *   5) Background event handlers execute as calls from within xmrEventDequeue, they do
         *      not execute as separate threads.  Thus any background event handlers must return
         *      before the call to xmrEventDequeue will return.
         *   6) If a background event handler changes state (eg, via 'state' statement), the state
         *      is immediately changed and the script-level xmrEventDequeue call does not return.
         *   7) For returned events, the detect parameters are overwritten by the returned event.
         *      For background events, the detect parameters are saved and restored.
         *   8) Scripts must contain dummy event handler definitions for any event types that may
         *      be returned by xmrEventDequeue, to let the runtime know that the script is capable
         *      of processing that event type.  Otherwise, the event may not be queued to the script.
         */
        private static LSL_List emptyList = new LSL_List(new object[0]);

        public override LSL_List xmrEventDequeue(double timeout, int returnMask1, int returnMask2,
                                                  int backgroundMask1, int backgroundMask2)
        {
            DateTime sleepUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeout * 1000.0);
            EventParams evt = null;
            int callNo, evc2;
            int evc1 = 0;
            int mask1 = returnMask1 | backgroundMask1;  // codes 00..31
            int mask2 = returnMask2 | backgroundMask2;  // codes 32..63
            LinkedListNode<EventParams> lln = null;
            object[] sv;
            ScriptEventCode evc = ScriptEventCode.None;

            callNo = -1;
            try
            {
                if(callMode == CallMode_NORMAL)
                    goto findevent;

                 // Stack frame is being restored as saved via CheckRun...().
                 // Restore necessary values then jump to __call<n> label to resume processing.
                sv = RestoreStackFrame("xmrEventDequeue", out callNo);
                sleepUntil = DateTime.Parse((string)sv[0]);
                returnMask1 = (int)sv[1];
                returnMask2 = (int)sv[2];
                mask1 = (int)sv[3];
                mask2 = (int)sv[4];
                switch(callNo)
                {
                    case 0:
                        goto __call0;
                    case 1:
                        {
                            evc1 = (int)sv[5];
                            evc = (ScriptEventCode)(int)sv[6];
                            DetectParams[] detprms = ObjArrToDetPrms((object[])sv[7]);
                            object[] ehargs = (object[])sv[8];
                            evt = new EventParams(evc.ToString(), ehargs, detprms);
                            goto __call1;
                        }
                }
                throw new ScriptBadCallNoException(callNo);

                 // Find first event that matches either the return or background masks.
                findevent:
                Monitor.Enter(m_QueueLock);
                for(lln = m_EventQueue.First; lln != null; lln = lln.Next)
                {
                    evt = lln.Value;
                    evc = (ScriptEventCode)Enum.Parse(typeof(ScriptEventCode), evt.EventName);
                    evc1 = (int)evc;
                    evc2 = evc1 - 32;
                    if((((uint)evc1 < (uint)32) && (((mask1 >> evc1) & 1) != 0)) ||
                        (((uint)evc2 < (uint)32) && (((mask2 >> evc2) & 1) != 0)))
                        goto remfromq;
                }

                 // Nothing found, sleep while one comes in.
                m_SleepUntil = sleepUntil;
                m_SleepEventMask1 = mask1;
                m_SleepEventMask2 = mask2;
                Monitor.Exit(m_QueueLock);
                suspendOnCheckRunTemp = true;
                callNo = 0;
                __call0:
                CheckRunQuick();
                goto checktmo;

                 // Found one, remove it from queue.
                remfromq:
                m_EventQueue.Remove(lln);
                if((uint)evc1 < (uint)m_EventCounts.Length)
                    m_EventCounts[evc1]--;

                Monitor.Exit(m_QueueLock);
                m_InstEHEvent++;

                 // See if returnable or background event.
                if((((uint)evc1 < (uint)32) && (((returnMask1 >> evc1) & 1) != 0)) ||
                    (((uint)evc2 < (uint)32) && (((returnMask2 >> evc2) & 1) != 0)))
                {
                     // Returnable event, return its parameters in a list.
                     // Also set the detect parameters to what the event has.
                    int plen = evt.Params.Length;
                    object[] plist = new object[plen + 1];
                    plist[0] = (LSL_Integer)evc1;
                    for(int i = 0; i < plen;)
                    {
                        object ob = evt.Params[i];
                        if(ob is int)
                            ob = (LSL_Integer)(int)ob;
                        else if(ob is double)
                            ob = (LSL_Float)(double)ob;
                        else if(ob is string)
                            ob = (LSL_String)(string)ob;
                        plist[++i] = ob;
                    }
                    m_DetectParams = evt.DetectParams;
                    return new LSL_List(plist);
                }

                 // It is a background event, simply call its event handler,
                 // then check event queue again.
                callNo = 1;
                __call1:
                ScriptEventHandler seh = m_ObjCode.scriptEventHandlerTable[stateCode, evc1];
                if(seh == null)
                    goto checktmo;

                DetectParams[] saveDetParams = this.m_DetectParams;
                object[] saveEHArgs = this.ehArgs;
                ScriptEventCode saveEventCode = this.eventCode;

                m_DetectParams = evt.DetectParams;
                ehArgs = evt.Params;
                eventCode = evc;

                try
                {
                    seh(this);
                }
                finally
                {
                    m_DetectParams = saveDetParams;
                    ehArgs = saveEHArgs;
                    eventCode = saveEventCode;
                }

                 // Keep waiting until we find a returnable event or timeout.
                checktmo:
                if(DateTime.UtcNow < sleepUntil)
                    goto findevent;

                 // We timed out, return an empty list.
                return emptyList;
            }
            finally
            {
                if(callMode != CallMode_NORMAL)
                {
                     // Stack frame is being saved by CheckRun...().
                     // Save everything we need at the __call<n> labels so we can restore it
                     // when we need to.
                    sv = CaptureStackFrame("xmrEventDequeue", callNo, 9);
                    sv[0] = sleepUntil.ToString();                  // needed at __call0,__call1
                    sv[1] = returnMask1;                             // needed at __call0,__call1
                    sv[2] = returnMask2;                             // needed at __call0,__call1
                    sv[3] = mask1;                                   // needed at __call0,__call1
                    sv[4] = mask2;                                   // needed at __call0,__call1
                    if(callNo == 1)
                    {
                        sv[5] = evc1;                                // needed at __call1
                        sv[6] = (int)evc;                            // needed at __call1
                        sv[7] = DetPrmsToObjArr(evt.DetectParams);  // needed at __call1
                        sv[8] = evt.Params;                          // needed at __call1
                    }
                }
            }
        }

        /**
         * @brief Enqueue an event
         * @param ev = as returned by xmrEventDequeue saying which event type to queue
         *             and what argument list to pass to it.  The llDetect...() parameters
         *             are as currently set for the script (use xmrEventLoadDets to set how
         *             you want them to be different).
         */
        public override void xmrEventEnqueue(LSL_List ev)
        {
            object[] data = ev.Data;
            ScriptEventCode evc = (ScriptEventCode)ListInt(data[0]);

            int nargs = data.Length - 1;
            object[] args = new object[nargs];
            Array.Copy(data, 1, args, 0, nargs);

            PostEvent(new EventParams(evc.ToString(), args, m_DetectParams));
        }

        /**
         * @brief Save current detect params into a list
         * @returns a list containing current detect param values
         */
        private const int saveDPVer = 1;

        public override LSL_List xmrEventSaveDets()
        {
            object[] obs = DetPrmsToObjArr(m_DetectParams);
            return new LSL_List(obs);
        }

        private static object[] DetPrmsToObjArr(DetectParams[] dps)
        {
            int len = dps.Length;
            object[] obs = new object[len * 16 + 1];
            int j = 0;
            obs[j++] = (LSL_Integer)saveDPVer;
            for(int i = 0; i < len; i++)
            {
                DetectParams dp = dps[i];
                obs[j++] = (LSL_String)dp.Key.ToString();    // UUID
                obs[j++] = dp.OffsetPos;                     // vector
                obs[j++] = (LSL_Integer)dp.LinkNum;          // integer
                obs[j++] = (LSL_String)dp.Group.ToString();  // UUID
                obs[j++] = (LSL_String)dp.Name;              // string
                obs[j++] = (LSL_String)dp.Owner.ToString();  // UUID
                obs[j++] = dp.Position;                      // vector
                obs[j++] = dp.Rotation;                      // rotation
                obs[j++] = (LSL_Integer)dp.Type;             // integer
                obs[j++] = dp.Velocity;                      // vector
                obs[j++] = dp.TouchST;                       // vector
                obs[j++] = dp.TouchNormal;                   // vector
                obs[j++] = dp.TouchBinormal;                 // vector
                obs[j++] = dp.TouchPos;                      // vector
                obs[j++] = dp.TouchUV;                       // vector
                obs[j++] = (LSL_Integer)dp.TouchFace;        // integer
            }
            return obs;
        }

        /**
         * @brief Load current detect params from a list
         * @param dpList = as returned by xmrEventSaveDets()
         */
        public override void xmrEventLoadDets(LSL_List dpList)
        {
            m_DetectParams = ObjArrToDetPrms(dpList.Data);
        }

        private static DetectParams[] ObjArrToDetPrms(object[] objs)
        {
            int j = 0;
            if((objs.Length % 16 != 1) || (ListInt(objs[j++]) != saveDPVer))
                throw new Exception("invalid detect param format");

            int len = objs.Length / 16;
            DetectParams[] dps = new DetectParams[len];

            for(int i = 0; i < len; i++)
            {
                DetectParams dp = new DetectParams();

                dp.Key = new UUID(ListStr(objs[j++]));
                dp.OffsetPos = (LSL_Vector)objs[j++];
                dp.LinkNum = ListInt(objs[j++]);
                dp.Group = new UUID(ListStr(objs[j++]));
                dp.Name = ListStr(objs[j++]);
                dp.Owner = new UUID(ListStr(objs[j++]));
                dp.Position = (LSL_Vector)objs[j++];
                dp.Rotation = (LSL_Rotation)objs[j++];
                dp.Type = ListInt(objs[j++]);
                dp.Velocity = (LSL_Vector)objs[j++];

                SurfaceTouchEventArgs stea = new SurfaceTouchEventArgs();

                stea.STCoord = LSLVec2OMVec((LSL_Vector)objs[j++]);
                stea.Normal = LSLVec2OMVec((LSL_Vector)objs[j++]);
                stea.Binormal = LSLVec2OMVec((LSL_Vector)objs[j++]);
                stea.Position = LSLVec2OMVec((LSL_Vector)objs[j++]);
                stea.UVCoord = LSLVec2OMVec((LSL_Vector)objs[j++]);
                stea.FaceIndex = ListInt(objs[j++]);

                dp.SurfaceTouchArgs = stea;

                dps[i] = dp;
            }
            return dps;
        }

        /**
         * @brief The script is executing a 'state <newState>;' command.
         * Tell outer layers to cancel any event triggers, like llListen(),
         * then tell outer layers which events the new state has handlers for.
         * We also clear the event queue as per http://wiki.secondlife.com/wiki/State
         * old scripts may want linked messages, but that is not as SL does now
         */
        public override void StateChange()
        {
             // Cancel any llListen()s etc.
             // But llSetTimerEvent() should persist.
            object[] timers = m_XMRLSLApi.acm.TimerPlugin.GetSerializationData(m_ItemID);
            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);
            m_XMRLSLApi.acm.TimerPlugin.CreateFromData(m_LocalID, m_ItemID, UUID.Zero, timers);

             // Tell whoever cares which event handlers the new state has.
            m_Part.SetScriptEvents(m_ItemID, GetStateEventFlags(stateCode));

            // keep link messages
            //ClearQueueExceptLinkMessages();
            // or Clear out all old events from the queue.
            lock(m_QueueLock)
            {
                m_EventQueue.Clear();
                for(int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;
            }
        }
    }

    /**
     * @brief Thrown by things like llResetScript() to unconditionally
     *        unwind as script and reset it to the default state_entry
     *        handler.  We don't want script-level try/catch to intercept
     *        these so scripts can't interfere with the behavior.
     */
    public class ScriptResetException: Exception, IXMRUncatchable
    {
    }

    /**
     * @brief Thrown by things like llDie() to unconditionally unwind as 
     *        script.  We don't want script-level try/catch to intercept
     *        these so scripts can't interfere with the behavior.
     */
    public class ScriptDieException: Exception, IXMRUncatchable
    {
    }
}
