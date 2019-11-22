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

// based on XMREngine from Mike Rieker (DreamNation), Melanie Thielker and meta7
// but with several changes to be more cross platform.

using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenMetaverse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Timers;
using System.Xml;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

[assembly: Addin("YEngine", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenSim.Region.ScriptEngine.Yengine
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "YEngine")]
    public partial class Yengine: INonSharedRegionModule, IScriptEngine,
            IScriptModule
    {
        public static readonly DetectParams[] zeroDetectParams = new DetectParams[0];
        private static ArrayList noScriptErrors = new ArrayList();
        public static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string[] scriptReferencedAssemblies = new string[0];

        private bool m_LateInit;
        private bool m_TraceCalls;
        public bool m_Verbose;
        public bool m_ScriptDebug;
        public bool m_ScriptDebugSaveSource;
        public bool m_ScriptDebugSaveIL;
        public Scene m_Scene;
        private IConfigSource m_ConfigSource;
        private IConfig m_Config;
        private string m_ScriptBasePath;
        private bool m_Enabled = false;
        public bool m_StartProcessing = false;
        private Dictionary<UUID, ArrayList> m_ScriptErrors =
                new Dictionary<UUID, ArrayList>();
        private Dictionary<UUID, List<UUID>> m_ObjectItemList =
                new Dictionary<UUID, List<UUID>>();
        private Dictionary<UUID, XMRInstance[]> m_ObjectInstArray =
                new Dictionary<UUID, XMRInstance[]>();
        public Dictionary<string, FieldInfo> m_XMRInstanceApiCtxFieldInfos =
                new Dictionary<string, FieldInfo>();
        public int m_StackSize;
        private int m_HeapSize;
        private Thread m_SleepThread = null;

        private bool m_Exiting = false;

        private int m_MaintenanceInterval = 10;
        private System.Timers.Timer m_MaintenanceTimer;
        public int numThreadScriptWorkers;

        private object m_FrameUpdateLock = new object();
        private event ThreadStart m_FrameUpdateList = null;

        // Various instance lists:
        //   m_InstancesDict = all known instances
        //                     find an instance given its itemID
        //   m_StartQueue = instances that have just had event queued to them
        //   m_YieldQueue = instances that are ready to run right now
        //   m_SleepQueue = instances that have m_SleepUntil valid
        //                  sorted by ascending m_SleepUntil
        private Dictionary<UUID, XMRInstance> m_InstancesDict =
                new Dictionary<UUID, XMRInstance>();
        public Queue<ThreadStart> m_ThunkQueue = new Queue<ThreadStart>();
        public XMRInstQueue m_StartQueue = new XMRInstQueue();
        public XMRInstQueue m_YieldQueue = new XMRInstQueue();
        public XMRInstQueue m_SleepQueue = new XMRInstQueue();
        private string m_LockedDict = "nobody";
        private ThreadPriority m_workersPrio;
        public Yengine()
        {
        }

        public string Name
        {
            get
            {
                return "YEngine";
            }
        }

        public Type ReplaceableInterface
        {
            get
            {
                return null;
            }
        }

        public string ScriptEnginePath
        {
            get
            {
                return m_ScriptBasePath;
            }
        }

        public string ScriptClassName
        {
            get
            {
                return "YEngineScript";
            }
        }

        public string ScriptBaseClassName
        {
            get
            {
                return typeof(XMRInstance).FullName;
            }
        }

        public ParameterInfo[] ScriptBaseClassParameters
        {
            get
            {
                return typeof(XMRInstance).GetConstructor(new Type[] { typeof(WaitHandle) }).GetParameters();
            }
        }

        public string[] ScriptReferencedAssemblies
        {
            get
            {
                return scriptReferencedAssemblies;
            }
        }

        public void Initialise(IConfigSource config)
        {
            TraceCalls("[YEngine]: Initialize entry");
            m_ConfigSource = config;

            ////foreach (IConfig icfg in config.Configs) {
            ////    m_log.Debug("[YEngine]: Initialise: configs[" + icfg.Name + "]");
            ////    foreach (string key in icfg.GetKeys ()) {
            ////        m_log.Debug("[YEngine]: Initialise:     " + key + "=" + icfg.GetExpanded (key));
            ////    }
            ////}

            m_Enabled = false;
            m_Config = config.Configs["YEngine"];
            if(m_Config == null)
            {
                m_log.Info("[YEngine]: no config, assuming disabled");
                return;
            }
            m_Enabled = m_Config.GetBoolean("Enabled", false);
            m_log.InfoFormat("[YEngine]: config enabled={0}", m_Enabled);
            if(!m_Enabled)
                return;

            numThreadScriptWorkers = m_Config.GetInt("NumThreadScriptWorkers", 2);
            string priority = m_Config.GetString("Priority", "Normal");
            m_TraceCalls = m_Config.GetBoolean("TraceCalls", false);
            m_Verbose = m_Config.GetBoolean("Verbose", false);
            m_ScriptDebug = m_Config.GetBoolean("ScriptDebug", false);
            m_ScriptDebugSaveSource = m_Config.GetBoolean("ScriptDebugSaveSource", false);
            m_ScriptDebugSaveIL = m_Config.GetBoolean("ScriptDebugSaveIL", false);

            m_StackSize = m_Config.GetInt("ScriptStackSize", 2048) << 10;
            m_HeapSize = m_Config.GetInt("ScriptHeapSize", 1024) << 10;

            // Verify that our ScriptEventCode's match OpenSim's scriptEvent's.
            bool err = false;
            for(int i = 0; i < 32; i++)
            {
                string mycode = "undefined";
                string oscode = "undefined";
                try
                {
                    mycode = ((ScriptEventCode)i).ToString();
                    Convert.ToInt32(mycode);
                    mycode = "undefined";
                }
                catch { }
                try
                {
                    oscode = ((OpenSim.Region.Framework.Scenes.scriptEvents)(1 << i)).ToString();
                    Convert.ToInt32(oscode);
                    oscode = "undefined";
                }
                catch { }
                if(mycode != oscode)
                {
                    m_log.ErrorFormat("[YEngine]: {0} mycode={1}, oscode={2}", i, mycode, oscode);
                    err = true;
                }
            }
            if(err)
            {
                m_Enabled = false;
                return;
            }

            m_workersPrio = ThreadPriority.Normal;
            switch (priority)
            {
                case "Lowest":
                    m_workersPrio = ThreadPriority.Lowest;
                    break;
                case "BelowNormal":
                    m_workersPrio = ThreadPriority.BelowNormal;
                    break;
                case "Normal":
                    m_workersPrio = ThreadPriority.Normal;
                    break;
                case "AboveNormal":
                    m_workersPrio = ThreadPriority.AboveNormal;
                    break;
                case "Highest":
                    m_workersPrio = ThreadPriority.Highest;
                    break;
                default:
                    m_log.ErrorFormat("[YEngine] Invalid thread priority: '{0}'. Assuming Normal", priority);
                    break;
            }


            m_MaintenanceInterval = m_Config.GetInt("MaintenanceInterval", 10);

            if(m_MaintenanceInterval > 0)
            {
                m_MaintenanceTimer = new System.Timers.Timer(m_MaintenanceInterval * 60000);
                m_MaintenanceTimer.Elapsed += DoMaintenance;
                m_MaintenanceTimer.Start();
            }

            MainConsole.Instance.Commands.AddCommand("yeng", false,
                    "yeng",
                    "yeng [...|help|...] ...",
                    "Run YEngine script engine commands",
                    RunTest);

            TraceCalls("[YEngine]: Initialize successful");
        }

        public void AddRegion(Scene scene)
        {
            if(!m_Enabled)
                return;

            TraceCalls("[YEngine]: YEngine.AddRegion({0})", scene.RegionInfo.RegionName);

            m_Scene = scene;

            m_Scene.RegisterModuleInterface<IScriptModule>(this);

            m_ScriptBasePath = m_Config.GetString("ScriptBasePath", "ScriptEngines");
            m_ScriptBasePath = Path.Combine(m_ScriptBasePath, "Yengine");
            m_ScriptBasePath = Path.Combine(m_ScriptBasePath, scene.RegionInfo.RegionID.ToString());

            Directory.CreateDirectory(m_ScriptBasePath);

            string sceneName = m_Scene.Name;

            m_log.InfoFormat("[YEngine]: Enabled for region {0}", sceneName);

            m_log.InfoFormat("[YEngine]: {0}.{1}MB stacksize, {2}.{3}MB heapsize",
                    (m_StackSize >> 20).ToString(),
                    (((m_StackSize % 0x100000) * 1000)
                            >> 20).ToString("D3"),
                    (m_HeapSize >> 20).ToString(),
                    (((m_HeapSize % 0x100000) * 1000)
                            >> 20).ToString("D3"));

            m_SleepThread = StartMyThread(RunSleepThread, "Yengine sleep" + " (" + sceneName + ")", ThreadPriority.Normal);
            for (int i = 0; i < numThreadScriptWorkers; i++)
                StartThreadWorker(i, m_workersPrio, sceneName);

            m_Scene.EventManager.OnRezScript += OnRezScript;

            m_Scene.StackModuleInterface<IScriptModule>(this);
        }

        private void OneTimeLateInitialization()
        {
            // Build list of defined APIs and their 'this' types and define a field in XMRInstanceSuperType.
            ApiManager am = new ApiManager();
            Dictionary<string, Type> apiCtxTypes = new Dictionary<string, Type>();
            foreach(string api in am.GetApis())
            {
                m_log.Debug("[YEngine]: adding api " + api);
                IScriptApi scriptApi = am.CreateApi(api);
                Type apiCtxType = scriptApi.GetType();
                if(api == "LSL")
                    apiCtxType = typeof(XMRLSL_Api);
                apiCtxTypes[api] = apiCtxType;
            }

            if(ScriptCodeGen.xmrInstSuperType == null) // Only create type once!
            {
                // Start creating type XMRInstanceSuperType that contains a field
                // m_ApiManager_<APINAME> that points to the per-instance context
                // struct for that API, ie, the 'this' value passed to all methods
                // in that API.  It is in essence:

                //  public class XMRInstanceSuperType : XMRInstance {
                //      public XMRLSL_Api m_ApiManager_LSL;   // 'this' value for all ll...() functions
                //      public MOD_Api    m_ApiManager_MOD;   // 'this' value for all mod...() functions
                //      public OSSL_Api   m_ApiManager_OSSL;  // 'this' value for all os...() functions
                //              ....
                //  }
                AssemblyName assemblyName = new AssemblyName();
                assemblyName.Name = "XMRInstanceSuperAssembly";
                AssemblyBuilder assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("XMRInstanceSuperModule");
                TypeBuilder typeBuilder = moduleBuilder.DefineType("XMRInstanceSuperType", TypeAttributes.Public | TypeAttributes.Class);
                typeBuilder.SetParent(typeof(XMRInstance));

                foreach(string apiname in apiCtxTypes.Keys)
                {
                    string fieldName = "m_ApiManager_" + apiname;
                    typeBuilder.DefineField(fieldName, apiCtxTypes[apiname], FieldAttributes.Public);
                }

                // Finalize definition of XMRInstanceSuperType.
                // Give the compiler a short name to reference it by,
                // otherwise it will try to use the AssemblyQualifiedName
                // and fail miserably.
                ScriptCodeGen.xmrInstSuperType = typeBuilder.CreateType();
                ScriptObjWriter.DefineInternalType("xmrsuper", ScriptCodeGen.xmrInstSuperType);
            }

            // Tell the compiler about all the constants and methods for each API.
            // We also tell the compiler how to get the per-instance context for each API
            // by reading the corresponding m_ApiManager_<APINAME> field of XMRInstanceSuperType.
            foreach(KeyValuePair<string, Type> kvp in apiCtxTypes)
            {
                // get API name and the corresponding per-instance context type
                string api = kvp.Key;
                Type apiCtxType = kvp.Value;

                // give script compiler an abbreviated name for the API context type
                ScriptObjWriter.DefineInternalType("apimanager_" + api, apiCtxType);

                // this field tells the compiled code where the per-instance API context object is
                // eg, for the OSSL API, it is in ((XMRInstanceSuperType)inst).m_ApiManager_OSSL
                string fieldName = "m_ApiManager_" + api;
                FieldInfo fieldInfo = ScriptCodeGen.xmrInstSuperType.GetField(fieldName);
                m_XMRInstanceApiCtxFieldInfos[api] = fieldInfo;

                // now tell the compiler about the constants and methods for the API
                ScriptConst.AddInterfaceConstants(null, apiCtxType.GetFields());
                TokenDeclInline.AddInterfaceMethods(null, apiCtxType.GetMethods(), fieldInfo);
            }

            // Add sim-specific APIs to the compiler.
            IScriptModuleComms comms = m_Scene.RequestModuleInterface<IScriptModuleComms>();
            if(comms != null)
            {
                // Add methods to list of built-in functions.
                Delegate[] methods = comms.GetScriptInvocationList();
                foreach(Delegate m in methods)
                {
                    MethodInfo mi = m.Method;
                    try
                    {
                        CommsCallCodeGen cccg = new CommsCallCodeGen(mi, comms, m_XMRInstanceApiCtxFieldInfos["MOD"]);
                        Verbose("[YEngine]: added comms function " + cccg.fullName);
                    }
                    catch(Exception e)
                    {
                        m_log.Error("[YEngine]: failed to add comms function " + mi.Name);
                        m_log.Error("[YEngine]: - " + e.ToString());
                    }
                }

                // Add constants to list of built-in constants.
                Dictionary<string, object> consts = comms.GetConstants();
                foreach(KeyValuePair<string, object> kvp in consts)
                {
                    try
                    {
                        ScriptConst sc = ScriptConst.AddConstant(kvp.Key, kvp.Value);
                        Verbose("[YEngine]: added comms constant " + sc.name);
                    }
                    catch(Exception e)
                    {
                        m_log.Error("[YEngine]: failed to add comms constant " + kvp.Key);
                        m_log.Error("[YEngine]: - " + e.Message);
                    }
                }
            }
            else
            {
                Verbose("[YEngine]: comms not enabled");
            }
        }

        /**
         * @brief Generate code for the calls to the comms functions.
         *        It is a tRUlY EvIL interface.
         *        To call the function we must call an XMRInstanceSuperType.m_ApiManager_MOD.modInvoker?()
         *        method passing it the name of the function as a string and the script
         *        argument list wrapped up in an object[] array.  The modInvoker?() methods 
         *        do some sick type conversions (with corresponding mallocs) so we can't 
         *        call the methods directly.
         */
        private class CommsCallCodeGen: TokenDeclInline
        {
            private static Type[] modInvokerArgTypes = new Type[] { typeof(string), typeof(object[]) };
            public static FieldInfo xmrInstModApiCtxField;

            private MethodInfo modInvokerMeth;
            private string methName;

            /**
             * @brief Constructor
             * @param mi = method to make available to scripts
             *        mi.Name = name that is used by scripts
             *        mi.GetParameters() = parameter list as defined by module
             *                             includes the 'UUID host','UUID script' parameters that script does not see
             *                             allowed types for script-visible parameters are as follows:
             *                                    Single -> float
             *                                     Int32 -> integer
             *                        OpenMetaverse.UUID -> key
             *                                  Object[] -> list
             *                  OpenMetaverse.Quaternion -> rotation
             *                                    String -> string
             *                     OpenMetaverse.Vector3 -> vector
             *        mi.ReturnType = return type as defined by module
             *                        types are same as allowed for parameters
             * @param comms = comms module the method came from
             * @param apictxfi = what field in XMRInstanceSuperType the 'this' value is for this method
             */
            public CommsCallCodeGen(MethodInfo mi, IScriptModuleComms comms, FieldInfo apictxfi)
                    : base(null, false, NameArgSig(mi), RetType(mi))
            {
                methName = mi.Name;
                string modInvokerName = comms.LookupModInvocation(methName);
                if(modInvokerName == null)
                    throw new Exception("cannot find comms method " + methName);
                modInvokerMeth = typeof(MOD_Api).GetMethod(modInvokerName, modInvokerArgTypes);
                xmrInstModApiCtxField = apictxfi;
            }

            // script-visible name(argtype,...) signature string
            private static string NameArgSig(MethodInfo mi)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(mi.Name);
                sb.Append('(');
                ParameterInfo[] mps = mi.GetParameters();
                for(int i = 2; i < mps.Length; i++)
                {
                    ParameterInfo pi = mps[i];
                    if(i > 2)
                        sb.Append(',');
                    sb.Append(ParamType(pi.ParameterType));
                }
                sb.Append(')');
                return sb.ToString();
            }

            // script-visible return type
            // note that although we support void, the comms stuff does not
            private static TokenType RetType(MethodInfo mi)
            {
                Type rt = mi.ReturnType;
                if(rt == typeof(float))
                    return new TokenTypeFloat(null);
                if(rt == typeof(int))
                    return new TokenTypeInt(null);
                if(rt == typeof(object[]))
                    return new TokenTypeList(null);
                if(rt == typeof(OpenMetaverse.UUID))
                    return new TokenTypeKey(null);
                if(rt == typeof(OpenMetaverse.Quaternion))
                    return new TokenTypeRot(null);
                if(rt == typeof(string))
                    return new TokenTypeStr(null);
                if(rt == typeof(OpenMetaverse.Vector3))
                    return new TokenTypeVec(null);
                if(rt == null || rt == typeof(void))
                    return new TokenTypeVoid(null);
                throw new Exception("unsupported return type " + rt.Name);
            }

            // script-visible parameter type
            private static string ParamType(Type t)
            {
                if(t == typeof(float))
                    return "float";
                if(t == typeof(int))
                    return "integer";
                if(t == typeof(OpenMetaverse.UUID))
                    return "key";
                if(t == typeof(object[]))
                    return "list";
                if(t == typeof(OpenMetaverse.Quaternion))
                    return "rotation";
                if(t == typeof(string))
                    return "string";
                if(t == typeof(OpenMetaverse.Vector3))
                    return "vector";
                throw new Exception("unsupported parameter type " + t.Name);
            }

            /**
             * @brief Called by the compiler to generate a call to the comms function.
             * @param scg = which script is being compiled
             * @param errorAt = where in the source code the call is being made (for error messages)
             * @param result = a temp location to put the return value in if any
             * @param args = array of script-visible arguments being passed to the function
             */
            public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
            {
                // Set up 'this' pointer for modInvoker?() = value from ApiManager.CreateApi("MOD").
                scg.PushXMRInst();
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, xmrInstModApiCtxField.DeclaringType);
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, xmrInstModApiCtxField);

                // Set up 'fname' argument to modInvoker?() = name of the function to be called.
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, methName);

                // Set up 'parms' argument to modInvoker?() = object[] of the script-visible parameters,
                // in their LSL-wrapped form.  Of course, the modInvoker?() method will malloc yet another 
                // object[] and type-convert these parameters one-by-one with another round of unwrapping 
                // and wrapping.
                // Types allowed in this object[]:
                //    LSL_Float, LSL_Integer, LSL_Key, LSL_List, LSL_Rotation, LSL_String, LSL_Vector
                int nargs = args.Length;
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, nargs);
                scg.ilGen.Emit(errorAt, OpCodes.Newarr, typeof(object));

                for(int i = 0; i < nargs; i++)
                {
                    scg.ilGen.Emit(errorAt, OpCodes.Dup);
                    scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, i);

                    // get location and type of argument
                    CompValu arg = args[i];
                    TokenType argtype = arg.type;

                    // if already in a form acceptable to modInvoker?(),
                    // just push it to the stack and convert to object
                    // by boxing it if necessary

                    // but if something like a double, int, string, etc
                    // push to stack converting to the LSL-wrapped type
                    // then convert to object by boxing if necessary

                    Type boxit = null;
                    if(argtype is TokenTypeLSLFloat)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_Float);
                    }
                    else if(argtype is TokenTypeLSLInt)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_Integer);
                    }
                    else if(argtype is TokenTypeLSLKey)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_Key);
                    }
                    else if(argtype is TokenTypeList)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_List);
                    }
                    else if(argtype is TokenTypeRot)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_Rotation);
                    }
                    else if(argtype is TokenTypeLSLString)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_String);
                    }
                    else if(argtype is TokenTypeVec)
                    {
                        args[i].PushVal(scg, errorAt);
                        boxit = typeof(LSL_Vector);
                    }
                    else if(argtype is TokenTypeFloat)
                    {
                        args[i].PushVal(scg, errorAt, new TokenTypeLSLFloat(argtype));
                        boxit = typeof(LSL_Float);
                    }
                    else if(argtype is TokenTypeInt)
                    {
                        args[i].PushVal(scg, errorAt, new TokenTypeLSLInt(argtype));
                        boxit = typeof(LSL_Integer);
                    }
                    else if(argtype is TokenTypeKey)
                    {
                        args[i].PushVal(scg, errorAt, new TokenTypeLSLKey(argtype));
                        boxit = typeof(LSL_Key);
                    }
                    else if(argtype is TokenTypeStr)
                    {
                        args[i].PushVal(scg, errorAt, new TokenTypeLSLString(argtype));
                        boxit = typeof(LSL_String);
                    }
                    else
                        throw new Exception("unsupported arg type " + argtype.GetType().Name);

                    if(boxit.IsValueType)
                        scg.ilGen.Emit(errorAt, OpCodes.Box, boxit);

                    // pop the object into the object[]
                    scg.ilGen.Emit(errorAt, OpCodes.Stelem, typeof(object));
                }

                // Call the modInvoker?() method.
                // It leaves an LSL-wrapped type on the stack.
                if(modInvokerMeth.IsVirtual)
                    scg.ilGen.Emit(errorAt, OpCodes.Callvirt, modInvokerMeth);
                else
                    scg.ilGen.Emit(errorAt, OpCodes.Call, modInvokerMeth);


                // The 3rd arg to Pop() is the type on the stack, 
                // ie, what modInvoker?() actually returns.
                // The Pop() method will wrap/unwrap as needed.
                Type retSysType = modInvokerMeth.ReturnType;
                if(retSysType == null)
                    retSysType = typeof(void);
                TokenType retTokType = TokenType.FromSysType(errorAt, retSysType);
                result.Pop(scg, errorAt, retTokType);
            }
        }

        /**
         * @brief Called late in shutdown procedure,
         *        after the 'Shutting down..." message.
         */
        public void RemoveRegion(Scene scene)
        {
            if(!m_Enabled)
                return;

            TraceCalls("[YEngine]: YEngine.RemoveRegion({0})", scene.RegionInfo.RegionName);

            if(m_MaintenanceTimer != null)
            {
                m_MaintenanceTimer.Stop();
                m_MaintenanceTimer.Dispose();
            }

            // Write script states out to .state files so it will be
            // available when the region is restarted.
            DoMaintenance(null, null);

            // Stop executing script threads and wait for final
            // one to finish (ie, script gets to CheckRun() call).
            m_Exiting = true;
            if(m_SleepThread != null)
            {
                lock(m_SleepQueue)
                    Monitor.PulseAll(m_SleepQueue);

                if(!m_SleepThread.Join(250))
                    m_SleepThread.Abort();
                m_SleepThread = null;
            }

            StopThreadWorkers();

            m_Scene.EventManager.OnFrame -= OnFrame;
            m_Scene.EventManager.OnRezScript -= OnRezScript;
            m_Scene.EventManager.OnRemoveScript -= OnRemoveScript;
            m_Scene.EventManager.OnScriptReset -= OnScriptReset;
            m_Scene.EventManager.OnStartScript -= OnStartScript;
            m_Scene.EventManager.OnStopScript -= OnStopScript;
            m_Scene.EventManager.OnGetScriptRunning -= OnGetScriptRunning;
            m_Scene.EventManager.OnShutdown -= OnShutdown;

            m_Enabled = false;
            m_Scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if(!m_Enabled)
                return;

            TraceCalls("[YEngine]: YEngine.RegionLoaded({0})", scene.RegionInfo.RegionName);

            m_Scene.EventManager.OnFrame += OnFrame;
            m_Scene.EventManager.OnRemoveScript += OnRemoveScript;
            m_Scene.EventManager.OnScriptReset += OnScriptReset;
            m_Scene.EventManager.OnStartScript += OnStartScript;
            m_Scene.EventManager.OnStopScript += OnStopScript;
            m_Scene.EventManager.OnGetScriptRunning += OnGetScriptRunning;
            m_Scene.EventManager.OnShutdown += OnShutdown;

            InitEvents();
        }

        public void StartProcessing()
        {
            m_log.Debug("[YEngine]: StartProcessing entry");
            m_StartProcessing = true;
            ResumeThreads();
            m_log.Debug("[YEngine]: StartProcessing return");
            m_Scene.EventManager.TriggerEmptyScriptCompileQueue(0, "");
        }

        public void Close()
        {
            TraceCalls("[YEngine]: YEngine.Close()");
        }

        private void RunTest(string module, string[] args)
        {
            if(args.Length < 2)
            {
                m_log.Info("[YEngine]: missing command, try 'yeng help'");
                return;
            }

            m_log.Info("[YEngine]: " + m_Scene.RegionInfo.RegionName);

            switch(args[1])
            {
                case "cvv":
                    m_log.InfoFormat("[YEngine]: compiled version value = {0}",
                                    ScriptCodeGen.COMPILED_VERSION_VALUE);
                    break;

                case "help":
                case "?":
                    m_log.Info("[YEngine]: yeng reset [-help ...]");
                    m_log.Info("[YEngine]: yeng resume - resume script processing");
                    m_log.Info("[YEngine]: yeng suspend - suspend script processing");
                    m_log.Info("[YEngine]: yeng ls [-help ...]");
                    m_log.Info("[YEngine]: yeng cvv - show compiler version value");
                    m_log.Info("[YEngine]: yeng mvv [<newvalue>] - show migration version value");
                    m_log.Info("[YEngine]: yeng tracecalls [yes | no]");
                    m_log.Info("[YEngine]: yeng verbose [yes | no]");
                    m_log.Info("[YEngine]: yeng pev [-help ...] - post event");
                    break;

                case "ls":
                    XmrTestLs(args, 2);
                    break;

                case "mvv":
                    m_log.InfoFormat("[YEngine]: migration version value = {0}",
                        XMRInstance.migrationVersion);
                    break;

                case "pev":
                    XmrTestPev(args, 2);
                    break;

                case "reset":
                    XmrTestReset(args, 2);
                    break;

                case "resume":
                    m_log.Info("[YEngine]: resuming scripts");
                    ResumeThreads();
                    break;

                case "suspend":
                    m_log.Info("[YEngine]: suspending scripts");
                    SuspendThreads();
                    break;

                case "tracecalls":
                    if(args.Length > 2)
                        m_TraceCalls = (args[2][0] & 1) != 0;
                    m_log.Info("[YEngine]: tracecalls " + (m_TraceCalls ? "yes" : "no"));
                    break;

                case "verbose":
                    if(args.Length > 2)
                        m_Verbose = (args[2][0] & 1) != 0;
                    m_log.Info("[YEngine]: verbose " + (m_Verbose ? "yes" : "no"));
                    break;

                default:
                    m_log.Error("[YEngine]: unknown command " + args[1] + ", try 'yeng help'");
                    break;
            }
        }

        // Not required when not using IScriptInstance
        //
        public IScriptWorkItem QueueEventHandler(object parms)
        {
            return null;
        }

        public Scene World
        {
            get
            {
                return m_Scene;
            }
        }

        public IScriptModule ScriptModule
        {
            get
            {
                return this;
            }
        }

        public void SaveAllState()
        {
            m_log.Error("[YEngine]: YEngine.SaveAllState() called!!");
        }

