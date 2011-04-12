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
using System.Net;
using System.Xml;
using System.Reflection;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess
{
    public class BasicInventoryAccessModule : INonSharedRegionModule, IInventoryAccessModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled = false;
        protected Scene m_Scene;
        protected IUserManagement m_UserManagement;
        protected IUserManagement UserManagementModule
        {
            get
            {
                if (m_UserManagement == null)
                    m_UserManagement = m_Scene.RequestModuleInterface<IUserManagement>();
                return m_UserManagement;
            }
        }


        #region INonSharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "BasicInventoryAccessModule"; }
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryAccessModule", "");
                if (name == Name)
                {
                    m_Enabled = true;
                    m_log.InfoFormat("[INVENTORY ACCESS MODULE]: {0} enabled.", Name);
                }
            }
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scene = scene;

            scene.RegisterModuleInterface<IInventoryAccessModule>(this);
            scene.EventManager.OnNewClient += OnNewClient;
        }

        protected virtual void OnNewClient(IClientAPI client)
        {
            
        }

        public virtual void Close()
        {
            if (!m_Enabled)
                return;
        }


        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
            m_Scene = null;
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

        }

        #endregion

        #region Inventory Access

        /// <summary>
        /// Capability originating call to update the asset of an item in an agent's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
            item = m_Scene.InventoryService.GetItem(item);

            if (item.Owner != remoteClient.AgentId)
                return UUID.Zero;

            if (item != null)
            {
                if ((InventoryType)item.InvType == InventoryType.Notecard)
                {
                    if (!m_Scene.Permissions.CanEditNotecard(itemID, UUID.Zero, remoteClient.AgentId))
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit notecard", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAgentAlertMessage("Notecard saved", false);
                }
                else if ((InventoryType)item.InvType == InventoryType.LSL)
                {
                    if (!m_Scene.Permissions.CanEditScript(itemID, UUID.Zero, remoteClient.AgentId))
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAgentAlertMessage("Script saved", false);
                }

                AssetBase asset =
                    CreateAsset(item.Name, item.Description, (sbyte)item.AssetType, data, remoteClient.AgentId.ToString());
                item.AssetID = asset.FullID;
                m_Scene.AssetService.Store(asset);

                m_Scene.InventoryService.UpdateItem(item);

                // remoteClient.SendInventoryItemCreateUpdate(item);
                return (asset.FullID);
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find item {0} for caps inventory update",
                    itemID);
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Delete a scene object from a scene and place in the given avatar's inventory.
        /// Returns the UUID of the newly created asset.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="folderID"></param>
        /// <param name="objectGroup"></param>
        /// <param name="remoteClient"> </param>
        public virtual UUID DeleteToInventory(DeRezAction action, UUID folderID,
                List<SceneObjectGroup> objectGroups, IClientAPI remoteClient)
        {
            UUID ret = UUID.Zero; 

            // The following code groups the SOG's by owner. No objects
            // belonging to different people can be coalesced, for obvious
            // reasons.
            Dictionary<UUID, List<SceneObjectGroup>> deletes =
                    new Dictionary<UUID, List<SceneObjectGroup>>();

            foreach (SceneObjectGroup g in objectGroups)
            {
                if (!deletes.ContainsKey(g.OwnerID))
                    deletes[g.OwnerID] = new List<SceneObjectGroup>();

                deletes[g.OwnerID].Add(g);
            }

            // This is pethod scoped and will be returned. It will be the
            // last created asset id
            UUID assetID = UUID.Zero;

            // Each iteration is really a separate asset being created,
            // with distinct destinations as well.
            foreach (List<SceneObjectGroup> objlist in deletes.Values)
            {
                Dictionary<UUID, string> xmlStrings =
                        new Dictionary<UUID, string>();

                foreach (SceneObjectGroup objectGroup in objlist)
                {
                    Vector3 inventoryStoredPosition = new Vector3
                                (((objectGroup.AbsolutePosition.X > (int)Constants.RegionSize)
                                      ? 250
                                      : objectGroup.AbsolutePosition.X)
                                 ,
                                 (objectGroup.AbsolutePosition.X > (int)Constants.RegionSize)
                                     ? 250
                                     : objectGroup.AbsolutePosition.X,
                                 objectGroup.AbsolutePosition.Z);

                    Vector3 originalPosition = objectGroup.AbsolutePosition;

                    objectGroup.AbsolutePosition = inventoryStoredPosition;

                    // Make sure all bits but the ones we want are clear
                    // on take.
                    // This will be applied to the current perms, so
                    // it will do what we want.
                    objectGroup.RootPart.NextOwnerMask &=
                            ((uint)PermissionMask.Copy |
                             (uint)PermissionMask.Transfer |
                             (uint)PermissionMask.Modify);
                    objectGroup.RootPart.NextOwnerMask |=
                            (uint)PermissionMask.Move;

                    string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(objectGroup);

                    objectGroup.AbsolutePosition = originalPosition;

                    xmlStrings[objectGroup.UUID] = sceneObjectXml;
                }

                string itemXml;

                if (objlist.Count > 1)
                {
                    float minX, minY, minZ;
                    float maxX, maxY, maxZ;

                    Vector3[] offsets = m_Scene.GetCombinedBoundingBox(objlist,
                            out minX, out maxX, out minY, out maxY,
                            out minZ, out maxZ);

                    // CreateWrapper
                    XmlDocument itemDoc = new XmlDocument();
                    XmlElement root = itemDoc.CreateElement("", "CoalescedObject", "");
                    itemDoc.AppendChild(root);

                    // Embed the offsets into the group XML
                    for ( int i = 0 ; i < objlist.Count ; i++ )
                    {
                        XmlDocument doc = new XmlDocument();
                        SceneObjectGroup g = objlist[i];
                        doc.LoadXml(xmlStrings[g.UUID]);
                        XmlElement e = (XmlElement)doc.SelectSingleNode("/SceneObjectGroup");
                        e.SetAttribute("offsetx", offsets[i].X.ToString());
                        e.SetAttribute("offsety", offsets[i].Y.ToString());
                        e.SetAttribute("offsetz", offsets[i].Z.ToString());

                        XmlNode objectNode = itemDoc.ImportNode(e, true);
                        root.AppendChild(objectNode);
                    }

                    float sizeX = maxX - minX;
                    float sizeY = maxY - minY;
                    float sizeZ = maxZ - minZ;

                    root.SetAttribute("x", sizeX.ToString());
                    root.SetAttribute("y", sizeY.ToString());
                    root.SetAttribute("z", sizeZ.ToString());

                    itemXml = itemDoc.InnerXml;
                }
                else
                {
                    itemXml = xmlStrings[objlist[0].UUID];
                }

                // Get the user info of the item destination
                //
                UUID userID = UUID.Zero;

                if (action == DeRezAction.Take || action == DeRezAction.TakeCopy ||
                    action == DeRezAction.SaveToExistingUserInventoryItem)
                {
                    // Take or take copy require a taker
                    // Saving changes requires a local user
                    //
                    if (remoteClient == null)
                        return UUID.Zero;

                    userID = remoteClient.AgentId;
                }
                else
                {
                    // All returns / deletes go to the object owner
                    //

                    userID = objlist[0].RootPart.OwnerID;
                }

                if (userID == UUID.Zero) // Can't proceed
                {
                    return UUID.Zero;
                }

                // If we're returning someone's item, it goes back to the
                // owner's Lost And Found folder.
                // Delete is treated like return in this case
                // Deleting your own items makes them go to trash
                //

                InventoryFolderBase folder = null;
                InventoryItemBase item = null;

                if (DeRezAction.SaveToExistingUserInventoryItem == action)
                {
                    item = new InventoryItemBase(objlist[0].RootPart.FromUserInventoryItemID, userID);
                    item = m_Scene.InventoryService.GetItem(item);

                    //item = userInfo.RootFolder.FindItem(
                    //        objectGroup.RootPart.FromUserInventoryItemID);

                    if (null == item)
                    {
                        m_log.DebugFormat(
                            "[AGENT INVENTORY]: Object {0} {1} scheduled for save to inventory has already been deleted.",
                            objlist[0].Name, objlist[0].UUID);
                        return UUID.Zero;
                    }
                }
                else
                {
                    // Folder magic
                    //
                    if (action == DeRezAction.Delete)
                    {
                        // Deleting someone else's item
                        //
                        if (remoteClient == null ||
                            objlist[0].OwnerID != remoteClient.AgentId)
                        {

                            folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.LostAndFoundFolder);
                        }
                        else
                        {
                             folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.TrashFolder);
                        }
                    }
                    else if (action == DeRezAction.Return)
                    {

                        // Dump to lost + found unconditionally
                        //
                        folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.LostAndFoundFolder);
                    }

                    if (folderID == UUID.Zero && folder == null)
                    {
                        if (action == DeRezAction.Delete)
                        {
                            // Deletes go to trash by default
                            //
                            folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.TrashFolder);
                        }
                        else
                        {
                            if (remoteClient == null ||
                                objlist[0].OwnerID != remoteClient.AgentId)
                            {
                                // Taking copy of another person's item. Take to
                                // Objects folder.
                                folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.Object);
                            }
                            else
                            {
                                // Catch all. Use lost & found
                                //

                                folder = m_Scene.InventoryService.GetFolderForType(userID, AssetType.LostAndFoundFolder);
                            }
                        }
                    }

                    // Override and put into where it came from, if it came
                    // from anywhere in inventory
                    //
                    if (action == DeRezAction.Take || action == DeRezAction.TakeCopy)
                    {
                        if (objlist[0].RootPart.FromFolderID != UUID.Zero)
                        {
                            InventoryFolderBase f = new InventoryFolderBase(objlist[0].RootPart.FromFolderID, userID);
                            folder = m_Scene.InventoryService.GetFolder(f);
                        }
                    }

                    if (folder == null) // None of the above
                    {
                        folder = new InventoryFolderBase(folderID);

                        if (folder == null) // Nowhere to put it
                        {
                            return UUID.Zero;
                        }
                    }

                    item = new InventoryItemBase();
                    // Can't know creator is the same, so null it in inventory
                    if (objlist.Count > 1)
                        item.CreatorId = UUID.Zero.ToString();
                    else
                        item.CreatorId = objlist[0].RootPart.CreatorID.ToString();
                    item.ID = UUID.Random();
                    item.InvType = (int)InventoryType.Object;
                    item.Folder = folder.ID;
                    item.Owner = userID;
                    if (objlist.Count > 1)
                    {
                        item.Flags = (uint)InventoryItemFlags.ObjectHasMultipleItems;
                    }
                    else
                    {
                        item.SaleType = objlist[0].RootPart.ObjectSaleType;
                        item.SalePrice = objlist[0].RootPart.SalePrice;
                    }
                }

                AssetBase asset = CreateAsset(
                    objlist[0].GetPartName(objlist[0].RootPart.LocalId),
                    objlist[0].GetPartDescription(objlist[0].RootPart.LocalId),
                    (sbyte)AssetType.Object,
                    Utils.StringToBytes(itemXml),
                    objlist[0].OwnerID.ToString());
                m_Scene.AssetService.Store(asset);
                assetID = asset.FullID;

                if (DeRezAction.SaveToExistingUserInventoryItem == action)
                {
                    item.AssetID = asset.FullID;
                    m_Scene.InventoryService.UpdateItem(item);
                }
                else
                {
                    item.AssetID = asset.FullID;

                    uint effectivePerms = (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify | PermissionMask.Move) | 7;
                    foreach (SceneObjectGroup grp in objlist)
                        effectivePerms &= grp.GetEffectivePermissions();
                    effectivePerms |= (uint)PermissionMask.Move;

                    if (remoteClient != null && (remoteClient.AgentId != objlist[0].RootPart.OwnerID) && m_Scene.Permissions.PropagatePermissions())
                    {
                        uint perms = effectivePerms;
                        uint nextPerms = (perms & 7) << 13;
                        if ((nextPerms & (uint)PermissionMask.Copy) == 0)
                            perms &= ~(uint)PermissionMask.Copy;
                        if ((nextPerms & (uint)PermissionMask.Transfer) == 0)
                            perms &= ~(uint)PermissionMask.Transfer;
                        if ((nextPerms & (uint)PermissionMask.Modify) == 0)
                            perms &= ~(uint)PermissionMask.Modify;

                        item.BasePermissions = perms & objlist[0].RootPart.NextOwnerMask;
                        item.CurrentPermissions = item.BasePermissions;
                        item.NextPermissions = perms & objlist[0].RootPart.NextOwnerMask;
                        item.EveryOnePermissions = objlist[0].RootPart.EveryoneMask & objlist[0].RootPart.NextOwnerMask;
                        item.GroupPermissions = objlist[0].RootPart.GroupMask & objlist[0].RootPart.NextOwnerMask;
                        
                        // Magic number badness. Maybe this deserves an enum.
                        // bit 4 (16) is the "Slam" bit, it means treat as passed
                        // and apply next owner perms on rez
                        item.CurrentPermissions |= 16; // Slam!
                    }
                    else
                    {
                        item.BasePermissions = effectivePerms;
                        item.CurrentPermissions = effectivePerms;
                        item.NextPermissions = objlist[0].RootPart.NextOwnerMask & effectivePerms;
                        item.EveryOnePermissions = objlist[0].RootPart.EveryoneMask & effectivePerms;
                        item.GroupPermissions = objlist[0].RootPart.GroupMask & effectivePerms;

                        item.CurrentPermissions &=
                                ((uint)PermissionMask.Copy |
                                 (uint)PermissionMask.Transfer |
                                 (uint)PermissionMask.Modify |
                                 (uint)PermissionMask.Move |
                                 7); // Preserve folded permissions
                    }

                    item.CreationDate = Util.UnixTimeSinceEpoch();
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;

                    m_Scene.AddInventoryItem(item);

                    if (remoteClient != null && item.Owner == remoteClient.AgentId)
                    {
                        remoteClient.SendInventoryItemCreateUpdate(item, 0);
                    }
                    else
                    {
                        ScenePresence notifyUser = m_Scene.GetScenePresence(item.Owner);
                        if (notifyUser != null)
                        {
                            notifyUser.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                        }
                    }
                }
            }
            return assetID;
        }


        /// <summary>
        /// Rez an object into the scene from the user's inventory
        /// </summary>
        /// <remarks>
        /// FIXME: It would be really nice if inventory access modules didn't also actually do the work of rezzing
        /// things to the scene.  The caller should be doing that, I think.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="RayEnd"></param>
        /// <param name="RayStart"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="BypassRayCast"></param>
        /// <param name="RayEndIsIntersection"></param>
        /// <param name="RezSelected"></param>
        /// <param name="RemoveItem"></param>
        /// <param name="fromTaskID"></param>
        /// <param name="attachment"></param>
        /// <returns>The SceneObjectGroup rezzed or null if rez was unsuccessful.</returns>
        public virtual SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
