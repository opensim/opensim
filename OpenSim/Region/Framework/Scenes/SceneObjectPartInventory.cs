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
using System.Text;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Framework.Scenes.Serialization;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.Framework.Scenes
{
    public class SceneObjectPartInventory : IEntityInventory
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private byte[] m_inventoryFileData = new byte[0];
        private byte[] m_inventoryFileNameBytes = new byte[0];
        private string m_inventoryFileName = "";
        private uint m_inventoryFileNameSerial = 0;
        private bool m_inventoryPrivileged = false;
        private object m_inventoryFileLock = new object();

        private Dictionary<UUID, ArrayList> m_scriptErrors = new Dictionary<UUID, ArrayList>();

        /// <value>
        /// The part to which the inventory belongs.
        /// </value>
        private SceneObjectPart m_part;

        /// <summary>
        /// Serial count for inventory file , used to tell if inventory has changed
        /// no need for this to be part of Database backup
        /// </summary>
        protected uint m_inventorySerial = 0;

        /// <summary>
        /// Holds in memory prim inventory
        /// </summary>
        protected TaskInventoryDictionary m_items = new TaskInventoryDictionary();

        /// <summary>
        /// Tracks whether inventory has changed since the last persistent backup
        /// </summary>
        internal bool HasInventoryChanged;

        /// <value>
        /// Inventory serial number
        /// </value>
        protected internal uint Serial
        {
            get { return m_inventorySerial; }
            set { m_inventorySerial = value; }
        }

        /// <value>
        /// Raw inventory data
        /// </value>
        protected internal TaskInventoryDictionary Items
        {
            get {
                return m_items;
            }
            set
            {
                m_items = value;
                m_inventorySerial++;
                QueryScriptStates();
            }
        }

        public int Count
        {
            get
            {
                lock (m_items)
                    return m_items.Count;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="part">
        /// A <see cref="SceneObjectPart"/>
        /// </param>
        public SceneObjectPartInventory(SceneObjectPart part)
        {
            m_part = part;
        }

        /// <summary>
        /// Force the task inventory of this prim to persist at the next update sweep
        /// </summary>
        public void ForceInventoryPersistence()
        {
            HasInventoryChanged = true;
        }

        /// <summary>
        /// Reset UUIDs for all the items in the prim's inventory.
        /// </summary>
        /// <remarks>
        /// This involves either generating
        /// new ones or setting existing UUIDs to the correct parent UUIDs.
        ///
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// </remarks>
        public void ResetInventoryIDs()
        {
            if (null == m_part)
            m_items.LockItemsForWrite(true);

            if (Items.Count == 0)
            {
                m_items.LockItemsForWrite(false);
                return;
            }

            IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
            Items.Clear();

            foreach (TaskInventoryItem item in items)
            {
                item.ResetIDs(m_part.UUID);
                Items.Add(item.ItemID, item);
            }
            m_items.LockItemsForWrite(false);
        }

        public void ResetObjectID()
        {
            m_items.LockItemsForWrite(true);

            if (Items.Count == 0)
            {
                m_items.LockItemsForWrite(false);
                return;
            }

            IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
            Items.Clear();

            foreach (TaskInventoryItem item in items)
            {
                item.ParentPartID = m_part.UUID;
                item.ParentID = m_part.UUID;
                Items.Add(item.ItemID, item);
            }
            m_items.LockItemsForWrite(false);
        }

        /// <summary>
        /// Change every item in this inventory to a new owner.
        /// </summary>
        /// <param name="ownerId"></param>
        public void ChangeInventoryOwner(UUID ownerId)
        {
            List<TaskInventoryItem> items = GetInventoryItems();

            if (items.Count == 0)
                return;

            m_items.LockItemsForWrite(true);
            HasInventoryChanged = true;
            m_part.ParentGroup.HasGroupChanged = true;
            foreach (TaskInventoryItem item in items)
            {
                if (ownerId != item.OwnerID)
                    item.LastOwnerID = item.OwnerID;

                item.OwnerID = ownerId;
                item.PermsMask = 0;
                item.PermsGranter = UUID.Zero;
                item.OwnerChanged = true;
            }
            m_items.LockItemsForWrite(false);
        }

        /// <summary>
        /// Change every item in this inventory to a new group.
        /// </summary>
        /// <param name="groupID"></param>
        public void ChangeInventoryGroup(UUID groupID)
        {
            m_items.LockItemsForWrite(true);
            if (0 == Items.Count)
            {
                m_items.LockItemsForWrite(false);
                return;
            }

            // Don't let this set the HasGroupChanged flag for attachments
            // as this happens during rez and we don't want a new asset
            // for each attachment each time
            if (!m_part.ParentGroup.IsAttachment)
            {
                HasInventoryChanged = true;
                m_part.ParentGroup.HasGroupChanged = true;
            }

            IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
            foreach (TaskInventoryItem item in items)
            {
                if (groupID != item.GroupID)
                {
                    item.GroupID = groupID;
                }
            }
            m_items.LockItemsForWrite(false);
        }

        private void QueryScriptStates()
        {
            if (m_part == null || m_part.ParentGroup == null || m_part.ParentGroup.Scene == null)
                return;

            Items.LockItemsForRead(true);
            foreach (TaskInventoryItem item in Items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    bool running;
                    if (TryGetScriptInstanceRunning(m_part.ParentGroup.Scene, item, out running))
                        item.ScriptRunning = running;
                }
            }

            Items.LockItemsForRead(false);
        }

        public bool TryGetScriptInstanceRunning(UUID itemId, out bool running)
        {
            running = false;

            TaskInventoryItem item = GetInventoryItem(itemId);

            if (item == null)
                return false;

            return TryGetScriptInstanceRunning(m_part.ParentGroup.Scene, item, out running);
        }

        public static bool TryGetScriptInstanceRunning(Scene scene, TaskInventoryItem item, out bool running)
        {
            running = false;

            if (item.InvType != (int)InventoryType.LSL)
                return false;

            IScriptModule[] engines = scene.RequestModuleInterfaces<IScriptModule>();
            if (engines == null) // No engine at all
                return false;

            foreach (IScriptModule e in engines)
            {
                if (e.HasScript(item.ItemID, out running))
                    return true;
            }

            return false;
        }

        public int CreateScriptInstances(int startParam, bool postOnRez, string engine, int stateSource)
        {
            int scriptsValidForStarting = 0;

            List<TaskInventoryItem> scripts = GetInventoryItems(InventoryType.LSL);
            foreach (TaskInventoryItem item in scripts)
                if (CreateScriptInstance(item, startParam, postOnRez, engine, stateSource))
                    scriptsValidForStarting++;

            return scriptsValidForStarting;
        }

        public ArrayList GetScriptErrors(UUID itemID)
        {
            ArrayList ret = new ArrayList();

            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();

            foreach (IScriptModule e in engines)
            {
                if (e != null)
                {
                    ArrayList errors = e.GetScriptErrors(itemID);
                    foreach (Object line in errors)
                        ret.Add(line);
                }
            }

            return ret;
        }

        /// <summary>
        /// Stop and remove all the scripts in this prim.
        /// </summary>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if these scripts are being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        public void RemoveScriptInstances(bool sceneObjectBeingDeleted)
        {
            List<TaskInventoryItem> scripts = GetInventoryItems(InventoryType.LSL);
            foreach (TaskInventoryItem item in scripts)
            {
                RemoveScriptInstance(item.ItemID, sceneObjectBeingDeleted);
                m_part.RemoveScriptEvents(item.ItemID);
            }
        }

        /// <summary>
        /// Stop all the scripts in this prim.
        /// </summary>
        public void StopScriptInstances()
        {
            GetInventoryItems(InventoryType.LSL).ForEach(i => StopScriptInstance(i));
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the script instance was created, false otherwise</returns>
        public bool CreateScriptInstance(TaskInventoryItem item, int startParam, bool postOnRez, string engine, int stateSource)
        {
//             m_log.DebugFormat("[PRIM INVENTORY]: Starting script {0} {1} in prim {2} {3} in {4}",
//                 item.Name, item.ItemID, m_part.Name, m_part.UUID, m_part.ParentGroup.Scene.RegionInfo.RegionName);

            if (!m_part.ParentGroup.Scene.Permissions.CanRunScript(item, m_part))
            {
                StoreScriptError(item.ItemID, "no permission");
                return false;
            }

            m_part.AddFlag(PrimFlags.Scripted);

            if (m_part.ParentGroup.Scene.RegionInfo.RegionSettings.DisableScripts)
                return false;

            if (stateSource == 2 && // Prim crossing
                    m_part.ParentGroup.Scene.m_trustBinaries)
            {
                m_items.LockItemsForWrite(true);
                m_items[item.ItemID].PermsMask = 0;
                m_items[item.ItemID].PermsGranter = UUID.Zero;
                m_items.LockItemsForWrite(false);
                m_part.ParentGroup.Scene.EventManager.TriggerRezScript(
                    m_part.LocalId, item.ItemID, String.Empty, startParam, postOnRez, engine, stateSource);
                StoreScriptErrors(item.ItemID, null);
                m_part.ParentGroup.AddActiveScriptCount(1);
                m_part.ScheduleFullUpdate();
                return true;
            }

            AssetBase asset = m_part.ParentGroup.Scene.AssetService.Get(item.AssetID.ToString());
            if (null == asset)
            {
                string msg = String.Format("asset ID {0} could not be found", item.AssetID);
                StoreScriptError(item.ItemID, msg);
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Couldn't start script {0}, {1} at {2} in {3} since asset ID {4} could not be found",
                    item.Name, item.ItemID, m_part.AbsolutePosition,
                    m_part.ParentGroup.Scene.RegionInfo.RegionName, item.AssetID);

                return false;
            }
            else
            {
                if (m_part.ParentGroup.m_savedScriptState != null)
                    item.OldItemID = RestoreSavedScriptState(item.LoadedItemID, item.OldItemID, item.ItemID);

                m_items.LockItemsForWrite(true);

                m_items[item.ItemID].OldItemID = item.OldItemID;
                m_items[item.ItemID].PermsMask = 0;
                m_items[item.ItemID].PermsGranter = UUID.Zero;

                m_items.LockItemsForWrite(false);

                string script = Utils.BytesToString(asset.Data);
                m_part.ParentGroup.Scene.EventManager.TriggerRezScript(
                    m_part.LocalId, item.ItemID, script, startParam, postOnRez, engine, stateSource);
                StoreScriptErrors(item.ItemID, null);
                if (!item.ScriptRunning)
                    m_part.ParentGroup.Scene.EventManager.TriggerStopScript(
                        m_part.LocalId, item.ItemID);
                m_part.ParentGroup.AddActiveScriptCount(1);
                m_part.ScheduleFullUpdate();

                return true;
            }
        }

        private UUID RestoreSavedScriptState(UUID loadedID, UUID oldID, UUID newID)
        {
//            m_log.DebugFormat(
//                "[PRIM INVENTORY]: Restoring scripted state for item {0}, oldID {1}, loadedID {2}",
//                newID, oldID, loadedID);

            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();
            if (engines.Length == 0) // No engine at all
                return oldID;

            UUID stateID = oldID;
            if (!m_part.ParentGroup.m_savedScriptState.ContainsKey(oldID))
                stateID = loadedID;
            if (m_part.ParentGroup.m_savedScriptState.ContainsKey(stateID))
            {
                XmlDocument doc = new XmlDocument();

                doc.LoadXml(m_part.ParentGroup.m_savedScriptState[stateID]);

                ////////// CRUFT WARNING ///////////////////////////////////
                //
                // Old objects will have <ScriptState><State> ...
                // This format is XEngine ONLY
                //
                // New objects have <State Engine="...." ...><ScriptState>...
                // This can be passed to any engine
                //
                XmlNode n = doc.SelectSingleNode("ScriptState");
                if (n != null) // Old format data
                {
                    XmlDocument newDoc = new XmlDocument();

                    XmlElement rootN = newDoc.CreateElement("", "State", "");
                    XmlAttribute uuidA = newDoc.CreateAttribute("", "UUID", "");
                    uuidA.Value = stateID.ToString();
                    rootN.Attributes.Append(uuidA);
                    XmlAttribute engineA = newDoc.CreateAttribute("", "Engine", "");
                    engineA.Value = "XEngine";
                    rootN.Attributes.Append(engineA);

                    newDoc.AppendChild(rootN);

                    XmlNode stateN = newDoc.ImportNode(n, true);
                    rootN.AppendChild(stateN);

                    // This created document has only the minimun data
                    // necessary for XEngine to parse it successfully

//                    m_log.DebugFormat("[PRIM INVENTORY]: Adding legacy state {0} in {1}", stateID, newID);

                    m_part.ParentGroup.m_savedScriptState[stateID] = newDoc.OuterXml;
                }

                foreach (IScriptModule e in engines)
                {
                    if (e != null)
                    {
                        if (e.SetXMLState(newID, m_part.ParentGroup.m_savedScriptState[stateID]))
                            break;
                    }
                }

                m_part.ParentGroup.m_savedScriptState.Remove(stateID);
            }

            return stateID;
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// Some processing may occur in the background, but this routine returns asap.
        /// </summary>
        /// <param name="itemId">
        /// A <see cref="UUID"/>
        /// </param>
        public bool CreateScriptInstance(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource)
        {
            lock (m_scriptErrors)
            {
                // Indicate to CreateScriptInstanceInternal() we don't want it to wait for completion
                m_scriptErrors.Remove(itemId);
            }
            CreateScriptInstanceInternal(itemId, startParam, postOnRez, engine, stateSource);
            return true;
        }

        private void CreateScriptInstanceInternal(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource)
        {
            m_items.LockItemsForRead(true);

            if (m_items.ContainsKey(itemId))
            {
                TaskInventoryItem it = m_items[itemId];
                m_items.LockItemsForRead(false);

                CreateScriptInstance(it, startParam, postOnRez, engine, stateSource);
            }
            else
            {
                m_items.LockItemsForRead(false);
                string msg = String.Format("couldn't be found for prim {0}, {1} at {2} in {3}", m_part.Name, m_part.UUID,
                    m_part.AbsolutePosition, m_part.ParentGroup.Scene.RegionInfo.RegionName);
                StoreScriptError(itemId, msg);
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't start script with ID {0} since it {1}", itemId, msg);
            }
        }

        /// <summary>
        /// Start a script which is in this prim's inventory and return any compilation error messages.
        /// </summary>
        /// <param name="itemId">
        /// A <see cref="UUID"/>
        /// </param>
        public ArrayList CreateScriptInstanceEr(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource)
        {
            ArrayList errors;

            // Indicate to CreateScriptInstanceInternal() we want it to
            // post any compilation/loading error messages
            lock (m_scriptErrors)
            {
                m_scriptErrors[itemId] = null;
            }

            // Perform compilation/loading
            CreateScriptInstanceInternal(itemId, startParam, postOnRez, engine, stateSource);

            // Wait for and retrieve any errors
            lock (m_scriptErrors)
            {
                while ((errors = m_scriptErrors[itemId]) == null)
                {
                    if (!System.Threading.Monitor.Wait(m_scriptErrors, 15000))
                    {
                        m_log.ErrorFormat(
                            "[PRIM INVENTORY]: " +
                            "timedout waiting for script {0} errors", itemId);
                        errors = m_scriptErrors[itemId];
                        if (errors == null)
                        {
                            errors = new ArrayList(1);
                            errors.Add("timedout waiting for errors");
                        }
                        break;
                    }
                }
                m_scriptErrors.Remove(itemId);
            }
            return errors;
        }

        // Signal to CreateScriptInstanceEr() that compilation/loading is complete
        private void StoreScriptErrors(UUID itemId, ArrayList errors)
        {
            lock (m_scriptErrors)
            {
                // If compilation/loading initiated via CreateScriptInstance(),
                // it does not want the errors, so just get out
                if (!m_scriptErrors.ContainsKey(itemId))
                {
                    return;
                }

                // Initiated via CreateScriptInstanceEr(), if we know what the
                // errors are, save them and wake CreateScriptInstanceEr().
                if (errors != null)
                {
                    m_scriptErrors[itemId] = errors;
                    System.Threading.Monitor.PulseAll(m_scriptErrors);
                    return;
                }
            }

            // Initiated via CreateScriptInstanceEr() but we don't know what
            // the errors are yet, so retrieve them from the script engine.
            // This may involve some waiting internal to GetScriptErrors().
            errors = GetScriptErrors(itemId);

            // Get a default non-null value to indicate success.
            if (errors == null)
            {
                errors = new ArrayList();
            }

            // Post to CreateScriptInstanceEr() and wake it up
            lock (m_scriptErrors)
            {
                m_scriptErrors[itemId] = errors;
                System.Threading.Monitor.PulseAll(m_scriptErrors);
            }
        }

        // Like StoreScriptErrors(), but just posts a single string message
        private void StoreScriptError(UUID itemId, string message)
        {
            ArrayList errors = new ArrayList(1);
            errors.Add(message);
            StoreScriptErrors(itemId, errors);
        }

        /// <summary>
        /// Stop and remove a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if this script is being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        public void RemoveScriptInstance(UUID itemId, bool sceneObjectBeingDeleted)
        {
            if (m_items.ContainsKey(itemId))
            {
                if (!sceneObjectBeingDeleted)
                    m_part.RemoveScriptEvents(itemId);

                m_part.ParentGroup.Scene.EventManager.TriggerRemoveScript(m_part.LocalId, itemId);
                m_part.ParentGroup.AddActiveScriptCount(-1);
            }
            else
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't stop script with ID {0} since it couldn't be found for prim {1}, {2} at {3} in {4}",
                    itemId, m_part.Name, m_part.UUID,
                    m_part.AbsolutePosition, m_part.ParentGroup.Scene.RegionInfo.RegionName);
            }
        }

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if this script is being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        public void StopScriptInstance(UUID itemId)
        {
            TaskInventoryItem scriptItem;

            lock (m_items)
                m_items.TryGetValue(itemId, out scriptItem);

            if (scriptItem != null)
            {
                StopScriptInstance(scriptItem);
            }
            else
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't stop script with ID {0} since it couldn't be found for prim {1}, {2} at {3} in {4}",
                    itemId, m_part.Name, m_part.UUID,
                    m_part.AbsolutePosition, m_part.ParentGroup.Scene.RegionInfo.RegionName);
            }
        }

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if this script is being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        public void StopScriptInstance(TaskInventoryItem item)
        {
            if (m_part.ParentGroup.Scene != null)
                m_part.ParentGroup.Scene.EventManager.TriggerStopScript(m_part.LocalId, item.ItemID);

            // At the moment, even stopped scripts are counted as active, which is probably wrong.
//            m_part.ParentGroup.AddActiveScriptCount(-1);
        }

        /// <summary>
        /// Check if the inventory holds an item with a given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool InventoryContainsName(string name)
        {
            m_items.LockItemsForRead(true);
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.Name == name)
                {
                    m_items.LockItemsForRead(false);
                    return true;
                }
            }
            m_items.LockItemsForRead(false);
            return false;
        }

        /// <summary>
        /// For a given item name, return that name if it is available.  Otherwise, return the next available
        /// similar name (which is currently the original name with the next available numeric suffix).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string FindAvailableInventoryName(string name)
        {
            if (!InventoryContainsName(name))
                return name;

            int suffix=1;
            while (suffix < 256)
            {
                string tryName=String.Format("{0} {1}", name, suffix);
                if (!InventoryContainsName(tryName))
                    return tryName;
                suffix++;
            }
            return String.Empty;
        }

        /// <summary>
        /// Add an item to this prim's inventory.  If an item with the same name already exists, then an alternative
        /// name is chosen.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItem(TaskInventoryItem item, bool allowedDrop)
        {
            AddInventoryItem(item.Name, item, allowedDrop);
        }

        /// <summary>
        /// Add an item to this prim's inventory.  If an item with the same name already exists, it is replaced.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItemExclusive(TaskInventoryItem item, bool allowedDrop)
        {
            m_items.LockItemsForRead(true);
            List<TaskInventoryItem> il = new List<TaskInventoryItem>(m_items.Values);
            m_items.LockItemsForRead(false);
            foreach (TaskInventoryItem i in il)
            {
                if (i.Name == item.Name)
                {
                    if (i.InvType == (int)InventoryType.LSL)
                        RemoveScriptInstance(i.ItemID, false);

                    RemoveInventoryItem(i.ItemID);
                    break;
                }
            }

            AddInventoryItem(item.Name, item, allowedDrop);
        }

        /// <summary>
        /// Add an item to this prim's inventory.
        /// </summary>
        /// <param name="name">The name that the new item should have.</param>
        /// <param name="item">
        /// The item itself.  The name within this structure is ignored in favour of the name
        /// given in this method's arguments
        /// </param>
        /// <param name="allowedDrop">
        /// Item was only added to inventory because AllowedDrop is set
        /// </param>
        protected void AddInventoryItem(string name, TaskInventoryItem item, bool allowedDrop)
        {
            name = FindAvailableInventoryName(name);
            if (name == String.Empty)
                return;

            item.ParentID = m_part.UUID;
            item.ParentPartID = m_part.UUID;
            item.Name = name;
            item.GroupID = m_part.GroupID;

            m_items.LockItemsForWrite(true);
            m_items.Add(item.ItemID, item);
            m_items.LockItemsForWrite(false);
                if (allowedDrop)
                    m_part.TriggerScriptChangedEvent(Changed.ALLOWED_DROP);
                else
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);

            m_part.AggregateInnerPerms();
            m_inventorySerial++;
            //m_inventorySerial += 2;
            HasInventoryChanged = true;
            m_part.ParentGroup.HasGroupChanged = true;
        }

        /// <summary>
        /// Restore a whole collection of items to the prim's inventory at once.
        /// We assume that the items already have all their fields correctly filled out.
        /// The items are not flagged for persistence to the database, since they are being restored
        /// from persistence rather than being newly added.
        /// </summary>
        /// <param name="items"></param>
        public void RestoreInventoryItems(ICollection<TaskInventoryItem> items)
        {
            m_items.LockItemsForWrite(true);
            foreach (TaskInventoryItem item in items)
            {
                m_items.Add(item.ItemID, item);
//                m_part.TriggerScriptChangedEvent(Changed.INVENTORY);
            }
            m_items.LockItemsForWrite(false);
            m_part.AggregateInnerPerms();
            m_inventorySerial++;
        }

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        public TaskInventoryItem GetInventoryItem(UUID itemId)
        {
            TaskInventoryItem item;
            m_items.LockItemsForRead(true);
            m_items.TryGetValue(itemId, out item);
            m_items.LockItemsForRead(false);
            return item;
        }

        public TaskInventoryItem GetInventoryItem(string name)
        {
            m_items.LockItemsForRead(true);
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.Name == name)
                {
                    m_items.LockItemsForRead(false);
                    return item;
                }
            }
            m_items.LockItemsForRead(false);

            return null;
        }

        public List<TaskInventoryItem> GetInventoryItems(string name)
        {
            List<TaskInventoryItem> items = new List<TaskInventoryItem>();

            m_items.LockItemsForRead(true);

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.Name == name)
                    items.Add(item);
            }

            m_items.LockItemsForRead(false);

            return items;
        }

        public bool GetRezReadySceneObjects(TaskInventoryItem item, out List<SceneObjectGroup> objlist, out List<Vector3> veclist, out Vector3 bbox, out float offsetHeight)
        {
            AssetBase rezAsset = m_part.ParentGroup.Scene.AssetService.Get(item.AssetID.ToString());

            if (null == rezAsset)
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: Could not find asset {0} for inventory item {1} in {2}",
                    item.AssetID, item.Name, m_part.Name);
                objlist = null;
                veclist = null;
                bbox = Vector3.Zero;
                offsetHeight = 0;
                return false;
            }

            bool single = m_part.ParentGroup.Scene.GetObjectsToRez(rezAsset.Data, false, out objlist, out veclist, out bbox, out offsetHeight);

            for (int i = 0; i < objlist.Count; i++)
            {
                SceneObjectGroup group = objlist[i];
/*
                group.RootPart.AttachPoint = group.RootPart.Shape.State;
                group.RootPart.AttachedPos = group.AbsolutePosition;
                group.RootPart.AttachRotation = group.GroupRotation;
*/
                group.ResetIDs();

                SceneObjectPart rootPart = group.GetPart(group.UUID);

                // Since renaming the item in the inventory does not affect the name stored
                // in the serialization, transfer the correct name from the inventory to the
                // object itself before we rez.
                // Only do these for the first object if we are rezzing a coalescence.
                // nahh dont mess with coalescence objects,
                // the name in inventory can be change for inventory purpuses only
                if (objlist.Count == 1)
                {
                    rootPart.Name = item.Name;
                    rootPart.Description = item.Description;
                }
/* reverted to old code till part.ApplyPermissionsOnRez is better reviewed/fixed
                group.SetGroup(m_part.GroupID, null);

                foreach (SceneObjectPart part in group.Parts)
                {
                    // Convert between InventoryItem classes. You can never have too many similar but slightly different classes :)
                    InventoryItemBase dest = new InventoryItemBase(item.ItemID, item.OwnerID);
                    dest.BasePermissions = item.BasePermissions;
                    dest.CurrentPermissions = item.CurrentPermissions;
                    dest.EveryOnePermissions = item.EveryonePermissions;
                    dest.GroupPermissions = item.GroupPermissions;
                    dest.NextPermissions = item.NextPermissions;
                    dest.Flags = item.Flags;

                    part.ApplyPermissionsOnRez(dest, false, m_part.ParentGroup.Scene);
                }
*/
// old code start
                SceneObjectPart[] partList = group.Parts;

                group.SetGroup(m_part.GroupID, null);

                if ((rootPart.OwnerID != item.OwnerID) || (item.CurrentPermissions & (uint)PermissionMask.Slam) != 0 || (item.Flags & (uint)InventoryItemFlags.ObjectSlamPerm) != 0)
                {
                    if (m_part.ParentGroup.Scene.Permissions.PropagatePermissions())
                    {
                        foreach (SceneObjectPart part in partList)
                        {
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteEveryone) != 0)
                                part.EveryoneMask = item.EveryonePermissions;
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteNextOwner) != 0)
                                part.NextOwnerMask = item.NextPermissions;
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteGroup) != 0)
                                part.GroupMask = item.GroupPermissions;
                        }

                        group.ApplyNextOwnerPermissions();
                    }
                }

                foreach (SceneObjectPart part in partList)
                {
                    if ((part.OwnerID != item.OwnerID) || (item.CurrentPermissions & (uint)PermissionMask.Slam) != 0 || (item.Flags & (uint)InventoryItemFlags.ObjectSlamPerm) != 0)
                    {
                        if(part.GroupID != part.OwnerID)
                            part.LastOwnerID = part.OwnerID;
                        part.OwnerID = item.OwnerID;
                        part.Inventory.ChangeInventoryOwner(item.OwnerID);
                    }

                    if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteEveryone) != 0)
                        part.EveryoneMask = item.EveryonePermissions;
                    if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteNextOwner) != 0)
                        part.NextOwnerMask = item.NextPermissions;
                    if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteGroup) != 0)
                        part.GroupMask = item.GroupPermissions;
                }
