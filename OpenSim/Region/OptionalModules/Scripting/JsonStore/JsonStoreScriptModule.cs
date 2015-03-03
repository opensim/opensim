/*
 * Copyright (c) Contributors 
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
using Mono.Addins;

using System;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Scripting;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.OptionalModules.Scripting.JsonStore
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "JsonStoreScriptModule")]

    public class JsonStoreScriptModule  : INonSharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig m_config = null;
        private bool m_enabled = false;
        private Scene m_scene = null;

        private IScriptModuleComms m_comms;
        private IJsonStoreModule m_store;

        private Dictionary<UUID,HashSet<UUID>> m_scriptStores = new Dictionary<UUID,HashSet<UUID>>();

#region Region Module interface

        // -----------------------------------------------------------------
        /// <summary>
        /// Name of this shared module is it's class name
        /// </summary>
        // -----------------------------------------------------------------
        public string Name
        {
            get { return this.GetType().Name; }
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Initialise this shared module
        /// </summary>
        /// <param name="scene">this region is getting initialised</param>
        /// <param name="source">nini config, we are not using this</param>
        // -----------------------------------------------------------------
        public void Initialise(IConfigSource config)
        {
            try 
            {
                if ((m_config = config.Configs["JsonStore"]) == null)
                {
                    // There is no configuration, the module is disabled
                    // m_log.InfoFormat("[JsonStoreScripts] no configuration info");
                    return;
                }

                m_enabled = m_config.GetBoolean("Enabled", m_enabled);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[JsonStoreScripts]: initialization error: {0}", e.Message);
                return;
            }

            if (m_enabled)
                m_log.DebugFormat("[JsonStoreScripts]: module is enabled");
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// everything is loaded, perform post load configuration
        /// </summary>
        // -----------------------------------------------------------------
        public void PostInitialise()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Nothing to do on close
        /// </summary>
        // -----------------------------------------------------------------
        public void Close()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void AddRegion(Scene scene)
        {
            scene.EventManager.OnScriptReset += HandleScriptReset;
            scene.EventManager.OnRemoveScript += HandleScriptReset;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnScriptReset -= HandleScriptReset;
            scene.EventManager.OnRemoveScript -= HandleScriptReset;

            // need to remove all references to the scene in the subscription
            // list to enable full garbage collection of the scene object
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        private void HandleScriptReset(uint localID, UUID itemID)
        {
            HashSet<UUID> stores;

            lock (m_scriptStores)
            {
                if (! m_scriptStores.TryGetValue(itemID, out stores))
                    return;
                m_scriptStores.Remove(itemID);
            }

            foreach (UUID id in stores)
                m_store.DestroyStore(id);
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Called when all modules have been added for a region. This is 
        /// where we hook up events
        /// </summary>
        // -----------------------------------------------------------------
        public void RegionLoaded(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;
                m_comms = m_scene.RequestModuleInterface<IScriptModuleComms>();
                if (m_comms == null)
                {
                    m_log.ErrorFormat("[JsonStoreScripts]: ScriptModuleComms interface not defined");
                    m_enabled = false;
                    return;
                }

                m_store = m_scene.RequestModuleInterface<IJsonStoreModule>();
                if (m_store == null)
                {
                    m_log.ErrorFormat("[JsonStoreScripts]: JsonModule interface not defined");
                    m_enabled = false;
                    return;
                }

                try
                {
                    m_comms.RegisterScriptInvocations(this);
                    m_comms.RegisterConstants(this);
                }
                catch (Exception e)
                {
                    // See http://opensimulator.org/mantis/view.php?id=5971 for more information
                    m_log.WarnFormat("[JsonStoreScripts]: script method registration failed; {0}", e.Message);
                    m_enabled = false;
                }
            }
        }

        /// -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public Type ReplaceableInterface
        {
            get { return null; }
        }

#endregion

#region ScriptConstantsInterface

        [ScriptConstant]
        public static readonly int JSON_NODETYPE_UNDEF = (int)JsonStoreNodeType.Undefined;

        [ScriptConstant]
        public static readonly int JSON_NODETYPE_OBJECT = (int)JsonStoreNodeType.Object;

        [ScriptConstant]
        public static readonly int JSON_NODETYPE_ARRAY = (int)JsonStoreNodeType.Array;

        [ScriptConstant]
        public static readonly int JSON_NODETYPE_VALUE = (int)JsonStoreNodeType.Value;

        [ScriptConstant]
        public static readonly int JSON_VALUETYPE_UNDEF = (int)JsonStoreValueType.Undefined;

        [ScriptConstant]
        public static readonly int JSON_VALUETYPE_BOOLEAN = (int)JsonStoreValueType.Boolean;

        [ScriptConstant]
        public static readonly int JSON_VALUETYPE_INTEGER = (int)JsonStoreValueType.Integer;

        [ScriptConstant]
        public static readonly int JSON_VALUETYPE_FLOAT = (int)JsonStoreValueType.Float;

        [ScriptConstant]
        public static readonly int JSON_VALUETYPE_STRING = (int)JsonStoreValueType.String;


#endregion

#region ScriptInvocationInteface
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonAttachObjectStore(UUID hostID, UUID scriptID)
        {
            UUID uuid = UUID.Zero;
            if (! m_store.AttachObjectStore(hostID))
                GenerateRuntimeError("Failed to create Json store");
            
            return hostID;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonCreateStore(UUID hostID, UUID scriptID, string value)
        {
            UUID uuid = UUID.Zero;
            if (! m_store.CreateStore(value, ref uuid))
                GenerateRuntimeError("Failed to create Json store");
            
            lock (m_scriptStores)
            {
                if (! m_scriptStores.ContainsKey(scriptID))
                    m_scriptStores[scriptID] = new HashSet<UUID>();
                
                m_scriptStores[scriptID].Add(uuid);
            }
            return uuid;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonDestroyStore(UUID hostID, UUID scriptID, UUID storeID)
        {
            lock(m_scriptStores)
            {
                if (m_scriptStores.ContainsKey(scriptID))
                    m_scriptStores[scriptID].Remove(storeID);
            }

            return m_store.DestroyStore(storeID) ? 1 : 0;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonTestStore(UUID hostID, UUID scriptID, UUID storeID)
        {
            return m_store.TestStore(storeID) ? 1 : 0;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonRezAtRoot(UUID hostID, UUID scriptID, string item, Vector3 pos, Vector3 vel, Quaternion rot, string param)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonRezObject(hostID, scriptID, reqID, item, pos, vel, rot, param), null, "JsonStoreScriptModule.DoJsonRezObject");
            return reqID;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonReadNotecard(UUID hostID, UUID scriptID, UUID storeID, string path, string notecardIdentifier)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonReadNotecard(reqID, hostID, scriptID, storeID, path, notecardIdentifier), null, "JsonStoreScriptModule.JsonReadNotecard");
            return reqID;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonWriteNotecard(UUID hostID, UUID scriptID, UUID storeID, string path, string name)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonWriteNotecard(reqID,hostID,scriptID,storeID,path,name), null, "JsonStoreScriptModule.DoJsonWriteNotecard");
            return reqID;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public string JsonList2Path(UUID hostID, UUID scriptID, object[] pathlist)
        {
            string ipath = ConvertList2Path(pathlist);
            string opath;
            
            if (JsonStore.CanonicalPathExpression(ipath,out opath))
                return opath;

            // This won't parse if passed to the other routines as opposed to
            // returning an empty string which is a valid path and would overwrite
            // the entire store
            return "**INVALID**";
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonGetNodeType(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            return (int)m_store.GetNodeType(storeID,path);
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonGetValueType(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            return (int)m_store.GetValueType(storeID,path);
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonSetValue(UUID hostID, UUID scriptID, UUID storeID, string path, string value)
        {
            return m_store.SetValue(storeID,path,value,false) ? 1 : 0;
        }

        [ScriptInvocation]
        public int JsonSetJson(UUID hostID, UUID scriptID, UUID storeID, string path, string value)
        {
            return m_store.SetValue(storeID,path,value,true) ? 1 : 0;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonRemoveValue(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            return m_store.RemoveValue(storeID,path) ? 1 : 0;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public int JsonGetArrayLength(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            return m_store.GetArrayLength(storeID,path);
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public string JsonGetValue(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            string value = String.Empty;
            m_store.GetValue(storeID,path,false,out value);
            return value;
        }

        [ScriptInvocation]
        public string JsonGetJson(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            string value = String.Empty;
            m_store.GetValue(storeID,path,true, out value);
            return value;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonTakeValue(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonTakeValue(scriptID,reqID,storeID,path,false), null, "JsonStoreScriptModule.DoJsonTakeValue");
            return reqID;
        }

        [ScriptInvocation]
        public UUID JsonTakeValueJson(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonTakeValue(scriptID,reqID,storeID,path,true), null, "JsonStoreScriptModule.DoJsonTakeValueJson");
            return reqID;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        [ScriptInvocation]
        public UUID JsonReadValue(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonReadValue(scriptID,reqID,storeID,path,false), null, "JsonStoreScriptModule.DoJsonReadValue");
            return reqID;
        }

        [ScriptInvocation]
        public UUID JsonReadValueJson(UUID hostID, UUID scriptID, UUID storeID, string path)
        {
            UUID reqID = UUID.Random();
            Util.FireAndForget(
                o => DoJsonReadValue(scriptID,reqID,storeID,path,true), null, "JsonStoreScriptModule.DoJsonReadValueJson");
            return reqID;
        }
        
#endregion

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        protected void GenerateRuntimeError(string msg)
        {
            m_log.InfoFormat("[JsonStore] runtime error: {0}",msg);
            throw new Exception("JsonStore Runtime Error: " + msg);
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        protected void DispatchValue(UUID scriptID, UUID reqID, string value)
        {
            m_comms.DispatchReply(scriptID,1,value,reqID.ToString());
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        private void DoJsonTakeValue(UUID scriptID, UUID reqID, UUID storeID, string path, bool useJson)
        {
            try
            {
                m_store.TakeValue(storeID,path,useJson,delegate(string value) { DispatchValue(scriptID,reqID,value); });
                return;
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStoreScripts]: unable to retrieve value; {0}",e.ToString());
            }
            
            DispatchValue(scriptID,reqID,String.Empty);
        }


        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        private void DoJsonReadValue(UUID scriptID, UUID reqID, UUID storeID, string path, bool useJson)
        {
            try
            {
                m_store.ReadValue(storeID,path,useJson,delegate(string value) { DispatchValue(scriptID,reqID,value); });
                return;
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStoreScripts]: unable to retrieve value; {0}",e.ToString());
            }
            
            DispatchValue(scriptID,reqID,String.Empty);
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        private void DoJsonReadNotecard(
            UUID reqID, UUID hostID, UUID scriptID, UUID storeID, string path, string notecardIdentifier)
        {
            UUID assetID;

            if (!UUID.TryParse(notecardIdentifier, out assetID))
            {
                SceneObjectPart part = m_scene.GetSceneObjectPart(hostID);               
                assetID = ScriptUtils.GetAssetIdFromItemName(part, notecardIdentifier, (int)AssetType.Notecard);
            }

            AssetBase a = m_scene.AssetService.Get(assetID.ToString());
            if (a == null)
                GenerateRuntimeError(String.Format("Unable to find notecard asset {0}", assetID));

            if (a.Type != (sbyte)AssetType.Notecard)
                GenerateRuntimeError(String.Format("Invalid notecard asset {0}", assetID));
            
            m_log.DebugFormat("[JsonStoreScripts]: read notecard in context {0}",storeID);

            try 
            {
                string jsondata = SLUtil.ParseNotecardToString(a.Data);
                int result = m_store.SetValue(storeID, path, jsondata,true) ? 1 : 0;
                m_comms.DispatchReply(scriptID, result, "", reqID.ToString());
                return;
            }
            catch(SLUtil.NotANotecardFormatException e)
            {
                m_log.WarnFormat("[JsonStoreScripts]: Notecard parsing failed; assetId {0} at line number {1}", assetID.ToString(), e.lineNumber);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[JsonStoreScripts]: Json parsing failed; {0}", e.Message);
            }

            GenerateRuntimeError(String.Format("Json parsing failed for {0}", assetID));
            m_comms.DispatchReply(scriptID, 0, "", reqID.ToString());
        }
            
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        private void DoJsonWriteNotecard(UUID reqID, UUID hostID, UUID scriptID, UUID storeID, string path, string name)
        {
            string data;
            if (! m_store.GetValue(storeID,path,true, out data))
            {
                m_comms.DispatchReply(scriptID,0,UUID.Zero.ToString(),reqID.ToString());
                return;
            }
                
            SceneObjectPart host = m_scene.GetSceneObjectPart(hostID);
            
            // Create new asset
            UUID assetID = UUID.Random();
            AssetBase asset = new AssetBase(assetID, name, (sbyte)AssetType.Notecard, host.OwnerID.ToString());
            asset.Description = "Json store";

            int textLength = data.Length;
            data = "Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length "
                    + textLength.ToString() + "\n" + data + "}\n";

            asset.Data = Util.UTF8.GetBytes(data);
            m_scene.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ResetIDs(host.UUID);
            taskItem.ParentID = host.UUID;
            taskItem.CreationDate = (uint)Util.UnixTimeSinceEpoch();
            taskItem.Name = asset.Name;
            taskItem.Description = asset.Description;
            taskItem.Type = (int)AssetType.Notecard;
            taskItem.InvType = (int)InventoryType.Notecard;
            taskItem.OwnerID = host.OwnerID;
            taskItem.CreatorID = host.OwnerID;
            taskItem.BasePermissions = (uint)PermissionMask.All;
            taskItem.CurrentPermissions = (uint)PermissionMask.All;
            taskItem.EveryonePermissions = 0;
            taskItem.NextPermissions = (uint)PermissionMask.All;
            taskItem.GroupID = host.GroupID;
            taskItem.GroupPermissions = 0;
            taskItem.Flags = 0;
            taskItem.PermsGranter = UUID.Zero;
            taskItem.PermsMask = 0;
            taskItem.AssetID = asset.FullID;

            host.Inventory.AddInventoryItem(taskItem, false);

            m_comms.DispatchReply(scriptID,1,assetID.ToString(),reqID.ToString());
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Convert a list of values that are path components to a single string path
        /// </summary>
        // -----------------------------------------------------------------
        protected static Regex m_ArrayPattern = new Regex("^([0-9]+|\\+)$");
        private string ConvertList2Path(object[] pathlist)
        {
            string path = "";
            for (int i = 0; i < pathlist.Length; i++)
            {
                string token = "";
                
                if (pathlist[i] is string)
                {
                    token = pathlist[i].ToString();

                    // Check to see if this is a bare number which would not be a valid
                    // identifier otherwise
                    if (m_ArrayPattern.IsMatch(token))
                        token = '[' + token + ']';
                }
                else if (pathlist[i] is int)
                {
                    token = "[" + pathlist[i].ToString() + "]";
                }
                else
                {
                    token = "." + pathlist[i].ToString() + ".";
                }
                
                path += token + ".";
            }
            
            return path;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        private void DoJsonRezObject(UUID hostID, UUID scriptID, UUID reqID, string name, Vector3 pos, Vector3 vel, Quaternion rot, string param)
        {
            if (Double.IsNaN(rot.X) || Double.IsNaN(rot.Y) || Double.IsNaN(rot.Z) || Double.IsNaN(rot.W))
            {
                GenerateRuntimeError("Invalid rez rotation");
                return;
            }

            SceneObjectGroup host = m_scene.GetSceneObjectGroup(hostID);
            if (host == null)
            {
                GenerateRuntimeError(String.Format("Unable to find rezzing host '{0}'",hostID));
                return;
            }

            // hpos = host.RootPart.GetWorldPosition()
            // float dist = (float)llVecDist(hpos, pos);
            // if (dist > m_ScriptDistanceFactor * 10.0f)
            //     return;

            TaskInventoryItem item = host.RootPart.Inventory.GetInventoryItem(name);
            if (item == null)
            {
                GenerateRuntimeError(String.Format("Unable to find object to rez '{0}'",name));
                return;
            }

            if (item.InvType != (int)InventoryType.Object)
            {
                GenerateRuntimeError("Can't create requested object; object is missing from database");
                return;
            }

            List<SceneObjectGroup> objlist;
            List<Vector3> veclist;
            
            bool success = host.RootPart.Inventory.GetRezReadySceneObjects(item, out objlist, out veclist);
            if (! success)
            {
                GenerateRuntimeError("Failed to create object");
                return;
            }

            int totalPrims = 0;
            foreach (SceneObjectGroup group in objlist)
                totalPrims += group.PrimCount;

            if (! m_scene.Permissions.CanRezObject(totalPrims, item.OwnerID, pos))
            {
                GenerateRuntimeError("Not allowed to create the object");
                return;
            }

            if (! m_scene.Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    host.RootPart.Inventory.RemoveInventoryItem(item.ItemID);
            }

            for (int i = 0; i < objlist.Count; i++)
            {
                SceneObjectGroup group = objlist[i];
                Vector3 curpos = pos + veclist[i];

                if (group.IsAttachment == false && group.RootPart.Shape.State != 0)
                {
                    group.RootPart.AttachedPos = group.AbsolutePosition;
                    group.RootPart.Shape.LastAttachPoint = (byte)group.AttachmentPoint;
                }

                group.FromPartID = host.RootPart.UUID;
                m_scene.AddNewSceneObject(group, true, curpos, rot, vel);

                UUID storeID = group.UUID;
                if (! m_store.CreateStore(param, ref storeID))
                {
                    GenerateRuntimeError("Unable to create jsonstore for new object");
                    continue;
                }

                // We can only call this after adding the scene object, since the scene object references the scene
                // to find out if scripts should be activated at all.
                group.RootPart.SetDieAtEdge(true);
                group.CreateScriptInstances(0, true, m_scene.DefaultScriptEngine, 3);
                group.ResumeScripts();

                group.ScheduleGroupForFullUpdate();

                // send the reply back to the host object, use the integer param to indicate the number 
                // of remaining objects
                m_comms.DispatchReply(scriptID, objlist.Count-i-1, group.RootPart.UUID.ToString(), reqID.ToString());
            }
        }
    }
}