//            m_log.DebugFormat("[INVENTORY ACCESS MODULE]: RezObject for {0}, item {1}", remoteClient.Name, itemID);
            
            // Work out position details
            byte bRayEndIsIntersection = (byte)0;

            if (RayEndIsIntersection)
            {
                bRayEndIsIntersection = (byte)1;
            }
            else
            {
                bRayEndIsIntersection = (byte)0;
            }

            Vector3 scale = new Vector3(0.5f, 0.5f, 0.5f);


            Vector3 pos = m_Scene.GetNewRezLocation(
                      RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                      BypassRayCast, bRayEndIsIntersection, true, scale, false);

            // Rez object
            InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
            item = m_Scene.InventoryService.GetItem(item);

            if (item != null)
            {
                item.Owner = remoteClient.AgentId;

                AssetBase rezAsset = m_Scene.AssetService.Get(item.AssetID.ToString());

                SceneObjectGroup group = null;

                if (rezAsset != null)
                {
                    UUID itemId = UUID.Zero;

                    // If we have permission to copy then link the rezzed object back to the user inventory
                    // item that it came from.  This allows us to enable 'save object to inventory'
                    if (!m_Scene.Permissions.BypassPermissions())
                    {
                        if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == (uint)PermissionMask.Copy && (item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                        {
                            itemId = item.ID;
                        }
                    }
                    else
                    {
                        if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                        {
                            // Brave new fullperm world
                            itemId = item.ID;
                        }
                    }

                    string xmlData = Utils.BytesToString(rezAsset.Data);
                    List<SceneObjectGroup> objlist =
                            new List<SceneObjectGroup>();
                    List<Vector3> veclist = new List<Vector3>();

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xmlData);
                    XmlElement e = (XmlElement)doc.SelectSingleNode("/CoalescedObject");
                    if (e == null || attachment) // Single
                    {
                        SceneObjectGroup g =
                                SceneObjectSerializer.FromOriginalXmlFormat(
                                itemId, xmlData);
                        objlist.Add(g);
                        veclist.Add(new Vector3(0, 0, 0));

                        float offsetHeight = 0;
                        pos = m_Scene.GetNewRezLocation(
                            RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                            BypassRayCast, bRayEndIsIntersection, true, g.GetAxisAlignedBoundingBox(out offsetHeight), false);
                        pos.Z += offsetHeight;
                    }
                    else
                    {
                        XmlElement coll = (XmlElement)e;
                        float bx = Convert.ToSingle(coll.GetAttribute("x"));
                        float by = Convert.ToSingle(coll.GetAttribute("y"));
                        float bz = Convert.ToSingle(coll.GetAttribute("z"));
                        Vector3 bbox = new Vector3(bx, by, bz);

                        pos = m_Scene.GetNewRezLocation(RayStart, RayEnd,
                                RayTargetID, Quaternion.Identity,
                                BypassRayCast, bRayEndIsIntersection, true,
                                bbox, false);

                        pos -= bbox / 2;

                        XmlNodeList groups = e.SelectNodes("SceneObjectGroup");
                        foreach (XmlNode n in groups)
                        {
                            SceneObjectGroup g =
                                    SceneObjectSerializer.FromOriginalXmlFormat(
                                    itemId, n.OuterXml);
                            objlist.Add(g);
                            XmlElement el = (XmlElement)n;
                            float x = Convert.ToSingle(el.GetAttribute("offsetx"));
                            float y = Convert.ToSingle(el.GetAttribute("offsety"));
                            float z = Convert.ToSingle(el.GetAttribute("offsetz"));
                            veclist.Add(new Vector3(x, y, z));
                        }
                    }

                    int primcount = 0;
                    foreach (SceneObjectGroup g in objlist)
                        primcount += g.PrimCount;

                    if (!m_Scene.Permissions.CanRezObject(
                        primcount, remoteClient.AgentId, pos)
                        && !attachment)
                    {
                        // The client operates in no fail mode. It will
                        // have already removed the item from the folder
                        // if it's no copy.
                        // Put it back if it's not an attachment
                        //
                        if (((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0) && (!attachment))
                            remoteClient.SendBulkUpdateInventory(item);
                        return null;
                    }

                    for (int i = 0 ; i < objlist.Count ; i++ )
                    {
                        group = objlist[i];

                        Vector3 storedPosition = group.AbsolutePosition;
                        if (group.UUID == UUID.Zero)
                        {
                            m_log.Debug("[InventoryAccessModule]: Inventory object has UUID.Zero! Position 3");
                        }
                        group.RootPart.FromFolderID = item.Folder;

                        // If it's rezzed in world, select it. Much easier to 
                        // find small items.
                        //
                        if (!attachment)
                        {
                            group.RootPart.CreateSelected = true;
                            foreach (SceneObjectPart child in group.Parts)
                                child.CreateSelected = true;
                        }
                        group.ResetIDs();

                        if (attachment)
                        {
                            group.RootPart.Flags |= PrimFlags.Phantom;
                            group.RootPart.IsAttachment = true;
                        }

                        // If we're rezzing an attachment then don't ask
                        // AddNewSceneObject() to update the client since
                        // we'll be doing that later on.  Scheduling more than
                        // one full update during the attachment
                        // process causes some clients to fail to display the
                        // attachment properly.
                        m_Scene.AddNewSceneObject(group, true, false);

                        // if attachment we set it's asset id so object updates
                        // can reflect that, if not, we set it's position in world.
                        if (!attachment)
                        {
                            group.ScheduleGroupForFullUpdate();
                            
                            group.AbsolutePosition = pos + veclist[i];
                        }
                        else
                        {
                            group.SetFromItemID(itemID);
                        }

                        SceneObjectPart rootPart = null;

                        try
                        {
                            rootPart = group.GetChildPart(group.UUID);
                        }
                        catch (NullReferenceException)
                        {
                            string isAttachment = "";

                            if (attachment)
                                isAttachment = " Object was an attachment";

                            m_log.Error("[AGENT INVENTORY]: Error rezzing ItemID: " + itemID + " object has no rootpart." + isAttachment);
                        }

                        // Since renaming the item in the inventory does not
                        // affect the name stored in the serialization, transfer
                        // the correct name from the inventory to the
                        // object itself before we rez.
                        rootPart.Name = item.Name;
                        rootPart.Description = item.Description;
                        rootPart.ObjectSaleType = item.SaleType;
                        rootPart.SalePrice = item.SalePrice;

                        group.SetGroup(remoteClient.ActiveGroupId, remoteClient);
                        if ((rootPart.OwnerID != item.Owner) ||
                            (item.CurrentPermissions & 16) != 0)
                        {
                            //Need to kill the for sale here
                            rootPart.ObjectSaleType = 0;
                            rootPart.SalePrice = 10;

                            if (m_Scene.Permissions.PropagatePermissions())
                            {
                                foreach (SceneObjectPart part in group.Parts)
                                {
                                    if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                                    {
                                        part.EveryoneMask = item.EveryOnePermissions;
                                        part.NextOwnerMask = item.NextPermissions;
                                    }
                                    part.GroupMask = 0; // DO NOT propagate here
                                }
                                
                                group.ApplyNextOwnerPermissions();
                            }
                        }

                        foreach (SceneObjectPart part in group.Parts)
                        {
                            if ((part.OwnerID != item.Owner) ||
                                (item.CurrentPermissions & 16) != 0)
                            {
                                part.LastOwnerID = part.OwnerID;
                                part.OwnerID = item.Owner;
                                part.Inventory.ChangeInventoryOwner(item.Owner);
                                part.GroupMask = 0; // DO NOT propagate here
                            }
                            part.EveryoneMask = item.EveryOnePermissions;
                            part.NextOwnerMask = item.NextPermissions;
                        }

                        rootPart.TrimPermissions();

                        if (!attachment)
                        {
                            if (group.RootPart.Shape.PCode == (byte)PCode.Prim)
                                group.ClearPartAttachmentData();
                            
                            // Fire on_rez
                            group.CreateScriptInstances(0, true, m_Scene.DefaultScriptEngine, 1);
                            rootPart.ParentGroup.ResumeScripts();

                            rootPart.ScheduleFullUpdate();
                        }
                    }

                    if (!m_Scene.Permissions.BypassPermissions())
                    {
                        if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                        {
                            // If this is done on attachments, no
                            // copy ones will be lost, so avoid it
                            //
                            if (!attachment)
                            {
                                List<UUID> uuids = new List<UUID>();
                                uuids.Add(item.ID);
                                m_Scene.InventoryService.DeleteItems(item.Owner, uuids);
                            }
                        }
                    }
                }
                return group;
            }

            return null;
        }

        protected void AddUserData(SceneObjectGroup sog)
        {
            UserManagementModule.AddUser(sog.RootPart.CreatorID, sog.RootPart.CreatorData);
            foreach (SceneObjectPart sop in sog.Parts)
                UserManagementModule.AddUser(sop.CreatorID, sop.CreatorData);
        }

        public virtual void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver)
        {
        }

        public virtual bool GetAgentInventoryItem(IClientAPI remoteClient, UUID itemID, UUID requestID)
        {
            InventoryItemBase assetRequestItem = GetItem(remoteClient.AgentId, itemID);
            if (assetRequestItem == null)
            {
                ILibraryService lib = m_Scene.RequestModuleInterface<ILibraryService>();
                if (lib != null)
                    assetRequestItem = lib.LibraryRootFolder.FindItem(itemID);
                if (assetRequestItem == null)
                    return false;
            }

            // At this point, we need to apply perms
            // only to notecards and scripts. All
            // other asset types are always available
            //
            if (assetRequestItem.AssetType == (int)AssetType.LSLText)
            {
                if (!m_Scene.Permissions.CanViewScript(itemID, UUID.Zero, remoteClient.AgentId))
                {
                    remoteClient.SendAgentAlertMessage("Insufficient permissions to view script", false);
                    return false;
                }
            }
            else if (assetRequestItem.AssetType == (int)AssetType.Notecard)
            {
                if (!m_Scene.Permissions.CanViewNotecard(itemID, UUID.Zero, remoteClient.AgentId))
                {
                    remoteClient.SendAgentAlertMessage("Insufficient permissions to view notecard", false);
                    return false;
                }
            }

            if (assetRequestItem.AssetID != requestID)
            {
                m_log.WarnFormat(
                    "[CLIENT]: {0} requested asset {1} from item {2} but this does not match item's asset {3}",
                    Name, requestID, itemID, assetRequestItem.AssetID);
                return false;
            }

            return true;
        }


        public virtual bool IsForeignUser(UUID userID, out string assetServerURL)
        {
            assetServerURL = string.Empty;
            return false;
        }

        #endregion

        #region Misc

        /// <summary>
        /// Create a new asset data structure.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="invType"></param>
        /// <param name="assetType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data, string creatorID)
        {
            AssetBase asset = new AssetBase(UUID.Random(), name, assetType, creatorID);
            asset.Description = description;
            asset.Data = (data == null) ? new byte[1] : data;

            return asset;
        }

        protected virtual InventoryItemBase GetItem(UUID agentID, UUID itemID)
        {
            IInventoryService invService = m_Scene.RequestModuleInterface<IInventoryService>();
            InventoryItemBase item = new InventoryItemBase(itemID, agentID);
            item = invService.GetItem(item);
            
            if (item.CreatorData != null && item.CreatorData != string.Empty)
                UserManagementModule.AddUser(item.CreatorIdAsUuid, item.CreatorData);

            return item;
        }

        #endregion
    }
}
