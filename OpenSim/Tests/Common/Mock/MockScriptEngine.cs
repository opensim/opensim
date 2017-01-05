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
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Tests.Common
{
    public class MockScriptEngine : INonSharedRegionModule, IScriptModule, IScriptEngine
    {
        public IConfigSource ConfigSource { get; private set; }

        public IConfig Config { get; private set; }

        private Scene m_scene;

        /// <summary>
        /// Expose posted events to tests.
        /// </summary>
        public Dictionary<UUID, List<EventParams>> PostedEvents { get; private set; }

        /// <summary>
        /// A very primitive way of hooking text cose to a posed event.
        /// </summary>
        /// <remarks>
        /// May be replaced with something that uses more original code in the future.
        /// </remarks>
        public event Action<UUID, EventParams> PostEventHook;

        public void Initialise(IConfigSource source)
        {
            ConfigSource = source;

            // Can set later on if required
            Config = new IniConfig("MockScriptEngine", ConfigSource);

            PostedEvents = new Dictionary<UUID, List<EventParams>>();
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            m_scene.StackModuleInterface<IScriptModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public string Name { get { return "Mock Script Engine"; } }
        public string ScriptEngineName { get { return Name; } }

        public Type ReplaceableInterface { get { return null; } }

#pragma warning disable 0067
        public event ScriptRemoved OnScriptRemoved;
        public event ObjectRemoved OnObjectRemoved;
#pragma warning restore 0067

        public string GetXMLState (UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public bool SetXMLState(UUID itemID, string xml)
        {
            throw new System.NotImplementedException ();
        }

        public bool PostScriptEvent(UUID itemID, string name, object[] args)
        {
//            Console.WriteLine("Posting event {0} for {1}", name, itemID);

            return PostScriptEvent(itemID, new EventParams(name, args, null));
        }

        public bool PostScriptEvent(UUID itemID, EventParams evParams)
        {
            List<EventParams> eventsForItem;

            if (!PostedEvents.ContainsKey(itemID))
            {
                eventsForItem = new List<EventParams>();
                PostedEvents.Add(itemID, eventsForItem);
            }
            else
            {
                eventsForItem = PostedEvents[itemID];
            }

            eventsForItem.Add(evParams);

            if (PostEventHook != null)
                PostEventHook(itemID, evParams);

            return true;
        }

        public bool PostObjectEvent(uint localID, EventParams evParams)
        {
            return PostObjectEvent(m_scene.GetSceneObjectPart(localID), evParams);
        }

        public bool PostObjectEvent(UUID itemID, string name, object[] args)
        {
            return PostObjectEvent(m_scene.GetSceneObjectPart(itemID), new EventParams(name, args, null));
        }

        private bool PostObjectEvent(SceneObjectPart part, EventParams evParams)
        {
            foreach (TaskInventoryItem item in part.Inventory.GetInventoryItems(InventoryType.LSL))
                PostScriptEvent(item.ItemID, evParams);

            return true;
        }

        public void SuspendScript(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public void ResumeScript(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public ArrayList GetScriptErrors(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public bool HasScript(UUID itemID, out bool running)
        {
            throw new System.NotImplementedException ();
        }

        public bool GetScriptState(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public void SaveAllState()
        {
            throw new System.NotImplementedException ();
        }

        public void StartProcessing()
        {
            throw new System.NotImplementedException ();
        }

        public float GetScriptExecutionTime(List<UUID> itemIDs)
        {
            throw new System.NotImplementedException ();
        }

        public Dictionary<uint, float> GetObjectScriptsExecutionTimes()
        {
            throw new System.NotImplementedException ();
        }

        public IScriptWorkItem QueueEventHandler(object parms)
        {
            throw new System.NotImplementedException ();
        }

        public DetectParams GetDetectParams(UUID item, int number)
        {
            throw new System.NotImplementedException ();
        }

        public void SetMinEventDelay(UUID itemID, double delay)
        {
            throw new System.NotImplementedException ();
        }

        public int GetStartParameter(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public void SetScriptState(UUID itemID, bool state, bool self)
        {
            throw new System.NotImplementedException ();
        }

        public void SetState(UUID itemID, string newState)
        {
            throw new System.NotImplementedException ();
        }

        public void ApiResetScript(UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public void ResetScript (UUID itemID)
        {
            throw new System.NotImplementedException ();
        }

        public IScriptApi GetApi(UUID itemID, string name)
        {
            throw new System.NotImplementedException ();
        }

        public Scene World { get { return m_scene; } }

        public IScriptModule ScriptModule { get { return this; } }

        public string ScriptEnginePath { get { throw new System.NotImplementedException (); }}

        public string ScriptClassName { get { throw new System.NotImplementedException (); } }

        public string ScriptBaseClassName { get { throw new System.NotImplementedException (); } }

        public string[] ScriptReferencedAssemblies { get { throw new System.NotImplementedException (); } }

        public ParameterInfo[] ScriptBaseClassParameters { get { throw new System.NotImplementedException (); } }

        public void ClearPostedEvents()
        {
            PostedEvents.Clear();
        }

        public void SleepScript(UUID itemID, int delay)
        {
        }
    }
}
