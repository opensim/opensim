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
 */

using log4net;
using System;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Interfaces;
using Amib.Threading;

namespace OpenSim.Region.ScriptEngine.Interfaces
{
    public interface IScriptEngine
    {
        //
        // An interface for a script API module to communicate with
        // the engine it's running under
        //

        Scene World { get; }
        IConfig Config { get; }
        Object AsyncCommands { get; }
        ILog Log { get; }
        string ScriptEngineName { get; }

        bool PostScriptEvent(LLUUID itemID, EventParams parms);
        bool PostObjectEvent(uint localID, EventParams parms);
        void ApiResetScript(LLUUID itemID);
        void ResetScript(LLUUID itemID);
        void SetScriptState(LLUUID itemID, bool state);
        bool GetScriptState(LLUUID itemID);
        void SetState(LLUUID itemID, string newState);
        int GetStartParameter(LLUUID itemID);
        IWorkItemResult QueueEventHandler(object parms);

        DetectParams GetDetectParams(LLUUID item, int number);
    }
}
