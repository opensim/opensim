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
* 
*/
/* Original code: Tedd Hansen */
using System;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ScriptEngine.RemoteServer
{
    /// <summary>
    /// This is the root object for ScriptEngine. Objects access each other trough this class.
    /// </summary>
    /// 
    [Serializable]
    public class ScriptEngine : IRegionModule
    {
        internal Scene World;
        internal EventManager m_EventManager; // Handles and queues incoming events from OpenSim
        internal RemoteServer m_RemoteServer;

        private LogBase m_log;

        public ScriptEngine()
        {
            Common.mySE = this;
        }

        public LogBase Log
        {
            get { return m_log; }
        }

        public void InitializeEngine(Scene Sceneworld, LogBase logger)
        {
            World = Sceneworld;
            m_log = logger;

            Log.Verbose("ScriptEngine", "RemoteEngine (Remote Script Server) initializing");
            // Create all objects we'll be using
            m_EventManager = new EventManager(this);
            m_RemoteServer = new RemoteServer();
            m_RemoteServer.Connect("localhost", 1234);
        }

        public void Shutdown()
        {
            // We are shutting down
        }


        #region IRegionModule

        public void Initialise(Scene scene, IConfigSource config)
        {
            InitializeEngine(scene, MainLog.Instance);
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