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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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

using System.Collections.Generic;
using OpenSim.Framework.Console;
using OpenSim.Region.ScriptEngine.Common;

namespace OpenSim.Grid.ScriptServer.ScriptServer
{
    internal class ScriptEngineManager
    {
        private ScriptEngineLoader ScriptEngineLoader;
        private List<ScriptServerInterfaces.ScriptEngine> scriptEngines = new List<ScriptServerInterfaces.ScriptEngine>();
        private ScriptServerMain m_ScriptServerMain;

        // Initialize
        public ScriptEngineManager(ScriptServerMain scm)
        {
            m_ScriptServerMain = scm;
            ScriptEngineLoader = new ScriptEngineLoader();
        }

        ~ScriptEngineManager()
        {
        }

        public ScriptServerInterfaces.ScriptEngine LoadEngine(string engineName)
        {
            // Load and add to list of ScriptEngines
            ScriptServerInterfaces.ScriptEngine sei = ScriptEngineLoader.LoadScriptEngine(engineName);
            if (sei != null)
            {
                scriptEngines.Add(sei);
            }
            return sei;
        }
    }
}