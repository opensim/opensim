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
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate void ScriptCommand(UUID script, string id, string module, string command, string k);

    /// <summary>
    /// Interface for communication between OpenSim modules and in-world scripts
    /// </summary>
    ///
    /// See OpenSim.Region.ScriptEngine.Shared.Api.MOD_Api.modSendCommand() for information on receiving messages
    /// from scripts in OpenSim modules.
    public interface IScriptModuleComms
    {
        /// <summary>
        /// Modules can subscribe to this event to receive command invocations from in-world scripts
        /// </summary>
        event ScriptCommand OnScriptCommand;

        void RegisterScriptInvocation(object target, string method);
        void RegisterScriptInvocation(object target, MethodInfo method);
        void RegisterScriptInvocation(object target, string[] methods);
        Delegate[] GetScriptInvocationList();

        Delegate LookupScriptInvocation(string fname);
        string LookupModInvocation(string fname);
        Type[] LookupTypeSignature(string fname);
        Type LookupReturnType(string fname);

        object InvokeOperation(UUID hostId, UUID scriptId, string fname, params object[] parms);

        /// <summary>
        /// Send a link_message event to an in-world script
        /// </summary>
        /// <param name="scriptId"></param>
        /// <param name="code"></param>
        /// <param name="text"></param>
        /// <param name="key"></param>
        void DispatchReply(UUID scriptId, int code, string text, string key);

        /// For constants
        void RegisterConstant(string cname, object value);
        object LookupModConstant(string cname);
        Dictionary<string, object> GetConstants();

        // For use ONLY by the script API
        void RaiseEvent(UUID script, string id, string module, string command, string key);
    }
}
