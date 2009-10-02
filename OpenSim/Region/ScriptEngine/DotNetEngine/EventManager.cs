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
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using log4net;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    public class EventManager
    {
        //
        // Class is instanced in "ScriptEngine" and Uses "EventQueueManager"
        // that is also instanced in "ScriptEngine".
        // This class needs a bit of explaining:
        //
        // This class it the link between an event inside OpenSim and
        // the corresponding event in a user script being executed.
        //
        // For example when an user touches an object then the
        // "myScriptEngine.World.EventManager.OnObjectGrab" event is fired
        // inside OpenSim.
        // We hook up to this event and queue a touch_start in
        // EventQueueManager with the proper LSL parameters.
        // It will then be delivered to the script by EventQueueManager.
        //
        // You can check debug C# dump of an LSL script if you need to
        // verify what exact parameters are needed.
        //

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ScriptEngine myScriptEngine;

        public EventManager(ScriptEngine _ScriptEngine, bool performHookUp)
        {
            myScriptEngine = _ScriptEngine;
            ReadConfig();

            if (performHookUp)
            {
                myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            }
        }
        
        public void HookUpEvents()
        {
            m_log.Info("[" + myScriptEngine.ScriptEngineName +
                       "]: Hooking up to server events");

            myScriptEngine.World.EventManager.OnObjectGrab +=
                    touch_start;
            myScriptEngine.World.EventManager.OnObjectDeGrab +=
                    touch_end;
            myScriptEngine.World.EventManager.OnRemoveScript +=
                    OnRemoveScript;
            myScriptEngine.World.EventManager.OnScriptChangedEvent +=
                    changed;
            myScriptEngine.World.EventManager.OnScriptAtTargetEvent +=
                    at_target;
            myScriptEngine.World.EventManager.OnScriptNotAtTargetEvent +=
                    not_at_target;
            myScriptEngine.World.EventManager.OnScriptControlEvent +=
                    control;
            myScriptEngine.World.EventManager.OnScriptColliderStart +=
                    collision_start;
            myScriptEngine.World.EventManager.OnScriptColliding +=
                    collision;
            myScriptEngine.World.EventManager.OnScriptCollidingEnd +=
                    collision_end;

            IMoneyModule money =
                    myScriptEngine.World.RequestModuleInterface<IMoneyModule>();
            if (money != null)
                money.OnObjectPaid+=HandleObjectPaid;
        }

        public void ReadConfig()
        {
        }

        private void HandleObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            SceneObjectPart part =
                    myScriptEngine.World.GetSceneObjectPart(objectID);

            if (part != null)
            {
                money(part.LocalId, agentID, amount);
            }
        }

        public void changed(uint localID, uint change)
        {
            // Add to queue for all scripts in localID, Object pass change.
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "changed",new object[] { new LSL_Types.LSLInteger(change) },
                    new DetectParams[0]));
        }

        public void state_entry(uint localID)
        {
            // Add to queue for all scripts in ObjectID object
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "state_entry",new object[] { },
                    new DetectParams[0]));
        }

        public void touch_start(uint localID, uint originalID,
                Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;
            det[0].Populate(myScriptEngine.World);

            if (originalID == 0)
            {
                SceneObjectPart part =
                        myScriptEngine.World.GetSceneObjectPart(localID);

                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                SceneObjectPart originalPart =
                        myScriptEngine.World.GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }
            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }

            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch_start", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch(uint localID, uint originalID, Vector3 offsetPos,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;
            det[0].Populate(myScriptEngine.World);
            det[0].OffsetPos = new LSL_Types.Vector3(offsetPos.X,
                                                     offsetPos.Y,
                                                     offsetPos.Z);

            if (originalID == 0)
            {
                SceneObjectPart part = myScriptEngine.World.GetSceneObjectPart(localID);
                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                SceneObjectPart originalPart = myScriptEngine.World.GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }

            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch_end(uint localID, uint originalID, IClientAPI remoteClient,
                              SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;
            det[0].Populate(myScriptEngine.World);

            if (originalID == 0)
            {
                SceneObjectPart part =
                        myScriptEngine.World.GetSceneObjectPart(localID);
                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                SceneObjectPart originalPart =
                        myScriptEngine.World.GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }

            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }

            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch_end", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void OnRezScript(uint localID, UUID itemID, string script,
                int startParam, bool postOnRez, string engine, int stateSource)
        {
            if (script.StartsWith("//MRM:"))
                return;

            List<IScriptModule> engines =
                new List<IScriptModule>(
                myScriptEngine.World.RequestModuleInterfaces<IScriptModule>());

            List<string> names = new List<string>();
            foreach (IScriptModule m in engines)
                names.Add(m.ScriptEngineName);

            int lineEnd = script.IndexOf('\n');

            if (lineEnd > 1)
            {
                string firstline = script.Substring(0, lineEnd).Trim();

                int colon = firstline.IndexOf(':');
                if (firstline.Length > 2 &&
                    firstline.Substring(0, 2) == "//" && colon != -1)
                {
                    string engineName = firstline.Substring(2, colon-2);

                    if (names.Contains(engineName))
                    {
                        engine = engineName;
                        script = "//" + script.Substring(script.IndexOf(':')+1);
                    }
                    else
                    {
                        if (engine == myScriptEngine.ScriptEngineName)
                        {
                            SceneObjectPart part =
                                    myScriptEngine.World.GetSceneObjectPart(
                                    localID);

                            TaskInventoryItem item =
                                    part.Inventory.GetInventoryItem(itemID);

                            ScenePresence presence =
                                    myScriptEngine.World.GetScenePresence(
                                    item.OwnerID);

                            if (presence != null)
                            {
                               presence.ControllingClient.SendAgentAlertMessage(
                                        "Selected engine unavailable. "+
                                        "Running script on "+
                                        myScriptEngine.ScriptEngineName,
                                        false);
                            }
                        }
                    }
                }
            }

            if (engine != myScriptEngine.ScriptEngineName)
                return;

            // m_log.Debug("OnRezScript localID: " + localID +
            //             " LLUID: " + itemID.ToString() + " Size: " +
            //             script.Length);

            myScriptEngine.m_ScriptManager.StartScript(localID, itemID, script,
                    startParam, postOnRez);
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            // m_log.Debug("OnRemoveScript localID: " + localID + " LLUID: " + itemID.ToString());
            myScriptEngine.m_ScriptManager.StopScript(
                localID,
                itemID
                );
        }

        public void money(uint localID, UUID agentID, int amount)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "money", new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(amount) },
                    new DetectParams[0]));
        }

        // TODO: Replace placeholders below
        // NOTE! THE PARAMETERS FOR THESE FUNCTIONS ARE NOT CORRECT!
        //  These needs to be hooked up to OpenSim during init of this class
        //   then queued in EventQueueManager.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)

        public void state_exit(uint localID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "state_exit", new object[] { },
                    new DetectParams[0]));
        }

        public void collision_start(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key =detobj.keyUUID;
                d.Populate(myScriptEngine.World);
                det.Add(d);
            }

            if (det.Count > 0)
                myScriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision_start",
                        new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void collision(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key =detobj.keyUUID;
                d.Populate(myScriptEngine.World);
                det.Add(d);
            }

            if (det.Count > 0)
                myScriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision", new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void collision_end(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key =detobj.keyUUID;
                d.Populate(myScriptEngine.World);
                det.Add(d);
            }

            if (det.Count > 0)
                myScriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision_end",
                        new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void land_collision_start(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "land_collision_start",
                    new object[0],
                    new DetectParams[0]));
        }

        public void land_collision(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "land_collision",
                    new object[0],
                    new DetectParams[0]));
        }

        public void land_collision_end(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "land_collision_end",
                    new object[0],
                    new DetectParams[0]));
        }

        // Handled by long commands
        public void timer(uint localID, UUID itemID)
        {
        }

        public void listen(uint localID, UUID itemID)
        {
        }

        public void control(uint localID, UUID itemID, UUID agentID, uint held, uint change)
        {
            if ((change == 0) && (myScriptEngine.m_EventQueueManager.CheckEeventQueueForEvent(localID,"control"))) return;
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "control",new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(held),
                    new LSL_Types.LSLInteger(change)},
                    new DetectParams[0]));
        }

        public void email(uint localID, UUID itemID, string timeSent,
                string address, string subject, string message, int numLeft)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "email",new object[] {
                    new LSL_Types.LSLString(timeSent),
                    new LSL_Types.LSLString(address),
                    new LSL_Types.LSLString(subject),
                    new LSL_Types.LSLString(message),
                    new LSL_Types.LSLInteger(numLeft)},
                    new DetectParams[0]));
        }

        public void at_target(uint localID, uint handle, Vector3 targetpos,
                Vector3 atpos)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "at_target", new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Vector3(targetpos.X,targetpos.Y,targetpos.Z),
                    new LSL_Types.Vector3(atpos.X,atpos.Y,atpos.Z) },
                    new DetectParams[0]));
        }

        public void not_at_target(uint localID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "not_at_target",new object[0],
                    new DetectParams[0]));
        }

        public void at_rot_target(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "at_rot_target",new object[0],
                    new DetectParams[0]));
        }

        public void not_at_rot_target(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "not_at_rot_target",new object[0],
                    new DetectParams[0]));
        }

        public void attach(uint localID, UUID itemID)
        {
        }

        public void dataserver(uint localID, UUID itemID)
        {
        }

        public void link_message(uint localID, UUID itemID)
        {
        }

        public void moving_start(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "moving_start",new object[0],
                    new DetectParams[0]));
        }

        public void moving_end(uint localID, UUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new EventParams(
                    "moving_end",new object[0],
                    new DetectParams[0]));
        }

        public void object_rez(uint localID, UUID itemID)
        {
        }

        public void remote_data(uint localID, UUID itemID)
        {
        }

        // Handled by long commands
        public void http_response(uint localID, UUID itemID)
        {
        }

        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;
    }
}
