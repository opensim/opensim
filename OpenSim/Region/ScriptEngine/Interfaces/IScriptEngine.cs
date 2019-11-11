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
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using Amib.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace OpenSim.Region.ScriptEngine.Interfaces
{
    /// <summary>
    /// An interface for a script API module to communicate with
    /// the engine it's running under
    /// </summary>
    public interface IScriptEngine
    {
        /// <summary>
        /// Queue an event for execution
        /// </summary>
        IScriptWorkItem QueueEventHandler(object parms);

        Scene World { get; }

        IScriptModule ScriptModule { get; }

        void CancelScriptEvent(UUID itemID, string eventName);
        /// <summary>
        /// Post an event to a single script
        /// </summary>
        bool PostScriptEvent(UUID itemID, EventParams parms);

        /// <summary>
        /// Post event to an entire prim
        /// </summary>
        bool PostObjectEvent(uint localID, EventParams parms);

        DetectParams GetDetectParams(UUID item, int number);
        void SetMinEventDelay(UUID itemID, double delay);
        int GetStartParameter(UUID itemID);

        void SetScriptState(UUID itemID, bool state, bool self);
        bool GetScriptState(UUID itemID);
        void SetState(UUID itemID, string newState);
        void ApiResetScript(UUID itemID);
        void ResetScript(UUID itemID);
        IConfig Config { get; }
        IConfigSource ConfigSource { get; }
        string ScriptEngineName { get; }
        string ScriptEnginePath { get; }

        /// <summary>
        /// Return the name of the class that will be used for all running scripts.
        /// </summary>
        /// <remarks>
        /// Each class goes in its own assembly so we don't need to otherwise distinguish the class name.
        /// </remarks>
        string ScriptClassName { get; }

        /// <summary>
        /// Return the name of the base class that will be used for all running scripts.
        /// </summary>
        string ScriptBaseClassName { get; }

        /// <summary>
        /// Assemblies that need to be referenced when compiling scripts.
        /// </summary>
        /// <remarks>
        /// These are currently additional to those always referenced by the compiler, BUT THIS MAY CHANGE IN THE
        /// FUTURE.
        /// This can be null if there are no additional assemblies.
        /// </remarks>
        string[] ScriptReferencedAssemblies { get; }

        /// <summary>
        /// Parameters for the generated script's constructor.
        /// </summary>
        /// <remarks>
        /// Can be null if there are no parameters
        /// </remarks>
        ParameterInfo[] ScriptBaseClassParameters { get; }

        IScriptApi GetApi(UUID itemID, string name);

        void SleepScript(UUID itemID, int delay);
    }
}
