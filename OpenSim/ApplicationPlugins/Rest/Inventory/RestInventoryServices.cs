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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using System.Drawing;
using OpenJPEGNet;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using libsecondlife;
using Nini.Config;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{
    public class RestInventoryServices : IRest
    {
        private bool       enabled = false;
        private string     qPrefix = "inventory";

        /// <summary>
        /// A simple constructor is used to handle any once-only
        /// initialization of working classes.
        /// </summary>

        public RestInventoryServices()
        {
            Rest.Log.InfoFormat("{0} Inventory services initializing", MsgId);
            Rest.Log.InfoFormat("{0} Using REST Implementation Version {1}", MsgId, Rest.Version);

            // If a relative path was specified for the handler's domain,
            // add the standard prefix to make it absolute, e.g. /admin

            if (!qPrefix.StartsWith(Rest.UrlPathSeparator))
            {
                qPrefix = Rest.Prefix + Rest.UrlPathSeparator + qPrefix;
            }

            // Register interface using the absolute URI.

            Rest.Plugin.AddPathHandler(DoInventory,qPrefix,Allocate);

            // Activate if everything went OK

            enabled = true;

            Rest.Log.InfoFormat("{0} Inventory services initialization complete", MsgId);
        }

        /// <summary>
        /// Post-construction, pre-enabled initialization opportunity
        /// Not currently exploited.
        /// </summary>

        public void Initialize()
        {
        }

        /// <summary>
        /// Called by the plug-in to halt REST processing. Local processing is
        /// disabled, and control blocks until all current processing has
        /// completed. No new processing will be started
        /// </summary>

        public void Close()
        {
            enabled = false;
            Rest.Log.InfoFormat("{0} Inventory services closing down", MsgId);
        }

        /// <summary>
        /// This property is declared locally because it is used a lot and
        /// brevity is nice.
        /// </summary>

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        #region Interface

        /// <summary>
        /// The plugin (RestHandler) calls this method to allocate the request
        /// state carrier for a new request. It is destroyed when the request
        /// completes. All request-instance specific state is kept here. This
        /// is registered when this service provider is registered.
        /// </summary>

        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response)
        {
            return (RequestData) new InventoryRequestData(request, response, qPrefix);
        }

        /// <summary>
        /// This method is registered with the handler when this service provider
        /// is initialized. It is called whenever the plug-in identifies this service
        /// provider as the best match.
        /// It handles all aspects of inventory REST processing.
        /// </summary>

        private void DoInventory(RequestData hdata)
        {
            InventoryRequestData rdata = (InventoryRequestData) hdata;

            Rest.Log.DebugFormat("{0} DoInventory ENTRY", MsgId);

            // If we're disabled, do nothing.

            if (!enabled)
            {
                return;
            }

            // Now that we know this is a serious attempt to
            // access inventory data, we should find out who
            // is asking, and make sure they are authorized
            // to do so. We need to validate the caller's
            // identity before revealing anything about the
            // status quo. Authenticate throws an exception
            // via Fail if no identity information is present.
            //
            // With the present HTTP server we can't use the
            // builtin authentication mechanisms because they
            // would be enforced for all in-bound requests.
            // Instead we look at the headers ourselves and
            // handle authentication directly.

            try
            {
                if (!rdata.IsAuthenticated)
                {
                    rdata.Fail(Rest.HttpStatusCodeNotAuthorized, Rest.HttpStatusDescNotAuthorized);
                }
            }
            catch (RestException e)
            {
                if (e.statusCode == Rest.HttpStatusCodeNotAuthorized)
                {
                    Rest.Log.WarnFormat("{0} User not authenticated", MsgId);
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
                }
                else
                {
                    Rest.Log.ErrorFormat("{0} User authentication failed", MsgId);
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
                }
                throw (e);
            }

            Rest.Log.DebugFormat("{0} Authenticated {1}", MsgId, rdata.userName);

            /// <remarks>
            /// We can only get here if we are authorized
            ///
            /// The requestor may have specified an LLUUID or
            /// a conjoined FirstName LastName string. We'll
            /// try both. If we fail with the first, UUID,
            /// attempt, we try the other. As an example, the
            /// URI for a valid inventory request might be:
            ///
            /// http://<host>:<port>/admin/inventory/Arthur Dent
            ///
            /// Indicating that this is an inventory request for
            /// an avatar named Arthur Dent. This is ALl that is
            /// required to designate a GET for an entire
            /// inventory.
            /// </remarks>

            // Do we have at least a user agent name?

            if (rdata.parameters.Length < 1)
            {
                Rest.Log.WarnFormat("{0} Inventory: No user agent identifier specified", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, Rest.HttpStatusDescBadRequest+": No user identity specified");
            }

            // The first parameter MUST be the agent identification, either an LLUUID
            // or a space-separated First-name Last-Name specification.

            try
            {
                rdata.uuid = new LLUUID(rdata.parameters[0]);
                Rest.Log.DebugFormat("{0} LLUUID supplied", MsgId);
                rdata.userProfile = Rest.UserServices.GetUserProfile(rdata.uuid);
            }
            catch
            {
                string[] names = rdata.parameters[0].Split(Rest.CA_SPACE);
                if (names.Length == 2)
                {
                    Rest.Log.DebugFormat("{0} Agent Name supplied [2]", MsgId);
                    rdata.userProfile = Rest.UserServices.GetUserProfile(names[0],names[1]);
                }
                else
                {
                    Rest.Log.DebugFormat("{0} A Valid UUID or both first and last names must be specified", MsgId);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest, Rest.HttpStatusDescBadRequest+": invalid user identity");
                }
            }

            // If the user rpofile is null then either the server is broken, or the
            // user is not known. We always assume the latter case.

            if (rdata.userProfile != null)
            {
                Rest.Log.DebugFormat("{0} Profile obtained for agent {1} {2}",
                                     MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            }
            else
            {
                Rest.Log.DebugFormat("{0} No profile for {1}", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound,Rest.HttpStatusDescNotFound+": unrecognized user identity");
            }

            // If we get to here, then we have effectively validated the user's
            // identity. Now we need to get the inventory. If the server does not
            // have the inventory, we reject the request with an appropriate explanation.
            //
            // Note that inventory retrieval is an asynchronous event, we use the rdata
            // class instance as the basis for our synchronization.
            //
            // TODO
            // If something went wrong in inventory processing the thread could stall here
            // indefinitely. There should be a watchdog timer to fail the request if the
            // response is not recieved in a timely fashion.

            rdata.uuid = rdata.userProfile.ID;

            if (Rest.InventoryServices.HasInventoryForUser(rdata.uuid))
            {
                rdata.root = Rest.InventoryServices.RequestRootFolder(rdata.uuid);

                Rest.Log.DebugFormat("{0} Inventory Root retrieved for {1} {2}",
                                     MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);

                Rest.InventoryServices.RequestInventoryForUser(rdata.uuid, rdata.GetUserInventory);

                Rest.Log.DebugFormat("{0} Inventory catalog requested for {1} {2}",
                                     MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);

                lock (rdata)
                {
                    if (!rdata.HaveInventory)
                    {
                        Monitor.Wait(rdata);
                    }
                }

                if (rdata.root == null)
                {
                    Rest.Log.DebugFormat("{0} Inventory is not available [1] for agent {1} {2}",
                                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
                    rdata.Fail(Rest.HttpStatusCodeServerError,Rest.HttpStatusDescServerError+": inventory retrieval failed");
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} Inventory is not available for agent [3] {1} {2}",
                                     MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
                rdata.Fail(Rest.HttpStatusCodeNotFound,Rest.HttpStatusDescNotFound+": no inventory for user");
            }

            // If we get here, then we have successfully retrieved the user's information
            // and inventory information is now available locally.

            switch (rdata.method)
            {
            case Rest.HEAD   : // Do the processing, set the status code, suppress entity
                DoGet(rdata);
                rdata.buffer = null;
                break;

            case Rest.GET    : // Do the processing, set the status code, return entity
                DoGet(rdata);
                break;

            case Rest.PUT    : // Add new information
                DoPut(rdata);
                break;

            case Rest.POST   : // Update (replace)
                DoPost(rdata);
                break;

            case Rest.DELETE : // Delete information
                DoDelete(rdata);
                break;

            default :
                Rest.Log.DebugFormat("{0} Method {1} not supported for {2}",
                                     MsgId, rdata.method, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeMethodNotAllowed,
                           Rest.HttpStatusDescMethodNotAllowed+": "+rdata.method+" not supported");
                break;
            }
        }

        #endregion Interface

        #region method-specific processing

        /// <summary>
        /// This method implements GET processing for inventory.
        /// Any remaining parameters are used to locate the
        /// corresponding subtree based upon node name.
        /// </summary>

        private void DoGet(InventoryRequestData rdata)
        {
            rdata.initXmlWriter();

            rdata.writer.WriteStartElement(String.Empty,"Inventory",String.Empty);

            // If there was only one parameter, then the entire
            // inventory is being requested.

            if (rdata.parameters.Length == 1)
            {
                formatInventory(rdata, rdata.root, String.Empty);
            }

            // If there are additional parameters, then these represent
            // a path relative to the root of the inventory. This path
            // must be traversed before we format the sub-tree thus
            // identified.

            else
            {
                traverseInventory(rdata, rdata.root, 1);
            }

            rdata.writer.WriteFullEndElement();

            rdata.Complete();
            rdata.Respond("Inventory " + rdata.method + ": Normal completion");
        }

        /// <summary>
        /// In the case of the inventory, and probably in general,
        /// the distinction between PUT and POST is not always
        /// easy to discern. Adding a directory can be viewed as
        /// an addition, or as a modification to the inventory as
        /// a whole. This is exacerbated by a lack of consistency
        /// across different implementations.
        ///
        /// For OpenSim POST is an update and PUT is an addition.
        ///
        /// The best way to exaplain the distinction is to
        /// consider the relationship between the URI and the
        /// entity in question. For POST, the URI identifies the
        /// entity to be modified or replaced.
        /// If the operation is PUT,then the URI describes the
        /// context into which the new entity will be added.
        ///
        /// As an example, suppose the URI contains:
        ///      /admin/inventory/Clothing
        ///
        /// A POST request will result in some modification of
        /// the folder or item named "Clothing". Whereas a PUT
        /// request will add some new information into the
        /// content identified by Clothing. It follows from this
        /// that for PUT, the element identified by the URI must
        /// be a folder.
        /// </summary>

        /// <summary>
        /// PUT adds new information to the inventory in the
        /// context identified by the URI.
        /// </summary>

        private void DoPut(InventoryRequestData rdata)
        {
            // Resolve the context node specified in the URI. Entity
            // data will be ADDED beneath this node.

            Object InventoryNode = getInventoryNode(rdata, rdata.root, 1);

            // Processing depends upon the type of inventory node
            // identified in the URI. This is the CONTEXT for the
            // change. We either got a context or we threw an
            // exception.

            // It follows that we can only add information if the URI
            // has identified a folder. So only a type of folder is supported
            // in this case.

            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
                typeof(InventoryFolderImpl) == InventoryNode.GetType())
            {

                // Cast the context node appropriately.

                InventoryFolderBase context    = (InventoryFolderBase) InventoryNode;

                Rest.Log.DebugFormat("{0} {1}: Resource(s) will be added to folder {2}",
                                     MsgId, rdata.method, rdata.path);

                // Reconstitute the inventory sub-tree from the XML supplied in the entity.
                // The result is a stand-alone inventory subtree, not yet integrated into the
                // existing tree. An inventory collection consists of three components:
                // [1] A (possibly empty) set of folders.
                // [2] A (possibly empty) set of items.
                // [3] A (possibly empty) set of assets.
                // If all of these are empty, then the PUT is a harmless no-operation.

                XmlInventoryCollection entity  = ReconstituteEntity(rdata);

                // Inlined assets can be included in entity. These must be incorporated into
                // the asset database before we attempt to update the inventory. If anything
                // fails, return a failure to requestor.

                if (entity.Assets.Count > 0)
                {
                    Rest.Log.DebugFormat("{0} Adding {1} assets to server",
                                         MsgId, entity.Assets.Count);

                    foreach (AssetBase asset in entity.Assets)
                    {
                        Rest.Log.DebugFormat("{0} Rest asset: {1} {2} {3}",
                                             MsgId, asset.ID, asset.Type, asset.Name);
                        Rest.AssetServices.AddAsset(asset);

                        if (Rest.DumpAsset)
                        {
                            Rest.Dump(asset.Data);
                        }
                    }
                }

                // Modify the context using the collection of folders and items
                // returned in the XmlInventoryCollection.

                foreach (InventoryFolderBase folder in entity.Folders)
                {
                    InventoryFolderBase found;

                    // If the parentID is zero, then this folder is going
                    // into the root folder identified by the URI. The requestor
                    // may have already set the parent ID explicitly, in which
                    // case we don't have to do it here.

                    if (folder.ParentID == LLUUID.Zero)
                    {
                        folder.ParentID = context.ID;
                    }

                    // Search the existing inventory for an existing entry. If
                    // we have one, we need to decide if it has really changed.
                    // It could just be present as (unnecessary) context, and we
                    // don't want to waste time updating the database in that
                    // case, OR, it could be being moved from another location
                    // in which case an update is most certainly necessary.

                    found = null;

                    foreach (InventoryFolderBase xf in rdata.folders)
                    {
                        // Compare identifying attribute
                        if (xf.ID == folder.ID)
                        {
                            found = xf;
                            break;
                        }
                    }

                    if (found != null && FolderHasChanged(folder,found))
                    {
                        Rest.Log.DebugFormat("{0} Updating existing folder", MsgId);
                        Rest.InventoryServices.MoveFolder(folder);
                    }
                    else
                    {
                        Rest.Log.DebugFormat("{0} Adding new folder", MsgId);
                        Rest.InventoryServices.AddFolder(folder);
                    }
                }

                // Now we repeat a similar process for the items included
                // in the entity.

                foreach (InventoryItemBase item in entity.Items)
                {
                    InventoryItemBase found = null;

                    // If the parentID is zero, then this is going
                    // directly into the root identified by the URI.

                    if (item.Folder == LLUUID.Zero)
                    {
                        item.Folder = context.ID;
                    }

                    // Determine whether this is a new item or a
                    // replacement definition.

                    foreach (InventoryItemBase xi in rdata.items)
                    {
                        // Compare identifying attribute
                        if (xi.ID == item.ID)
                        {
                            found = xi;
                            break;
                        }
                    }

                    if (found != null && ItemHasChanged(item, found))
                    {
                        Rest.Log.DebugFormat("{0} Updating item {1} {2} {3} {4} {5}",
                                             MsgId, item.ID, item.AssetID, item.InvType, item.AssetType, item.Name);
                        Rest.InventoryServices.UpdateItem(item);
                    }
                    else
                    {
                        Rest.Log.DebugFormat("{0} Adding item {1} {2} {3} {4} {5}",
                                             MsgId, item.ID, item.AssetID, item.InvType, item.AssetType, item.Name);
                        Rest.InventoryServices.AddItem(item);
                    }
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} {1}: Resource {2} is not a valid context: {3}",
                                     MsgId, rdata.method, rdata.path, InventoryNode.GetType());
                rdata.Fail(Rest.HttpStatusCodeBadRequest,
                           Rest.HttpStatusDescBadRequest+": invalid resource context");
            }

            rdata.Complete();
            rdata.Respond("Inventory " + rdata.method + ": Normal completion");
        }

        /// <summary>
        /// POST updates the URI-identified element in the inventory. This
        /// is actually far more flexible than it might at first sound. For
        /// POST the URI serves two purposes:
        ///     [1] It identifies the user whose inventory is to be
        ///         processed.
        ///     [2] It optionally specifies a subtree of the inventory
        ///         that is to be used to resolve any relative subtree
        ///         specifications in the entity. If nothing is specified
        ///         then the whole inventory is implied.
        /// Please note that the subtree specified by the URI is only relevant
        /// to an entity containing a URI relative specification, i.e. one or
        /// more elements do not specify parent folder information. These
        /// elements will be implicitly referenced within the context identified
        /// by the URI.
        /// If an element in the entity specifies an explicit parent folder, then
        /// that parent is effective, regardless of any value specified in the
        /// URI. If the parent does not exist, then the element, and any dependent
        /// elements, are ignored. This case is actually detected and handled
        /// during the reconstitution process.
        /// </summary>

        private void DoPost(InventoryRequestData rdata)
        {
            int count = 0;

            // Resolve the inventory node that is to be modified.

            Object InventoryNode = getInventoryNode(rdata, rdata.root, 1);

            // As long as we have a node, then we have something
            // meaningful to do, unlike PUT. So we reconstitute the
            // subtree before doing anything else. Note that we
            // etiher got a valid node or we threw an exception.

            XmlInventoryCollection entity = ReconstituteEntity(rdata);

            // Incorporate any inlined assets first. Any failures
            // will terminate the request.

            if (entity.Assets.Count > 0)
            {
                Rest.Log.DebugFormat("{0} Adding {1} assets to server",
                                     MsgId, entity.Assets.Count);

                foreach (AssetBase asset in entity.Assets)
                {
                    Rest.Log.DebugFormat("{0} Rest asset: {1} {2} {3}",
                                         MsgId, asset.ID, asset.Type, asset.Name);

                    // The asset was validated during the collection process

                    Rest.AssetServices.AddAsset(asset);

                    if (Rest.DumpAsset)
                    {
                        Rest.Dump(asset.Data);
                    }
                }
            }

            /// <summary>
            /// The URI specifies either a folder or an item to be updated.
            /// </summary>
            /// <remarks>
            /// The root node in the entity will replace the node identified
            /// by the URI. This means the parent will remain the same, but
            /// any or all attributes associated with the named element
            /// will change.
            ///
            /// If the inventory collection contains an element with a zero
            /// parent ID, then this is taken to be the replacement for the
            /// named node. The collection MAY also specify an explicit
            /// parent ID, in this case it MAY identify the same parent as
            /// the current node, or it MAY specify a different parent,
            /// indicating that the folder is being moved in addition to any
            /// other modifications being made.
            /// </remarks>

            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
                typeof(InventoryFolderImpl) == InventoryNode.GetType())
            {
                InventoryFolderBase uri = (InventoryFolderBase) InventoryNode;
                InventoryFolderBase xml = null;

                // Scan the set of folders in the entity collection for an
                // entry that matches the context folder. It is assumed that
                // the only reliable indicator of this is a zero UUID (using
                // implicit context), or the parent's UUID matches that of the
                // URI designated node (explicit context). We don't allow
                // ambiguity in this case because this is POST and we are
                // supposed to be modifying a specific node.
                // We assign any element IDs required as an economy; we don't
                // want to iterate over the fodler set again if it can be
                // helped.

                foreach (InventoryFolderBase folder in entity.Folders)
                {
                    if (folder.ParentID == uri.ParentID ||
                        folder.ParentID == LLUUID.Zero)
                    {
                        folder.ParentID = uri.ParentID;
                        xml = folder;
                        count++;
                    }
                    if (xml.ID == LLUUID.Zero)
                    {
                        xml.ID = LLUUID.Random();
                    }
                }

                // More than one entry is ambiguous. Other folders should be
                // added using the PUT verb.

                if (count > 1)
                {
                    Rest.Log.DebugFormat("{0} {1}: Request for <{2}> is ambiguous",
                                         MsgId, rdata.method, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                               Rest.HttpStatusDescBadRequest+": context is ambiguous");
                }

                // Exactly one entry means we ARE replacing the node
                // identified by the URI. So we delete the old folder
                // by moving it to the trash and then purging it.
                // We then add all of the folders and items we
                // included in the entity. The subtree has been
                // modified.

                if (count == 1)
                {
                    InventoryFolderBase TrashCan = GetTrashCan(rdata);

                    uri.ParentID = TrashCan.ID;
                    Rest.InventoryServices.MoveFolder(uri);
                    Rest.InventoryServices.PurgeFolder(TrashCan);
                }

                // Now, regardelss of what they represent, we
                // integrate all of the elements in the entity.

                foreach (InventoryFolderBase f in entity.Folders)
                {
                    Rest.InventoryServices.MoveFolder(f);
                }

                foreach (InventoryItemBase it in entity.Items)
                {
                    Rest.InventoryServices.AddItem(it);
                }
            }

            /// <summary>
            /// URI specifies an item to be updated
            /// </summary>
            /// <remarks>
            /// The entity must contain a single item node to be
            /// updated. ID and Folder ID must be correct.
            /// </remarks>

            else
            {
                InventoryItemBase uri = (InventoryItemBase) InventoryNode;
                InventoryItemBase xml = null;

                if (entity.Folders.Count != 0)
                {
                    Rest.Log.DebugFormat("{0} {1}: Request should not contain any folders <{2}>",
                                         MsgId, rdata.method, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                               Rest.HttpStatusDescBadRequest+": folder is not allowed");
                }

                if (entity.Items.Count > 1)
                {
                    Rest.Log.DebugFormat("{0} {1}: Entity contains too many items <{2}>",
                                         MsgId, rdata.method, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                               Rest.HttpStatusDescBadRequest+": too may items");
                }

                xml = entity.Items[0];

                if (xml.ID == LLUUID.Zero)
                {
                    xml.ID = LLUUID.Random();
                }

                // If the folder reference has changed, then this item is
                // being moved. Otherwise we'll just delete the old, and
                // add in the new.

                // Delete the old item

                Rest.InventoryServices.DeleteItem(uri);

                // Add the new item to the inventory

                Rest.InventoryServices.AddItem(xml);
            }

            rdata.Complete();
            rdata.Respond("Inventory " + rdata.method + ": Normal completion");
        }

        /// <summary>
        /// Arguably the most damaging REST interface. It deletes the inventory
        /// item or folder identified by the URI.
        ///
        /// We only process if the URI identified node appears to exist
        /// We do not test for success because we either get a context,
        /// or an exception is thrown.
        ///
        /// Folders are deleted by moving them to another folder and then
        /// purging that folder. We'll do that by creating a temporary
        /// sub-folder in the TrashCan and purging that folder's
        /// contents. If we can't can it, we don't delete it...
        /// So, if no trashcan is available, the request does nothing.
        /// Items are summarily deleted.
        ///
        /// In the interests of safety, a delete request should normally
        /// be performed using UUID, as a name might identify several
        /// elements.
        /// </summary>

        private void DoDelete(InventoryRequestData rdata)
        {
            Object InventoryNode = getInventoryNode(rdata, rdata.root, 1);

            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
                typeof(InventoryFolderImpl) == InventoryNode.GetType())
            {
                InventoryFolderBase TrashCan = GetTrashCan(rdata);

                InventoryFolderBase folder = (InventoryFolderBase) InventoryNode;
                Rest.Log.DebugFormat("{0} {1}: Folder {2} will be deleted",
                                     MsgId, rdata.method, rdata.path);
                folder.ParentID = TrashCan.ID;
                Rest.InventoryServices.MoveFolder(folder);
                Rest.InventoryServices.PurgeFolder(TrashCan);
            }

            // Deleting items is much more straight forward.

            else
            {
                InventoryItemBase item = (InventoryItemBase) InventoryNode;
                Rest.Log.DebugFormat("{0} {1}: Item {2} will be deleted",
                                     MsgId, rdata.method, rdata.path);
                Rest.InventoryServices.DeleteItem(item);
            }

            rdata.Complete();
            rdata.Respond("Inventory " + rdata.method + ": Normal completion");
        }

        #endregion method-specific processing

        /// <summary>
        /// This method is called to obtain the OpenSim inventory object identified
        /// by the supplied URI. This may be either an Item or a Folder, so a suitably
        /// ambiguous return type is employed (Object). This method recurses as
        /// necessary to process the designated hierarchy.
        ///
        /// If we reach the end of the URI then we return the contextural folder to
        /// our caller.
        ///
        /// If we are not yet at the end of the URI we attempt to find a child folder
        /// and if we succeed we recurse.
        ///
        /// If this is the last node, then we look to see if this is an item. If it is,
        /// we return that item.
        ///
        /// Otherwise we fail the request on the ground of an invalid URI.
        ///
        /// <note>
        /// This mechanism cannot detect the case where duplicate subtrees satisfy a
        /// request. In such a case the 1st element gets processed. If this is a
        /// problem, then UUID should be used to identify the end-node. This is basic
        /// premise of normal inventory processing. The name is an informational, and
        /// not a defining, attribute.
        /// </note>
        ///
        /// </summary>

        private Object getInventoryNode(InventoryRequestData rdata, InventoryFolderBase folder, int pi)
        {
            Rest.Log.DebugFormat("{0} Searching folder {1} {2} [{3}]", MsgId, folder.ID, folder.Name, pi);

            // We have just run off the end of the parameter sequence

            if (pi >= rdata.parameters.Length)
            {
                return folder;
            }

            // More names in the sequence, look for a folder that might
            // get us there.

            if (rdata.folders != null)
            {
                foreach (InventoryFolderBase f in rdata.folders)
                {
                    // Look for the present node in the directory list
                    if (f.ParentID == folder.ID &&
                        (f.Name == rdata.parameters[pi] ||
                         f.ID.ToString() == rdata.parameters[pi]))
                    {
                        return getInventoryNode(rdata, f, pi+1);
                    }
                }
            }

            // No folders that match. Perhaps this parameter identifies an item? If
            // it does, then it MUST also be the last name in the sequence.

            if (pi == rdata.parameters.Length-1)
            {
                if (rdata.items != null)
                {
                    int k = 0;
                    InventoryItemBase li = null;
                    foreach (InventoryItemBase i in rdata.items)
                    {
                        if (i.Folder == folder.ID &&
                            (i.Name == rdata.parameters[pi] ||
                             i.ID.ToString() == rdata.parameters[pi]))
                        {
                            li = i;
                            k++;
                        }
                    }
                    if (k == 1)
                    {
                        return li;
                    }
                    else
                    {
                        Rest.Log.DebugFormat("{0} {1}: Request for {2} is ambiguous",
                                             MsgId, rdata.method, rdata.path);
                        rdata.Fail(Rest.HttpStatusCodeNotFound, Rest.HttpStatusDescNotFound+": request is ambiguous");
                    }
                }
            }

            // No, so abandon the request

            Rest.Log.DebugFormat("{0} {1}: Resource {2} not found",
                                 MsgId, rdata.method, rdata.path);
            rdata.Fail(Rest.HttpStatusCodeNotFound, Rest.HttpStatusDescNotFound+": resource "+rdata.path+" not found");

            return null; /* Never reached */
        }

        /// <summary>
        /// This routine traverse the inventory's structure until the end-point identified
        /// in the URI is reached, the remainder of the inventory (if any) is then formatted
        /// and returned to the requestor.
        ///
        /// Note that this method is only interested in those folder that match elements of
        /// the URI supplied by the requestor, so once a match is fund, the processing does
        /// not need to consider any further elements.
        ///
        /// Only the last element in the URI should identify an item.
        /// </summary>

        private void traverseInventory(InventoryRequestData rdata, InventoryFolderBase folder, int pi)
        {
            Rest.Log.DebugFormat("{0} Folder : {1} {2} [{3}]", MsgId, folder.ID, folder.Name, pi);

            if (rdata.folders != null)
            {
                foreach (InventoryFolderBase f in rdata.folders)
                {
                    if (f.ParentID == folder.ID &&
                        (f.Name == rdata.parameters[pi] ||
                         f.ID.ToString() == rdata.parameters[pi]))
                    {
                        if (pi < rdata.parameters.Length-1)
                        {
                            traverseInventory(rdata, f, pi+1);
                        }
                        else
                        {
                            formatInventory(rdata, f, String.Empty);
                        }
                        return;
                    }
                }
            }

            if (pi == rdata.parameters.Length-1)
            {
                if (rdata.items != null)
                {
                    foreach (InventoryItemBase i in rdata.items)
                    {
                        if (i.Folder == folder.ID &&
                            (i.Name == rdata.parameters[pi] ||
                             i.ID.ToString() == rdata.parameters[pi]))
                        {
                            // Fetching an Item has a special significance. In this
                            // case we also want to fetch the associated asset.
                            // To make it interesting, we'll d this via redirection.
                            string asseturl = "http://" + rdata.hostname + ":" + rdata.port +
                                "/admin/assets" + Rest.UrlPathSeparator + i.AssetID.ToString();
                            rdata.Redirect(asseturl,Rest.PERMANENT);
                            Rest.Log.DebugFormat("{0} Never Reached");
                        }
                    }
                }
            }

            Rest.Log.DebugFormat("{0} Inventory does not contain item/folder: <{1}>",
                                 MsgId, rdata.path);
            rdata.Fail(Rest.HttpStatusCodeNotFound,Rest.HttpStatusDescNotFound+": no such item/folder");
        }

        /// <summary>
        /// This method generates XML that describes an instance of InventoryFolderBase.
        /// It recurses as necessary to reflect a folder hierarchy, and calls formatItem
        /// to generate XML for any items encountered along the way.
        /// The indentation parameter is solely for the benefit of trace record
        /// formatting.
        /// </summary>

        private void formatInventory(InventoryRequestData rdata, InventoryFolderBase folder, string indent)
        {
            if (Rest.DEBUG)
            {
                Rest.Log.DebugFormat("{0} Folder : {1} {2} {3}", MsgId, folder.ID, indent, folder.Name);
                indent += "\t";
            }

            // Start folder item

            rdata.writer.WriteStartElement(String.Empty,"Folder",String.Empty);
            rdata.writer.WriteAttributeString("name",String.Empty,folder.Name);
            rdata.writer.WriteAttributeString("uuid",String.Empty,folder.ID.ToString());
            rdata.writer.WriteAttributeString("owner",String.Empty,folder.Owner.ToString());
            rdata.writer.WriteAttributeString("type",String.Empty,folder.Type.ToString());
            rdata.writer.WriteAttributeString("version",String.Empty,folder.Version.ToString());

            if (rdata.folders != null)
            {
                foreach (InventoryFolderBase f in rdata.folders)
                {
                    if (f.ParentID == folder.ID)
                    {
                        formatInventory(rdata, f, indent);
                    }
                }
            }

            if (rdata.items != null)
            {
                foreach (InventoryItemBase i in rdata.items)
                {
                    if (i.Folder == folder.ID)
                    {
                        formatItem(rdata, i, indent);
                    }
                }
            }

            // End folder item

            rdata.writer.WriteEndElement();
        }

        /// <summary>
        /// This method generates XML that describes an instance of InventoryItemBase.
        /// </summary>

        private void formatItem(InventoryRequestData rdata, InventoryItemBase i, string indent)
        {
            Rest.Log.DebugFormat("{0}   Item : {1} {2} {3}", MsgId, i.ID, indent, i.Name);

            rdata.writer.WriteStartElement(String.Empty,"Item",String.Empty);

            rdata.writer.WriteAttributeString("name",String.Empty,i.Name);
            rdata.writer.WriteAttributeString("desc",String.Empty,i.Description);
            rdata.writer.WriteAttributeString("uuid",String.Empty,i.ID.ToString());
            rdata.writer.WriteAttributeString("owner",String.Empty,i.Owner.ToString());
            rdata.writer.WriteAttributeString("creator",String.Empty,i.Creator.ToString());
            rdata.writer.WriteAttributeString("creationdate",String.Empty,i.CreationDate.ToString());
            rdata.writer.WriteAttributeString("type",String.Empty,i.InvType.ToString());
            rdata.writer.WriteAttributeString("assettype",String.Empty,i.AssetType.ToString());
            rdata.writer.WriteAttributeString("groupowned",String.Empty,i.GroupOwned.ToString());
            rdata.writer.WriteAttributeString("groupid",String.Empty,i.GroupID.ToString());
            rdata.writer.WriteAttributeString("saletype",String.Empty,i.SaleType.ToString());
            rdata.writer.WriteAttributeString("saleprice",String.Empty,i.SalePrice.ToString());
            rdata.writer.WriteAttributeString("flags",String.Empty,i.Flags.ToString("X"));

            rdata.writer.WriteStartElement(String.Empty,"Permissions",String.Empty);
            rdata.writer.WriteAttributeString("current",String.Empty,i.CurrentPermissions.ToString("X"));
            rdata.writer.WriteAttributeString("next",String.Empty,i.NextPermissions.ToString("X"));
            rdata.writer.WriteAttributeString("everyone",String.Empty,i.EveryOnePermissions.ToString("X"));
            rdata.writer.WriteAttributeString("base",String.Empty,i.BasePermissions.ToString("X"));
            rdata.writer.WriteEndElement();

            rdata.writer.WriteElementString("Asset",i.AssetID.ToString());

            rdata.writer.WriteEndElement();
        }

        /// <summary>
        /// This method creates a "trashcan" folder to support folder and item
        /// deletions by this interface. The xisting trash folder is found and
        /// this folder is created within it. It is called "tmp" to indicate to
        /// the client that it is OK to delete this folder. The REST interface
        /// will recreate the folder on an as-required basis.
        /// If the trash can cannot be created, then by implication the request
        /// that required it cannot be completed, and it fails accordingly.
        /// </summary>

        private InventoryFolderBase GetTrashCan(InventoryRequestData rdata)
        {
            InventoryFolderBase TrashCan = null;

            foreach (InventoryFolderBase f in rdata.folders)
            {
                if (f.Name == "Trash")
                {
                    foreach (InventoryFolderBase t in rdata.folders)
                    {
                        if (t.Name == "tmp")
                        {
                            TrashCan = t;
                        }
                    }
                    if (TrashCan == null)
                    {
                        TrashCan = new InventoryFolderBase();
                        TrashCan.Name = "tmp";
                        TrashCan.ID   = LLUUID.Random();
                        TrashCan.Version = 1;
                        TrashCan.Type = (short) AssetType.TrashFolder;
                        TrashCan.ParentID = f.ID;
                        TrashCan.Owner = f.Owner;
                        Rest.InventoryServices.AddFolder(TrashCan);
                    }
                }
            }

            if (TrashCan == null)
            {
                Rest.Log.DebugFormat("{0} No Trash Can available", MsgId);
                rdata.Fail(Rest.HttpStatusCodeServerError,
                           Rest.HttpStatusDescServerError+": unable to create trash can");
            }

            return TrashCan;
        }

        /// <summary>
        /// Make sure that an unchanged folder is not unnecessarily
        /// processed.
        /// </summary>

        private bool FolderHasChanged(InventoryFolderBase newf, InventoryFolderBase oldf)
        {
            return (newf.Name    != oldf.Name
                    || newf.ParentID    != oldf.ParentID
                    || newf.Owner       != oldf.Owner
                    || newf.Type        != oldf.Type
                    || newf.Version     != oldf.Version
                );
        }

        /// <summary>
        /// Make sure that an unchanged item is not unnecessarily
        /// processed.
        /// </summary>

        private bool ItemHasChanged(InventoryItemBase newf, InventoryItemBase oldf)
        {
            return (newf.Name    != oldf.Name
                    || newf.Folder      != oldf.Description
                    || newf.Description != oldf.Description
                    || newf.Owner       != oldf.Owner
                    || newf.Creator     != oldf.Creator
                    || newf.AssetID     != oldf.AssetID
                    || newf.GroupID     != oldf.GroupID
                    || newf.GroupOwned  != oldf.GroupOwned
                    || newf.InvType     != oldf.InvType
                    || newf.AssetType   != oldf.AssetType
                );
        }

        /// <summary>
        /// This method is called by PUT and POST to create an XmlInventoryCollection
        /// instance that reflects the content of the entity supplied on the request.
        /// Any elements in the completed collection whose UUID is zero, are
        /// considered to be located relative to the end-point identified int he
        /// URI. In this way, an entire sub-tree can be conveyed in a single REST
        /// PUT or POST request.
        ///
        /// A new instance of XmlInventoryCollection is created and, if the request
        /// has an entity, it is more completely initialized. thus, if no entity was
        /// provided the collection is valid, but empty.
        ///
        /// The entity is then scanned and each tag is processed to produce the
        /// appropriate inventory elements. At the end f the scan, teh XmlInventoryCollection
        /// will reflect the subtree described by the entity.
        ///
        /// This is a very flexible mechanism, the entity may contain arbitrary,
        /// discontiguous tree fragments, or may contain single element. The caller is
        /// responsible for integrating this collection (and ensuring that any
        /// missing parent IDs are resolved).
        /// </summary>

        internal XmlInventoryCollection ReconstituteEntity(InventoryRequestData rdata)
        {
            Rest.Log.DebugFormat("{0} Reconstituting entity", MsgId);

            XmlInventoryCollection ic = new XmlInventoryCollection();

            if (rdata.request.HasEntityBody)
            {
                Rest.Log.DebugFormat("{0} Entity present", MsgId);

                ic.init(rdata);

                try
                {
                    while (ic.xml.Read())
                    {
                        switch (ic.xml.NodeType)
                        {
                        case XmlNodeType.Element :
                            Rest.Log.DebugFormat("{0} StartElement: <{1}>",
                                                 MsgId, ic.xml.Name);
                            switch (ic.xml.Name)
                            {
                            case "Folder" :
                                Rest.Log.DebugFormat("{0} Processing {1} element",
                                                     MsgId, ic.xml.Name);
                                CollectFolder(ic);
                                break;
                            case "Item"   :
                                Rest.Log.DebugFormat("{0} Processing {1} element",
                                                     MsgId, ic.xml.Name);
                                CollectItem(ic);
                                break;
                            case "Asset"  :
                                Rest.Log.DebugFormat("{0} Processing {1} element",
                                                     MsgId, ic.xml.Name);
                                CollectAsset(ic);
                                break;
                            case "Permissions"  :
                                Rest.Log.DebugFormat("{0} Processing {1} element",
                                                     MsgId, ic.xml.Name);
                                CollectPermissions(ic);
                                break;
                            default :
                                Rest.Log.DebugFormat("{0} Ignoring {1} element",
                                                     MsgId, ic.xml.Name);
                                break;
                            }
                            // This stinks, but the ReadElement call above not only reads
                            // the imbedded data, but also consumes the end tag for Asset
                            // and moves the element pointer on to the containing Item's
                            // element-end, however, if there was a permissions element
                            // following, it would get us to the start of that..
                            if (ic.xml.NodeType == XmlNodeType.EndElement &&
                                ic.xml.Name     == "Item")
                            {
                                Validate(ic);
                            }
                            break;
                        case XmlNodeType.EndElement :
                            switch (ic.xml.Name)
                            {
                            case "Folder" :
                                Rest.Log.DebugFormat("{0} Completing {1} element",
                                                     MsgId, ic.xml.Name);
                                ic.Pop();
                                break;
                            case "Item"   :
                                Rest.Log.DebugFormat("{0} Completing {1} element",
                                                     MsgId, ic.xml.Name);
                                Validate(ic);
                                break;
                            case "Asset"  :
                                Rest.Log.DebugFormat("{0} Completing {1} element",
                                                     MsgId, ic.xml.Name);
                                break;
                            case "Permissions"  :
                                Rest.Log.DebugFormat("{0} Completing {1} element",
                                                     MsgId, ic.xml.Name);
                                break;
                            default :
                                Rest.Log.DebugFormat("{0} Ignoring {1} element",
                                                     MsgId, ic.xml.Name);
                                break;
                            }
                            break;
                        default :
                            Rest.Log.DebugFormat("{0} [0] Ignoring: <{1}>:<2>",
                                                 MsgId, ic.xml.NodeType, ic.xml.Value);
                            break;
                        }
                    }
                }
                catch (XmlException e)
                {
                    Rest.Log.WarnFormat("{0} XML parsing error: {1}", MsgId, e.Message);
                    throw e;
                }
                catch (Exception e)
                {
                    Rest.Log.WarnFormat("{0} Unexpected XML parsing error: {1}", MsgId, e.Message);
                    throw e;
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} Entity absent", MsgId);
            }

            if (Rest.DEBUG)
            {
                Rest.Log.DebugFormat("{0} Reconstituted entity", MsgId);
                Rest.Log.DebugFormat("{0} {1} assets", MsgId, ic.Assets.Count);
                Rest.Log.DebugFormat("{0} {1} folder", MsgId, ic.Folders.Count);
                Rest.Log.DebugFormat("{0} {1} items", MsgId, ic.Items.Count);
            }

            return ic;
        }

        /// <summary>
        /// This method creates an inventory Folder from the
        /// information supplied in the request's entity.
        /// A folder instance is created and initialized to reflect
        /// default values. These values are then overridden
        /// by information supplied in the entity.
        /// If context was not explicitly provided, then the
        /// appropriate ID values are determined.
        /// </summary>

        private void CollectFolder(XmlInventoryCollection ic)
        {
            Rest.Log.DebugFormat("{0} Interpret folder element", MsgId);

            InventoryFolderBase result = new InventoryFolderBase();

            // Default values

            result.Name     = String.Empty;
            result.ID       = LLUUID.Zero;
            result.Owner    = ic.UserID;
            result.ParentID = LLUUID.Zero; // Context
            result.Type     = (short) AssetType.Folder;
            result.Version  =  1;

            if (ic.xml.HasAttributes)
            {
                for (int i = 0; i < ic.xml.AttributeCount; i++)
                {
                    ic.xml.MoveToAttribute(i);
                    switch (ic.xml.Name)
                    {
                    case "name" :
                        result.Name     =     ic.xml.Value;
                        break;
                    case "uuid" :
                        result.ID       = new LLUUID(ic.xml.Value);
                        break;
                    case "parent" :
                        result.ParentID = new LLUUID(ic.xml.Value);
                        break;
                    case "owner" :
                        result.Owner    = new LLUUID(ic.xml.Value);
                        break;
                    case "type" :
                        result.Type     =     Int16.Parse(ic.xml.Value);
                        break;
                    case "version" :
                        result.Version  =     UInt16.Parse(ic.xml.Value);
                        break;
                    default :
                        Rest.Log.DebugFormat("{0} Folder: unrecognized attribute: {1}:{2}",
                                             MsgId, ic.xml.Name, ic.xml.Value);
                        ic.Fail(Rest.HttpStatusCodeBadRequest,
                                Rest.HttpStatusDescBadRequest+": unrecognized attribute");
                        break;
                    }
                }
            }

            ic.xml.MoveToElement();

            // The client is relying upon the reconstitution process
            // to determine the parent's UUID based upon context. This
            // is necessary where a new folder may have been
            // introduced.

            if (result.ParentID == LLUUID.Zero)
            {
                result.ParentID = ic.Parent();
            }
            else
            {
                bool found = false;

                foreach (InventoryFolderBase parent in ic.rdata.folders)
                {
                    if (parent.ID == result.ParentID)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Rest.Log.ErrorFormat("{0} Invalid parent ID ({1}) in folder {2}",
                                         MsgId, ic.Item.Folder, result.ID);
                    ic.Fail(Rest.HttpStatusCodeBadRequest,
                            Rest.HttpStatusDescBadRequest+": invalid parent");
                }
            }

            // This is a new folder, so no existing UUID is available
            // or appropriate

            if (result.ID == LLUUID.Zero)
            {
                result.ID = LLUUID.Random();
            }

            // Treat this as a new context. Any other information is
            // obsolete as a consequence.

            ic.Push(result);
        }

        /// <summary>
        /// This method is called to handle the construction of an Item
        /// instance from the supplied request entity. It is called
        /// whenever an Item start tag is detected.
        /// An instance of an Item is created and initialized to default
        /// values. These values are then overridden from values supplied
        /// as attributes to the Item element.
        /// This item is then stored in the XmlInventoryCollection and
        /// will be verified by Validate.
        /// All context is reset whenever the effective folder changes
        /// or an item is successfully validated.
        /// </summary>

        private void CollectItem(XmlInventoryCollection ic)
        {
            Rest.Log.DebugFormat("{0} Interpret item element", MsgId);

            InventoryItemBase result = new InventoryItemBase();

            result.Name        = String.Empty;
            result.Description = String.Empty;
            result.ID          = LLUUID.Zero;
            result.Folder      = LLUUID.Zero;
            result.Owner       = ic.UserID;
            result.Creator     = ic.UserID;
            result.AssetID     = LLUUID.Zero;
            result.GroupID     = LLUUID.Zero;
            result.GroupOwned  = false;
            result.InvType     = (int) InventoryType.Unknown;
            result.AssetType   = (int) AssetType.Unknown;

            if (ic.xml.HasAttributes)
            {
                for (int i = 0; i < ic.xml.AttributeCount; i++)
                {
                    ic.xml.MoveToAttribute(i);

                    switch (ic.xml.Name)
                    {
                    case "name" :
                        result.Name         = ic.xml.Value;
                        break;
                    case "desc" :
                        result.Description  = ic.xml.Value;
                        break;
                    case "uuid" :
                        result.ID           = new LLUUID(ic.xml.Value);
                        break;
                    case "folder" :
                        result.Folder       = new LLUUID(ic.xml.Value);
                        break;
                    case "owner" :
                        result.Owner        = new LLUUID(ic.xml.Value);
                        break;
                    case "invtype" :
                        result.InvType      =     Int32.Parse(ic.xml.Value);
                        break;
                    case "creator" :
                        result.Creator      = new LLUUID(ic.xml.Value);
                        break;
                    case "assettype" :
                        result.AssetType    =     Int32.Parse(ic.xml.Value);
                        break;
                    case "groupowned" :
                        result.GroupOwned   =     Boolean.Parse(ic.xml.Value);
                        break;
                    case "groupid" :
                        result.GroupID      = new LLUUID(ic.xml.Value);
                        break;
                    case "flags" :
                        result.Flags        =     UInt32.Parse(ic.xml.Value);
                        break;
                    case "creationdate" :
                        result.CreationDate =     Int32.Parse(ic.xml.Value);
                        break;
                    case "saletype" :
                        result.SaleType     =     Byte.Parse(ic.xml.Value);
                        break;
                    case "saleprice" :
                        result.SalePrice    =     Int32.Parse(ic.xml.Value);
                        break;

                    default :
                        Rest.Log.DebugFormat("{0} Item: Unrecognized attribute: {1}:{2}",
                                             MsgId, ic.xml.Name, ic.xml.Value);
                        ic.Fail(Rest.HttpStatusCodeBadRequest,
                                Rest.HttpStatusDescBadRequest+": unrecognized attribute");
                        break;
                    }
                }
            }

            ic.xml.MoveToElement();

            ic.Push(result);
        }

        /// <summary>
        /// This method assembles an asset instance from the
        /// information supplied in the request's entity. It is
        /// called as a result of detecting a start tag for a
        /// type of Asset.
        /// The information is collected locally, and an asset
        /// instance is created only if the basic XML parsing
        /// completes successfully.
        /// Default values for all parts of the asset are
        /// established before overriding them from the supplied
        /// XML.
        /// If an asset has inline=true as an attribute, then
        /// the element contains the data representing the
        /// asset. This is saved as the data component.
        /// inline=false means that the element's payload is
        /// simply the UUID of the asset referenced by the
        /// item being constructed.
        /// An asset, if created is stored in the
        /// XmlInventoryCollection
        /// </summary>

        private void CollectAsset(XmlInventoryCollection ic)
        {
            Rest.Log.DebugFormat("{0} Interpret asset element", MsgId);

            AssetBase asset = null;

            string    name  = String.Empty;
            string    desc  = String.Empty;
            sbyte     type  = (sbyte) AssetType.Unknown;
            bool      temp  = false;
            bool     local  = false;

            // This is not a persistent attribute
            bool    inline  = true;

            LLUUID    uuid  = LLUUID.Zero;

            // Attribute is optional
            if (ic.xml.HasAttributes)
            {
                for (int i = 0; i < ic.xml.AttributeCount; i++)
                {
                    ic.xml.MoveToAttribute(i);
                    switch (ic.xml.Name)
                    {

                    case "name" :
                        name = ic.xml.Value;
                        break;

                    case "type" :
                        type = SByte.Parse(ic.xml.Value);
                        break;

                    case "description" :
                        desc = ic.xml.Value;
                        break;

                    case "temporary" :
                        temp = Boolean.Parse(ic.xml.Value);
                        break;

                    case "uuid" :
                        uuid = new LLUUID(ic.xml.Value);
                        break;

                    case "inline" :
                        inline = Boolean.Parse(ic.xml.Value);
                        break;

                    case "local" :
                        local = Boolean.Parse(ic.xml.Value);
                        break;

                    default :
                        Rest.Log.DebugFormat("{0} Asset: Unrecognized attribute: {1}:{2}",
                                             MsgId, ic.xml.Name, ic.xml.Value);
                        ic.Fail(Rest.HttpStatusCodeBadRequest,
                                Rest.HttpStatusDescBadRequest);
                        break;
                    }
                }
            }

            ic.xml.MoveToElement();

            // If this is a reference to an existing asset, just store the
            // asset ID into the item.

            if (!inline)
            {
                if (ic.Item != null)
                {
                    ic.Item.AssetID = new LLUUID(ic.xml.ReadElementContentAsString());
                    Rest.Log.DebugFormat("{0} Asset ID supplied: {1}", MsgId, ic.Item.AssetID);
                }
                else
                {
                    Rest.Log.DebugFormat("{0} LLUID unimbedded asset must be inline", MsgId);
                    ic.Fail(Rest.HttpStatusCodeBadRequest,
                            Rest.HttpStatusDescBadRequest+": no context for asset");
                }
            }

            // Otherwise, generate an asset ID, store that into the item, and
            // create an entry in the asset list for the inlined asset. But
            // only if the size is non-zero.

            else
            {
                string b64string = null;

                // Generate a UUID of none were given, and generally none should
                // be. Ever.

                if (uuid == LLUUID.Zero)
                {
                    uuid = LLUUID.Random();
                }

                // Create AssetBase entity to hold the inlined asset

                asset = new AssetBase(uuid, name);

                asset.Description = desc;
                asset.Type        = type; // type == 0 == texture
                asset.Local       = local;
                asset.Temporary   = temp;

                b64string         = ic.xml.ReadElementContentAsString();

                Rest.Log.DebugFormat("{0} Data length is {1}", MsgId, b64string.Length);
                Rest.Log.DebugFormat("{0} Data content starts with: \n\t<{1}>", MsgId,
                                     b64string.Substring(0, b64string.Length > 132 ? 132 : b64string.Length));

                asset.Data        = Convert.FromBase64String(b64string);

                // Ensure the asset always has some kind of data component

                if (asset.Data == null)
                {
                    asset.Data = new byte[1];
                }

                // If this is in the context of an item, establish
                // a link with the item in context.

                if (ic.Item != null && ic.Item.AssetID == LLUUID.Zero)
                {
                    ic.Item.AssetID = uuid;
                }
            }

            ic.Push(asset);
        }

        /// <summary>
        /// Store any permissions information provided by the request.
        /// This overrides the default permissions set when the
        /// XmlInventoryCollection object was created.
        /// </summary>

        private void CollectPermissions(XmlInventoryCollection ic)
        {
            if (ic.xml.HasAttributes)
            {
                for (int i = 0; i < ic.xml.AttributeCount; i++)
                {
                    ic.xml.MoveToAttribute(i);
                    switch (ic.xml.Name)
                    {
                    case "current" :
                        ic.CurrentPermissions  = UInt32.Parse(ic.xml.Value, System.Globalization.NumberStyles.HexNumber);
                        break;
                    case "next" :
                        ic.NextPermissions     = UInt32.Parse(ic.xml.Value, System.Globalization.NumberStyles.HexNumber);
                        break;
                    case "everyone" :
                        ic.EveryOnePermissions = UInt32.Parse(ic.xml.Value, System.Globalization.NumberStyles.HexNumber);
                        break;
                    case "base" :
                        ic.BasePermissions     = UInt32.Parse(ic.xml.Value, System.Globalization.NumberStyles.HexNumber);
                        break;
                    default :
                        Rest.Log.DebugFormat("{0} Permissions:  invalid attribute {1}:{2}",
                                             MsgId,ic.xml.Name, ic.xml.Value);
                        ic.Fail(Rest.HttpStatusCodeBadRequest,
                                Rest.HttpStatusDescBadRequest);
                        break;
                    }
                }
            }

            ic.xml.MoveToElement();
        }

        /// <summary>
        /// This method is called whenever an Item has been successfully
        /// reconstituted from the request's entity.
        /// It uses the information curren tin the XmlInventoryCollection
        /// to complete the item's specification, including any implied
        /// context and asset associations.
        /// It fails the request if any necessary item or asset information
        /// is missing.
        /// </summary>

        private void Validate(XmlInventoryCollection ic)
        {
            // There really should be an item present if we've
            // called validate. So fail if there is not.

            if (ic.Item == null)
            {
                Rest.Log.ErrorFormat("{0} Unable to parse request", MsgId);
                ic.Fail(Rest.HttpStatusCodeBadRequest,
                        Rest.HttpStatusDescBadRequest+": request parse error");
            }

            // Every item is required to have a name (via REST anyway)

            if (ic.Item.Name == String.Empty)
            {
                Rest.Log.ErrorFormat("{0} An item name MUST be specified", MsgId);
                ic.Fail(Rest.HttpStatusCodeBadRequest,
                        Rest.HttpStatusDescBadRequest+": item name required");
            }

            // An item MUST have an asset ID. AssetID should never be zero
            // here. It should always get set from the information stored
            // when the Asset element was processed.

            if (ic.Item.AssetID == LLUUID.Zero)
            {
                Rest.Log.ErrorFormat("{0} Unable to complete request", MsgId);
                Rest.Log.InfoFormat("{0} Asset information is missing", MsgId);
                ic.Fail(Rest.HttpStatusCodeBadRequest,
                        Rest.HttpStatusDescBadRequest+": asset information required");
            }

            // If the item is new, then assign it an ID

            if (ic.Item.ID == LLUUID.Zero)
            {
                ic.Item.ID = LLUUID.Random();
            }

            // If the context is being implied, obtain the current
            // folder item's ID. If it was specified explicitly, make
            // sure that theparent folder exists.

            if (ic.Item.Folder == LLUUID.Zero)
            {
                ic.Item.Folder = ic.Parent();
            }
            else
            {
                bool found = false;

                foreach (InventoryFolderBase parent in ic.rdata.folders)
                {
                    if (parent.ID == ic.Item.Folder)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Rest.Log.ErrorFormat("{0} Invalid parent ID ({1}) in item {2}",
                                         MsgId, ic.Item.Folder, ic.Item.ID);
                    ic.Fail(Rest.HttpStatusCodeBadRequest,
                            Rest.HttpStatusDescBadRequest+": parent information required");
                }
            }

            // If this is an inline asset being constructed in the context
            // of a new Item, then use the itm's name here too.

            if (ic.Asset != null)
            {
                if (ic.Asset.Name == String.Empty)
                    ic.Asset.Name = ic.Item.Name;
                if (ic.Asset.Description == String.Empty)
                    ic.Asset.Description = ic.Item.Description;
            }

            // Assign permissions

            ic.Item.CurrentPermissions  = ic.CurrentPermissions;
            ic.Item.EveryOnePermissions = ic.EveryOnePermissions;
            ic.Item.BasePermissions     = ic.BasePermissions;
            ic.Item.NextPermissions     = ic.NextPermissions;

            // If no type was specified for this item, we can attempt to
            // infer something from the file type maybe. This is NOT as
            // good as having type be specified in the XML.

            if (ic.Item.AssetType == (int) AssetType.Unknown ||
                ic.Item.InvType   == (int) AssetType.Unknown)
            {
                Rest.Log.DebugFormat("{0} Attempting to infer item type", MsgId);

                string[] parts = ic.Item.Name.Split(Rest.CA_PERIOD);

                if (Rest.DEBUG)
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        Rest.Log.DebugFormat("{0} Name part {1} : {2}",
                                             MsgId, i, parts[i]);
                    }
                }

                // If the associated item name is multi-part, then maybe
                // the last part will indicate the item type - if we're
                // lucky.

                if (parts.Length > 1)
                {
                    Rest.Log.DebugFormat("{0} File type is {1}",
                                         MsgId, parts[parts.Length - 1]);
                    switch (parts[parts.Length - 1])
                    {
                    case "jpeg2000" :
                    case "jpeg-2000" :
                    case "jpg2000" :
                    case "jpg-2000" :
                        Rest.Log.DebugFormat("{0} Type {1} inferred",
                                             MsgId, parts[parts.Length-1]);
                        if (ic.Item.AssetType == (int) AssetType.Unknown)
                            ic.Item.AssetType = (int) AssetType.ImageJPEG;
                        if (ic.Item.InvType == (int) AssetType.Unknown)
                            ic.Item.InvType   = (int) AssetType.ImageJPEG;
                        break;
                    case "jpg" :
                    case "jpeg" :
                        Rest.Log.DebugFormat("{0} Type {1} inferred",
                                             MsgId, parts[parts.Length - 1]);
                        if (ic.Item.AssetType == (int) AssetType.Unknown)
                            ic.Item.AssetType = (int) AssetType.ImageJPEG;
                        if (ic.Item.InvType == (int) AssetType.Unknown)
                            ic.Item.InvType   = (int) AssetType.ImageJPEG;
                        break;
                    case "tga" :
                        if (parts[parts.Length - 2].IndexOf("_texture") != -1)
                        {
                            if (ic.Item.AssetType == (int) AssetType.Unknown)
                                ic.Item.AssetType = (int) AssetType.TextureTGA;
                            if (ic.Item.InvType == (int) AssetType.Unknown)
                                ic.Item.InvType   = (int) AssetType.TextureTGA;
                        }
                        else
                        {
                            if (ic.Item.AssetType == (int) AssetType.Unknown)
                                ic.Item.AssetType = (int) AssetType.ImageTGA;
                            if (ic.Item.InvType == (int) AssetType.Unknown)
                                ic.Item.InvType   = (int) AssetType.ImageTGA;
                        }
                        break;
                    default :
                        Rest.Log.DebugFormat("{0} Type was not inferred", MsgId);
                        break;
                    }
                }
            }

            /// If this is a TGA remember the fact

            if (ic.Item.AssetType == (int) AssetType.TextureTGA ||
               ic.Item.AssetType == (int) AssetType.ImageTGA)
            {
                Bitmap temp;
                Stream tgadata = new MemoryStream(ic.Asset.Data);

                temp = OpenJPEGNet.LoadTGAClass.LoadTGA(tgadata);
                ic.Asset.Data = OpenJPEGNet.OpenJPEG.EncodeFromImage(temp, true);
            }

            ic.reset();
        }

        #region Inventory RequestData extension

        internal class InventoryRequestData : RequestData
        {
            /// <summary>
            /// These are the inventory specific request/response state
            /// extensions.
            /// </summary>

            internal bool                       HaveInventory = false;
            internal ICollection<InventoryFolderImpl> folders = null;
            internal ICollection<InventoryItemBase>     items = null;
            internal UserProfileData              userProfile = null;
            internal InventoryFolderBase                 root = null;

            internal InventoryRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }

            /// <summary>
            /// This is the callback method required by inventory services. The
            /// requestor issues an inventory request and then blocks until this
            /// method signals the monitor.
            /// </summary>

            internal void GetUserInventory(ICollection<InventoryFolderImpl> folders, ICollection<InventoryItemBase> items)
            {
                Rest.Log.DebugFormat("{0} Asynchronously updating inventory data", MsgId);
                this.folders = folders;
                this.items   = items;
                this.HaveInventory = true;
                lock (this)
                {
                    Monitor.Pulse(this);
                }
            }
        }

        #endregion Inventory RequestData extension

        /// <summary>
        /// This class is used to record and manage the hierarchy
        /// constructed from the entity supplied in the request for
        /// PUT and POST.
        /// </summary>

        internal class XmlInventoryCollection : InventoryCollection
        {
            internal InventoryRequestData       rdata;
            private  Stack<InventoryFolderBase> stk;

            internal List<AssetBase>            Assets;

            internal InventoryItemBase          Item;
            internal AssetBase                  Asset;
            internal XmlReader                  xml;

            internal /*static*/ const uint DefaultCurrent  = 0x7FFFFFFF;
            internal /*static*/ const uint DefaultNext     = 0x82000;
            internal /*static*/ const uint DefaultBase     = 0x7FFFFFFF;
            internal /*static*/ const uint DefaultEveryOne = 0x0;

            internal uint      CurrentPermissions  = 0x00;
            internal uint      NextPermissions     = 0x00;
            internal uint      BasePermissions     = 0x00;
            internal uint      EveryOnePermissions = 0x00;

            internal XmlInventoryCollection()
            {
                Folders = new List<InventoryFolderBase>();
                Items   = new List<InventoryItemBase>();
                Assets  = new List<AssetBase>();
            }

            internal void init(InventoryRequestData p_rdata)
            {
                rdata   = p_rdata;
                UserID  = rdata.uuid;
                stk     = new Stack<InventoryFolderBase>();
                rdata.initXmlReader();
                xml     = rdata.reader;
                initPermissions();
            }

            internal void initPermissions()
            {
                CurrentPermissions  = DefaultCurrent;
                NextPermissions     = DefaultNext;
                BasePermissions     = DefaultBase;
                EveryOnePermissions = DefaultEveryOne;
            }

            internal LLUUID Parent()
            {
                if (stk.Count != 0)
                {
                    return stk.Peek().ID;
                }
                else
                {
                    return LLUUID.Zero;
                }
            }

            internal void Push(InventoryFolderBase folder)
            {
                stk.Push(folder);
                Folders.Add(folder);
                reset();
            }

            internal void Push(InventoryItemBase item)
            {
                Item = item;
                Items.Add(item);
            }

            internal void Push(AssetBase asset)
            {
                Asset = asset;
                Assets.Add(asset);
            }

            internal void Pop()
            {
                stk.Pop();
                reset();
            }

            internal void reset()
            {
                Item  = null;
                Asset = null;
                initPermissions();
            }

            internal void Fail(int code, string desc)
            {
                rdata.Fail(code, desc);
            }
        }
    }
}