#pragma warning disable 0067
        public event ScriptRemoved OnScriptRemoved;
        public event ObjectRemoved OnObjectRemoved;
#pragma warning restore 0067

        // Events targeted at a specific script
        // ... like listen() for an llListen() call
        //
        public bool PostScriptEvent(UUID itemID, EventParams parms)
        {
            XMRInstance instance = GetInstance(itemID);
            if (instance == null)
                return false;

            TraceCalls("[YEngine]: YEngine.PostScriptEvent({0},{1})", itemID.ToString(), parms.EventName);

            instance.PostEvent(parms);
            return true;
        }

        public void CancelScriptEvent(UUID itemID, string eventName)
        {
            XMRInstance instance = GetInstance(itemID);
            if (instance == null)
                return;

            TraceCalls("[YEngine]: YEngine.CancelScriptEvent({0},{1})", itemID.ToString(), eventName);

            instance.CancelEvent(eventName);
        }

        // Events targeted at all scripts in the given prim.
        //   localID = which prim
        //   parms   = event to post
        //
        public bool PostObjectEvent(uint localID, EventParams parms)
        {
            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);

            if(part == null)
                return false;

            TraceCalls("[YEngine]: YEngine.PostObjectEvent({0},{1})", localID.ToString(), parms.EventName);

            // In SecondLife, attach events go to all scripts of all prims
            // in a linked object.  So here we duplicate that functionality,
            // as all we ever get is a single attach event for the whole
            // object.
            if(parms.EventName == "attach")
            {
                bool posted = false;
                foreach(SceneObjectPart primpart in part.ParentGroup.Parts)
                    posted |= PostPrimEvent(primpart, parms);

                return posted;
            }

            // Other events go to just the scripts in that prim.
            return PostPrimEvent(part, parms);
        }

        private bool PostPrimEvent(SceneObjectPart part, EventParams parms)
        {
            UUID partUUID = part.UUID;

            // Get list of script instances running in the object.
            XMRInstance[] objInstArray;
            lock(m_InstancesDict)
            {
                if(!m_ObjectInstArray.TryGetValue(partUUID, out objInstArray))
                    return false;

                if(objInstArray == null)
                {
                    objInstArray = RebuildObjectInstArray(partUUID);
                    m_ObjectInstArray[partUUID] = objInstArray;
                }
            }

            // Post event to all script instances in the object.
            if(objInstArray.Length <= 0)
                return false;
            foreach(XMRInstance inst in objInstArray)
                inst.PostEvent(parms);

            return true;
        }

        public DetectParams GetDetectParams(UUID itemID, int number)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance == null)
                return null;
            return instance.GetDetectParams(number);
        }

        public void SetMinEventDelay(UUID itemID, double delay)
        {
            XMRInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.MinEventDelay = delay;
        }

        public int GetStartParameter(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if (instance == null)
                return 0;
            return instance.StartParam;
        }

        // This is the "set running" method
        //
        public void SetScriptState(UUID itemID, bool state, bool self)
        {
            SetScriptState(itemID, state);
        }
        public void SetScriptState(UUID itemID, bool state)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.Running = state;
        }

        // Control display of the "running" checkbox
        //
        public bool GetScriptState(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance == null)
                return false;
            return instance.Running;
        }

        public void SetState(UUID itemID, string newState)
        {
            TraceCalls("[YEngine]: YEngine.SetState({0},{1})", itemID.ToString(), newState);
        }

        public void ApiResetScript(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.ApiReset();
        }

        public void ResetScript(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
            {
                IUrlModule urlModule = m_Scene.RequestModuleInterface<IUrlModule>();
                if(urlModule != null)
                    urlModule.ScriptRemoved(itemID);

                instance.Reset();
            }
        }

        public IConfig Config
        {
            get
            {
                return m_Config;
            }
        }

        public IConfigSource ConfigSource
        {
            get
            {
                return m_ConfigSource;
            }
        }

        public string ScriptEngineName
        {
            get
            {
                return "YEngine";
            }
        }

        public IScriptApi GetApi(UUID itemID, string name)
        {
            FieldInfo fi;
            if(!m_XMRInstanceApiCtxFieldInfos.TryGetValue(name, out fi))
                return null;
            XMRInstance inst = GetInstance(itemID);
            if(inst == null)
                return null;
            return (IScriptApi)fi.GetValue(inst);
        }

        /**
         * @brief Get script's current state as an XML string
         *        - called by "Take", "Take Copy" and when object deleted (ie, moved to Trash)
         *        This includes the .state file
         */
        public string GetXMLState(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance == null)
                return String.Empty;

            TraceCalls("[YEngine]: YEngine.GetXMLState({0})", itemID.ToString());

            if(!instance.m_HasRun)
                return String.Empty;

            XmlDocument doc = new XmlDocument();

            /*
             * Set up <State Engine="YEngine" UUID="itemID" Asset="assetID"> tag.
             */
            XmlElement stateN = doc.CreateElement("", "State", "");
            doc.AppendChild(stateN);

            XmlAttribute engineA = doc.CreateAttribute("", "Engine", "");
            engineA.Value = ScriptEngineName;
            stateN.Attributes.Append(engineA);

            XmlAttribute uuidA = doc.CreateAttribute("", "UUID", "");
            uuidA.Value = itemID.ToString();
            stateN.Attributes.Append(uuidA);

            XmlAttribute assetA = doc.CreateAttribute("", "Asset", "");
            string assetID = instance.AssetID.ToString();
            assetA.Value = assetID;
            stateN.Attributes.Append(assetA);

            // Get <ScriptState>...</ScriptState> item that hold's script's state.
            // This suspends the script if necessary then takes a snapshot.
            XmlElement scriptStateN = instance.GetExecutionState(doc);
            stateN.AppendChild(scriptStateN);

            return doc.OuterXml;
        }

        // Set script's current state from an XML string
        // - called just before a script is instantiated
        // So we write the .state file so the .state file will be seen when 
        // the script is instantiated.
        public bool SetXMLState(UUID itemID, string xml)
        {
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.LoadXml(xml);
            }
            catch
            {
                return false;
            }
            TraceCalls("[YEngine]: YEngine.SetXMLState({0})", itemID.ToString());

            // Make sure <State Engine="YEngine"> so we know it is in our
            // format.
            XmlElement stateN = (XmlElement)doc.SelectSingleNode("State");
            if(stateN == null)
                return false;

            bool isX = false;
            string sen = stateN.GetAttribute("Engine");
            if (sen == null)
                return false;
            if (sen != ScriptEngineName)
            {   
                if(sen != "XEngine")
                    return false;
                isX = true;
            }
                // <ScriptState>...</ScriptState> contains contents of .state file.
                XmlElement scriptStateN = (XmlElement)stateN.SelectSingleNode("ScriptState");
            if(scriptStateN == null)
                return false;

            if(!isX)
            {
                sen = stateN.GetAttribute("Engine");
                if ((sen == null) || (sen != ScriptEngineName))
                    return false;
            }

            XmlAttribute assetA = doc.CreateAttribute("", "Asset", "");
            assetA.Value = stateN.GetAttribute("Asset");
            scriptStateN.Attributes.Append(assetA);

            // Write out the .state file with the <ScriptState ...>...</ScriptState> XML text
            string statePath = XMRInstance.GetStateFileName(m_ScriptBasePath, itemID);
            using (FileStream ss = File.Create(statePath))
            {
                using (StreamWriter sw = new StreamWriter(ss))
                    sw.Write(scriptStateN.OuterXml);
            }

            return true;
        }

        public bool PostScriptEvent(UUID itemID, string name, Object[] p)
        {
            if(!m_Enabled)
                return false;

            TraceCalls("[YEngine]: YEngine.PostScriptEvent({0},{1})", itemID.ToString(), name);

            return PostScriptEvent(itemID, new EventParams(name, p, zeroDetectParams));
        }

        public bool PostObjectEvent(UUID itemID, string name, Object[] p)
        {
            if(!m_Enabled)
                return false;

            TraceCalls("[YEngine]: YEngine.PostObjectEvent({0},{1})", itemID.ToString(), name);

            SceneObjectPart part = m_Scene.GetSceneObjectPart(itemID);
            if(part == null)
                return false;

            return PostObjectEvent(part.LocalId, new EventParams(name, p, zeroDetectParams));
        }

        // about the 3523rd entrypoint for a script to put itself to sleep
        public void SleepScript(UUID itemID, int delay)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.Sleep(delay);
        }

        // Get a script instance loaded, compiling it if necessary
        //
        //  localID     = the object as a whole, may contain many scripts
        //  itemID      = this instance of the script in this object
        //  script      = script source code
        //  startParam  = value passed to 'on_rez' event handler
        //  postOnRez   = true to post an 'on_rez' event to script on load
        //  defEngine   = default script engine
        //  stateSource = post this event to script on load

        public void OnRezScript(uint localID, UUID itemID, string script,
                int startParam, bool postOnRez, string defEngine, int stateSource)
        {
            if (script.StartsWith("//MRM:"))
                return;

            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            TaskInventoryItem item = part.Inventory.GetInventoryItem(itemID);

            if(!m_LateInit)
            {
                m_LateInit = true;
                OneTimeLateInitialization();
            }

            TraceCalls("[YEngine]: OnRezScript(...,{0},...)", itemID.ToString());

            // Assume script uses the default engine
            string engineName = defEngine;

            // Very first line might contain // scriptengine : language
            string langsrt = "";
            if (script.StartsWith("//"))
            {
                int lineEnd = script.IndexOf('\n');
                if(lineEnd > 5)
                {
                    string firstline = script.Substring(2, lineEnd - 2).Trim();
                    int colon = firstline.IndexOf(':');
                    if(colon >= 3)
                    {
                        engineName = firstline.Substring(0, colon).TrimEnd();
                        if(string.IsNullOrEmpty(engineName))
                            engineName = defEngine;
                    }
                    if (colon > 0 && colon < firstline.Length - 2)
                    {
                        langsrt = firstline.Substring(colon + 1).Trim();
                        langsrt = langsrt.ToLower();
                    }
                }
            }

            // Make sure the default or requested engine is us.
            if(engineName != ScriptEngineName)
            {
                // Not us, if requested engine exists, silently ignore script and let
                // requested engine handle it.
                IScriptModule[] engines = m_Scene.RequestModuleInterfaces<IScriptModule>();
                foreach(IScriptModule eng in engines)
                {
                    if(eng.ScriptEngineName == engineName)
                        return;
                }

                // Requested engine not defined, warn on console.
                // Then we try to handle it if we're the default engine, else we ignore it.
                //m_log.Warn("[YEngine]: " + itemID.ToString() + " requests undefined/disabled engine " + engineName);
                //m_log.Info("[YEngine]: - " + part.GetWorldPosition());
                //m_log.Info("[YEngine]: first line: " + firstline);
                if(defEngine != ScriptEngineName)
                {
                    //m_log.Info("[YEngine]: leaving it to the default script engine (" + defEngine + ") to process it");
                    return;
                }
                // m_log.Info("[YEngine]: will attempt to processing it anyway as default script engine");

                langsrt = "";
            }

            if(!string.IsNullOrEmpty(langsrt) && langsrt !="lsl")
                return;

            // Put on object/instance lists.
            XMRInstance instance = (XMRInstance)Activator.CreateInstance(ScriptCodeGen.xmrInstSuperType);
            instance.m_LocalID = localID;
            instance.m_ItemID = itemID;
            instance.m_SourceCode = script;
            instance.m_StartParam = startParam;
            instance.m_PostOnRez = postOnRez;
            instance.m_StateSource = (StateSource)stateSource;
            instance.m_Part = part;
            instance.m_PartUUID = part.UUID;
            instance.m_Item = item;
            instance.m_DescName = part.Name + ":" + item.Name;
            instance.m_IState = XMRInstState.CONSTRUCT;

            lock(m_InstancesDict)
            {
                m_LockedDict = "RegisterInstance";

                // Insert on internal list of all scripts being handled by this engine instance.
                m_InstancesDict[instance.m_ItemID] = instance;

                // Insert on internal list of all scripts being handled by this engine instance
                // that are part of the object.
                List<UUID> itemIDList;
                if(!m_ObjectItemList.TryGetValue(instance.m_PartUUID, out itemIDList))
                {
                    itemIDList = new List<UUID>();
                    m_ObjectItemList[instance.m_PartUUID] = itemIDList;
                }
                if(!itemIDList.Contains(instance.m_ItemID))
                {
                    itemIDList.Add(instance.m_ItemID);
                    m_ObjectInstArray[instance.m_PartUUID] = null;
                }

                m_LockedDict = "~RegisterInstance";
            }

            // Compile and load it.
            lock(m_ScriptErrors)
                m_ScriptErrors.Remove(instance.m_ItemID);

            LoadThreadWork(instance);
        }

        /**
         * @brief This routine instantiates one script.
         */
        private void LoadThreadWork(XMRInstance instance)
        {
            // Compile and load the script in memory.
            ArrayList errors = new ArrayList();
            Exception initerr = null;
            try
            {
                instance.Initialize(this, m_ScriptBasePath, m_StackSize, m_HeapSize, errors);
            }
            catch(Exception e1)
            {
                initerr = e1;
            }
            if(initerr != null && !instance.m_ForceRecomp && initerr is CVVMismatchException)
            {
                UUID itemID = instance.m_ItemID;
                Verbose("[YEngine]: {0}/{2} first load failed ({1}), retrying after recompile",
                        itemID.ToString(), initerr.Message, instance.m_Item.AssetID.ToString());
                Verbose("[YEngine]:\n{0}", initerr.ToString());
                initerr = null;
                errors = new ArrayList();
                instance.m_ForceRecomp = true;
                try
                {
                    instance.Initialize(this, m_ScriptBasePath, m_StackSize, m_HeapSize, errors);
                }
                catch(Exception e2)
                {
                    initerr = e2;
                }
            }
            if(initerr != null)
            {
                UUID itemID = instance.m_ItemID;
                Verbose("[YEngine]: Error starting script {0}/{2}: {1}",
                                  itemID.ToString(), initerr.Message, instance.m_Item.AssetID.ToString());
                if(initerr.Message != "compilation errors")
                {
                    Verbose("[YEngine]: - " + instance.m_Part.GetWorldPosition() + " " + instance.m_DescName);
                    Verbose("[YEngine]:   exception:\n{0}", initerr.ToString());
                }

                OnRemoveScript(0, itemID);

                // Post errors where GetScriptErrors() can see them.
                if(errors.Count == 0)
                    errors.Add(initerr.Message);
                else
                {
                    foreach(Object err in errors)
                    {
                        if(m_ScriptDebug)
                            m_log.DebugFormat("[YEngine]:   {0}", err.ToString());
                    }
                }
                lock(m_ScriptErrors)
                    m_ScriptErrors[instance.m_ItemID] = errors;

                return;
            }

            // Tell GetScriptErrors() that we have finished compiling/loading
            // successfully (by posting a 0 element array).
            lock(m_ScriptErrors)
            {
                if(instance.m_IState != XMRInstState.CONSTRUCT)
                    throw new Exception("bad state");
                m_ScriptErrors[instance.m_ItemID] = noScriptErrors;
            }

            // Transition from CONSTRUCT->ONSTARTQ and give to RunScriptThread().
            // Put it on the start queue so it will run any queued event handlers,
            // such as state_entry() or on_rez().  If there aren't any queued, it
            // will just go to idle state when RunOne() tries to dequeue an event.
            lock(instance.m_QueueLock)
            {
                if(instance.m_IState != XMRInstState.CONSTRUCT)
                    throw new Exception("bad state");
                instance.m_IState = XMRInstState.ONSTARTQ;
                if(!instance.m_Running)
                    instance.EmptyEventQueues();
            }
            QueueToStart(instance);
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            TraceCalls("[YEngine]: OnRemoveScript(...,{0})", itemID.ToString());

            // Remove from our list of known scripts.
            // After this, no more events can queue because we won't be
            // able to translate the itemID to an XMRInstance pointer.
            XMRInstance instance = null;
            lock(m_InstancesDict)
            {
                m_LockedDict = "OnRemoveScript:" + itemID.ToString();

                // Tell the instance to free off everything it can.
                if(!m_InstancesDict.TryGetValue(itemID, out instance))
                {
                    m_LockedDict = "~OnRemoveScript";
                    return;
                }

                // Tell it to stop executing anything.
                instance.suspendOnCheckRunHold = true;

                // Remove it from our list of known script instances
                // mostly so no more events can queue to it.
                m_InstancesDict.Remove(itemID);

                List<UUID> itemIDList;
                if(m_ObjectItemList.TryGetValue(instance.m_PartUUID, out itemIDList))
                {
                    itemIDList.Remove(itemID);
                    if(itemIDList.Count == 0)
                    {
                        m_ObjectItemList.Remove(instance.m_PartUUID);
                        m_ObjectInstArray.Remove(instance.m_PartUUID);
                    }
                    else
                        m_ObjectInstArray[instance.m_PartUUID] = null;
                }

                // Delete the .state file as any needed contents were fetched with GetXMLState()
                // and stored on the database server.
                string stateFileName = XMRInstance.GetStateFileName(m_ScriptBasePath, itemID);
                File.Delete(stateFileName);

                ScriptRemoved handlerScriptRemoved = OnScriptRemoved;
                if(handlerScriptRemoved != null)
                    handlerScriptRemoved(itemID);

                m_LockedDict = "~~OnRemoveScript";
            }

            // Free off its stack and fun things like that.
            // If it is running, abort it.
            instance.Dispose();
        }

        public void OnScriptReset(uint localID, UUID itemID)
        {
            TraceCalls("[YEngine]: YEngine.OnScriptReset({0},{1})", localID.ToString(), itemID.ToString());
            ResetScript(itemID);
        }

        public void OnStartScript(uint localID, UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.Running = true;
        }

        public void OnStopScript(uint localID, UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.Running = false;
        }

        public void OnGetScriptRunning(IClientAPI controllingClient,
                UUID objectID, UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
            {
                TraceCalls("[YEngine]: YEngine.OnGetScriptRunning({0},{1})", objectID.ToString(), itemID.ToString());

                IEventQueue eq = World.RequestModuleInterface<IEventQueue>();
                if(eq == null)
                {
                    controllingClient.SendScriptRunningReply(objectID, itemID,
                            instance.Running);
                }
                else
                {
                    eq.ScriptRunningEvent(objectID, itemID, instance.Running, controllingClient.AgentId);
                }
            }
        }

        public bool HasScript(UUID itemID, out bool running)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance == null)
            {
                running = true;
                return false;
            }
            running = instance.Running;
            return true;
        }

        /**
         * @brief Called once per frame update to see if scripts have
         *        any such work to do.
         */
        private void OnFrame()
        {
            if(m_FrameUpdateList != null)
            {
                ThreadStart frameupdates;
                lock(m_FrameUpdateLock)
                {
                    frameupdates = m_FrameUpdateList;
                    m_FrameUpdateList = null;
                }
                frameupdates();
            }
        }

        /**
         * @brief Add a one-shot delegate to list of things to do
         *        synchronized with frame updates.
         */
        public void AddOnFrameUpdate(ThreadStart thunk)
        {
            lock(m_FrameUpdateLock)
                m_FrameUpdateList += thunk;
        }

        /**
         * @brief Gets called early as part of shutdown,
         *        right after "Persisting changed objects" message.
         */
        public void OnShutdown()
        {
            TraceCalls("[YEngine]: YEngine.OnShutdown()");
        }

        /**
         * @brief Queue an instance to the StartQueue so it will run.
         *        This queue is used for instances that have just had
         *        an event queued to them when they were previously
         *        idle.  It must only be called by the thread that
         *        transitioned the thread to XMRInstState.ONSTARTQ so
         *        we don't get two threads trying to queue the same
         *        instance to the m_StartQueue at the same time.
         */
        public void QueueToStart(XMRInstance inst)
        {
            if (inst.m_IState != XMRInstState.ONSTARTQ)
                throw new Exception("bad state");

            lock (m_StartQueue)
                m_StartQueue.InsertTail(inst);

            WakeUpOne();
        }

        public void QueueToYield(XMRInstance inst)
        {
            if (inst.m_IState != XMRInstState.ONYIELDQ)
                throw new Exception("bad state");

            lock (m_YieldQueue)
                m_YieldQueue.InsertTail(inst);

            WakeUpOne();
        }

        public void RemoveFromSleep(XMRInstance inst)
        {
            lock (m_SleepQueue)
            {
                if (inst.m_IState != XMRInstState.ONSLEEPQ)
                    return;

                m_SleepQueue.Remove(inst);
                inst.m_IState = XMRInstState.REMDFROMSLPQ;
            }
        }

        /**
         * @brief A script may be sleeping, in which case we wake it.
         */
        public void WakeFromSleep(XMRInstance inst)
        {
            // Remove from sleep queue unless someone else already woke it.
            lock(m_SleepQueue)
            {
                if(inst.m_IState != XMRInstState.ONSLEEPQ)
                    return;

                m_SleepQueue.Remove(inst);
                inst.m_IState = XMRInstState.REMDFROMSLPQ;
            }

            // Put on end of list of scripts that are ready to run.
            lock(m_YieldQueue)
            {
                inst.m_IState = XMRInstState.ONYIELDQ;
                m_YieldQueue.InsertTail(inst);
            }

            // Make sure the OS thread is running so it will see the script.
            WakeUpOne();
        }

        /**
         * @brief An instance has just finished running for now,
         *        figure out what to do with it next.
         * @param inst = instance in question, not on any queue at the moment
         * @param newIState = its new state
         * @returns with instance inserted onto proper queue (if any)
         */
        public void HandleNewIState(XMRInstance inst, XMRInstState newIState)
        {
            // RunOne() should have left the instance in RUNNING state.
            if(inst.m_IState != XMRInstState.RUNNING)
                throw new Exception("bad state");

            // Now see what RunOne() wants us to do with the instance next.
            switch(newIState)
            {
                // Instance has set m_SleepUntil to when it wants to sleep until.
                // So insert instance in sleep queue by ascending wake time.
                // Then wake the timer thread if this is the new first entry
                // so it will reset its timer.
                case XMRInstState.ONSLEEPQ:
                    lock(m_SleepQueue)
                    {
                        XMRInstance after;

                        inst.m_IState = XMRInstState.ONSLEEPQ;
                        for(after = m_SleepQueue.PeekHead(); after != null; after = after.m_NextInst)
                        {
                            if(after.m_SleepUntil > inst.m_SleepUntil)
                                break;
                        }
                        m_SleepQueue.InsertBefore(inst, after);
                        if(m_SleepQueue.PeekHead() == inst)
                            Monitor.Pulse(m_SleepQueue);
                    }
                    break;

                // Instance just took a long time to run and got wacked by the
                // slicer.  So put on end of yield queue to let someone else
                // run.  If there is no one else, it will run again right away.
                case XMRInstState.ONYIELDQ:
                    lock(m_YieldQueue)
                    {
                        inst.m_IState = XMRInstState.ONYIELDQ;
                        m_YieldQueue.InsertTail(inst);
                    }
                    break;

                // Instance finished executing an event handler.  So if there is
                // another event queued for it, put it on the start queue so it
                // will process the new event.  Otherwise, mark it idle and the
                // next event to queue to it will start it up.
                case XMRInstState.FINISHED:
                    Monitor.Enter(inst.m_QueueLock);
                    if(!inst.m_Suspended && (inst.m_EventQueue.Count > 0))
                    {
                        inst.m_IState = XMRInstState.ONSTARTQ;
                        Monitor.Exit(inst.m_QueueLock);
                        lock(m_StartQueue)
                            m_StartQueue.InsertTail(inst);
                    }
                    else
                    {
                        inst.m_IState = XMRInstState.IDLE;
                        Monitor.Exit(inst.m_QueueLock);
                    }
                    break;

                // Its m_SuspendCount > 0.
                // Don't put it on any queue and it won't run.
                // Since it's not IDLE, even queuing an event won't start it.
                case XMRInstState.SUSPENDED:
                    inst.m_IState = XMRInstState.SUSPENDED;
                    break;

                // It has been disposed of.
                // Just set the new state and all refs should theoretically drop off
                // as the instance is no longer in any list.
                case XMRInstState.DISPOSED:
                    inst.m_IState = XMRInstState.DISPOSED;
                    break;

                // RunOne returned something bad.
                default:
                    throw new Exception("bad new state");
            }
        }

        /**
         * @brief Thread that moves instances from the Sleep queue to the Yield queue.
         */
        private void RunSleepThread()
        {
            double deltaTS;
            int deltaMS;
            XMRInstance inst;

            while(true)
            {
                lock(m_SleepQueue)
                {
                    // Wait here until there is a script on the timer queue that has expired.
                    while(true)
                    {
                        UpdateMyThread();
                        if(m_Exiting)
                        {
                            MyThreadExiting();
                            return;
                        }
                        inst = m_SleepQueue.PeekHead();
                        if(inst == null)
                        {
                            Monitor.Wait(m_SleepQueue, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);
                            continue;
                        }
                        if(inst.m_IState != XMRInstState.ONSLEEPQ)
                            throw new Exception("bad state");
                        deltaTS = (inst.m_SleepUntil - DateTime.UtcNow).TotalMilliseconds;
                        if(deltaTS <= 0.0)
                            break;
                        deltaMS = Int32.MaxValue;
                        if(deltaTS < Int32.MaxValue)
                            deltaMS = (int)deltaTS;
                        if(deltaMS > Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2)
                            deltaMS = Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2;

                        Monitor.Wait(m_SleepQueue, deltaMS);
                    }

                    // Remove the expired entry from the timer queue.
                    m_SleepQueue.RemoveHead();
                    inst.m_IState = XMRInstState.REMDFROMSLPQ;
                }

                // Post the script to the yield queue so it will run and wake a script thread to run it.
                lock(m_YieldQueue)
                {
                    inst.m_IState = XMRInstState.ONYIELDQ;
                    m_YieldQueue.InsertTail(inst);
                }
                WakeUpOne();
            }
        }

        public void Suspend(UUID itemID, int ms)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
                instance.Sleep(ms);
        }

        public void Die(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
            {
                TraceCalls("[YEngine]: YEngine.Die({0})", itemID.ToString());
                instance.Die();
            }
        }

        /**
         * @brief Get specific script instance for which OnRezScript()
         *        has been called for an YEngine script, and that
         *        OnRemoveScript() has not been called since.
         * @param itemID = as passed to OnRezScript() identifying a specific script instance
         * @returns null: not one of our scripts (maybe XEngine etc)
         *          else: points to the script instance
         */
        public XMRInstance GetInstance(UUID itemID)
        {
            XMRInstance instance;
            lock(m_InstancesDict)
            {
                if(!m_InstancesDict.TryGetValue(itemID, out instance))
                    instance = null;
            }
            return instance;
        }

        // Called occasionally to write script state to .state file so the
        // script will restart from its last known state if the region crashes
        // and gets restarted.
        private void DoMaintenance(object source, ElapsedEventArgs e)
        {
            XMRInstance[] instanceArray;

            lock(m_InstancesDict)
                instanceArray = System.Linq.Enumerable.ToArray(m_InstancesDict.Values);

            foreach(XMRInstance ins in instanceArray)
            {
                // Don't save attachments
                if(ins.m_Part.ParentGroup.IsAttachment)
                    continue;
                ins.GetExecutionState(new XmlDocument());
            }
        }

        /**
         * @brief Retrieve errors generated by a previous call to OnRezScript().
         *        We are guaranteed this routine will not be called before the
         *        corresponding OnRezScript() has returned.  It blocks until the
         *        compile has completed.
         */
        public ArrayList GetScriptErrors(UUID itemID)
        {
            ArrayList errors;

            lock(m_ScriptErrors)
            {
                while(!m_ScriptErrors.TryGetValue(itemID, out errors))
                {
                    Monitor.Wait(m_ScriptErrors);
                }
                m_ScriptErrors.Remove(itemID);
            }
            return errors;
        }

        /**
         * @brief Return a list of all script execution times.
         */
        public Dictionary<uint, float> GetObjectScriptsExecutionTimes()
        {
            Dictionary<uint, float> topScripts = new Dictionary<uint, float>();
            lock(m_InstancesDict)
            {
                foreach(XMRInstance instance in m_InstancesDict.Values)
                {
                    uint rootLocalID = instance.m_Part.ParentGroup.LocalId;
                    float oldTotal;
                    if(!topScripts.TryGetValue(rootLocalID, out oldTotal))
                        oldTotal = 0;

                    topScripts[rootLocalID] = (float)instance.m_CPUTime + oldTotal;
                }
            }
            return topScripts;
        }

        /**
         * @brief A float the value is a representative execution time in
         *        milliseconds of all scripts in the link set.
         * @param itemIDs = list of scripts in the link set
         * @returns milliseconds for all those scripts
         */
        public float GetScriptExecutionTime(List<UUID> itemIDs)
        {
            if((itemIDs == null) || (itemIDs.Count == 0))
                return 0;

            float time = 0;
            foreach(UUID itemID in itemIDs)
            {
                XMRInstance instance = GetInstance(itemID);
                if((instance != null) && instance.Running)
                    time += (float)instance.m_CPUTime;
            }
            return time;
        }

        /**
         * @brief Block script from dequeuing events.
         */
        public void SuspendScript(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
            {
                TraceCalls("[YEngine]: YEngine.SuspendScript({0})", itemID.ToString());
                instance.SuspendIt();
            }
        }

        /**
         * @brief Allow script to dequeue events.
         */
        public void ResumeScript(UUID itemID)
        {
            XMRInstance instance = GetInstance(itemID);
            if(instance != null)
            {
                TraceCalls("[YEngine]: YEngine.ResumeScript({0})", itemID.ToString());
                instance.ResumeIt();
            }
            else
            {
                // probably an XEngine script
            }
        }

        /**
         * @brief Rebuild m_ObjectInstArray[partUUID] from m_ObjectItemList[partUUID]
         * @param partUUID = which object in scene to rebuild for
         */
        private XMRInstance[] RebuildObjectInstArray(UUID partUUID)
        {
            List<UUID> itemIDList = m_ObjectItemList[partUUID];
            int n = 0;
            foreach(UUID itemID in itemIDList)
            {
                if(m_InstancesDict.ContainsKey(itemID))
                    n++;
            }

            XMRInstance[] a = new XMRInstance[n];
            n = 0;
            foreach(UUID itemID in itemIDList)
            {
                if(m_InstancesDict.TryGetValue(itemID, out a[n]))
                    n++;
            }
            m_ObjectInstArray[partUUID] = a;
            return a;
        }

        public void TraceCalls(string format, params object[] args)
        {
            if(m_TraceCalls)
                m_log.DebugFormat(format, args);
        }
        public void Verbose(string format, params object[] args)
        {
            if(m_Verbose)
                m_log.DebugFormat(format, args);
        }

        /**
         * @brief Manage our threads.
         */
        public static Thread StartMyThread(ThreadStart start, string name, ThreadPriority priority)
        {
            m_log.Debug("[YEngine]: starting thread " + name);
            Thread thread = WorkManager.StartThread(start, name, priority, false, false);
            return thread;
        }

        public static void UpdateMyThread()
        {
            Watchdog.UpdateThread();
        }

        public static void MyThreadExiting()
        {
            Watchdog.RemoveThread(true);
        }
    }
}
