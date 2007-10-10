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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Interfaces;
using libsecondlife;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// This is the root object for ScriptEngine
    /// </summary>
    [Serializable]
    public class ScriptEngine :IRegionModule
    {

        internal OpenSim.Region.Environment.Scenes.Scene World;
        internal EventManager m_EventManager;                   // Handles and queues incoming events from OpenSim
        internal EventQueueManager m_EventQueueManager;         // Executes events
        internal ScriptManager m_ScriptManager;                 // Load, unload and execute scripts
        internal AppDomainManager m_AppDomainManager;
        internal LSLLongCmdHandler m_LSLLongCmdHandler;

        private OpenSim.Framework.Console.LogBase m_log;

        public ScriptEngine()
        {
            //Common.SendToDebug("ScriptEngine Object Initialized");
            Common.mySE = this;
        }

        public LogBase Log
        {
            get { return m_log; }
        }

        public void InitializeEngine(OpenSim.Region.Environment.Scenes.Scene Sceneworld, OpenSim.Framework.Console.LogBase logger)
        {

            World = Sceneworld;
            m_log = logger;

            Log.Verbose("ScriptEngine", "DotNet & LSL ScriptEngine initializing");

            //m_logger.Status("ScriptEngine", "InitializeEngine");

            // Create all objects we'll be using
            m_EventQueueManager = new EventQueueManager(this);
            m_EventManager = new EventManager(this);
            m_ScriptManager = new ScriptManager(this);
            m_AppDomainManager = new AppDomainManager();
            m_LSLLongCmdHandler = new LSLLongCmdHandler(this);

            // Should we iterate the region for scripts that needs starting?
            // Or can we assume we are loaded before anything else so we can use proper events?
            
            
        }
            
        public void Shutdown()
        {
            // We are shutting down
        }

        //// !!!FOR DEBUGGING ONLY!!! (for executing script directly from test app)
        //[Obsolete("!!!FOR DEBUGGING ONLY!!!")]
        //public void StartScript(string ScriptID, IScriptHost ObjectID)
        //{
        //    this.myEventManager.TEMP_OBJECT_ID = ObjectID;
        //    Log.Status("ScriptEngine", "DEBUG FUNCTION: StartScript: " + ScriptID);
        //    myScriptManager.StartScript(ScriptID, ObjectID);
        //}

        #region IRegionModule

        public void Initialise(Scene scene)
        {
            this.InitializeEngine(scene, MainLog.Instance);
        }

        public void PostInitialise()
        {

        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LSLScriptingModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
