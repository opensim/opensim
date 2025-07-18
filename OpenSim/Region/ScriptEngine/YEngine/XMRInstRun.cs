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
        public bool PostEvent(EventParams evt)
        {
            if (!m_eventCodeMap.TryGetValue(evt.EventName, out ScriptEventCode evc))
                return false;

            if (!m_HaveEventHandlers[(int)evc]) // don't bother if we don't have such a handler in any state
                return false;

            // Put event on end of event queue.
            bool startIt = false;
            bool wakeIt = false;
            lock(m_QueueLock)
            {
                 // Ignore event if we don't even have such an handler in any state.
                 // We can't be state-specific here because state might be different
                 // by the time this event is dequeued and delivered to the script.
                if(m_IState != XMRInstState.CONSTRUCT)
                {
                    if(!m_Running)
                    {
                        if(m_IState == XMRInstState.SUSPENDED)
                        {
                            if(evc == ScriptEventCode.state_entry && m_EventQueue.Count == 0)
                            {
                                LinkedListNode<EventParams> llns = new(evt);
                                m_EventQueue.AddFirst(llns);
                            }
                        }
                        return true;
                    }
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
                                return false;
                            m_nextEventTime = now + m_minEventDelay;
                            break;
                        }
                        case ScriptEventCode.changed:
                        {
                            const int canignore = ~(CHANGED_SCALE | CHANGED_POSITION);
                            int change = (int)evt.Params[0];
                            if(change == 0) // what?
                                return false;
                            if((change & canignore) == 0)
                            {
                                double now = Util.GetTimeStamp();
                                if (now < m_nextEventTime)
                                    return false;
                                m_nextEventTime = now + m_minEventDelay;
                            }
                            break;
                        }
                        default:
                            break;
                    }
                }

                if(evc == ScriptEventCode.timer)
                {
                    if (m_EventCounts[(int)evc] >= 1)
                        return false;
                    m_EventCounts[(int)evc]++;
                    m_EventQueue.AddLast(new LinkedListNode<EventParams>(evt));
                }
                else
                {
                    if (m_EventCounts[(int)evc] >= MAXEVENTQUEUE)
                        return false;

                    m_EventCounts[(int)evc]++;

                    LinkedListNode<EventParams> lln = new(evt);
                    switch (evc)
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
                            if (evt.Params[0].ToString().Equals(UUID.ZeroString))
                            {
                                LinkedListNode<EventParams> lln2 = null;
                                for(lln2 = m_EventQueue.First; lln2 is not null; lln2 = lln2.Next)
                                {
                                    EventParams evt2 = lln2.Value;
                                    m_eventCodeMap.TryGetValue(evt2.EventName, out ScriptEventCode evc2);
                                    if((evc2 != ScriptEventCode.state_entry) && (evc2 != ScriptEventCode.attach))
                                        break;
                                }
                                if(lln2 is null)
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
            return true;
        }

        public void CancelEvent(string eventName)
        {
            if (!m_eventCodeMap.TryGetValue(eventName, out ScriptEventCode evc))
                return;

            lock (m_QueueLock)
            {
                if(m_EventQueue.Count == 0)
                    return;

                LinkedListNode<EventParams> lln2 = null;
                for (lln2 = m_EventQueue.First; lln2 is not null; lln2 = lln2.Next)
                {
                    EventParams evt2 = lln2.Value;
                    if(evt2.EventName.Equals(eventName))
                    {
                        m_EventQueue.Remove(lln2);
                        if (evc >= 0 && m_EventCounts[(int)evc] > 0)
                            m_EventCounts[(int)evc]--;
                    }
                }
            }
        }

         // This is called in the script thread to step script until it calls
         // CheckRun().  It returns what the instance's next state should be,
         // ONSLEEPQ, ONYIELDQ, SUSPENDED or FINISHED.
        public XMRInstState RunOne()
        {
            // someone may have called Suspend().
            //m_RunOnePhase = "check m_SuspendCount";
            if (m_SuspendCount > 0)
            {
                //m_RunOnePhase = "return is suspended";
                return XMRInstState.SUSPENDED;
            }

            DateTime now = DateTime.UtcNow;
            m_SliceStart = Util.GetTimeStampMS();

             // If script has called llSleep(), don't do any more until time is up.
            //m_RunOnePhase = "check m_SleepUntil";
            if(m_SleepUntil > now)
            {
                //m_RunOnePhase = "return is sleeping";
                return XMRInstState.ONSLEEPQ;
            }

            // Make sure we aren't being migrated in or out and prevent that 
            // whilst we are in here.  If migration has it locked, don't call
            // back right away, delay a bit so we don't get in infinite loop.
            //m_RunOnePhase = "lock m_RunLock";
            if(!Monitor.TryEnter(m_RunLock))
            {
                m_SleepUntil = now.AddMilliseconds(15);
                //m_RunOnePhase = "return was locked";
                return XMRInstState.ONSLEEPQ;
            }

            try
            {
                // Maybe it has been Disposed()
                if (m_Part is null || m_Part.Inventory is null)
                {
                    //m_RunOnePhase = "runone saw it disposed";
                    return XMRInstState.DISPOSED;
                }

                if(!m_Running)
                {
                    //m_RunOnePhase = "return is not running";
                    return XMRInstState.SUSPENDED;
                }

                Exception e = null;

                // Do some more of the last event if it didn't finish.
                if (eventCode != ScriptEventCode.None)
                {
                    lock(m_QueueLock)
                    {
                        if(m_DetachQuantum > 0 && --m_DetachQuantum == 0)
                        {
                            m_Suspended = true;
                            m_DetachReady.Set();
                            //m_RunOnePhase = "detach quantum went zero";
                            return XMRInstState.FINISHED;
                        }
                    }

                    //m_RunOnePhase = "resume old event handler";
                    m_LastRanAt = now;
                    m_InstEHSlice++;
                    callMode = CallMode_NORMAL;
                    e = ResumeEx();
                }

                 // Otherwise, maybe we can dequeue a new event and start 
                 // processing it.
                else
                {
                    //m_RunOnePhase = "lock event queue";
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
                            //m_RunOnePhase = "m_Suspended is set";
                            return XMRInstState.FINISHED;
                        }

                        //m_RunOnePhase = "dequeue event";
                        if(m_EventQueue.First is not null)
                        {
                            evt = m_EventQueue.First.Value;
                            m_eventCodeMap.TryGetValue(evt.EventName, out evc);
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
                                    //m_RunOnePhase = "nothing to do #3";
                                    return XMRInstState.FINISHED;
                                }
                            }
                            m_EventQueue.RemoveFirst();
                            if(evc >= 0)
                                m_EventCounts[(int)evc]--;
                        }

                         // If there is no event to dequeue, don't run this script
                         // until another event gets queued.
                        if(evt is null)
                        {
                            if(m_DetachQuantum > 0)
                            {
                                 // This will happen if the attach event has run
                                 // and exited with time slice left.
                                m_Suspended = true;
                                m_DetachReady.Set();
                                m_DetachQuantum = 0;
                            }
                            //m_RunOnePhase = "nothing to do #4";
                            return XMRInstState.FINISHED;
                        }
                    }

                    // Dequeued an event, so start it going until it either 
                    // finishes or it calls CheckRun().
                    //m_RunOnePhase = "start event handler";

                    m_DetectParams = evt.DetectParams;
                    m_LastRanAt = now;
                    m_InstEHEvent++;
                    e = StartEventHandler(evc, evt.Params);
                }

                //m_RunOnePhase = "done running";
                m_CPUTime +=  Util.GetTimeStampMS() - m_SliceStart;

                // Maybe it puqued.
                if (e is not null)
                {
                    //m_RunOnePhase = "handling exception " + e.Message;
                    HandleScriptException(e);
                    //m_RunOnePhase = "return had exception " + e.Message;
                    return XMRInstState.FINISHED;
                }
            }
            finally
            {
                // If event handler completed, get rid of detect params.
                if (eventCode == ScriptEventCode.None)
                    m_DetectParams = null;

                //m_RunOnePhase += "; checking exit invariants and unlocking";
                Monitor.Exit(m_RunLock);
            }

             // Cycle script through the yield queue and call it back asap.
            //m_RunOnePhase = "last return";
            return XMRInstState.ONYIELDQ;
        }

        /**
         * @brief Immediately after taking m_RunLock or just before releasing it, check invariants.
         */
        public void CheckRunLockInvariants(bool throwIt)
        {
            // If not executing any event handler, there shouldn't be any saved stack frames.
            // If executing an event handler, there should be some saved stack frames.
            if (eventCode == ScriptEventCode.None)
            {
                if (stackFrames is not null)
                {
                    m_log.Error($"CheckRunLockInvariants: script {m_DescName}, eventcode: None, stackFrame not null");
                    if (throwIt)
                        throw new Exception("CheckRunLockInvariants: eventcode=None, stackFrame not null");
                }
            }
            else
            {
                if (stackFrames is null)
                {
                    m_log.Error($"CheckRunLockInvariants: script {m_DescName}, eventcode {eventCode}, stackFrame null");
                    if (throwIt)
                        throw new Exception("CheckRunLockInvariants: eventcode=" + eventCode.ToString() + ", stackFrame null");
                }
            }
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

            // The microthread shouldn't be processing any event code.
            // These are assert checks so we throw them directly as exceptions.
            if (eventCode != ScriptEventCode.None)
                throw new Exception("still processing event " + this.eventCode.ToString());

            // Silly to even try if there is no handler defined for this event.
            if ((newEventCode >= 0) && (m_ObjCode.scriptEventHandlerTable[stateCode, (int)newEventCode] is null))
                return null;

            // Save eventCode so we know what event handler to run in the microthread.
            // And it also marks us busy so we can't be started again and this event lost.
            eventCode = newEventCode;
            ehArgs = newEhArgs;

            // This calls ScriptUThread.Main() directly, and returns when Main() [indirectly]
            // calls Suspend() or when Main() returns, whichever occurs first.
            // Setting stackFrames = null means run the event handler from the beginning
            // without doing any stack frame restores first.
            stackFrames = null;
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
            m_DetectParams = null;

            if (m_Part is null || m_Part.Inventory is null || m_Part.ParentGroup is null)
            {
                //we are gone and don't know it still
                m_SleepUntil = DateTime.MaxValue;
                return;
            }

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
                //m_RunOnePhase = "dying...";
                m_SleepUntil = DateTime.MaxValue;
                m_Engine.World.DeleteSceneObject(m_Part.ParentGroup, false);
            }
            else if (e is ScriptResetException)
            {
                 // Script did an llResetScript().
                //m_RunOnePhase = "resetting...";
                ResetLocked("HandleScriptResetException");
            }
            else if (e is OutOfHeapException)
            {
                // Some general script error.
                SendScriptErrorMessage(e, curevent);
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
            StringBuilder msg = new();
            bool toowner = false;
            msg.Append("YEngine: ");
            string evMessage = null;
            if (e is not null && !string.IsNullOrEmpty(e.Message))
            {
                evMessage = e.Message;
                if (evMessage.StartsWith("(OWNER)"))
                {
                    evMessage = evMessage[7..];
                    toowner = true;
                }
                if (e is OutOfHeapException)
                    evMessage = "OutOfHeap: " + evMessage;
                msg.Append(evMessage);
            }

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
                msgst = msgst[..1000];

            if (toowner)
            {
                ScenePresence sp = m_Engine.World.GetScenePresence(m_Part.OwnerID);
                if (sp != null && !sp.IsNPC)
                    m_Engine.World.SimChatToAgent(m_Part.OwnerID, Utils.StringToBytes(msgst), 0x7FFFFFFF, m_Part.AbsolutePosition,
                                                           m_Part.Name, m_Part.UUID, false);
            }
            else
                m_Engine.World.SimChat(Utils.StringToBytes(msgst),
                                                           ChatTypeEnum.DebugChannel, 0x7FFFFFFF,
                                                           m_Part.AbsolutePosition,
                                                           m_Part.Name, m_Part.UUID, false);
            m_log.Debug(string.Format(
                "[SCRIPT ERROR]: {0} (at event {1}, part {2} {3} at {4} in {5}",
                (string.IsNullOrEmpty(evMessage) ? "" : evMessage),
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
            StringBuilder msg = new();

            msg.Append("[YEngine]: Exception while running ");
            msg.Append(m_ItemID);
            msg.Append('\n');

             // Add exception message.
            string des = e.Message;
            des = (des is null) ? "" : (": " + des);
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
            for(int i = 0; (i = msgst.IndexOf('\n', i)) >= 0; j = ++i)
            {
                string line = msgst[j..i];
                if(line.StartsWith("at "))
                {
                    if(line.StartsWith("at (wrapper"))
                        continue;  // at (wrapper ...
                    int k = line.LastIndexOf(".cs:");  // ... .cs:linenumber
                    if(Int32.TryParse(line.AsSpan(k + 4), out _))
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
                for (int i = 0; i < m_EventCounts.Length; ++i)
                    m_EventCounts[i] = 0;

                for (int i = 0; i < n; i++)
                    m_EventQueue.AddLast(linkMessages[i]);

                m_EventCounts[(int)ScriptEventCode.link_message] = n;
            }
        }

        private void ClearQueue()
        {
            lock(m_QueueLock)
            {
                m_EventQueue.Clear();               // no events queued
                for (int i = 0; i < m_EventCounts.Length; ++i)
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
            //m_RunOnePhase = "ResetLocked: releasing controls";
            ReleaseControlsOrPermissions(true);
            m_Part.CollisionSound = UUID.Zero;

            m_XMRLSLApi?.llResetTime();

            //m_RunOnePhase = "ResetLocked: removing script";
            IUrlModule urlModule = m_Engine.World.RequestModuleInterface<IUrlModule>();
            urlModule?.ScriptRemoved(m_ItemID);

            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);

            //m_RunOnePhase = "ResetLocked: clearing current event";
            eventCode = ScriptEventCode.None;  // not processing an event
            m_DetectParams = null;                  // not processing an event
            m_SleepUntil = DateTime.MinValue;     // not doing llSleep()
            m_ResetCount++;                        // has been reset once more

            m_localsHeapUsed = 0;
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
            //m_RunOnePhase = "ResetLocked: posting default:state_entry() event";
            stateCode = 0;
            m_Part.RemoveScriptTargets(m_ItemID);
            m_Part.SetScriptEvents(m_ItemID, GetStateEventFlags(0));
            PostEvent(EventParams.StateEntryParams);

             // Tell CheckRun() to let script run.
            suspendOnCheckRunHold = false;
            suspendOnCheckRunTemp = false;
            //m_RunOnePhase = "ResetLocked: reset complete";
        }

        private void ReleaseControlsOrPermissions(bool fullPermissions)
        {
            if(m_Part is not null && m_Part.TaskInventory is not null)
            {
                int permsMask;
                UUID permsGranter;
                m_Part.TaskInventory.LockItemsForWrite(true);
                if (!m_Part.TaskInventory.TryGetValue(m_ItemID, out TaskInventoryItem item))
                {
                    m_Part.TaskInventory.LockItemsForWrite(false);
                    return;
                }
                permsGranter = item.PermsGranter;
                permsMask = item.PermsMask;
                if(fullPermissions)
                {
                    item.PermsGranter = UUID.Zero;
                    item.PermsMask = 0;
                }
                else
                    item.PermsMask = permsMask & ~(ScriptBaseClass.PERMISSION_TAKE_CONTROLS | ScriptBaseClass.PERMISSION_CONTROL_CAMERA);
                m_Part.TaskInventory.LockItemsForWrite(false);

                if ((permsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                {
                    ScenePresence presence = m_Engine.World.GetScenePresence(permsGranter);
                    presence?.UnRegisterControlEventsToScript(m_LocalID, m_ItemID);
                }
            }
        }

        /**
         * @brief The script code should call this routine whenever it is
         *        convenient to perform a migation or switch microthreads.
         */
        public override void CheckRunWork()
        {
            if (!suspendOnCheckRunHold && !suspendOnCheckRunTemp)
            {
                if(Util.GetTimeStampMS() - m_SliceStart < 60.0)
                    return;
                suspendOnCheckRunTemp = true;
            }
            //m_CheckRunPhase = "entered";

             // Stay stuck in this loop as long as something wants us suspended.
            while(suspendOnCheckRunHold || suspendOnCheckRunTemp)
            {
                //m_CheckRunPhase = "top of while";
                suspendOnCheckRunTemp = false;

                switch(this.callMode)
                {
                    // Now we are ready to suspend or resume.
                    case CallMode_NORMAL:
                        //m_CheckRunPhase = "suspending";
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
                    {
                        throw new Exception("callMode=" + callMode);
                    }
                }

                //m_CheckRunPhase = "resumed";
            }

            //m_CheckRunPhase = "returning";

             // Upon return from CheckRun() it should always be the case that the script is
             // going to process calls normally, neither saving nor restoring stack frame state.
            if(callMode != CallMode_NORMAL)
            {
                throw new Exception("bad callMode " + callMode);
            }
        }

        /**
         * @brief Allow script to dequeue events.
         */
        public void ResumeIt()
        {
            lock(m_QueueLock)
            {
                m_SuspendCount = 0;
                m_Suspended = false;
                m_DetachQuantum = 0;
                m_DetachReady.Set();
                if ((m_EventQueue is not null) &&
                    (m_EventQueue.First is not null) &&
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
                m_SuspendCount = 1;
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
