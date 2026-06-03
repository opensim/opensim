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
            m_localsHeapUsed = 0;
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
        }

        private void InitScriptApi(Yengine engine, string api, IScriptApi scriptApi)
        {
            // Set up m_ApiManager_<APINAME> = instance pointer.
            engine.m_XMRInstanceApiCtxFieldInfos[api].SetValue(this, scriptApi);

            // Initialize the API instance.
            scriptApi.Initialize(m_Engine, m_Part, m_Item);
            InitApi(api, scriptApi);
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
            if((i >= len))
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
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                    ByteArrayToSixbitStr(sb, sha.ComputeHash(scbytes));
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
            stackLimit = m_StackSize / 2;

            // This is how many total heap bytes script is allowed to use.
            heapLimit = m_HeapSize;

            // Allocate global variable arrays.
            glblVars.AllocVarArrays(m_ObjCode.glblSizes);

            // Script can handle these event codes.
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
                eventCode = ScriptEventCode.None;  // not processing any event

                // default state_entry() must initialize global variables
                doGblInit = true;
                stateCode = 0;

                PostEvent(EventParams.StateEntryParams);
            }
            else
            {
                try
                {
                    string xml;
                    using (FileStream fs = File.Open(m_StateFileName,
                                          FileMode.Open,
                                          FileAccess.Read))
                    {
                        using(StreamReader ss = new StreamReader(fs))
                            xml = ss.ReadToEnd();
                    }

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);
                    LoadScriptState(doc);
                }
                catch
                {
                    File.Delete(m_StateFileName);

                    eventCode = ScriptEventCode.None;  // not processing any event

                    // default state_entry() must initialize global variables
                    glblVars.AllocVarArrays(m_ObjCode.glblSizes); // reset globals
                    doGblInit = true;
                    stateCode = 0;

                    PostEvent(EventParams.StateEntryParams);
                }
            }

             // Post event(s) saying what caused the script to start.
            if(m_Running)
            {
                if(m_PostOnRez)
                {
                    PostEvent(new EventParams("on_rez",
                              new object[] { m_StartParam },
                              zeroDetectParams));
                }

                switch(m_StateSource)
                {
                    case StateSource.AttachedRez:
                        PostEvent(new EventParams("attach",
                                  new object[] { m_Part.ParentGroup.AttachedAvatar.ToString() }, 
                                  zeroDetectParams));
                        break;

                    case StateSource.PrimCrossing:
                        PostEvent(changedEvent_CR);
                        break;

                    case StateSource.Teleporting:
                        PostEvent(changedEvent_CRT);
                        break;

                    case StateSource.RegionStart:
                        PostEvent(changedEvent_CRS);
                        break;
                }
            }
        }

        private static EventParams changedEvent_CR = new EventParams("changed", new object[] { CHANGED_REGION }, zeroDetectParams);
        private static EventParams changedEvent_CT = new EventParams("changed", new object[] { CHANGED_TELEPORT }, zeroDetectParams);
        private static EventParams changedEvent_CRT = new EventParams("changed", new object[] { CHANGED_REGION | CHANGED_TELEPORT }, zeroDetectParams);
        private static EventParams changedEvent_CRS = new EventParams("changed", new object[] { CHANGED_REGION_START }, zeroDetectParams);

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

            // Everything we know is enclosed in <ScriptState>...</ScriptState>
            XmlElement scriptStateN = (XmlElement)doc.SelectSingleNode("ScriptState");
            if(scriptStateN == null)
                throw new Exception("no <ScriptState> tag");

            XmlElement XvariablesN = null;
            string sen = scriptStateN.GetAttribute("Engine");
            if((sen == null) || (sen != m_Engine.ScriptEngineName))
            {
                XvariablesN = (XmlElement)scriptStateN.SelectSingleNode("Variables");
                if(XvariablesN == null)
                    throw new Exception("<ScriptState> missing Engine=\"YEngine\" attribute");
                processXstate(doc);
                return;
            }

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
            m_Running &= bool.Parse(runningN.InnerText);

            XmlElement doGblInitN = (XmlElement)scriptStateN.SelectSingleNode("DoGblInit");
            doGblInit = bool.Parse(doGblInitN.InnerText);

            if (m_XMRLSLApi != null)
            {
                XmlElement scpttimeN = (XmlElement)scriptStateN.SelectSingleNode("scrpTime");
                if (scpttimeN != null && Double.TryParse(scpttimeN.InnerText, out double t))
                {
                    m_XMRLSLApi.SetLSLTimerMS(Util.GetTimeStampMS() - t);
                }
            }

            double minEventDelay = 0.0;
            XmlElement minEventDelayN = (XmlElement)scriptStateN.SelectSingleNode("mEvtDly");
            if (minEventDelayN != null)
                minEventDelay = Double.Parse(minEventDelayN.InnerText);

            // get values used by stuff like llDetectedGrab, etc.
            DetectParams[] detParams = RestoreDetectParams(scriptStateN.SelectSingleNode("DetectArray"));

            // Restore queued events
            LinkedList<EventParams> eventQueue = RestoreEventQueue(scriptStateN.SelectSingleNode("EventQueue"));

            // Restore timers and listeners
            XmlElement pluginN = (XmlElement)scriptStateN.SelectSingleNode("Plugins");
            Object[] pluginData = ExtractXMLObjectArray(pluginN, "plugin");

            // Script's global variables and stack contents
            XmlElement snapshotN = (XmlElement)scriptStateN.SelectSingleNode("Snapshot");

            Byte[] data = Convert.FromBase64String(snapshotN.InnerText);
            using(MemoryStream ms = new MemoryStream())
            {
                ms.Write(data, 0, data.Length);
                ms.Seek(0, SeekOrigin.Begin);
                MigrateInEventHandler(ms);
            }

            XmlElement localHeapN = (XmlElement)scriptStateN.SelectSingleNode("LHeapUse");
            if (localHeapN != null)
                m_localsHeapUsed = int.Parse(localHeapN.InnerText);
            //XmlElement stkN = (XmlElement)scriptStateN.SelectSingleNode("stkLft");
            //if (stkN != null)
            //    m_StackLeft = int.Parse(stkN.InnerText);

            XmlElement permissionsN = (XmlElement)scriptStateN.SelectSingleNode("Permissions");
            m_Item.PermsGranter = new UUID(permissionsN.GetAttribute("granter"));
            m_Item.PermsMask = Convert.ToInt32(permissionsN.GetAttribute("mask"));
            m_Part.Inventory.UpdateInventoryItem(m_Item, false, false);

            // Restore event queues, preserving any events that queued
            // whilst we were restoring the state
            lock (m_QueueLock)
            {
                m_DetectParams = detParams;
                foreach(EventParams evt in m_EventQueue)
                    eventQueue.AddLast(evt);

                m_EventQueue = eventQueue;
                for(int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;
                foreach(EventParams evt in m_EventQueue)
                {
                    if(m_eventCodeMap.TryGetValue(evt.EventName, out ScriptEventCode eventCode))
                        m_EventCounts[(int)eventCode]++;
                }
            }

            // Requeue timer and listeners (possibly queuing new events)
            AsyncCommandManager.CreateFromData(m_Engine,
                    m_LocalID, m_ItemID, m_Part.UUID,
                    pluginData);

            MinEventDelay = minEventDelay;
        }

        private void processXstate(XmlDocument doc)
        {

            XmlNodeList rootL = doc.GetElementsByTagName("ScriptState");
            if (rootL.Count != 1)
                throw new Exception("Xstate <ScriptState> missing");

            XmlNode rootNode = rootL[0];
            if (rootNode == null)
                throw new Exception("Xstate root missing");

            string stateName = "";
            bool running = false;

            UUID permsGranter = UUID.Zero;
            int permsMask = 0;
            double minEventDelay = 0.0;
            Object[] pluginData = new Object[0];

            LinkedList<EventParams> eventQueue = new LinkedList<EventParams>();

            Dictionary<string, int> intNames = new Dictionary<string, int>();
            Dictionary<string, int> doubleNames = new Dictionary<string, int>();
            Dictionary<string, int> stringNames = new Dictionary<string, int>();
            Dictionary<string, int> vectorNames = new Dictionary<string, int>();
            Dictionary<string, int> rotationNames = new Dictionary<string, int>();
            Dictionary<string, int> listNames = new Dictionary<string, int>();

            int[] ints = null;
            double[] doubles = null;
            string[] strings = null;
            LSL_Vector[] vectors = null;
            LSL_Rotation[] rotations = null;
            LSL_List[] lists = null;

            int nn = m_ObjCode.globalVarNames.Count;
            if (nn > 0)
            {
                Dictionary<int, string> tmpVars;
                if (m_ObjCode.globalVarNames.TryGetValue("iarIntegers", out tmpVars))
                {
                    getvarNames(tmpVars, intNames);
                    ints = new int[tmpVars.Count];
                }
                if (m_ObjCode.globalVarNames.TryGetValue("iarFloats", out tmpVars))
                {
                    getvarNames(tmpVars, doubleNames);
                    doubles = new double[tmpVars.Count];
                }
                if (m_ObjCode.globalVarNames.TryGetValue("iarVectors", out tmpVars))
                {
                    getvarNames(tmpVars, vectorNames);
                    vectors = new LSL_Vector[tmpVars.Count];
                }
                if (m_ObjCode.globalVarNames.TryGetValue("iarRotations", out tmpVars))
                {
                    getvarNames(tmpVars, rotationNames);
                    rotations = new LSL_Rotation[tmpVars.Count];
                }
                if (m_ObjCode.globalVarNames.TryGetValue("iarStrings", out tmpVars))
                {
                    getvarNames(tmpVars, stringNames);
                    strings = new string[tmpVars.Count];
                }
                if (m_ObjCode.globalVarNames.TryGetValue("iarLists", out tmpVars))
                {
                    getvarNames(tmpVars, listNames);
                    lists = new LSL_List[tmpVars.Count];
                }
            }

            int heapsz = 0;

            try
            {
                XmlNodeList partL = rootNode.ChildNodes;
                foreach (XmlNode part in partL)
                {
                    switch (part.Name)
                    {
                        case "State":
                            stateName = part.InnerText;
                            break;
                        case "Running":
                            running = bool.Parse(part.InnerText);
                            break;
                        case "Variables":
                            int indx;
                            XmlNodeList varL = part.ChildNodes;
                            foreach (XmlNode var in varL)
                            {
                                object o = ReadXTypedValue(var, out string varName);
                                if (o is LSL_Integer lio)
                                {
                                    if (intNames.TryGetValue(varName, out indx))
                                        ints[indx] = lio;
                                    continue;
                                }
                                if (o is LSL_Float lfo)
                                {
                                    if (doubleNames.TryGetValue(varName, out indx))
                                        doubles[indx] = lfo;
                                    continue;
                                }
                                if (o is LSL_String lso)
                                {
                                    if (stringNames.TryGetValue(varName, out indx))
                                    {
                                        strings[indx] = lso;
                                        heapsz += lso.Length;
                                    }
                                    continue;
                                }
                                if (o is LSL_Rotation lro)
                                {
                                    if (rotationNames.TryGetValue(varName, out indx))
                                        rotations[indx] = lro;
                                    continue;
                                }
                                if (o is LSL_Vector lvo)
                                {
                                    if (vectorNames.TryGetValue(varName, out indx))
                                        vectors[indx] = lvo;
                                    continue;
                                }
                                if (o is LSL_Key lko)
                                {
                                    if (stringNames.TryGetValue(varName, out indx))
                                    {
                                        strings[indx] = lko;
                                        heapsz += lko.Length;
                                    }
                                    continue;
                                }
                                if (o is UUID uo)
                                {
                                    if (stringNames.TryGetValue(varName, out indx))
                                    {
                                        LSL_String id = uo.ToString();
                                        strings[indx] = id;
                                        heapsz += id.Length;
                                    }
                                    continue;
                                }
                                if (o is LSL_List llo)
                                {
                                    if (listNames.TryGetValue(varName, out indx))
                                    {
                                        lists[indx] = (llo);
                                        heapsz += llo.Size;
                                    }
                                    continue;
                                }
                            }
                            break;
                        case "Queue":
                            XmlNodeList itemL = part.ChildNodes;
                            foreach (XmlNode item in itemL)
                            {
                                List<Object> parms = new List<Object>();
                                List<DetectParams> detected = new List<DetectParams>();

                                string eventName = item.Attributes.GetNamedItem("event").Value;
                                XmlNodeList eventL = item.ChildNodes;
                                foreach (XmlNode evt in eventL)
                                {
                                    switch (evt.Name)
                                    {
                                        case "Params":
                                            XmlNodeList prms = evt.ChildNodes;
                                            foreach (XmlNode pm in prms)
                                                parms.Add(ReadXTypedValue(pm));

                                            break;
                                        case "Detected":
                                            XmlNodeList detL = evt.ChildNodes;
                                            foreach (XmlNode det in detL)
                                            {
                                                string vect = det.Attributes.GetNamedItem("pos").Value;
                                                LSL_Vector v = new LSL_Vector(vect);

                                                int d_linkNum = 0;
                                                UUID d_group = UUID.Zero;
                                                string d_name = String.Empty;
                                                UUID d_owner = UUID.Zero;
                                                LSL_Vector d_position = new LSL_Vector();
                                                LSL_Rotation d_rotation = new LSL_Rotation();
                                                int d_type = 0;
                                                LSL_Vector d_velocity = new LSL_Vector();

                                                try
                                                {
                                                    string tmp;

                                                    tmp = det.Attributes.GetNamedItem("linkNum").Value;
                                                    int.TryParse(tmp, out d_linkNum);

                                                    tmp = det.Attributes.GetNamedItem("group").Value;
                                                    UUID.TryParse(tmp, out d_group);

                                                    d_name = det.Attributes.GetNamedItem("name").Value;

                                                    tmp = det.Attributes.GetNamedItem("owner").Value;
                                                    UUID.TryParse(tmp, out d_owner);

                                                    tmp = det.Attributes.GetNamedItem("position").Value;
                                                    d_position = new LSL_Types.Vector3(tmp);

                                                    tmp = det.Attributes.GetNamedItem("rotation").Value;
                                                    d_rotation = new LSL_Rotation(tmp);

                                                    tmp = det.Attributes.GetNamedItem("type").Value;
                                                    int.TryParse(tmp, out d_type);

                                                    tmp = det.Attributes.GetNamedItem("velocity").Value;
                                                    d_velocity = new LSL_Vector(tmp);
                                                }
                                                catch (Exception) // Old version XML
                                                {
                                                }

                                                UUID uuid = new UUID();
                                                UUID.TryParse(det.InnerText, out uuid);

                                                DetectParams d = new DetectParams();
                                                d.Key = uuid;
                                                d.OffsetPos = v;
                                                d.LinkNum = d_linkNum;
                                                d.Group = d_group;
                                                d.Name = d_name;
                                                d.Owner = d_owner;
                                                d.Position = d_position;
                                                d.Rotation = d_rotation;
                                                d.Type = d_type;
                                                d.Velocity = d_velocity;

                                                detected.Add(d);
                                            }
                                            break;
                                    }
                                }
                                EventParams ep = new EventParams(
                                        eventName, parms.ToArray(),
                                        detected.ToArray());
                                eventQueue.AddLast(ep);
                            }
                            break;
                        case "Plugins":
                            List<Object> olist = new List<Object>();
                            XmlNodeList itemLP = part.ChildNodes;
                            foreach (XmlNode item in itemLP)
                                olist.Add(ReadXTypedValue(item));
                            pluginData = olist.ToArray();
                            break;
                        case "Permissions":
                            string tmpPerm;
                            int mask = 0;
                            tmpPerm = part.Attributes.GetNamedItem("mask").Value;
                            if (tmpPerm != null)
                            {
                                int.TryParse(tmpPerm, out mask);
                                if (mask != 0)
                                {
                                    tmpPerm = part.Attributes.GetNamedItem("granter").Value;
                                    if (tmpPerm != null)
                                    {
                                        UUID granter = new UUID();
                                        UUID.TryParse(tmpPerm, out granter);
                                        if (!granter.IsZero())
                                        {
                                            permsMask = mask;
                                            permsGranter = granter;
                                        }
                                    }
                                }
                            }
                            break;
                        case "MinEventDelay":
                            double.TryParse(part.InnerText, out minEventDelay);
                            break;
                    }
                }
            }
            catch
            {
                throw new Exception("Xstate fail decode");
            }

            int k = 0;
            stateCode = 0;
            foreach (string sn in m_ObjCode.stateNames)
            {
                if (stateName == sn)
                {
                    stateCode = k;
                    break;
                }
                k++;
            }
            eventCode = ScriptEventCode.None;
            m_Running = running;
            doGblInit = false;

            m_Item.PermsGranter = permsGranter;
            m_Item.PermsMask = permsMask;
            m_Part.Inventory.UpdateInventoryItem(m_Item, false, false);

            lock (m_RunLock)
            {
                glblVars.iarIntegers = ints ?? [];
                glblVars.iarFloats = doubles ?? [];
                glblVars.iarStrings = strings ?? [];
                glblVars.iarVectors = vectors ?? [];
                glblVars.iarRotations = rotations ?? [];
                glblVars.iarLists = lists ?? [];
                glblVars.iarChars = [];
                glblVars.iarArrays = [];
                glblVars.iarObjects = [];
                glblVars.iarSDTClObjs = [];
                glblVars.iarSDTIntfObjs = [];
            }

            lock (m_QueueLock)
            {
                m_DetectParams = null;
                foreach (EventParams evt in m_EventQueue)
                    eventQueue.AddLast(evt);

                m_EventQueue = eventQueue;
                for (int i = m_EventCounts.Length; --i >= 0;)
                    m_EventCounts[i] = 0;
                foreach (EventParams evt in m_EventQueue)
                {
                    if(m_eventCodeMap.TryGetValue(evt.EventName, out ScriptEventCode evtCode))
                        m_EventCounts[(int)evtCode]++;
                }
            }

            AsyncCommandManager.CreateFromData(m_Engine,
                     m_LocalID, m_ItemID, m_Part.UUID, pluginData);

            MinEventDelay = minEventDelay;
        }

        private static void getvarNames(Dictionary<int, string> s, Dictionary<string, int> d)
        {
            foreach(KeyValuePair<int, string> kvp in s)
                d[kvp.Value] = kvp.Key;
        }

        private static LSL_Types.list ReadXList(XmlNode parent)
        {
            List<Object> olist = new List<Object>();

            XmlNodeList itemL = parent.ChildNodes;
            foreach (XmlNode item in itemL)
                olist.Add(ReadXTypedValue(item));

            return new LSL_Types.list(olist.ToArray());
        }

        private static object ReadXTypedValue(XmlNode tag, out string name)
        {
            name = tag.Attributes.GetNamedItem("name").Value;

            return ReadXTypedValue(tag);
        }

        private static object ReadXTypedValue(XmlNode tag)
        {
            Object varValue;
            string assembly;

            string itemType = tag.Attributes.GetNamedItem("type").Value;

            if (itemType == "list")
                return ReadXList(tag);

            if (itemType == "OpenMetaverse.UUID")
            {
                UUID.TryParse(tag.InnerText, out UUID val);
                return val;
            }

            Type itemT = Type.GetType(itemType);
            if (itemT == null)
            {
                Object[] args =
                    new Object[] { tag.InnerText };

                assembly = itemType + ", OpenSim.Region.ScriptEngine.Shared";
                itemT = Type.GetType(assembly);
                if (itemT == null)
                    return null;

                varValue = Activator.CreateInstance(itemT, args);

                if (varValue == null)
                    return null;
            }
            else
            {
                varValue = Convert.ChangeType(tag.InnerText, itemT);
            }
            return varValue;
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

                //m_RunOnePhase = "MigrateInEventHandler finished";
                //CheckRunLockInvariants(true);
            }
        }
    }
}
