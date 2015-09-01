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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate void ScriptRemoved(UUID script);
    public delegate void ObjectRemoved(UUID prim);

    public interface IScriptModule: INonSharedRegionModule
    {
        /// <summary>
        /// Triggered when a script is removed from the script module.
        /// </summary>
        event ScriptRemoved OnScriptRemoved;

        /// <summary>
        /// Triggered when an object is removed via the script module.
        /// </summary>
        event ObjectRemoved OnObjectRemoved;

        string ScriptEngineName { get; }

        string GetXMLState(UUID itemID);
        bool SetXMLState(UUID itemID, string xml);

        /// <summary>
        /// Post a script event to a single script.
        /// </summary>
        /// <returns>true if the post suceeded, false if it did not</returns>
        /// <param name='itemID'>The item ID of the script.</param>
        /// <param name='name'>The name of the event.</param>
        /// <param name='args'>
        /// The arguments of the event.  These are in the order in which they appear.
        /// e.g. for http_request this will be an object array of key request_id, string method, string body
        /// </param>
        bool PostScriptEvent(UUID itemID, string name, Object[] args);

        bool PostObjectEvent(UUID itemID, string name, Object[] args);

        /// <summary>
        /// Suspends a script.
        /// </summary>
        /// <param name="itemID">The item ID of the script.</param>
        void SuspendScript(UUID itemID);

        /// <summary>
        /// Resumes a script.
        /// </summary>
        /// <param name="itemID">The item ID of the script.</param>
        void ResumeScript(UUID itemID);

        ArrayList GetScriptErrors(UUID itemID);

        bool HasScript(UUID itemID, out bool running);

        /// <summary>
        /// Returns true if a script is running.
        /// </summary>
        /// <param name="itemID">The item ID of the script.</param>
        bool GetScriptState(UUID itemID);

        void SaveAllState();

        /// <summary>
        /// Starts the processing threads.
        /// </summary>
        void StartProcessing();

        /// <summary>
        /// Get the execution times of all scripts in the given array if they are currently running.
        /// </summary>
        /// <returns>
        /// A float the value is a representative execution time in milliseconds of all scripts in that Array.
        /// </returns>
        float GetScriptExecutionTime(List<UUID> itemIDs);

        /// <summary>
        /// Get the execution times of all scripts in each object.
        /// </summary>
        /// <returns>
        /// A dictionary where the key is the root object ID of a linkset
        /// and the value is a representative execution time in milliseconds of all scripts in that linkset.
        /// </returns>
        Dictionary<uint, float> GetObjectScriptsExecutionTimes();
    }
}