// old code end
                rootPart.TrimPermissions();
                group.AggregateDeepPerms();
            }

            return true;
        }

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory.</param>
        /// <returns>false if the item did not exist, true if the update occurred successfully</returns>
        public bool UpdateInventoryItem(TaskInventoryItem item)
        {
            return UpdateInventoryItem(item, true, true);
        }

        public bool UpdateInventoryItem(TaskInventoryItem item, bool fireScriptEvents)
        {
            return UpdateInventoryItem(item, fireScriptEvents, true);
        }

        public bool UpdateInventoryItem(TaskInventoryItem item, bool fireScriptEvents, bool considerChanged)
        {
            m_items.LockItemsForWrite(true);

            if (m_items.ContainsKey(item.ItemID))
            {
//                m_log.DebugFormat("[PRIM INVENTORY]: Updating item {0} in {1}", item.Name, m_part.Name);

                item.ParentID = m_part.UUID;
                item.ParentPartID = m_part.UUID;

                // If group permissions have been set on, check that the groupID is up to date in case it has
                // changed since permissions were last set.
                if (item.GroupPermissions != (uint)PermissionMask.None)
                    item.GroupID = m_part.GroupID;

                if (item.AssetID == UUID.Zero)
                    item.AssetID = m_items[item.ItemID].AssetID;

                m_items[item.ItemID] = item;

                m_inventorySerial++;
                if (fireScriptEvents)
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);

                if (considerChanged)
                {
                    m_part.AggregateInnerPerms();
                    m_part.ParentGroup.AggregatePerms();
                    HasInventoryChanged = true;
                    m_part.ParentGroup.HasGroupChanged = true;
                }
                m_items.LockItemsForWrite(false);

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Tried to retrieve item ID {0} from prim {1}, {2} at {3} in {4} but the item does not exist in this inventory",
                    item.ItemID, m_part.Name, m_part.UUID,
                    m_part.AbsolutePosition, m_part.ParentGroup.Scene.RegionInfo.RegionName);
            }
            m_items.LockItemsForWrite(false);

            return false;
        }

        /// <summary>
        /// Remove an item from this prim's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Numeric asset type of the item removed.  Returns -1 if the item did not exist
        /// in this prim's inventory.</returns>
        public int RemoveInventoryItem(UUID itemID)
        {
            m_items.LockItemsForRead(true);

            if (m_items.ContainsKey(itemID))
            {
                int type = m_items[itemID].InvType;
                m_items.LockItemsForRead(false);
                if (type == 10) // Script
                {
                    m_part.ParentGroup.Scene.EventManager.TriggerRemoveScript(m_part.LocalId, itemID);
                }
                m_items.LockItemsForWrite(true);
                m_items.Remove(itemID);
                m_items.LockItemsForWrite(false);

                m_part.AggregateInnerPerms();
                m_part.ParentGroup.AggregatePerms();

                m_inventorySerial++;
                m_part.TriggerScriptChangedEvent(Changed.INVENTORY);

                HasInventoryChanged = true;
                m_part.ParentGroup.HasGroupChanged = true;

                int scriptcount = 0;
                m_items.LockItemsForRead(true);
                foreach (TaskInventoryItem item in m_items.Values)
                {
                    if (item.Type == 10)
                    {
                        scriptcount++;
                    }
                }
                m_items.LockItemsForRead(false);


                if (scriptcount <= 0)
                {
                    m_part.RemFlag(PrimFlags.Scripted);
                }

                m_part.ScheduleFullUpdate();

                return type;
            }
            else
            {
                m_items.LockItemsForRead(false);
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Tried to remove item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                    itemID, m_part.Name, m_part.UUID);
            }

            return -1;
        }


        /// <summary>
        /// Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="xferManager"></param>
        public void RequestInventoryFile(IClientAPI client, IXfer xferManager)
        {
            lock (m_inventoryFileLock)
            {
                bool changed = false;

                Items.LockItemsForRead(true);

                if (m_inventorySerial == 0) // No inventory
                {
                    Items.LockItemsForRead(false);
                    client.SendTaskInventory(m_part.UUID, 0, new byte[0]);
                    return;
                }

                if (m_items.Count == 0) // No inventory
                {
                    Items.LockItemsForRead(false);
                    client.SendTaskInventory(m_part.UUID, 0, new byte[0]);
                    return;
                }

                if (m_inventoryFileNameSerial != m_inventorySerial)
                {
                    m_inventoryFileNameSerial = m_inventorySerial;
                    changed = true;
                }

                Items.LockItemsForRead(false);

                if (m_inventoryFileData.Length < 2)
                    changed = true;

                bool includeAssets = false;
                if (m_part.ParentGroup.Scene.Permissions.CanEditObjectInventory(m_part.UUID, client.AgentId))
                    includeAssets = true;

                if (m_inventoryPrivileged != includeAssets)
                    changed = true;

                if (!changed)
                {
                    xferManager.AddNewFile(m_inventoryFileName, m_inventoryFileData);
                    client.SendTaskInventory(m_part.UUID, (short)m_inventoryFileNameSerial,
                            m_inventoryFileNameBytes);

                    return;
                }

                m_inventoryPrivileged = includeAssets;

                InventoryStringBuilder invString = new InventoryStringBuilder(m_part.UUID, UUID.Zero);

                Items.LockItemsForRead(true);

                foreach (TaskInventoryItem item in m_items.Values)
                {
                    UUID ownerID = item.OwnerID;
                    uint everyoneMask = item.EveryonePermissions;
                    uint baseMask = item.BasePermissions;
                    uint ownerMask = item.CurrentPermissions;
                    uint groupMask = item.GroupPermissions;

                    invString.AddItemStart();
                    invString.AddNameValueLine("item_id", item.ItemID.ToString());
                    invString.AddNameValueLine("parent_id", m_part.UUID.ToString());

                    invString.AddPermissionsStart();

                    invString.AddNameValueLine("base_mask", Utils.UIntToHexString(baseMask));
                    invString.AddNameValueLine("owner_mask", Utils.UIntToHexString(ownerMask));
                    invString.AddNameValueLine("group_mask", Utils.UIntToHexString(groupMask));
                    invString.AddNameValueLine("everyone_mask", Utils.UIntToHexString(everyoneMask));
                    invString.AddNameValueLine("next_owner_mask", Utils.UIntToHexString(item.NextPermissions));

                    invString.AddNameValueLine("creator_id", item.CreatorID.ToString());
                    invString.AddNameValueLine("owner_id", ownerID.ToString());

                    invString.AddNameValueLine("last_owner_id", item.LastOwnerID.ToString());

                    invString.AddNameValueLine("group_id", item.GroupID.ToString());
                    invString.AddSectionEnd();

                    if (includeAssets)
                        invString.AddNameValueLine("asset_id", item.AssetID.ToString());
                    else
                        invString.AddNameValueLine("asset_id", UUID.Zero.ToString());
                    invString.AddNameValueLine("type", Utils.AssetTypeToString((AssetType)item.Type));
                    invString.AddNameValueLine("inv_type", Utils.InventoryTypeToString((InventoryType)item.InvType));
                    invString.AddNameValueLine("flags", Utils.UIntToHexString(item.Flags));

                    invString.AddSaleStart();
                    invString.AddNameValueLine("sale_type", "not");
                    invString.AddNameValueLine("sale_price", "0");
                    invString.AddSectionEnd();

                    invString.AddNameValueLine("name", item.Name + "|");
                    invString.AddNameValueLine("desc", item.Description + "|");

                    invString.AddNameValueLine("creation_date", item.CreationDate.ToString());
                    invString.AddSectionEnd();
                }

                Items.LockItemsForRead(false);

                m_inventoryFileData = Utils.StringToBytes(invString.GetString());

                if (m_inventoryFileData.Length > 2)
                {
                    m_inventoryFileName = "inventory_" + UUID.Random().ToString() + ".tmp";
                    m_inventoryFileNameBytes = Util.StringToBytes256(m_inventoryFileName);
                    xferManager.AddNewFile(m_inventoryFileName, m_inventoryFileData);
                    client.SendTaskInventory(m_part.UUID, (short)m_inventoryFileNameSerial,m_inventoryFileNameBytes);
                    return;
                }

                client.SendTaskInventory(m_part.UUID, 0, new byte[0]);
            }
        }

        /// <summary>
        /// Process inventory backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessInventoryBackup(ISimulationDataService datastore)
        {
// Removed this because linking will cause an immediate delete of the new
// child prim from the database and the subsequent storing of the prim sees
// the inventory of it as unchanged and doesn't store it at all. The overhead
// of storing prim inventory needlessly is much less than the aggravation
// of prim inventory loss.
//            if (HasInventoryChanged)
//            {
                Items.LockItemsForRead(true);
                ICollection<TaskInventoryItem> itemsvalues = Items.Values;
                HasInventoryChanged = false;
                Items.LockItemsForRead(false);
                try
                {
                    datastore.StorePrimInventory(m_part.UUID, itemsvalues);
                }
                catch {}
//            }
        }

        public class InventoryStringBuilder
        {
            private StringBuilder BuildString = new StringBuilder(1024);

            public InventoryStringBuilder(UUID folderID, UUID parentID)
            {
                BuildString.Append("\tinv_object\t0\n\t{\n");
                AddNameValueLine("obj_id", folderID.ToString());
                AddNameValueLine("parent_id", parentID.ToString());
                AddNameValueLine("type", "category");
                AddNameValueLine("name", "Contents|\n\t}");
            }

            public void AddItemStart()
            {
                BuildString.Append("\tinv_item\t0\n\t{\n");
            }

            public void AddPermissionsStart()
            {
                BuildString.Append("\tpermissions 0\n\t{\n");
            }

            public void AddSaleStart()
            {
                BuildString.Append("\tsale_info\t0\n\t{\n");
            }

            protected void AddSectionStart()
            {
                BuildString.Append("\t{\n");
            }

            public void AddSectionEnd()
            {
                BuildString.Append("\t}\n");
            }

            public void AddLine(string addLine)
            {
                BuildString.Append(addLine);
            }

            public void AddNameValueLine(string name, string value)
            {
                BuildString.Append("\t\t");
                BuildString.Append(name);
                BuildString.Append("\t");
                BuildString.Append(value);
                BuildString.Append("\n");
            }

            public String GetString()
            {
                return BuildString.ToString();
            }

            public void Close()
            {
            }
        }

        public void AggregateInnerPerms(ref uint owner, ref uint group, ref uint everyone)
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                owner &= item.CurrentPermissions;
                group &= item.GroupPermissions;
                everyone &= item.EveryonePermissions;
            }
        }

        public uint MaskEffectivePermissions()
        {
            uint mask=0x7fffffff;

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Copy) == 0)
                    mask &= ~((uint)PermissionMask.Copy >> 13);
                if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Transfer) == 0)
                    mask &= ~((uint)PermissionMask.Transfer >> 13);
                if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Modify) == 0)
                    mask &= ~((uint)PermissionMask.Modify >> 13);

                if (item.InvType == (int)InventoryType.Object)
                {
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Copy >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Copy >> 13);
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Transfer >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Transfer >> 13);
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Modify >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Modify >> 13);
                }

                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    mask &= ~(uint)PermissionMask.Copy;
                if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                    mask &= ~(uint)PermissionMask.Transfer;
                if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    mask &= ~(uint)PermissionMask.Modify;
            }
            return mask;
        }

        public void ApplyNextOwnerPermissions()
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.Object && (item.CurrentPermissions & 7) != 0)
                {
//                    m_log.DebugFormat (
//                        "[SCENE OBJECT PART INVENTORY]: Applying next permissions {0} to {1} in {2} with current {3}, base {4}, everyone {5}",
//                        item.NextPermissions, item.Name, m_part.Name, item.CurrentPermissions, item.BasePermissions, item.EveryonePermissions);

                    if ((item.CurrentPermissions & ((uint)PermissionMask.Copy >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Copy;
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Transfer >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Transfer;
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Modify >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Modify;
                }
                item.CurrentPermissions &= item.NextPermissions;
                item.BasePermissions &= item.NextPermissions;
                item.EveryonePermissions &= item.NextPermissions;
                item.OwnerChanged = true;
                item.PermsMask = 0;
                item.PermsGranter = UUID.Zero;
            }
        }

        public void ApplyGodPermissions(uint perms)
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                item.CurrentPermissions = perms;
                item.BasePermissions = perms;
            }

            m_inventorySerial++;
            HasInventoryChanged = true;
        }

        /// <summary>
        /// Returns true if this part inventory contains any scripts.  False otherwise.
        /// </summary>
        /// <returns></returns>
        public bool ContainsScripts()
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the count of scripts in this parts inventory.
        /// </summary>
        /// <returns></returns>
        public int ScriptCount()
        {
            int count = 0;
            Items.LockItemsForRead(true);
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    count++;
                }
            }
            Items.LockItemsForRead(false);
            return count;
        }
        /// <summary>
        /// Returns the count of running scripts in this parts inventory.
        /// </summary>
        /// <returns></returns>
        public int RunningScriptCount()
        {
            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();
            if (engines.Length == 0)
                return 0;

            int count = 0;
            List<TaskInventoryItem> scripts = GetInventoryItems(InventoryType.LSL);

            foreach (TaskInventoryItem item in scripts)
            {
                foreach (IScriptModule engine in engines)
                {
                    if (engine != null)
                    {
                        if (engine.GetScriptState(item.ItemID))
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        public List<UUID> GetInventoryList()
        {
            List<UUID> ret = new List<UUID>();

            foreach (TaskInventoryItem item in m_items.Values)
                ret.Add(item.ItemID);

            return ret;
        }

        public List<TaskInventoryItem> GetInventoryItems()
        {
            List<TaskInventoryItem> ret = new List<TaskInventoryItem>();

            Items.LockItemsForRead(true);
            ret = new List<TaskInventoryItem>(m_items.Values);
            Items.LockItemsForRead(false);

            return ret;
        }

        public List<TaskInventoryItem> GetInventoryItems(InventoryType type)
        {
            List<TaskInventoryItem> ret = new List<TaskInventoryItem>();

            Items.LockItemsForRead(true);

            foreach (TaskInventoryItem item in m_items.Values)
                if (item.InvType == (int)type)
                    ret.Add(item);

            Items.LockItemsForRead(false);

            return ret;
        }

        public Dictionary<UUID, string> GetScriptStates()
        {
            return GetScriptStates(false);
        }

        public Dictionary<UUID, string> GetScriptStates(bool oldIDs)
        {
            Dictionary<UUID, string> ret = new Dictionary<UUID, string>();

            if (m_part.ParentGroup.Scene == null) // Group not in a scene
                return ret;

            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();

            if (engines.Length == 0) // No engine at all
                return ret;

            List<TaskInventoryItem> scripts = GetInventoryItems(InventoryType.LSL);

            foreach (TaskInventoryItem item in scripts)
            {
                foreach (IScriptModule e in engines)
                {
                    if (e != null)
                    {
//                        m_log.DebugFormat(
//                            "[PRIM INVENTORY]: Getting script state from engine {0} for {1} in part {2} in group {3} in {4}",
//                            e.Name, item.Name, m_part.Name, m_part.ParentGroup.Name, m_part.ParentGroup.Scene.Name);

                        string n = e.GetXMLState(item.ItemID);
                        if (n != String.Empty)
                        {
                            if (oldIDs)
                            {
                                if (!ret.ContainsKey(item.OldItemID))
                                    ret[item.OldItemID] = n;
                            }
                            else
                            {
                                if (!ret.ContainsKey(item.ItemID))
                                    ret[item.ItemID] = n;
                            }
                            break;
                        }
                    }
                }
            }
            return ret;
        }

        public void ResumeScripts()
        {
            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();
            if (engines.Length == 0)
                return;

            List<TaskInventoryItem> scripts = GetInventoryItems(InventoryType.LSL);

            foreach (TaskInventoryItem item in scripts)
            {
                foreach (IScriptModule engine in engines)
                {
                    if (engine != null)
                    {
//                        m_log.DebugFormat(
//                            "[PRIM INVENTORY]: Resuming script {0} {1} for {2}, OwnerChanged {3}",
//                            item.Name, item.ItemID, item.OwnerID, item.OwnerChanged);

                        engine.ResumeScript(item.ItemID);

                        if (item.OwnerChanged)
                            engine.PostScriptEvent(item.ItemID, "changed", new Object[] { (int)Changed.OWNER });

                        item.OwnerChanged = false;
                    }
                }
            }
        }
    }
}
