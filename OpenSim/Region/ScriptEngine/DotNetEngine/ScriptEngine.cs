/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class ScriptEngine : OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineInterface
    {
        //
        // This is the root object for ScriptEngine
        //

        internal OpenSim.Region.Environment.Scenes.Scene World;
        internal EventManager myEventManager;                   // Handles and queues incoming events from OpenSim
        internal EventQueueManager myEventQueueManager;         // Executes events
        internal ScriptManager myScriptManager;                 // Load, unload and execute scripts
        internal OpenSim.Framework.Console.LogBase m_logger;

        public ScriptEngine()
        {
            //Common.SendToDebug("ScriptEngine Object Initialized");
            Common.mySE = this;
        }

        public void InitializeEngine(OpenSim.Region.Environment.Scenes.Scene Sceneworld, OpenSim.Framework.Console.LogBase logger)
        {
            World = Sceneworld;
            m_logger = logger;
            
            //m_logger.Status("ScriptEngine", "InitializeEngine");

            // Create all objects we'll be using
            myEventQueueManager = new EventQueueManager(this);
            myEventManager = new EventManager(this);
            myScriptManager = new ScriptManager(this);

            // Should we iterate the region for scripts that needs starting?
            // Or can we assume we are loaded before anything else so we can use proper events?
            
        }
        public void Shutdown()
        {
            // We are shutting down
        }

        // !!!FOR DEBUGGING ONLY!!! (for executing script directly from test app)
        [Obsolete("!!!FOR DEBUGGING ONLY!!!")]
        public void StartScript(string ScriptID, string ObjectID)
        {
            m_logger.Status("ScriptEngine", "DEBUG FUNCTION: StartScript: " + ScriptID);
            myScriptManager.StartScript(ScriptID, ObjectID);
        }
    }
}
