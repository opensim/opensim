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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using System.IO;
using System.Xml;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Yengine;
using OpenSim.Region.Framework.Scenes;
using log4net;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

// This class exists in the main app domain
//
namespace OpenSim.Region.ScriptEngine.Yengine
{
    public partial class XMRInstance
    {

        // In case Dispose() doesn't get called, we want to be sure to clean
        // up.  This makes sure we decrement m_CompiledScriptRefCount.
        ~XMRInstance()
        {
            Dispose();
        }

        /**
         * @brief Clean up stuff.
         *        We specifically leave m_DescName intact for 'xmr ls' command.
         */
        public void Dispose()
        {
             // Tell script stop executing next time it calls CheckRun().
            suspendOnCheckRunHold = true;

             // Don't send us any more events.
            lock(m_RunLock)
            {
                if(m_Part != null)
                {
                    m_Part.RemoveScriptEvents(m_ItemID);
                    AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);
                    m_Part = null;
                }
            }

             // Let script methods get garbage collected if no one else is using
             // them.
            DecObjCodeRefCount();
        }

        private void DecObjCodeRefCount()
        {
            if(m_ObjCode != null)
            {
                lock(m_CompileLock)
                {
                    ScriptObjCode objCode;

                    if(m_CompiledScriptObjCode.TryGetValue(m_ScriptObjCodeKey, out objCode) &&
                        (objCode == m_ObjCode) &&
                        (--objCode.refCount == 0))
                    {
                        m_CompiledScriptObjCode.Remove(m_ScriptObjCodeKey);
                    }
                }
                m_ObjCode = null;
            }
        }

        public void Verbose(string format, params object[] args)
        {
            if(m_Engine.m_Verbose)
                m_log.DebugFormat(format, args);
        }

        // Called by 'xmr top' console command
        // to dump this script's state to console
        //  Sacha 
        public void RunTestTop()
        {
            if(m_InstEHSlice > 0)
            {
                Console.WriteLine(m_DescName);
                Console.WriteLine("    m_LocalID       = " + m_LocalID);
                Console.WriteLine("    m_ItemID        = " + m_ItemID);
                Console.WriteLine("    m_Item.AssetID  = " + m_Item.AssetID);
                Console.WriteLine("    m_StartParam    = " + m_StartParam);
                Console.WriteLine("    m_PostOnRez     = " + m_PostOnRez);
                Console.WriteLine("    m_StateSource   = " + m_StateSource);
                Console.WriteLine("    m_SuspendCount  = " + m_SuspendCount);
                Console.WriteLine("    m_SleepUntil    = " + m_SleepUntil);
                Console.WriteLine("    m_IState        = " + m_IState.ToString());
                Console.WriteLine("    m_StateCode     = " + GetStateName(stateCode));
                Console.WriteLine("    eventCode       = " + eventCode.ToString());
                Console.WriteLine("    m_LastRanAt     = " + m_LastRanAt.ToString());
                Console.WriteLine("    heapUsed/Limit  = " + xmrHeapUsed() + "/" + heapLimit);
                Console.WriteLine("    m_InstEHEvent   = " + m_InstEHEvent.ToString());
                Console.WriteLine("    m_InstEHSlice   = " + m_InstEHSlice.ToString());
            }
        }

