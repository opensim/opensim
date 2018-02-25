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

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public partial class XMRInstance
    {
        /****************************************************************************\
         *  The only method of interest to outside this module is the Initializer.  *
         *                                                                          *
         *  The rest of this module contains support routines for the Initializer.  *
        \****************************************************************************/

        /**
         * @brief Initializer, loads script in memory and all ready for running.
         * @param engine = YEngine instance this is part of
         * @param scriptBasePath = directory name where files are
         * @param stackSize = number of bytes to allocate for stacks
         * @param errors = return compiler errors in this array
         * @param forceRecomp = force recompile
         * Throws exception if any error, so it was successful if it returns.
         */
        public void Initialize(Yengine engine, string scriptBasePath,
                               int stackSize, int heapSize, ArrayList errors)
        {
            if(stackSize < 16384)
                stackSize = 16384;
            if(heapSize < 16384)
                heapSize = 16384;

            // Save all call parameters in instance vars for easy access.
            m_Engine = engine;
            m_ScriptBasePath = scriptBasePath;
            m_StackSize = stackSize;
            m_StackLeft = stackSize;
            m_HeapSize = heapSize;
            m_CompilerErrors = errors;
            m_StateFileName = GetStateFileName(scriptBasePath, m_ItemID);

            // Not in any XMRInstQueue.
            m_NextInst = this;
            m_PrevInst = this;

            // Set up list of API calls it has available.
            // This also gets the API modules ready to accept setup data, such as
            // active listeners being restored.
            IScriptApi scriptApi;
            ApiManager am = new ApiManager();
            foreach(string api in am.GetApis())
            {
                // Instantiate the API for this script instance.
                if(api != "LSL")
                    scriptApi = am.CreateApi(api);
                else
                    scriptApi = m_XMRLSLApi = new XMRLSL_Api();

                // Connect it up to the instance.
                InitScriptApi(engine, api, scriptApi);
            }

            m_XMRLSLApi.InitXMRLSLApi(this);

            // Get object loaded, compiling script and reading .state file as
            // necessary to restore the state.
            suspendOnCheckRunHold = true;
            InstantiateScript();
            m_SourceCode = null;
            if(m_ObjCode == null)
                throw new ArgumentNullException("m_ObjCode");
            if(m_ObjCode.scriptEventHandlerTable == null)
                throw new ArgumentNullException("m_ObjCode.scriptEventHandlerTable");

            suspendOnCheckRunHold = false;
            suspendOnCheckRunTemp = false;

            // Declare which events the script's current state can handle.
            int eventMask = GetStateEventFlags(stateCode);
            m_Part.SetScriptEvents(m_ItemID, eventMask);
        }

        private void InitScriptApi(Yengine engine, string api, IScriptApi scriptApi)
        {
            // Set up m_ApiManager_<APINAME> = instance pointer.
            engine.m_XMRInstanceApiCtxFieldInfos[api].SetValue(this, scriptApi);

            // Initialize the API instance.
            scriptApi.Initialize(m_Engine, m_Part, m_Item);
            this.InitApi(api, scriptApi);
        }


        /*
         * Get script object code loaded in memory and all ready to run,
         * ready to resume it from where the .state file says it was last
         */
        private void InstantiateScript()
        {
            bool compiledIt = false;
            ScriptObjCode objCode;

            // If source code string is empty, use the asset ID as the object file name.
            // Allow lines of // comments at the beginning (for such as engine selection).
            int i, j, len;
            if(m_SourceCode == null)
                m_SourceCode = String.Empty;
            for(len = m_SourceCode.Length; len > 0; --len)
            {
                if(m_SourceCode[len - 1] > ' ')
                    break;
            }
            for(i = 0; i < len; i++)
            {
                char c = m_SourceCode[i];
                if(c <= ' ')
                    continue;
                if(c != '/')
                    break;
                if((i + 1 >= len) || (m_SourceCode[i + 1] != '/'))
                    break;
                i = m_SourceCode.IndexOf('\n', i);
                if(i < 0)
                    i = len - 1;
            }
            if((i >= len) || !m_Engine.m_UseSourceHashCode)
            {
                // Source consists of nothing but // comments and whitespace,
                // or we are being forced to use the asset-id as the key, to
                // open an already existing object code file.
                m_ScriptObjCodeKey = m_Item.AssetID.ToString();
                if(i >= len)
                    m_SourceCode = "";
            }
            else
            {
                // Make up dictionary key for the object code.
                // Use the same object code for identical source code
                // regardless of asset ID, so we don't care if they
                // copy scripts or not.
                byte[] scbytes = System.Text.Encoding.UTF8.GetBytes(m_SourceCode);
                StringBuilder sb = new StringBuilder((256 + 5) / 6);
                ByteArrayToSixbitStr(sb, System.Security.Cryptography.SHA256.Create().ComputeHash(scbytes));
                m_ScriptObjCodeKey = sb.ToString();

                // But source code can be just a sixbit string itself
                // that identifies an already existing object code file.
                if(len - i == m_ScriptObjCodeKey.Length)
                {
                    for(j = len; --j >= i;)
                    {
                        if(sixbit.IndexOf(m_SourceCode[j]) < 0)
                            break;
                    }
                    if(j < i)
                    {
                        m_ScriptObjCodeKey = m_SourceCode.Substring(i, len - i);
                        m_SourceCode = "";
                    }
                }
            }

            // There may already be an ScriptObjCode struct in memory that
            // we can use.  If not, try to compile it.
            lock(m_CompileLock)
            {
                if(!m_CompiledScriptObjCode.TryGetValue(m_ScriptObjCodeKey, out objCode) || m_ForceRecomp)
                {
                    objCode = TryToCompile();
                    compiledIt = true;
                }

                // Loaded successfully, increment reference count.
                // If we just compiled it though, reset count to 0 first as
                // this is the one-and-only existance of this objCode struct,
                // and we want any old ones for this source code to be garbage
                // collected.

                if(compiledIt)
                {
                    m_CompiledScriptObjCode[m_ScriptObjCodeKey] = objCode;
                    objCode.refCount = 0;
                }
                objCode.refCount++;

                // Now set up to decrement ref count on dispose.
                m_ObjCode = objCode;
            }

            try
            {

                // Fill in script instance from object code
                // Script instance is put in a "never-ever-has-run-before" state.
                LoadObjCode();

                // Fill in script intial state
                // - either as loaded from a .state file
                // - or initial default state_entry() event
                LoadInitialState();
            }
            catch
            {

                // If any error loading, decrement object code reference count.
                DecObjCodeRefCount();
                throw;
            }
        }

        private const string sixbit = "0123456789_abcdefghijklmnopqrstuvwxyz@ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static void ByteArrayToSixbitStr(StringBuilder sb, byte[] bytes)
        {
            int bit = 0;
            int val = 0;
            foreach(byte b in bytes)
            {
                val |= (int)((uint)b << bit);
                bit += 8;
                while(bit >= 6)
                {
                    sb.Append(sixbit[val & 63]);
                    val >>= 6;
                    bit -= 6;
                }
            }
            if(bit > 0)
                sb.Append(sixbit[val & 63]);
        }

        // Try to create object code from source code
        // If error, just throw exception
        private ScriptObjCode TryToCompile()
        {
            m_CompilerErrors.Clear();

            // If object file exists, create ScriptObjCode directly from that.
            // Otherwise, compile the source to create object file then create
            // ScriptObjCode from that.
            string assetID = m_Item.AssetID.ToString();
            m_CameFrom = "asset://" + assetID;
            ScriptObjCode objCode = Compile();
            if(m_CompilerErrors.Count != 0)
                throw new Exception("compilation errors");

            if(objCode == null)
                throw new Exception("compilation failed");

            return objCode;
        }

        /*
         * Retrieve source from asset server.
         */
        private string FetchSource(string cameFrom)
        {
            m_log.Debug("[YEngine]: fetching source " + cameFrom);
            if(!cameFrom.StartsWith("asset://"))
                throw new Exception("unable to retrieve source from " + cameFrom);

            string assetID = cameFrom.Substring(8);
            AssetBase asset = m_Engine.World.AssetService.Get(assetID);
            if(asset == null)
                throw new Exception("source not found " + cameFrom);

            string source = Encoding.UTF8.GetString(asset.Data);
            if(EmptySource(source))
                throw new Exception("fetched source empty " + cameFrom);

            return source;
        }

        /*
         * Fill in script object initial contents.
         * Set the initial state to "default".
         */
        private void LoadObjCode()
        {
            // Script must leave this much stack remaining on calls to CheckRun().
            this.stackLimit = m_StackSize / 2;

            // This is how many total heap bytes script is allowed to use.
            this.heapLimit = m_HeapSize;

            // Allocate global variable arrays.
            this.glblVars.AllocVarArrays(m_ObjCode.glblSizes);

            // Script can handle these event codes.
            m_HaveEventHandlers = new bool[m_ObjCode.scriptEventHandlerTable.GetLength(1)];
            for(int i = m_ObjCode.scriptEventHandlerTable.GetLength(0); --i >= 0;)
            {
                for(int j = m_ObjCode.scriptEventHandlerTable.GetLength(1); --j >= 0;)
                {
                    if(m_ObjCode.scriptEventHandlerTable[i, j] != null)
                    {
                        m_HaveEventHandlers[j] = true;
                    }
                }
            }
        }

        /*
         *  LoadInitialState()
         *      if no state XML file exists for the asset,
         *          post initial default state events
         *      else
         *          try to restore from .state file
         *  If any error, throw exception
         */
        private void LoadInitialState()
        {
            // If no .state file exists, start from default state
            // Otherwise, read initial state from the .state file

            if(!File.Exists(m_StateFileName))
            {
                m_Running = true;                  // event processing is enabled
                eventCode = ScriptEventCode.None;  // not processing any event

                // default state_entry() must initialize global variables
                doGblInit = true;
                stateCode = 0;

                PostEvent(new EventParams("state_entry",
                                          zeroObjectArray,
                                          zeroDetectParams));
            }
            else
            {
                FileStream fs = File.Open(m_StateFileName,
                                          FileMode.Open,
                                          FileAccess.Read);
                StreamReader ss = new StreamReader(fs);
                string xml = ss.ReadToEnd();
                ss.Close();
                fs.Close();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                LoadScriptState(doc);
            }

             // Post event(s) saying what caused the script to start.
            if(m_PostOnRez)
            {
                PostEvent(new EventParams("on_rez",
                          new Object[] { m_StartParam },
                          zeroDetectParams));
            }

            switch(m_StateSource)
            {
                case StateSource.AttachedRez:
                    //                    PostEvent(new EventParams("attach",
                    //                              new object[] { m_Part.ParentGroup.AttachedAvatar.ToString() }, 
                    //                              zeroDetectParams));
                    break;

                case StateSource.PrimCrossing:
                    PostEvent(new EventParams("changed",
                              sbcCR,
                              zeroDetectParams));
                    break;


                case StateSource.Teleporting:
                    PostEvent(new EventParams("changed",
                              sbcCR,
                              zeroDetectParams));
                    PostEvent(new EventParams("changed",
                              sbcCT,
                              zeroDetectParams));
                    break;

                case StateSource.RegionStart:
                    PostEvent(new EventParams("changed",
                              sbcCRS,
                              zeroDetectParams));
                    break;
            }
        }

        private static Object[] sbcCRS = new Object[] { ScriptBaseClass.CHANGED_REGION_START };
        private static Object[] sbcCR = new Object[] { ScriptBaseClass.CHANGED_REGION };
        private static Object[] sbcCT = new Object[] { ScriptBaseClass.CHANGED_TELEPORT };

        /**
         * @brief Save compilation error messages for later retrieval
         *        via GetScriptErrors().
         */
        private void ErrorHandler(Token token, string message)
        {
            if(token != null)
            {
                string srcloc = token.SrcLoc;
                if(srcloc.StartsWith(m_CameFrom))
                    srcloc = srcloc.Substring(m_CameFrom.Length);

                m_CompilerErrors.Add(srcloc + " Error: " + message);
            }
            else if(message != null)
                m_CompilerErrors.Add("(0,0) Error: " + message);
            else
                m_CompilerErrors.Add("(0,0) Error compiling, see exception in log");
        }

        /**
         * @brief Load script state from the given XML doc into the script memory
         *  <ScriptState Engine="YEngine" Asset=...>
         *      <Running>...</Running>
         *      <DoGblInit>...</DoGblInit>
         *      <Permissions granted=... mask=... />
         *      RestoreDetectParams()
         *      <Plugins>
         *          ExtractXMLObjectArray("plugin")
         *      </Plugins>
         *      <Snapshot>
         *          MigrateInEventHandler()
         *      </Snapshot>
         *  </ScriptState>
         */
        private void LoadScriptState(XmlDocument doc)
        {
            DetectParams[] detParams;
            LinkedList<EventParams> eventQueue;

            // Everything we know is enclosed in <ScriptState>...</ScriptState>
            XmlElement scriptStateN = (XmlElement)doc.SelectSingleNode("ScriptState");
            if(scriptStateN == null)
                throw new Exception("no <ScriptState> tag");

            string sen = scriptStateN.GetAttribute("Engine");
            if((sen == null) || (sen != m_Engine.ScriptEngineName))
                throw new Exception("<ScriptState> missing Engine=\"YEngine\" attribute");

            // AssetID is unique for the script source text so make sure the
            // state file was written for that source file
            string assetID = scriptStateN.GetAttribute("Asset");
            if(assetID != m_Item.AssetID.ToString())
                throw new Exception("<ScriptState> assetID mismatch");

            // Also match the sourceHash in case script was
            // loaded via 'xmroption fetchsource' and has changed
            string sourceHash = scriptStateN.GetAttribute("SourceHash");
            if((sourceHash == null) || (sourceHash != m_ObjCode.sourceHash))
                throw new Exception("<ScriptState> SourceHash mismatch");

            // Get various attributes
            XmlElement runningN = (XmlElement)scriptStateN.SelectSingleNode("Running");
            m_Running = bool.Parse(runningN.InnerText);

            XmlElement doGblInitN = (XmlElement)scriptStateN.SelectSingleNode("DoGblInit");
            doGblInit = bool.Parse(doGblInitN.InnerText);

            XmlElement permissionsN = (XmlElement)scriptStateN.SelectSingleNode("Permissions");
            m_Item.PermsGranter = new UUID(permissionsN.GetAttribute("granter"));
            m_Item.PermsMask = Convert.ToInt32(permissionsN.GetAttribute("mask"));
            m_Part.Inventory.UpdateInventoryItem(m_Item, false, false);

            // get values used by stuff like llDetectedGrab, etc.
            detParams = RestoreDetectParams(scriptStateN.SelectSingleNode("DetectArray"));

            // Restore queued events
            eventQueue = RestoreEventQueue(scriptStateN.SelectSingleNode("EventQueue"));

            // Restore timers and listeners
            XmlElement pluginN = (XmlElement)scriptStateN.SelectSingleNode("Plugins");
            Object[] pluginData = ExtractXMLObjectArray(pluginN, "plugin");

            // Script's global variables and stack contents
            XmlElement snapshotN =
                    (XmlElement)scriptStateN.SelectSingleNode("Snapshot");

            Byte[] data = Convert.FromBase64String(snapshotN.InnerText);
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            MigrateInEventHandler(ms);
            ms.Close();

            // Restore event queues, preserving any events that queued
            // whilst we were restoring the state
            lock(m_QueueLock)
            {
                m_DetectParams = detParams;
                foreach(EventParams evt in m_EventQueue)
                    eventQueue.AddLast(evt);

                m_EventQueue = eventQueue;
                for(int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;
                foreach(EventParams evt in m_EventQueue)
                {
                    ScriptEventCode eventCode = (ScriptEventCode)Enum.Parse(typeof(ScriptEventCode),
                                                                             evt.EventName);
                    m_EventCounts[(int)eventCode]++;
                }
            }

            // Requeue timer and listeners (possibly queuing new events)
            AsyncCommandManager.CreateFromData(m_Engine,
                    m_LocalID, m_ItemID, m_Part.UUID,
                    pluginData);
        }

        /**
         * @brief Read llDetectedGrab, etc, values from XML
         *  <EventQueue>
         *      <DetectParams>...</DetectParams>
         *          .
         *          .
         *          .
         *  </EventQueue>
         */
        private LinkedList<EventParams> RestoreEventQueue(XmlNode eventsN)
        {
            LinkedList<EventParams> eventQueue = new LinkedList<EventParams>();
            if(eventsN != null)
            {
                XmlNodeList eventL = eventsN.SelectNodes("Event");
                foreach(XmlNode evnt in eventL)
                {
                    string name = ((XmlElement)evnt).GetAttribute("Name");
                    object[] parms = ExtractXMLObjectArray(evnt, "param");
                    DetectParams[] detects = RestoreDetectParams(evnt);

                    if(parms == null)
                        parms = zeroObjectArray;
                    if(detects == null)
                        detects = zeroDetectParams;

                    EventParams evt = new EventParams(name, parms, detects);
                    eventQueue.AddLast(evt);
                }
            }
            return eventQueue;
        }

        /**
         * @brief Read llDetectedGrab, etc, values from XML
         *  <DetectArray>
         *      <DetectParams>...</DetectParams>
         *          .
         *          .
         *          .
         *  </DetectArray>
         */
        private DetectParams[] RestoreDetectParams(XmlNode detectedN)
        {
            if(detectedN == null)
                return null;

            List<DetectParams> detected = new List<DetectParams>();
            XmlNodeList detectL = detectedN.SelectNodes("DetectParams");

            DetectParams detprm = new DetectParams();
            foreach(XmlNode detxml in detectL)
            {
                try
                {
                    detprm.Group = new UUID(detxml.Attributes.GetNamedItem("group").Value);
                    detprm.Key = new UUID(detxml.Attributes.GetNamedItem("key").Value);
                    detprm.Owner = new UUID(detxml.Attributes.GetNamedItem("owner").Value);

                    detprm.LinkNum = Int32.Parse(detxml.Attributes.GetNamedItem("linkNum").Value);
                    detprm.Type = Int32.Parse(detxml.Attributes.GetNamedItem("type").Value);

                    detprm.Name = detxml.Attributes.GetNamedItem("name").Value;

                    detprm.OffsetPos = new LSL_Types.Vector3(detxml.Attributes.GetNamedItem("pos").Value);
                    detprm.Position = new LSL_Types.Vector3(detxml.Attributes.GetNamedItem("position").Value);
                    detprm.Velocity = new LSL_Types.Vector3(detxml.Attributes.GetNamedItem("velocity").Value);

                    detprm.Rotation = new LSL_Types.Quaternion(detxml.Attributes.GetNamedItem("rotation").Value);

                    detected.Add(detprm);
                    detprm = new DetectParams();
                }
                catch(Exception e)
                {
                    m_log.Warn("[YEngine]: RestoreDetectParams bad XML: " + detxml.ToString());
                    m_log.Warn("[YEngine]: ... " + e.ToString());
                }
            }

            return detected.ToArray();
        }

        /**
         * @brief Extract elements of an array of objects from an XML parent.
         *        Each element is of form <tag ...>...</tag>
         * @param parent = XML parent to extract them from
         * @param tag = what the value's tag is
         * @returns object array of the values
         */
        private static object[] ExtractXMLObjectArray(XmlNode parent, string tag)
        {
            List<Object> olist = new List<Object>();

            XmlNodeList itemL = parent.SelectNodes(tag);
            foreach(XmlNode item in itemL)
            {
                olist.Add(ExtractXMLObjectValue(item));
            }

            return olist.ToArray();
        }

        private static object ExtractXMLObjectValue(XmlNode item)
        {
            string itemType = item.Attributes.GetNamedItem("type").Value;

            if(itemType == "list")
            {
                return new LSL_List(ExtractXMLObjectArray(item, "item"));
            }

            if(itemType == "OpenMetaverse.UUID")
            {
                UUID val = new UUID();
                UUID.TryParse(item.InnerText, out val);
                return val;
            }

            Type itemT = Type.GetType(itemType);
            if(itemT == null)
            {
                Object[] args = new Object[] { item.InnerText };

                string assembly = itemType + ", OpenSim.Region.ScriptEngine.Shared";
                itemT = Type.GetType(assembly);
                if(itemT == null)
                {
                    return null;
                }
                return Activator.CreateInstance(itemT, args);
            }

            return Convert.ChangeType(item.InnerText, itemT);
        }

        /*
         * Migrate an event handler in from a stream.
         *
         * Input:
         *  stream = as generated by MigrateOutEventHandler()
         */
        private void MigrateInEventHandler(Stream stream)
        {
            int mv = stream.ReadByte();
            if(mv != migrationVersion)
                throw new Exception("incoming migration version " + mv + " but accept only " + migrationVersion);

            stream.ReadByte();  // ignored

            /*
             * Restore script variables and stack and other state from stream.
             * And it also marks us busy (by setting this.eventCode) so we can't be
             * started again and this event lost.  If it restores this.eventCode =
             * None, the the script was idle.
             */
            lock(m_RunLock)
            {
                BinaryReader br = new BinaryReader(stream);
                this.MigrateIn(br);

                m_RunOnePhase = "MigrateInEventHandler finished";
                CheckRunLockInvariants(true);
            }
        }
    }
}
