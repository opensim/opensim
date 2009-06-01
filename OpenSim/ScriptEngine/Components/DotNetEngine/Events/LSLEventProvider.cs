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
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.ScriptEngine.Shared;
using EventParams = OpenSim.ScriptEngine.Shared.EventParams;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Events
{
    public class LSLEventProvider : IScriptEventProvider
    {
        public delegate void RezScriptDelegate(uint localID, UUID itemID, string script, int startParam, bool postOnRez,
                                    string engine);
        public event RezScriptDelegate RezScript;
        public delegate void RemoveScriptDelegate(uint localID, UUID itemID);
        public event RemoveScriptDelegate RemoveScript;
        public delegate void ScriptChangedDelegate(uint localID, uint change);
        public event ScriptChangedDelegate ScriptChanged;

        private RegionInfoStructure CurrentRegion;
        public void Initialize(RegionInfoStructure currentRegion)
        {
            CurrentRegion = currentRegion;
            HookupEvents();
        }

        private void HookupEvents()
        {
            CurrentRegion.Scene.EventManager.OnObjectGrab += OnObjectGrab;
            CurrentRegion.Scene.EventManager.OnRezScript += OnRezScript;
            CurrentRegion.Scene.EventManager.OnRemoveScript += OnRemoveScript;
            CurrentRegion.Scene.EventManager.OnScriptChangedEvent += OnScriptChangedEvent;


        }

        private void OnScriptChangedEvent(uint localID, uint change)
        {
            // Script is being changed, fire event
            if (ScriptChanged != null)
                ScriptChanged(localID, change);
        }

        private void OnRemoveScript(uint localID, UUID itemID)
        {
            // Script is being removed, fire event
            if (RemoveScript != null)
                RemoveScript(localID, itemID);
        }

        private void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            // New script being created, fire event
            if (RezScript != null)
                RezScript(localID, itemID, script, startParam, postOnRez, engine);
        }

        private void OnObjectGrab(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;
            //det[0].Populate(World);

            if (originalID == 0)
            {
                SceneObjectPart part =
                        CurrentRegion.Scene.GetSceneObjectPart(localID);

                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                SceneObjectPart originalPart =
                        CurrentRegion.Scene.GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }
            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }
            Shared.EventParams ep =
                new Shared.EventParams(localID, "touch_start", new Object[] {new LSL_Types.LSLInteger(1)}, det);
            CurrentRegion.Executors_Execute(ep);
                
        }
    }
}
