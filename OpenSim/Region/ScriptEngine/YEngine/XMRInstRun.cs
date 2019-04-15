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
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.Framework.Scenes;
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
    public partial class XMRInstance
    {
        /************************************************************************************\
         * This module contains these externally useful methods:                            *
         *   PostEvent() - queues an event to script and wakes script thread to process it  *
         *   RunOne() - runs script for a time slice or until it volunteers to give up cpu  *
         *   CallSEH() - runs in the microthread to call the event handler                  *
        \************************************************************************************/

        /**
         * @brief This can be called in any thread (including the script thread itself)
         *        to queue event to script for processing.
         */
        public void PostEvent(EventParams evt)
        {
            ScriptEventCode evc = (ScriptEventCode)Enum.Parse(typeof(ScriptEventCode), evt.EventName);

             // Put event on end of event queue.
            bool startIt = false;
            bool wakeIt = false;
            lock(m_QueueLock)
            {
                bool construct = (m_IState == XMRInstState.CONSTRUCT);

                 // Ignore event if we don't even have such an handler in any state.
                 // We can't be state-specific here because state might be different
                 // by the time this event is dequeued and delivered to the script.
                if(!construct &&                      // make sure m_HaveEventHandlers is filled in 
                        ((uint)evc < (uint)m_HaveEventHandlers.Length) &&
                        !m_HaveEventHandlers[(int)evc])  // don't bother if we don't have such a handler in any state
                    return;

                // Not running means we ignore any incoming events.
                // But queue if still constructing because m_Running is not yet valid.

                if(!m_Running && !construct)
                {
                    if(m_IState == XMRInstState.SUSPENDED)
                    {
                        if(evc == ScriptEventCode.state_entry && m_EventQueue.Count == 0)
                        {
                            LinkedListNode<EventParams> llns = new LinkedListNode<EventParams>(evt);
                            m_EventQueue.AddFirst(llns);
                        }
                    }
                    return;
                }

                if(m_minEventDelay != 0)
                {
                    switch (evc)
                    {
                        // ignore some events by time set by llMinEventDelay
                        case ScriptEventCode.collision:
                        case ScriptEventCode.land_collision:
                        case ScriptEventCode.listen:
                        case ScriptEventCode.not_at_target:
                        case ScriptEventCode.not_at_rot_target:
                        case ScriptEventCode.no_sensor:
                        case ScriptEventCode.sensor:
                        case ScriptEventCode.timer:
                        case ScriptEventCode.touch:
                        {
                            double now = Util.GetTimeStamp();
                            if (now < m_nextEventTime)
                                return;
                            m_nextEventTime = now + m_minEventDelay;
                            break;
                        }
                        case ScriptEventCode.changed:
                        {
                            const int canignore = ~(CHANGED_SCALE | CHANGED_POSITION);
                            int change = (int)evt.Params[0];
                            if(change == 0) // what?
                                return;
                            if((change & canignore) == 0)
                            {
                                double now = Util.GetTimeStamp();
                                if (now < m_nextEventTime)
                                    return;
                                m_nextEventTime = now + m_minEventDelay;
                            }
                            break;
                        }
                        default:
                            break;
                    }
                }

                 // Only so many of each event type allowed to queue.
                if((uint)evc < (uint)m_EventCounts.Length)
                {
                    if(evc == ScriptEventCode.timer)
                    {
                        if(m_EventCounts[(int)evc] >= 1)
                            return;
                    }
                    else if(m_EventCounts[(int)evc] >= MAXEVENTQUEUE)
                        return;

                    m_EventCounts[(int)evc]++;
                }

                 // Put event on end of instance's event queue.
                LinkedListNode<EventParams> lln = new LinkedListNode<EventParams>(evt);
                switch(evc)
                {
                     // These need to go first.  The only time we manually
                     // queue them is for the default state_entry() and we
                     // need to make sure they go before any attach() events
                     // so the heapLimit value gets properly initialized.
                    case ScriptEventCode.state_entry:
                        m_EventQueue.AddFirst(lln);
                        break;

                     // The attach event sneaks to the front of the queue.
                     // This is needed for quantum limiting to work because
                     // we want the attach(NULL_KEY) event to come in front
                     // of all others so the m_DetachQuantum won't run out
                     // before attach(NULL_KEY) is executed.
                    case ScriptEventCode.attach:
                        if(evt.Params[0].ToString() == UUID.Zero.ToString())
                        {
                            LinkedListNode<EventParams> lln2 = null;
                            for(lln2 = m_EventQueue.First; lln2 != null; lln2 = lln2.Next)
                            {
                                EventParams evt2 = lln2.Value;
                                ScriptEventCode evc2 = (ScriptEventCode)Enum.Parse(typeof(ScriptEventCode), evt2.EventName);
                                if((evc2 != ScriptEventCode.state_entry) && (evc2 != ScriptEventCode.attach))
                                    break;
                            }
                            if(lln2 == null)
                                m_EventQueue.AddLast(lln);
                            else
                                m_EventQueue.AddBefore(lln2, lln);

                             // If we're detaching, limit the qantum. This will also
                             // cause the script to self-suspend after running this
                             // event
                            m_DetachReady.Reset();
                            m_DetachQuantum = 100;
                        }
                        else
                            m_EventQueue.AddLast(lln);

                        break;

                     // All others just go on end in the order queued.
                    default:
                        m_EventQueue.AddLast(lln);
                        break;
                }

                 // If instance is idle (ie, not running or waiting to run),
                 // flag it to be on m_StartQueue as we are about to do so.
                 // Flag it now before unlocking so another thread won't try
                 // to do the same thing right now.
                 // Dont' flag it if it's still suspended!
                if((m_IState == XMRInstState.IDLE) && !m_Suspended)
                {
                    m_IState = XMRInstState.ONSTARTQ;
                    startIt = true;
                }

                 // If instance is sleeping (ie, possibly in xmrEventDequeue),
                 // wake it up if event is in the mask.
                if((m_SleepUntil > DateTime.UtcNow) && !m_Suspended)
                {
                    int evc1 = (int)evc;
                    int evc2 = evc1 - 32;
                    if((((uint)evc1 < (uint)32) && (((m_SleepEventMask1 >> evc1) & 1) != 0)) ||
                            (((uint)evc2 < (uint)32) && (((m_SleepEventMask2 >> evc2) & 1) != 0)))
                        wakeIt = true;
                }
            }

             // If transitioned from IDLE->ONSTARTQ, actually go insert it
             // on m_StartQueue and give the RunScriptThread() a wake-up.
            if(startIt)
                m_Engine.QueueToStart(this);

             // Likewise, if the event mask triggered a wake, wake it up.
            if(wakeIt)
            {
                m_SleepUntil = DateTime.MinValue;
                m_Engine.WakeFromSleep(this);
            }
        }

         // This is called in the script thread to step script until it calls
         // CheckRun().  It returns what the instance's next state should be,
         // ONSLEEPQ, ONYIELDQ, SUSPENDED or FINISHED.
        public XMRInstState RunOne()
        {
            DateTime now = DateTime.UtcNow;
            m_SliceStart = Util.GetTimeStampMS();

             // If script has called llSleep(), don't do any more until time is up.
            m_RunOnePhase = "check m_SleepUntil";
            if(m_SleepUntil > now)
            {
                m_RunOnePhase = "return is sleeping";
                return XMRInstState.ONSLEEPQ;
            }

             // Also, someone may have called Suspend().
            m_RunOnePhase = "check m_SuspendCount";
            if(m_SuspendCount > 0)
            {
                m_RunOnePhase = "return is suspended";
                return XMRInstState.SUSPENDED;
            }

            // Make sure we aren't being migrated in or out and prevent that 
            // whilst we are in here.  If migration has it locked, don't call
            // back right away, delay a bit so we don't get in infinite loop.
            m_RunOnePhase = "lock m_RunLock";
            if(!Monitor.TryEnter(m_RunLock))
            {
                m_SleepUntil = now.AddMilliseconds(15);
                m_RunOnePhase = "return was locked";
                return XMRInstState.ONSLEEPQ;
            }
            try
            {
                m_RunOnePhase = "check entry invariants";
                CheckRunLockInvariants(true);
                Exception e = null;

                 // Maybe it has been Disposed()
                if(m_Part == null)
                {
                    m_RunOnePhase = "runone saw it disposed";
                    return XMRInstState.DISPOSED;
                }

                if(!m_Running)
                {
                    m_RunOnePhase = "return is not running";
                    return XMRInstState.SUSPENDED;
                }

                 // Do some more of the last event if it didn't finish.
                if(this.eventCode != ScriptEventCode.None)
                {
                    lock(m_QueueLock)
                    {
                        if(m_DetachQuantum > 0 && --m_DetachQuantum == 0)
                        {
                            m_Suspended = true;
                            m_DetachReady.Set();
                            m_RunOnePhase = "detach quantum went zero";
                            CheckRunLockInvariants(true);
                            return XMRInstState.FINISHED;
                        }
                    }

                    m_RunOnePhase = "resume old event handler";
                    m_LastRanAt = now;
                    m_InstEHSlice++;
                    callMode = CallMode_NORMAL;
                    e = ResumeEx();
                }

                 // Otherwise, maybe we can dequeue a new event and start 
                 // processing it.
                else
                {
                    m_RunOnePhase = "lock event queue";
                    EventParams evt = null;
                    ScriptEventCode evc = ScriptEventCode.None;

                    lock(m_QueueLock)
                    {

                         // We can't get here unless the script has been resumed
                         // after creation, then suspended again, and then had
                         // an event posted to it. We just pretend there is no
                         // event int he queue and let the normal mechanics
                         // carry out the suspension. A Resume will handle the
                         // restarting gracefully. This is taking the easy way
                         // out and may be improved in the future.

                        if(m_Suspended)
                        {
                            m_RunOnePhase = "m_Suspended is set";
                            CheckRunLockInvariants(true);
                            return XMRInstState.FINISHED;
                        }

                        m_RunOnePhase = "dequeue event";
                        if(m_EventQueue.First != null)
                        {
                            evt = m_EventQueue.First.Value;
                            evc = (ScriptEventCode)Enum.Parse(typeof(ScriptEventCode), evt.EventName);
                            if (m_DetachQuantum > 0)
                            {
                                if(evc != ScriptEventCode.attach)
                                {
                                     // This is the case where the attach event
                                     // has completed and another event is queued
                                     // Stop it from running and suspend
                                    m_Suspended = true;
                                    m_DetachReady.Set();
                                    m_DetachQuantum = 0;
                                    m_RunOnePhase = "nothing to do #3";
                                    CheckRunLockInvariants(true);
                                    return XMRInstState.FINISHED;
                                }
                            }
                            m_EventQueue.RemoveFirst();
                            if((int)evc >= 0)
                                m_EventCounts[(int)evc]--;
                        }

                         // If there is no event to dequeue, don't run this script
                         // until another event gets queued.
                        if(evt == null)
                        {
                            if(m_DetachQuantum > 0)
                            {
                                 // This will happen if the attach event has run
                                 // and exited with time slice left.
                                m_Suspended = true;
                                m_DetachReady.Set();
                                m_DetachQuantum = 0;
                            }
                            m_RunOnePhase = "nothing to do #4";
                            CheckRunLockInvariants(true);
                            return XMRInstState.FINISHED;
                        }
                    }

                     // Dequeued an event, so start it going until it either 
                     // finishes or it calls CheckRun().
                    m_RunOnePhase = "start event handler";
                    m_DetectParams = evt.DetectParams;
                    m_LastRanAt = now;
                    m_InstEHEvent++;
                    e = StartEventHandler(evc, evt.Params);
                }
                m_RunOnePhase = "done running";
                m_CPUTime += DateTime.UtcNow.Subtract(now).TotalMilliseconds;

                 // Maybe it puqued.
                if(e != null)
                {
                    m_RunOnePhase = "handling exception " + e.Message;
                    HandleScriptException(e);
                    m_RunOnePhase = "return had exception " + e.Message;
                    CheckRunLockInvariants(true);
                    return XMRInstState.FINISHED;
                }

                 // If event handler completed, get rid of detect params.
                if(this.eventCode == ScriptEventCode.None)
                    m_DetectParams = null;

            }
            finally
            {
                m_RunOnePhase += "; checking exit invariants and unlocking";
                CheckRunLockInvariants(false);
                Monitor.Exit(m_RunLock);
            }

             // Cycle script through the yield queue and call it back asap.
            m_RunOnePhase = "last return";
            return XMRInstState.ONYIELDQ;
        }

        /**
         * @brief Immediately after taking m_RunLock or just before releasing it, check invariants.
         */
        private ScriptEventCode lastEventCode = ScriptEventCode.None;
        private bool lastActive = false;
        private string lastRunPhase = "";

        public void CheckRunLockInvariants(bool throwIt)
        {
             // If not executing any event handler, there shouldn't be any saved stack frames.
             // If executing an event handler, there should be some saved stack frames.
            bool active = (stackFrames != null);
            ScriptEventCode ec = this.eventCode;
            if(((ec == ScriptEventCode.None) && active) ||
                ((ec != ScriptEventCode.None) && !active))
            {
                m_log.Error("CheckRunLockInvariants: script=" + m_DescName);
                m_log.Error("CheckRunLockInvariants: eventcode=" + ec.ToString() + ", active=" + active.ToString());
                m_log.Error("CheckRunLockInvariants: m_RunOnePhase=" + m_RunOnePhase);
                m_log.Error("CheckRunLockInvariants: lastec=" + lastEventCode + ", lastAct=" + lastActive + ", lastPhase=" + lastRunPhase);
                if(throwIt)
                    throw new Exception("CheckRunLockInvariants: eventcode=" + ec.ToString() + ", active=" + active.ToString());
            }
            lastEventCode = ec;
            lastActive = active;
            lastRunPhase = m_RunOnePhase;
        }

        /*
         * Start event handler.
         *
         * Input:
         *  newEventCode = code of event to be processed
         *  newEhArgs    = arguments for the event handler
         *
         * Caution:
         *  It is up to the caller to make sure ehArgs[] is correct for
         *  the particular event handler being called.  The first thing
         *  a script event handler method does is to unmarshall the args
         *  from ehArgs[] and will throw an array bounds or cast exception 
         *  if it can't.
         */
        private Exception StartEventHandler(ScriptEventCode newEventCode, object[] newEhArgs)
        {
             // We use this.eventCode == ScriptEventCode.None to indicate we are idle.
             // So trying to execute ScriptEventCode.None might make a mess.
            if(newEventCode == ScriptEventCode.None)
                return new Exception("Can't process ScriptEventCode.None");

             // Silly to even try if there is no handler defined for this event.
            if(((int)newEventCode >= 0) && (m_ObjCode.scriptEventHandlerTable[this.stateCode, (int)newEventCode] == null))
                return null;

             // The microthread shouldn't be processing any event code.
             // These are assert checks so we throw them directly as exceptions.
            if(this.eventCode != ScriptEventCode.None)
                throw new Exception("still processing event " + this.eventCode.ToString());

             // Save eventCode so we know what event handler to run in the microthread.
             // And it also marks us busy so we can't be started again and this event lost.
            this.eventCode = newEventCode;
            this.ehArgs = newEhArgs;

             // This calls ScriptUThread.Main() directly, and returns when Main() [indirectly]
             // calls Suspend() or when Main() returns, whichever occurs first.
             // Setting stackFrames = null means run the event handler from the beginning
             // without doing any stack frame restores first.
            this.stackFrames = null;
            return StartEx();
        }

        /**
         * @brief There was an exception whilst starting/running a script event handler.
         *        Maybe we handle it directly or just print an error message.
         */
        private void HandleScriptException(Exception e)
        {
            // The script threw some kind of exception that was not caught at
            // script level, so the script is no longer running an event handler.

            ScriptEventCode curevent = eventCode;
            eventCode = ScriptEventCode.None;
            stackFrames = null;

            if (e is ScriptDeleteException)
            {
                 // Script did something like llRemoveInventory(llGetScriptName());
                 // ... to delete itself from the object.
                m_SleepUntil = DateTime.MaxValue;
                Verbose("[YEngine]: script self-delete {0}", m_ItemID);
                m_Part.Inventory.RemoveInventoryItem(m_ItemID);
            }
            else if(e is ScriptDieException)
            {
                 // Script did an llDie()
                m_RunOnePhase = "dying...";
                m_SleepUntil = DateTime.MaxValue;
                m_Engine.World.DeleteSceneObject(m_Part.ParentGroup, false);
            }
            else if (e is ScriptResetException)
            {
                 // Script did an llResetScript().
                m_RunOnePhase = "resetting...";
                ResetLocked("HandleScriptResetException");
            }
            else if (e is ScriptException)
            {
                // Some general script error.
                SendScriptErrorMessage(e, curevent);
            }
            else
            {
                // Some general script error.
                SendErrorMessage(e);
            }
        }

        private void SendScriptErrorMessage(Exception e, ScriptEventCode ev)
        {
            StringBuilder msg = new StringBuilder();

            msg.Append("YEngine: ");
            if (e.Message != null)
                msg.Append(e.Message);

            msg.Append(" (script: ");
            msg.Append(m_Item.Name);
            msg.Append(" event: ");
            msg.Append(ev.ToString());
            msg.Append(" primID: ");
            msg.Append(m_Part.UUID.ToString());
            msg.Append(" at: <");
            Vector3 pos = m_Part.AbsolutePosition;
            msg.Append((int)Math.Floor(pos.X));
            msg.Append(',');
            msg.Append((int)Math.Floor(pos.Y));
            msg.Append(',');
            msg.Append((int)Math.Floor(pos.Z));
            msg.Append(">) Script must be Reset to re-enable.\n");

            string msgst = msg.ToString();
            if (msgst.Length > 1000)
                msgst = msgst.Substring(0, 1000);

            m_Engine.World.SimChat(Utils.StringToBytes(msgst),
                                                           ChatTypeEnum.DebugChannel, 2147483647,
                                                           m_Part.AbsolutePosition,
                                                           m_Part.Name, m_Part.UUID, false);
            m_log.Debug(string.Format(
                "[SCRIPT ERROR]: {0} (at event {1}, part {2} {3} at {4} in {5}",
                (e.Message == null)? "" : e.Message,
                ev.ToString(),
                m_Part.Name,
                m_Part.UUID,
                m_Part.AbsolutePosition,
                m_Part.ParentGroup.Scene.Name));

            m_SleepUntil = DateTime.MaxValue;
        }

        /**
         * @brief There was an exception running script event handler.
         *        Display error message and disable script (in a way
         *        that the script can be reset to be restarted).
         */
        private void SendErrorMessage(Exception e)
        {
            StringBuilder msg = new StringBuilder();

            msg.Append("[YEngine]: Exception while running ");
            msg.Append(m_ItemID);
            msg.Append('\n');

             // Add exception message.
            string des = e.Message;
            des = (des == null) ? "" : (": " + des);
            msg.Append(e.GetType().Name + des + "\n");

             // Tell script owner what to do.
            msg.Append("Prim: <");
            msg.Append(m_Part.Name);
            msg.Append(">, Script: <");
            msg.Append(m_Item.Name);
            msg.Append(">, Location: ");
            msg.Append(m_Engine.World.RegionInfo.RegionName);
            msg.Append(" <");
            Vector3 pos = m_Part.AbsolutePosition;
            msg.Append((int)Math.Floor(pos.X));
            msg.Append(',');
            msg.Append((int)Math.Floor(pos.Y));
            msg.Append(',');
            msg.Append((int)Math.Floor(pos.Z));
            msg.Append(">\nScript must be Reset to re-enable.\n");

             // Display full exception message in log.
            m_log.Info(msg.ToString() + XMRExceptionStackString(e), e);

             // Give script owner the stack dump.
            msg.Append(XMRExceptionStackString(e));

             // Send error message to owner.
             // Suppress internal code stack trace lines.
            string msgst = msg.ToString();
            if(!msgst.EndsWith("\n"))
                msgst += '\n';
            int j = 0;
            StringBuilder imstr = new StringBuilder();
            for(int i = 0; (i = msgst.IndexOf('\n', i)) >= 0; j = ++i)
            {
                string line = msgst.Substring(j, i - j);
                if(line.StartsWith("at "))
                {
                    if(line.StartsWith("at (wrapper"))
                        continue;  // at (wrapper ...
                    int k = line.LastIndexOf(".cs:");  // ... .cs:linenumber
                    if(Int32.TryParse(line.Substring(k + 4), out k))
                        continue;
                }
                this.llOwnerSay(line);
            }

            // Say script is sleeping for a very long time.
            // Reset() is able to cancel this sleeping.
            m_SleepUntil = DateTime.MaxValue;
        }

        /**
         * @brief The user clicked the Reset Script button.
         *        We want to reset the script to a never-has-ever-run-before state.
         */
        public void Reset()
        {
        checkstate:
            XMRInstState iState = m_IState;
            switch(iState)
            {
                 // If it's really being constructed now, that's about as reset as we get.
                case XMRInstState.CONSTRUCT:
                    return;

                 // If it's idle, that means it is ready to receive a new event.
                 // So we lock the event queue to prevent another thread from taking
                 // it out of idle, verify that it is still in idle then transition
                 // it to resetting so no other thread will touch it.
                case XMRInstState.IDLE:
                    lock(m_QueueLock)
                    {
                        if(m_IState == XMRInstState.IDLE)
                        {
                            m_IState = XMRInstState.RESETTING;
                            break;
                        }
                    }
                    goto checkstate;

                 // If it's on the start queue, that means it is about to dequeue an
                 // event and start processing it.  So we lock the start queue so it
                 // can't be started and transition it to resetting so no other thread
                 // will touch it.
                case XMRInstState.ONSTARTQ:
                    lock(m_Engine.m_StartQueue)
                    {
                        if(m_IState == XMRInstState.ONSTARTQ)
                        {
                            m_Engine.m_StartQueue.Remove(this);
                            m_IState = XMRInstState.RESETTING;
                            break;
                        }
                    }
                    goto checkstate;

                 // If it's running, tell CheckRun() to suspend the thread then go back
                 // to see what it got transitioned to.
                case XMRInstState.RUNNING:
                    suspendOnCheckRunHold = true;
                    lock(m_QueueLock)
                    {
                    }
                    goto checkstate;

                 // If it's sleeping, remove it from sleep queue and transition it to
                 // resetting so no other thread will touch it.
                case XMRInstState.ONSLEEPQ:
                    lock(m_Engine.m_SleepQueue)
                    {
                        if(m_IState == XMRInstState.ONSLEEPQ)
                        {
                            m_Engine.m_SleepQueue.Remove(this);
                            m_IState = XMRInstState.RESETTING;
                            break;
                        }
                    }
                    goto checkstate;

                 // It was just removed from the sleep queue and is about to be put
                 // on the yield queue (ie, is being woken up).
                 // Let that thread complete transition and try again.
                case XMRInstState.REMDFROMSLPQ:
                    Sleep(10);
                    goto checkstate;

                 // If it's yielding, remove it from yield queue and transition it to
                 // resetting so no other thread will touch it.
                case XMRInstState.ONYIELDQ:
                    lock(m_Engine.m_YieldQueue)
                    {
                        if(m_IState == XMRInstState.ONYIELDQ)
                        {
                            m_Engine.m_YieldQueue.Remove(this);
                            m_IState = XMRInstState.RESETTING;
                            break;
                        }
                    }
                    goto checkstate;

                 // If it just finished running something, let that thread transition it
                 // to its next state then check again.
                case XMRInstState.FINISHED:
                    Sleep(10);
                    goto checkstate;

                 // If it's disposed, that's about as reset as it gets.
                case XMRInstState.DISPOSED:
                    return;

                // Some other thread is already resetting it, let it finish.

                case XMRInstState.RESETTING:
                    return;

                case XMRInstState.SUSPENDED:
                    break;

                default:
                    throw new Exception("bad state");
            }

             // This thread transitioned the instance to RESETTING so reset it.
            lock(m_RunLock)
            {
                CheckRunLockInvariants(true);

                // No other thread should have transitioned it from RESETTING.
                if (m_IState != XMRInstState.SUSPENDED)
                {
                    if (m_IState != XMRInstState.RESETTING)
                        throw new Exception("bad state");

                    m_IState = XMRInstState.IDLE;
                }

                // Reset everything and queue up default's start_entry() event.
                ClearQueue();
                ResetLocked("external Reset");

                // Mark it idle now so it can get queued to process new stuff.

                CheckRunLockInvariants(true);
            }
        }

        private void ClearQueueExceptLinkMessages()
        {
            lock(m_QueueLock)
            {
                EventParams[] linkMessages = new EventParams[m_EventQueue.Count];
                int n = 0;
                foreach(EventParams evt2 in m_EventQueue)
                {
                    if(evt2.EventName == "link_message")
                        linkMessages[n++] = evt2;
                }

                m_EventQueue.Clear();
                for(int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;

                for(int i = 0; i < n; i++)
                    m_EventQueue.AddLast(linkMessages[i]);

                m_EventCounts[(int)ScriptEventCode.link_message] = n;
            }
        }

        private void ClearQueue()
        {
            lock(m_QueueLock)
            {
                m_EventQueue.Clear();               // no events queued
                for(int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;
            }
        }

        /**
         * @brief The script called llResetScript() while it was running and
         *        has suspended.  We want to reset the script to a never-has-
         *        ever-run-before state.
         *
         *        Caller must have m_RunLock locked so we know script isn't
         *        running.
         */
        private void ResetLocked(string from)
        {
            m_RunOnePhase = "ResetLocked: releasing controls";
            ReleaseControls();

            m_RunOnePhase = "ResetLocked: removing script";
            m_Part.Inventory.GetInventoryItem(m_ItemID).PermsMask = 0;
            m_Part.Inventory.GetInventoryItem(m_ItemID).PermsGranter = UUID.Zero;
            IUrlModule urlModule = m_Engine.World.RequestModuleInterface<IUrlModule>();
            if(urlModule != null)
                urlModule.ScriptRemoved(m_ItemID);

            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);

            m_RunOnePhase = "ResetLocked: clearing current event";
            this.eventCode = ScriptEventCode.None;  // not processing an event
            m_DetectParams = null;                  // not processing an event
            m_SleepUntil = DateTime.MinValue;     // not doing llSleep()
            m_ResetCount++;                        // has been reset once more

            heapUsed = 0;
            glblVars.Clear();

             // Tell next call to 'default state_entry()' to reset all global
             // vars to their initial values.
            doGblInit = true;

            // Throw away all its stack frames. 
            // If the script is resetting itself, there shouldn't be any stack frames. 
            // If the script is being reset by something else, we throw them away cuz we want to start from the beginning of an event handler. 
            stackFrames = null;

             // Set script to 'default' state and queue call to its 
             // 'state_entry()' event handler.
            m_RunOnePhase = "ResetLocked: posting default:state_entry() event";
            stateCode = 0;
            m_Part.SetScriptEvents(m_ItemID, GetStateEventFlags(0));
            PostEvent(new EventParams("state_entry",
                                      zeroObjectArray,
                                      zeroDetectParams));

             // Tell CheckRun() to let script run.
            suspendOnCheckRunHold = false;
            suspendOnCheckRunTemp = false;
            m_RunOnePhase = "ResetLocked: reset complete";
        }

        private void ReleaseControls()
        {
            if(m_Part != null)
            {
                bool found;
                int permsMask;
                UUID permsGranter;

                try
                {
                    permsGranter = m_Part.TaskInventory[m_ItemID].PermsGranter;
                    permsMask = m_Part.TaskInventory[m_ItemID].PermsMask;
                    found = true;
                }
                catch
                {
                    permsGranter = UUID.Zero;
                    permsMask = 0;
                    found = false;
                }

                if(found && ((permsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0))
                {
                    ScenePresence presence = m_Engine.World.GetScenePresence(permsGranter);
                    if(presence != null)
                        presence.UnRegisterControlEventsToScript(m_LocalID, m_ItemID);
                }
            }
        }

        /**
         * @brief The script code should call this routine whenever it is
         *        convenient to perform a migation or switch microthreads.
         */
        public override void CheckRunWork()
        {
            if(!suspendOnCheckRunHold && !suspendOnCheckRunTemp)
            {
                if(Util.GetTimeStampMS() - m_SliceStart < 60.0)
                    return;
                suspendOnCheckRunTemp = true;
            }
            m_CheckRunPhase = "entered";

             // Stay stuck in this loop as long as something wants us suspended.
            while(suspendOnCheckRunHold || suspendOnCheckRunTemp)
            {
                m_CheckRunPhase = "top of while";
                suspendOnCheckRunTemp = false;

                switch(this.callMode)
                {
                    // Now we are ready to suspend or resume.
                    case CallMode_NORMAL:
                        m_CheckRunPhase = "suspending";
                        callMode = XMRInstance.CallMode_SAVE;
                        stackFrames = null;
                        throw new StackHibernateException(); // does not return

                    // We get here when the script state has been read in by MigrateInEventHandler().
                    // Since the stack is completely restored at this point, any subsequent calls
                    // within the functions should do their normal processing instead of trying to 
                    // restore their state.

                    // the stack has been restored as a result of calling ResumeEx()
                    // tell script code to process calls normally
                    case CallMode_RESTORE:
                        this.callMode = CallMode_NORMAL;
                        break;

                    default:
                        throw new Exception("callMode=" + callMode);
                }

                m_CheckRunPhase = "resumed";
            }

            m_CheckRunPhase = "returning";

             // Upon return from CheckRun() it should always be the case that the script is
             // going to process calls normally, neither saving nor restoring stack frame state.
            if(callMode != CallMode_NORMAL)
                throw new Exception("bad callMode " + callMode);
        }

        /**
         * @brief Allow script to dequeue events.
         */
        public void ResumeIt()
        {
            lock(m_QueueLock)
            {
                m_Suspended = false;
                m_DetachQuantum = 0;
                m_DetachReady.Set();
                if ((m_EventQueue != null) &&
                    (m_EventQueue.First != null) &&
                    (m_IState == XMRInstState.IDLE))
                {
                    m_IState = XMRInstState.ONSTARTQ;
                    m_Engine.QueueToStart(this);
                }
                m_HasRun = true;
            }
        }

        /**
         * @brief Block script from dequeuing events.
         */
        public void SuspendIt()
        {
            lock(m_QueueLock)
            {
                m_Suspended = true;
            }
        }
    }

    /**
     * @brief Thrown by CheckRun() to unwind the script stack, capturing frames to
     *        instance.stackFrames as it unwinds.  We don't want scripts to be able
     *        to intercept this exception as it would block the stack capture
     *        functionality.
     */
    public class StackCaptureException: Exception, IXMRUncatchable
    {
    }
}