        // Called by 'xmr ls' console command
        // to dump this script's state to console
        public string RunTestLs(bool flagFull)
        {
            if(flagFull)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(m_DescName);
                sb.AppendLine("    m_LocalID            = " + m_LocalID);
                sb.AppendLine("    m_ItemID             = " + m_ItemID + "  (.state file)");
                sb.AppendLine("    m_Item.AssetID       = " + m_Item.AssetID);
                sb.AppendLine("    m_Part.WorldPosition = " + m_Part.GetWorldPosition());
                sb.AppendLine("    m_ScriptObjCodeKey   = " + m_ScriptObjCodeKey + "  (source text)");
                sb.AppendLine("    m_StartParam         = " + m_StartParam);
                sb.AppendLine("    m_PostOnRez          = " + m_PostOnRez);
                sb.AppendLine("    m_StateSource        = " + m_StateSource);
                sb.AppendLine("    m_SuspendCount       = " + m_SuspendCount);
                sb.AppendLine("    m_SleepUntil         = " + m_SleepUntil);
                sb.AppendLine("    m_SleepEvMask1       = 0x" + m_SleepEventMask1.ToString("X"));
                sb.AppendLine("    m_SleepEvMask2       = 0x" + m_SleepEventMask2.ToString("X"));
                sb.AppendLine("    m_IState             = " + m_IState.ToString());
                sb.AppendLine("    m_StateCode          = " + GetStateName(stateCode));
                sb.AppendLine("    eventCode            = " + eventCode.ToString());
                sb.AppendLine("    m_LastRanAt          = " + m_LastRanAt.ToString());
                sb.AppendLine("    m_RunOnePhase        = " + m_RunOnePhase);
                sb.AppendLine("    suspOnCkRunHold      = " + suspendOnCheckRunHold);
                sb.AppendLine("    suspOnCkRunTemp      = " + suspendOnCheckRunTemp);
                sb.AppendLine("    m_CheckRunPhase      = " + m_CheckRunPhase);
                sb.AppendLine("    heapUsed/Limit       = " + xmrHeapUsed() + "/" + heapLimit);
                sb.AppendLine("    m_InstEHEvent        = " + m_InstEHEvent.ToString());
                sb.AppendLine("    m_InstEHSlice        = " + m_InstEHSlice.ToString());
                sb.AppendLine("    m_CPUTime            = " + m_CPUTime);
                sb.AppendLine("    callMode             = " + callMode);
                lock(m_QueueLock)
                {
                    sb.AppendLine("    m_Running            = " + m_Running);
                    foreach(EventParams evt in m_EventQueue)
                    {
                        sb.AppendLine("        evt.EventName        = " + evt.EventName);
                    }
                }
                return sb.ToString();
            }
            else
            {
                return String.Format("{0} {1} {2} {3} {4} {5}",
                        m_ItemID,
                        m_CPUTime.ToString("F3").PadLeft(9),
                        m_InstEHEvent.ToString().PadLeft(9),
                        m_IState.ToString().PadRight(10),
                        m_Part.GetWorldPosition().ToString().PadRight(32),
                        m_DescName);
            }
        }

        /**
         * @brief For a given stateCode, get a mask of the low 32 event codes
         *        that the state has handlers defined for.
         */
        public int GetStateEventFlags(int stateCode)
        {
            if((stateCode < 0) ||
                (stateCode >= m_ObjCode.scriptEventHandlerTable.GetLength(0)))
            {
                return 0;
            }

            int code = 0;
            for(int i = 0; i < 32; i++)
            {
                if(m_ObjCode.scriptEventHandlerTable[stateCode, i] != null)
                {
                    code |= 1 << i;
                }
            }

            return code;
        }

        /**
         * @brief Get the .state file name.
         */
        public static string GetStateFileName(string scriptBasePath, UUID itemID)
        {
            return GetScriptFileName(scriptBasePath, itemID.ToString() + ".state");
        }

        public string GetScriptFileName(string filename)
        {
            return GetScriptFileName(m_ScriptBasePath, filename);
        }

        public string GetScriptILFileName(string filename)
        {
            string path = Path.Combine(m_ScriptBasePath, "DebugIL");
            Directory.CreateDirectory(path);
            return Path.Combine(path, filename);
        }

        public static string GetScriptFileName(string scriptBasePath, string filename)
        {
             // Get old path, ie, all files lumped in a single huge directory.
            string oldPath = Path.Combine(scriptBasePath, filename);

             // Get new path, ie, files split up based on first 2 chars of name.
             //           string subdir = filename.Substring (0, 2);
             //           filename = filename.Substring (2);
            string subdir = filename.Substring(0, 1);
            filename = filename.Substring(1);
            scriptBasePath = Path.Combine(scriptBasePath, subdir);
            Directory.CreateDirectory(scriptBasePath);
            string newPath = Path.Combine(scriptBasePath, filename);

             // If file exists only in old location, move to new location.
             // If file exists in both locations, delete old location.
            if(File.Exists(oldPath))
            {
                if(File.Exists(newPath))
                {
                    File.Delete(oldPath);
                }
                else
                {
                    File.Move(oldPath, newPath);
                }
            }

             // Always return new location.
            return newPath;
        }

        /**
         * @brief Decode state code (int) to state name (string).
         */
        public string GetStateName(int stateCode)
        {
            try
            {
                return m_ObjCode.stateNames[stateCode];
            }
            catch
            {
                return stateCode.ToString();
            }
        }

        /**
         * @brief various gets & sets.
         */
        public int StartParam
        {
            get
            {
                return m_StartParam;
            }
            set
            {
                m_StartParam = value;
            }
        }

        public double MinEventDelay
        {
            get
            {
                return m_minEventDelay;
            }
            set
            {
                if (value > 0.001)
                    m_minEventDelay = value;
                else
                    m_minEventDelay = 0.0;

                m_nextEventTime = 0.0; // reset it
            }
        }


        public SceneObjectPart SceneObject
        {
            get
            {
                return m_Part;
            }
        }

        public DetectParams[] DetectParams
        {
            get
            {
                return m_DetectParams;
            }
            set
            {
                m_DetectParams = value;
            }
        }

        public UUID ItemID
        {
            get
            {
                return m_ItemID;
            }
        }

        public UUID AssetID
        {
            get
            {
                return m_Item.AssetID;
            }
        }

        public bool Running
        {
            get
            {
                return m_Running;
            }
            set
            {
                lock(m_QueueLock)
                {
                    m_Running = value;
                    if(value)
                    {
                        if (m_IState == XMRInstState.SUSPENDED && m_SuspendCount == 0)
                        {
                            if(eventCode != ScriptEventCode.None)
                            {
                                m_IState = XMRInstState.ONYIELDQ;
                                m_Engine.QueueToYield(this);
                            }
                            else if ((m_EventQueue != null) && (m_EventQueue.First != null))
                            {
                                m_IState = XMRInstState.ONSTARTQ;
                                m_Engine.QueueToStart(this);
                            }
                            else
                                m_IState = XMRInstState.IDLE;
                        }
                        else if(m_SuspendCount != 0)
                            m_IState = XMRInstState.IDLE;
                    }
                    else
                    {
                        if(m_IState == XMRInstState.ONSLEEPQ)
                        {
                            m_Engine.RemoveFromSleep(this);
                            m_IState = XMRInstState.SUSPENDED;
                        }
                        EmptyEventQueues();
                    }
                }
            }
        }

        /**
         * @brief Empty out the event queues.
         *        Assumes caller has the m_QueueLock locked.
         */
        public void EmptyEventQueues()
        {
            m_EventQueue.Clear();
            for(int i = m_EventCounts.Length; --i >= 0;)
                m_EventCounts[i] = 0;
        }

        /**
         * @brief Convert an LSL vector to an Openmetaverse vector.
         */
        public static OpenMetaverse.Vector3 LSLVec2OMVec(LSL_Vector lslVec)
        {
            return new OpenMetaverse.Vector3((float)lslVec.x, (float)lslVec.y, (float)lslVec.z);
        }

        /**
         * @brief Extract an integer from an element of an LSL_List.
         */
        public static int ListInt(object element)
        {
            if(element is LSL_Integer)
            {
                return (int)(LSL_Integer)element;
            }
            return (int)element;
        }

        /**
         * @brief Extract a string from an element of an LSL_List.
         */
        public static string ListStr(object element)
        {
            if(element is LSL_String)
            {
                return (string)(LSL_String)element;
            }
            return (string)element;
        }
    }
}
