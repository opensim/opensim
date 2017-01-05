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
using System.Collections.Generic;
using OpenMetaverse;
using Nini.Config;

namespace OpenSim.Region.ScriptEngine.Interfaces
{
    public interface ICompiler
    {
        /// <summary>
        /// Performs the script compile.
        /// </summary>
        /// <param name="Script"></param>
        /// <param name="asset"></param>
        /// <param name="ownerUUID"></param>
        /// <param name="alwaysRecompile">
        /// If set to true then always recompile the script, even if we have a DLL already cached.
        /// </param>
        /// <param name="assembly"></param>
        /// <param name="linemap"></param>
        void PerformScriptCompile(
            string source, string asset, UUID ownerID,
            out string assembly, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap);

        /// <summary>
        /// Performs the script compile.
        /// </summary>
        /// <param name="Script"></param>
        /// <param name="asset"></param>
        /// <param name="ownerUUID"></param>
        /// <param name="alwaysRecompile">
        /// If set to true then always recompile the script, even if we have a DLL already cached.
        /// </param>
        /// <param name="assembly"></param>
        /// <param name="linemap"></param>
        void PerformScriptCompile(
            string source, string asset, UUID ownerID, bool alwaysRecompile,
            out string assembly, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap);

        string[] GetWarnings();
    }
}
