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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Timers;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;

using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Timer=System.Timers.Timer;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{
    public class RestInventoryServices : IRest
    {
//        private static readonly int PARM_USERID = 0;
//        private static readonly int PARM_PATH   = 1;

//        private bool       enabled = false;
        private string     qPrefix = "inventory";

//        private static readonly string PRIVATE_ROOT_NAME = "My Inventory";

        /// <summary>
        /// The constructor makes sure that the service prefix is absolute
        /// and the registers the service handler and the allocator.
        /// </summary>

        public RestInventoryServices()
        {
            Rest.Log.InfoFormat("{0} Inventory services initializing", MsgId);
            Rest.Log.InfoFormat("{0} Using REST Implementation Version {1}", MsgId, Rest.Version);

            // If a relative path was specified for the handler's domain,
            // add the standard prefix to make it absolute, e.g. /admin

            if (!qPrefix.StartsWith(Rest.UrlPathSeparator))
            {
                Rest.Log.InfoFormat("{0} Domain is relative, adding absolute prefix", MsgId);
                qPrefix = String.Format("{0}{1}{2}", Rest.Prefix, Rest.UrlPathSeparator, qPrefix);
                Rest.Log.InfoFormat("{0} Domain is now <{1}>", MsgId, qPrefix);
            }

            // Register interface using the absolute URI.

            Rest.Plugin.AddPathHandler(DoInventory,qPrefix,Allocate);

            // Activate if everything went OK

//            enabled = true;

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
        /// Called by the plug-in to halt service processing. Local processing is
        /// disabled.
        /// </summary>

        public void Close()
        {
//            enabled = false;
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
        /// <param name=request>Inbound HTTP request information</param>
        /// <param name=response>Outbound HTTP request information</param>
        /// <param name=qPrefix>REST service domain prefix</param>
        /// <returns>A RequestData instance suitable for this service</returns>
        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response, string prefix)
        {
            return (RequestData) new InventoryRequestData(request, response, prefix);
        }

        /// <summary>
        /// This method is registered with the handler when this service provider
        /// is initialized. It is called whenever the plug-in identifies this service
        /// provider as the best match for a given request.
        /// It handles all aspects of inventory REST processing, i.e. /admin/inventory
        /// </summary>
        /// <param name=hdata>A consolidated HTTP request work area</param>
        private void DoInventory(RequestData hdata)
        {
//            InventoryRequestData rdata = (InventoryRequestData) hdata;

            Rest.Log.DebugFormat("{0} DoInventory ENTRY", MsgId);

            // !!! REFACTORING PROBLEM

            //// If we're disabled, do nothing.

            //if (!enabled)
            //{
            //    return;
            //}

            //// Now that we know this is a serious attempt to
            //// access inventory data, we should find out who
            //// is asking, and make sure they are authorized
            //// to do so. We need to validate the caller's
            //// identity before revealing anything about the
            //// status quo. Authenticate throws an exception
            //// via Fail if no identity information is present.
            ////
            //// With the present HTTP server we can't use the
            //// builtin authentication mechanisms because they
            //// would be enforced for all in-bound requests.
            //// Instead we look at the headers ourselves and
            //// handle authentication directly.

            //try
            //{
            //    if (!rdata.IsAuthenticated)
            //    {
            //        rdata.Fail(Rest.HttpStatusCodeNotAuthorized,String.Format("user \"{0}\" could not be authenticated", rdata.userName));
            //    }
            //}
            //catch (RestException e)
            //{
            //    if (e.statusCode == Rest.HttpStatusCodeNotAuthorized)
            //    {
            //        Rest.Log.WarnFormat("{0} User not authenticated", MsgId);
            //        Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
            //    }
            //    else
            //    {
            //        Rest.Log.ErrorFormat("{0} User authentication failed", MsgId);
            //        Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
            //    }
            //    throw (e);
            //}

            //Rest.Log.DebugFormat("{0} Authenticated {1}", MsgId, rdata.userName);

            //// We can only get here if we are authorized
            ////
            //// The requestor may have specified an UUID or
            //// a conjoined FirstName LastName string. We'll
            //// try both. If we fail with the first, UUID,
            //// attempt, we try the other. As an example, the
            //// URI for a valid inventory request might be:
            ////
            //// http://<host>:<port>/admin/inventory/Arthur Dent
            ////
            //// Indicating that this is an inventory request for
            //// an avatar named Arthur Dent. This is ALL that is
            //// required to designate a GET for an entire
            //// inventory.
            ////


            //// Do we have at least a user agent name?

            //if (rdata.Parameters.Length < 1)
            //{
            //    Rest.Log.WarnFormat("{0} Inventory: No user agent identifier specified", MsgId);
            //    rdata.Fail(Rest.HttpStatusCodeBadRequest, "no user identity specified");
            //}

            //// The first parameter MUST be the agent identification, either an UUID
            //// or a space-separated First-name Last-Name specification. We check for
            //// an UUID first, if anyone names their character using a valid UUID
            //// that identifies another existing avatar will cause this a problem...

            //try
            //{
            //    rdata.uuid = new UUID(rdata.Parameters[PARM_USERID]);
            //    Rest.Log.DebugFormat("{0} UUID supplied", MsgId);
            //    rdata.userProfile = Rest.UserServices.GetUserProfile(rdata.uuid);
            //}
            //catch
            //{
            //    string[] names = rdata.Parameters[PARM_USERID].Split(Rest.CA_SPACE);
            //    if (names.Length == 2)
            //    {
            //        Rest.Log.DebugFormat("{0} Agent Name supplied [2]", MsgId);
            //        rdata.userProfile = Rest.UserServices.GetUserProfile(names[0],names[1]);
            //    }
            //    else
            //    {
            //        Rest.Log.WarnFormat("{0} A Valid UUID or both first and last names must be specified", MsgId);
            //        rdata.Fail(Rest.HttpStatusCodeBadRequest, "invalid user identity");
            //    }
            //}

            //// If the user profile is null then either the server is broken, or the
            //// user is not known. We always assume the latter case.

            //if (rdata.userProfile != null)
            //{
            //    Rest.Log.DebugFormat("{0} Profile obtained for agent {1} {2}",
            //                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            //}
            //else
            //{
            //    Rest.Log.WarnFormat("{0} No profile for {1}", MsgId, rdata.path);
            //    rdata.Fail(Rest.HttpStatusCodeNotFound, "unrecognized user identity");
            //}

            //// If we get to here, then we have effectively validated the user's
            //// identity. Now we need to get the inventory. If the server does not
            //// have the inventory, we reject the request with an appropriate explanation.
            ////
            //// Note that inventory retrieval is an asynchronous event, we use the rdata
            //// class instance as the basis for our synchronization.
            ////

            //rdata.uuid = rdata.userProfile.ID;

            //if (Rest.InventoryServices.HasInventoryForUser(rdata.uuid))
            //{
            //    rdata.root = Rest.InventoryServices.GetRootFolder(rdata.uuid);

            //    Rest.Log.DebugFormat("{0} Inventory Root retrieved for {1} {2}",
            //                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);

            //    Rest.InventoryServices.GetUserInventory(rdata.uuid, rdata.GetUserInventory);

            //    Rest.Log.DebugFormat("{0} Inventory catalog requested for {1} {2}",
            //                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);

            //    lock (rdata)
            //    {
            //        if (!rdata.HaveInventory)
            //        {
            //            rdata.startWD(1000);
            //            rdata.timeout = false;
            //            Monitor.Wait(rdata);
            //        }
            //    }

            //    if (rdata.timeout)
            //    {
            //        Rest.Log.WarnFormat("{0} Inventory not available for {1} {2}. No response from service.",
            //                             MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            //        rdata.Fail(Rest.HttpStatusCodeServerError, "inventory server not responding");
            //    }

            //    if (rdata.root == null)
            //    {
            //        Rest.Log.WarnFormat("{0} Inventory is not available [1] for agent {1} {2}",
            //                             MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            //        rdata.Fail(Rest.HttpStatusCodeServerError, "inventory retrieval failed");
            //    }

            //}
            //else
            //{
            //    Rest.Log.WarnFormat("{0} Inventory is not locally available for agent {1} {2}",
            //                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            //    rdata.Fail(Rest.HttpStatusCodeNotFound, "no local inventory for user");
            //}

            //// If we get here, then we have successfully retrieved the user's information
            //// and inventory information is now available locally.

            //switch (rdata.method)
            //{
            //    case Rest.HEAD   : // Do the processing, set the status code, suppress entity
            //        DoGet(rdata);
            //        rdata.buffer = null;
            //        break;

            //    case Rest.GET    : // Do the processing, set the status code, return entity
            //        DoGet(rdata);
            //        break;

            //    case Rest.PUT    : // Update named element
            //        DoUpdate(rdata);
            //        break;

            //    case Rest.POST   : // Add new information to identified context.
            //        DoExtend(rdata);
            //        break;

            //    case Rest.DELETE : // Delete information
            //        DoDelete(rdata);
            //        break;

            //    default :
            //        Rest.Log.WarnFormat("{0} Method {1} not supported for {2}",
            //                            MsgId, rdata.method, rdata.path);
            //        rdata.Fail(Rest.HttpStatusCodeMethodNotAllowed,
            //                   String.Format("{0} not supported", rdata.method));
            //        break;
            //}
        }

        #endregion Interface

        #region method-specific processing

        /// <summary>
        /// This method implements GET processing for inventory.
        /// Any remaining parameters are used to locate the
        /// corresponding subtree based upon node name.
        /// </summary>
        /// <param name=rdata>HTTP service request work area</param>
//        private void DoGet(InventoryRequestData rdata)
//        {
//            rdata.initXmlWriter();
//
//            rdata.writer.WriteStartElement(String.Empty,"Inventory",String.Empty);
//
//            // If there are additional parameters, then these represent
//            // a path relative to the root of the inventory. This path
//            // must be traversed before we format the sub-tree thus
//            // identified.
//
//            traverse(rdata, rdata.root, PARM_PATH);
//
//            // Close all open elements
//
//            rdata.writer.WriteFullEndElement();
//
//            // Indicate a successful request
//
//            rdata.Complete();
//
//            // Send the response to the user. The body will be implicitly
//            // constructed from the result of the XML writer.
//
//            rdata.Respond(String.Format("Inventory {0} Normal completion", rdata.method));
//        }

        /// <summary>
        /// In the case of the inventory, and probably in general,
        /// the distinction between PUT and POST is not always
        /// easy to discern. The standard is badly worded in places,
        /// and adding a node to a hierarchy can be viewed as
        /// an addition, or as a modification to the inventory as
        /// a whole. This is exacerbated by an unjustified lack of
        /// consistency across different implementations.
        ///
        /// For OpenSim PUT is an update and POST is an addition. This
        /// is the behavior required by the HTTP specification and
        /// therefore as required by REST.
        ///
        /// The best way to explain the distinction is to
        /// consider the relationship between the URI and the
        /// enclosed entity. For PUT, the URI identifies the
        /// actual entity to be modified or replaced, i.e. the
        /// enclosed entity.
        ///
        /// If the operation is POST,then the URI describes the
        /// context into which the new entity will be added.
        ///
        /// As an example, suppose the URI contains:
        ///      /admin/inventory/Clothing
        ///
        /// A PUT request will normally result in some modification of
        /// the folder or item named "Clothing". Whereas a POST
        /// request will normally add some new information into the
        /// content identified by Clothing. It follows from this
        /// that for POST, the element identified by the URI MUST
        /// be a folder.
        /// </summary>

        /// <summary>
        /// POST adds new information to the inventory in the
        /// context identified by the URI.
        /// </summary>
        /// <param name=rdata>HTTP service request work area</param>
//        private void DoExtend(InventoryRequestData rdata)
//        {
//            bool  created  = false;
//            bool  modified = false;
//            string newnode = String.Empty;
//
//            // Resolve the context node specified in the URI. Entity
//            // data will be ADDED beneath this node. rdata already contains
//            // information about the current content of the user's
//            // inventory.
//
//            Object InventoryNode = getInventoryNode(rdata, rdata.root, PARM_PATH, Rest.Fill);
//
//            // Processing depends upon the type of inventory node
//            // identified in the URI. This is the CONTEXT for the
//            // change. We either got a context or we threw an
//            // exception.
//
//            // It follows that we can only add information if the URI
//            // has identified a folder. So only a type of folder is supported
//            // in this case.
//
//            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
//                typeof(InventoryFolderImpl) == InventoryNode.GetType())
//            {
//                // Cast the context node appropriately.
//
//                InventoryFolderBase context    = (InventoryFolderBase) InventoryNode;
//
//                Rest.Log.DebugFormat("{0} {1}: Resource(s) will be added to folder {2}",
//                                     MsgId, rdata.method, rdata.path);
//
//                // Reconstitute the inventory sub-tree from the XML supplied in the entity.
//                // The result is a stand-alone inventory subtree, not yet integrated into the
//                // existing tree. An inventory collection consists of three components:
//                // [1] A (possibly empty) set of folders.
//                // [2] A (possibly empty) set of items.
//                // [3] A (possibly empty) set of assets.
//                // If all of these are empty, then the POST is a harmless no-operation.
//
//                XmlInventoryCollection entity = ReconstituteEntity(rdata);
//
//                // Inlined assets can be included in entity. These must be incorporated into
//                // the asset database before we attempt to update the inventory. If anything
//                // fails, return a failure to requestor.
//
//                if (entity.Assets.Count > 0)
//                {
//                    Rest.Log.DebugFormat("{0} Adding {1} assets to server",
//                                         MsgId, entity.Assets.Count);
//
//                    foreach (AssetBase asset in entity.Assets)
//                    {
//                        Rest.Log.DebugFormat("{0} Rest asset: {1} {2} {3}",
//                                             MsgId, asset.ID, asset.Type, asset.Name);
//                        Rest.AssetServices.Store(asset);
//
//                        created = true;
//                        rdata.appendStatus(String.Format("<p> Created asset {0}, UUID {1} <p>",
//                                        asset.Name, asset.ID));
//
//                        if (Rest.DEBUG && Rest.DumpAsset)
//                        {
//                            Rest.Dump(asset.Data);
//                        }
//                    }
//                }
//
//                // Modify the context using the collection of folders and items
//                // returned in the XmlInventoryCollection.
//
//                foreach (InventoryFolderBase folder in entity.Folders)
//                {
//                    InventoryFolderBase found;
//
//                    // If the parentID is zero, then this folder is going
//                    // into the root folder identified by the URI. The requestor
//                    // may have already set the parent ID explicitly, in which
//                    // case we don't have to do it here.
//
//                    if (folder.ParentID == UUID.Zero || folder.ParentID == context.ID)
//                    {
//                        if (newnode != String.Empty)
//                        {
//                            Rest.Log.DebugFormat("{0} Too many resources", MsgId);
//                            rdata.Fail(Rest.HttpStatusCodeBadRequest, "only one root entity is allowed");
//                        }
//                        folder.ParentID = context.ID;
//                        newnode = folder.Name;
//                    }
//
//                    // Search the existing inventory for an existing entry. If
//                    // we have one, we need to decide if it has really changed.
//                    // It could just be present as (unnecessary) context, and we
//                    // don't want to waste time updating the database in that
//                    // case, OR, it could be being moved from another location
//                    // in which case an update is most certainly necessary.
//
//                    found = null;
//
//                    foreach (InventoryFolderBase xf in rdata.folders)
//                    {
//                        // Compare identifying attribute
//                        if (xf.ID == folder.ID)
//                        {
//                            found = xf;
//                            break;
//                        }
//                    }
//
//                    if (found != null && FolderHasChanged(folder,found))
//                    {
//                        Rest.Log.DebugFormat("{0} Updating existing folder", MsgId);
//                        Rest.InventoryServices.MoveFolder(folder);
//
//                        modified = true;
//                        rdata.appendStatus(String.Format("<p> Created folder {0}, UUID {1} <p>",
//                                                         folder.Name, folder.ID));
//                    }
//                    else
//                    {
//                        Rest.Log.DebugFormat("{0} Adding new folder", MsgId);
//                        Rest.InventoryServices.AddFolder(folder);
//
//                        created = true;
//                        rdata.appendStatus(String.Format("<p> Modified folder {0}, UUID {1} <p>",
//                                                         folder.Name, folder.ID));
//                    }
//                }
//
//                // Now we repeat a similar process for the items included
//                // in the entity.
//
//                foreach (InventoryItemBase item in entity.Items)
//                {
//                    InventoryItemBase found = null;
//
//                    // If the parentID is zero, then this is going
//                    // directly into the root identified by the URI.
//
//                    if (item.Folder == UUID.Zero)
//                    {
//                        item.Folder = context.ID;
//                    }
//
//                    // Determine whether this is a new item or a
//                    // replacement definition.
//
//                    foreach (InventoryItemBase xi in rdata.items)
//                    {
//                        // Compare identifying attribute
//                        if (xi.ID == item.ID)
//                        {
//                            found = xi;
//                            break;
//                        }
//                    }
//
//                    if (found != null && ItemHasChanged(item, found))
//                    {
//                        Rest.Log.DebugFormat("{0} Updating item {1} {2} {3} {4} {5}",
//                                             MsgId, item.ID, item.AssetID, item.InvType, item.AssetType, item.Name);
//                        Rest.InventoryServices.UpdateItem(item);
//                        modified = true;
//                        rdata.appendStatus(String.Format("<p> Modified item {0}, UUID {1} <p>", item.Name, item.ID));
//                    }
//                    else
//                    {
//                        Rest.Log.DebugFormat("{0} Adding item {1} {2} {3} {4} {5}",
//                                             MsgId, item.ID, item.AssetID, item.InvType, item.AssetType, item.Name);
//                        Rest.InventoryServices.AddItem(item);
//                        created = true;
//                        rdata.appendStatus(String.Format("<p> Created item {0}, UUID {1} <p>", item.Name, item.ID));
//                    }
//                }
//
//                if (created)
//                {
//                    // Must include a location header with a URI that identifies the new resource.
//                    rdata.AddHeader(Rest.HttpHeaderLocation,String.Format("http://{0}{1}:{2}/{3}",
//                             rdata.hostname, rdata.port,rdata.path,newnode));
//                    rdata.Complete(Rest.HttpStatusCodeCreated);
//                }
//                else
//                {
//                    if (modified)
//                    {
//                        rdata.Complete(Rest.HttpStatusCodeOK);
//                    }
//                    else
//                    {
//                        rdata.Complete(Rest.HttpStatusCodeNoContent);
//                    }
//                }
//
//                rdata.Respond(String.Format("Profile {0} : Normal completion", rdata.method));
//            }
//            else
//            {
//                Rest.Log.DebugFormat("{0} {1}: Resource {2} is not a valid context: {3}",
//                                     MsgId, rdata.method, rdata.path, InventoryNode.GetType());
//                rdata.Fail(Rest.HttpStatusCodeBadRequest, "invalid resource context");
//            }
//        }

        /// <summary>
        /// PUT updates the URI-identified element in the inventory. This
        /// is actually far more flexible than it might at first sound. For
        /// PUT the URI serves two purposes:
        ///     [1] It identifies the user whose inventory is to be
        ///         processed.
        ///     [2] It optionally specifies a subtree of the inventory
        ///         that is to be used to resolve any relative subtree
        ///         specifications in the entity. If nothing is specified
        ///         then the whole of the private inventory is implied.
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
        /// <param name=rdata>HTTP service request work area</param>
//        private void DoUpdate(InventoryRequestData rdata)
//        {
//            int     count  = 0;
//            bool  created  = false;
//            bool  modified = false;
//
//            // Resolve the inventory node that is to be modified.
//            // rdata already contains information about the current
//            // content of the user's inventory.
//
//            Object InventoryNode = getInventoryNode(rdata, rdata.root, PARM_PATH, Rest.Fill);
//
//            // As long as we have a node, then we have something
//            // meaningful to do, unlike POST. So we reconstitute the
//            // subtree before doing anything else. Note that we
//            // etiher got a valid node or we threw an exception.
//
//            XmlInventoryCollection entity = ReconstituteEntity(rdata);
//
//            // Incorporate any inlined assets first. Any failures
//            // will terminate the request.
//
//            if (entity.Assets.Count > 0)
//            {
//                Rest.Log.DebugFormat("{0} Adding {1} assets to server",
//                                     MsgId, entity.Assets.Count);
//
//                foreach (AssetBase asset in entity.Assets)
//                {
//                    Rest.Log.DebugFormat("{0} Rest asset: {1} {2} {3}",
//                                         MsgId, asset.ID, asset.Type, asset.Name);
//
//                    // The asset was validated during the collection process
//
//                    Rest.AssetServices.Store(asset);
//
//                    created = true;
//                    rdata.appendStatus(String.Format("<p> Created asset {0}, UUID {1} <p>", asset.Name, asset.ID));
//
//                    if (Rest.DEBUG && Rest.DumpAsset)
//                    {
//                        Rest.Dump(asset.Data);
//                    }
//                }
//            }
//
//            // The URI specifies either a folder or an item to be updated.
//            //
//            // The root node in the entity will replace the node identified
//            // by the URI. This means the parent will remain the same, but
//            // any or all attributes associated with the named element
//            // will change.
//            //
//            // If the inventory collection contains an element with a zero
//            // parent ID, then this is taken to be the replacement for the
//            // named node. The collection MAY also specify an explicit
//            // parent ID, in this case it MAY identify the same parent as
//            // the current node, or it MAY specify a different parent,
//            // indicating that the folder is being moved in addition to any
//            // other modifications being made.
//
//            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
//                typeof(InventoryFolderImpl) == InventoryNode.GetType())
//            {
//                bool rfound = false;
//                InventoryFolderBase uri = (InventoryFolderBase) InventoryNode;
//                InventoryFolderBase xml = null;
//
//                // If the entity to be replaced resolved to be the root
//                // directory itself (My Inventory), then make sure that
//                // the supplied data include as appropriately typed and
//                // named folder. Note that we can;t rule out the possibility
//                // of a sub-directory being called "My Inventory", so that
//                // is anticipated.
//
//                if (uri == rdata.root)
//                {
//                    foreach (InventoryFolderBase folder in entity.Folders)
//                    {
//                        if ((rfound = (folder.Name == PRIVATE_ROOT_NAME)))
//                        {
//                            if ((rfound = (folder.ParentID == UUID.Zero)))
//                                break;
//                        }
//                    }
//
//                    if (!rfound)
//                    {
//                        Rest.Log.DebugFormat("{0} {1}: Path <{2}> will result in loss of inventory",
//                                             MsgId, rdata.method, rdata.path);
//                        rdata.Fail(Rest.HttpStatusCodeBadRequest, "invalid inventory structure");
//                    }
//                }
//
//                // Scan the set of folders in the entity collection for an
//                // entry that matches the context folder. It is assumed that
//                // the only reliable indicator of this is a zero UUID (using
//                // implicit context), or the parent's UUID matches that of the
//                // URI designated node (explicit context). We don't allow
//                // ambiguity in this case because this is POST and we are
//                // supposed to be modifying a specific node.
//                // We assign any element IDs required as an economy; we don't
//                // want to iterate over the fodler set again if it can be
//                // helped.
//
//                foreach (InventoryFolderBase folder in entity.Folders)
//                {
//                    if (folder.ParentID == uri.ParentID ||
//                        folder.ParentID == UUID.Zero)
//                    {
//                        folder.ParentID = uri.ParentID;
//                        xml = folder;
//                        count++;
//                    }
//                }
//
//                // More than one entry is ambiguous. Other folders should be
//                // added using the POST verb.
//
//                if (count > 1)
//                {
//                    Rest.Log.DebugFormat("{0} {1}: Request for <{2}> is ambiguous",
//                                         MsgId, rdata.method, rdata.path);
//                    rdata.Fail(Rest.HttpStatusCodeConflict, "context is ambiguous");
//                }
//
//                // Exactly one entry means we ARE replacing the node
//                // identified by the URI. So we delete the old folder
//                // by moving it to the trash and then purging it.
//                // We then add all of the folders and items we
//                // included in the entity. The subtree has been
//                // modified.
//
//                if (count == 1)
//                {
//                    InventoryFolderBase TrashCan = GetTrashCan(rdata);
//
//                    // All went well, so we generate a UUID is one is
//                    // needed.
//
//                    if (xml.ID == UUID.Zero)
//                    {
//                        xml.ID = UUID.Random();
//                    }
//
//                    uri.ParentID = TrashCan.ID;
//                    Rest.InventoryServices.MoveFolder(uri);
//                    Rest.InventoryServices.PurgeFolder(TrashCan);
//                    modified = true;
//                }
//
//                // Now, regardelss of what they represent, we
//                // integrate all of the elements in the entity.
//
//                foreach (InventoryFolderBase f in entity.Folders)
//                {
//                    rdata.appendStatus(String.Format("<p>Moving folder {0} UUID {1} <p>", f.Name, f.ID));
//                    Rest.InventoryServices.MoveFolder(f);
//                }
//
//                foreach (InventoryItemBase it in entity.Items)
//                {
//                    rdata.appendStatus(String.Format("<p>Storing item {0} UUID {1} <p>", it.Name, it.ID));
//                    Rest.InventoryServices.AddItem(it);
//                }
//            }
//
//            /// <summary>
//            /// URI specifies an item to be updated
//            /// </summary>
//            /// <remarks>
//            /// The entity must contain a single item node to be
//            /// updated. ID and Folder ID must be correct.
//            /// </remarks>
//
//            else
//            {
//                InventoryItemBase uri = (InventoryItemBase) InventoryNode;
//                InventoryItemBase xml = null;
//
//                if (entity.Folders.Count != 0)
//                {
//                    Rest.Log.DebugFormat("{0} {1}: Request should not contain any folders <{2}>",
//                                         MsgId, rdata.method, rdata.path);
//                    rdata.Fail(Rest.HttpStatusCodeBadRequest, "folder is not allowed");
//                }
//
//                if (entity.Items.Count > 1)
//                {
//                    Rest.Log.DebugFormat("{0} {1}: Entity contains too many items <{2}>",
//                                         MsgId, rdata.method, rdata.path);
//                    rdata.Fail(Rest.HttpStatusCodeBadRequest, "too may items");
//                }
//
//                xml = entity.Items[0];
//
//                if (xml.ID == UUID.Zero)
//                {
//                    xml.ID = UUID.Random();
//                }
//
//                // If the folder reference has changed, then this item is
//                // being moved. Otherwise we'll just delete the old, and
//                // add in the new.
//
//                // Delete the old item
//
//                List<UUID> uuids = new List<UUID>();
//                uuids.Add(uri.ID);
//                Rest.InventoryServices.DeleteItems(uri.Owner, uuids);
//
//                // Add the new item to the inventory
//
//                Rest.InventoryServices.AddItem(xml);
//
//                rdata.appendStatus(String.Format("<p>Storing item {0} UUID {1} <p>", xml.Name, xml.ID));
//            }
//
//            if (created)
//            {
//                rdata.Complete(Rest.HttpStatusCodeCreated);
//            }
//            else
//            {
//                if (modified)
//                {
//                    rdata.Complete(Rest.HttpStatusCodeOK);
//                }
//                else
//                {
//                    rdata.Complete(Rest.HttpStatusCodeNoContent);
//                }
//            }
//
//            rdata.Respond(String.Format("Profile {0} : Normal completion", rdata.method));
//        }

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
        /// <param name=rdata>HTTP service request work area</param>
//        private void DoDelete(InventoryRequestData rdata)
//        {
//            Object InventoryNode = getInventoryNode(rdata, rdata.root, PARM_PATH, false);
//
//            if (typeof(InventoryFolderBase) == InventoryNode.GetType() ||
//                typeof(InventoryFolderImpl) == InventoryNode.GetType())
//            {
//                InventoryFolderBase TrashCan = GetTrashCan(rdata);
//
//                InventoryFolderBase folder = (InventoryFolderBase) InventoryNode;
//                Rest.Log.DebugFormat("{0} {1}: Folder {2} will be deleted",
//                                     MsgId, rdata.method, rdata.path);
//                folder.ParentID = TrashCan.ID;
//                Rest.InventoryServices.MoveFolder(folder);
//                Rest.InventoryServices.PurgeFolder(TrashCan);
//
//                rdata.appendStatus(String.Format("<p>Deleted folder {0} UUID {1} <p>", folder.Name, folder.ID));
//            }
//
//            // Deleting items is much more straight forward.
//
//            else
//            {
//                InventoryItemBase item = (InventoryItemBase) InventoryNode;
//                Rest.Log.DebugFormat("{0} {1}: Item {2} will be deleted",
//                                     MsgId, rdata.method, rdata.path);
//                List<UUID> uuids = new List<UUID>();
//                uuids.Add(item.ID);
//                Rest.InventoryServices.DeleteItems(item.Owner, uuids);
//                rdata.appendStatus(String.Format("<p>Deleted item {0} UUID {1} <p>", item.Name, item.ID));
//            }
//
//            rdata.Complete();
//            rdata.Respond(String.Format("Profile {0} : Normal completion", rdata.method));
//        }

#endregion method-specific processing

        /// <summary>
        /// This method is called to obtain the OpenSim inventory object identified
        /// by the supplied URI. This may be either an Item or a Folder, so a suitably
        /// ambiguous return type is employed (Object). This method recurses as
        /// necessary to process the designated hierarchy.
        ///
        /// If we reach the end of the URI then we return the contextual folder to
        /// our caller.
        ///
        /// If we are not yet at the end of the URI we attempt to find a child folder
        /// and if we succeed we recurse.
        ///
        /// If this is the last node, then we look to see if this is an item. If it is,
        /// we return that item.
        ///
        /// If we reach the end of an inventory path and the URI si not yet exhausted,
        /// then if 'fill' is specified, we create the intermediate nodes.
        ///
        /// Otherwise we fail the request on the ground of an invalid URI.
        ///
        /// An ambiguous request causes the request to fail.
        ///
        /// </summary>
        /// <param name=rdata>HTTP service request work area</param>
        /// <param name=folder>The folder to be searched (parent)</param>
        /// <param name=pi>URI parameter index</param>
        /// <param name=fill>Should missing path members be created?</param>

        private Object getInventoryNode(InventoryRequestData rdata,
                                        InventoryFolderBase folder,
                                        int pi, bool fill)
        {
            InventoryFolderBase foundf = null;
            int fk = 0;

            Rest.Log.DebugFormat("{0} Searching folder {1} {2} [{3}]", MsgId, folder.ID, folder.Name, pi);

            // We have just run off the end of the parameter sequence

            if (pi >= rdata.Parameters.Length)
            {
                return folder;
            }

            // There are more names in the parameter sequence,
            // look for the folder named by param[pi] as a
            // child of the folder supplied as an argument.
            // Note that a UUID may have been supplied as the
            // identifier (it is the ONLY guaranteed unambiguous
            // option.

            if (rdata.folders != null)
            {
                foreach (InventoryFolderBase f in rdata.folders)
                {
                    // Look for the present node in the directory list
                    if (f.ParentID == folder.ID &&
                        (f.Name == rdata.Parameters[pi] ||
                         f.ID.ToString() == rdata.Parameters[pi]))
                    {
                        foundf = f;
                        fk++;
                    }
                }
            }

            // If more than one node matched, then the path, as specified
            // is ambiguous.

            if (fk > 1)
            {
                Rest.Log.DebugFormat("{0} {1}: Request for {2} is ambiguous",
                                             MsgId, rdata.method, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeConflict, "request is ambiguous");
            }

            // If we find a match, then the method
            // increment the parameter index, and calls itself
            // passing the found folder as the new context.

            if (foundf != null)
            {
                return getInventoryNode(rdata, foundf, pi+1, fill);
            }

            // No folders that match. Perhaps this parameter identifies an item? If
            // it does, then it MUST also be the last name in the sequence.

            if (pi == rdata.Parameters.Length-1)
            {
                if (rdata.items != null)
                {
                    int k = 0;
                    InventoryItemBase li = null;
                    foreach (InventoryItemBase i in rdata.items)
                    {
                        if (i.Folder == folder.ID &&
                            (i.Name == rdata.Parameters[pi] ||
                             i.ID.ToString() == rdata.Parameters[pi]))
                        {
                            li = i;
                            k++;
                        }
                    }
                    if (k == 1)
                    {
                        return li;
                    }
                    else if (k > 1)
                    {
                        Rest.Log.DebugFormat("{0} {1}: Request for {2} is ambiguous",
                                             MsgId, rdata.method, rdata.path);
                        rdata.Fail(Rest.HttpStatusCodeConflict, "request is ambiguous");
                    }
                }
            }

            // If fill is enabled, then we must create the missing intermediate nodes.
            // And of course, even this is not straightforward. All intermediate nodes
            // are obviously folders, but the last node may be a folder or an item.

            if (fill)
            {
            }

            // No fill, so abandon the request

            Rest.Log.DebugFormat("{0} {1}: Resource {2} not found",
                                 MsgId, rdata.method, rdata.path);
            rdata.Fail(Rest.HttpStatusCodeNotFound,
                        String.Format("resource {0}:{1} not found", rdata.method, rdata.path));

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
        /// <param name=rdata>HTTP service request work area</param>
        /// <param name=folder>The folder to be searched (parent)</param>
        /// <param name=pi>URI parameter index</param>

        private void traverse(InventoryRequestData rdata, InventoryFolderBase folder, int pi)
        {
            Rest.Log.DebugFormat("{0} Traverse[initial] : {1} {2} [{3}]", MsgId, folder.ID, folder.Name, pi);

            if (rdata.folders != null)
            {
                // If there was only one parameter (avatar name), then the entire
                // inventory is being requested.

                if (rdata.Parameters.Length == 1)
                {
                    formatInventory(rdata, rdata.root, String.Empty);
                }

                // Has the client specified the root directory name explicitly?
                // if yes, then we just absorb the reference, because the folder
                // we start looking in for a match *is* the root directory. If there
                // are more parameters remaining we tarverse, otehrwise it's time
                // to format. Otherwise,we consider the "My Inventory" to be implied
                // and we just traverse normally.

                else if (folder.ID.ToString() == rdata.Parameters[pi] ||
                         folder.Name          == rdata.Parameters[pi])
                {
                    // Length is -1 because the avatar name is a parameter
                    if (pi<(rdata.Parameters.Length-1))
                    {
                        traverseInventory(rdata, folder, pi+1);
                    }
                    else
                    {
                        formatInventory(rdata, folder, String.Empty);
                    }
                }
                else
                {
                    traverseInventory(rdata, folder, pi);
                }

                return;
            }
        }

        /// <summary>
        /// This is the recursive method. I've separated them in this way so that
        /// we do not have to waste cycles on any first-case-only processing.
        /// </summary>

        private void traverseInventory(InventoryRequestData rdata, InventoryFolderBase folder, int pi)
        {
            int fk = 0;
            InventoryFolderBase ffound = null;
            InventoryItemBase   ifound = null;

            Rest.Log.DebugFormat("{0} Traverse Folder : {1} {2} [{3}]", MsgId, folder.ID, folder.Name, pi);

            foreach (InventoryFolderBase f in rdata.folders)
            {
                if (f.ParentID == folder.ID &&
                    (f.Name == rdata.Parameters[pi] ||
                     f.ID.ToString() == rdata.Parameters[pi]))
                {
                    fk++;
                    ffound = f;
                }
            }

            // If this is the last element in the parameter sequence, then
            // it is reasonable to check for an item. All intermediate nodes
            // MUST be folders.

            if (pi == rdata.Parameters.Length-1)
            {
                // Only if there are any items, and there pretty much always are.

                if (rdata.items != null)
                {
                    foreach (InventoryItemBase i in rdata.items)
                    {
                        if (i.Folder == folder.ID &&
                            (i.Name == rdata.Parameters[pi] ||
                             i.ID.ToString() == rdata.Parameters[pi]))
                        {
                            fk++;
                            ifound = i;
                        }
                    }
                }
            }

            if (fk == 1)
            {
                if (ffound != null)
                {
                    if (pi < rdata.Parameters.Length-1)
                    {
                        traverseInventory(rdata, ffound, pi+1);
                    }
                    else
                    {
                        formatInventory(rdata, ffound, String.Empty);
                    }
                    return;
                }
                else
                {
                    // Fetching an Item has a special significance. In this
                    // case we also want to fetch the associated asset.
                    // To make it interesting, we'll do this via redirection.
                    string asseturl = String.Format("http://{0}:{1}/{2}{3}{4}", rdata.hostname, rdata.port,
                        "admin/assets",Rest.UrlPathSeparator,ifound.AssetID.ToString());
                    rdata.Redirect(asseturl,Rest.PERMANENT);
                    Rest.Log.DebugFormat("{0} Never Reached", MsgId);
                }
            }
            else if (fk > 1)
            {
                rdata.Fail(Rest.HttpStatusCodeConflict,
                           String.Format("ambiguous element ({0}) in path specified: <{1}>",
                                         pi, rdata.path));
            }

            Rest.Log.DebugFormat("{0} Inventory does not contain item/folder: <{1}>",
                                 MsgId, rdata.path);
            rdata.Fail(Rest.HttpStatusCodeNotFound,String.Format("no such item/folder : {0}",
                                                                 rdata.Parameters[pi]));

        }

        /// <summary>
        /// This method generates XML that describes an instance of InventoryFolderBase.
        /// It recurses as necessary to reflect a folder hierarchy, and calls formatItem
        /// to generate XML for any items encountered along the way.
        /// The indentation parameter is solely for the benefit of trace record
        /// formatting.
        /// </summary>
        /// <param name=rdata>HTTP service request work area</param>
        /// <param name=folder>The folder to be searched (parent)</param>
        /// <param name=indent>pretty print indentation</param>
        private void formatInventory(InventoryRequestData rdata, InventoryFolderBase folder, string indent)
        {
            if (Rest.DEBUG)
            {
                Rest.Log.DebugFormat("{0} Folder : {1} {2} {3} type = {4}",
                        MsgId, folder.ID, indent, folder.Name, folder.Type);
                indent += "\t";
            }

            // Start folder item

            rdata.writer.WriteStartElement(String.Empty,"Folder",String.Empty);
            rdata.writer.WriteAttributeString("name",String.Empty,folder.Name);
            rdata.writer.WriteAttributeString("uuid",String.Empty,folder.ID.ToString());
            rdata.writer.WriteAttributeString("parent",String.Empty,folder.ParentID.ToString());
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
        /// <param name="rdata">HTTP service request work area</param>
        /// <param name="i">The item to be formatted</param>
        /// <param name="indent">Pretty print indentation</param>
        private void formatItem(InventoryRequestData rdata, InventoryItemBase i, string indent)
        {
            Rest.Log.DebugFormat("{0}   Item : {1} {2} {3} Type = {4}, AssetType = {5}",
                                 MsgId, i.ID, indent, i.Name, i.InvType, i.AssetType);

            rdata.writer.WriteStartElement(String.Empty, "Item", String.Empty);

            rdata.writer.WriteAttributeString("name", String.Empty, i.Name);
            rdata.writer.WriteAttributeString("desc", String.Empty, i.Description);
            rdata.writer.WriteAttributeString("uuid", String.Empty, i.ID.ToString());
            rdata.writer.WriteAttributeString("folder", String.Empty, i.Folder.ToString());
            rdata.writer.WriteAttributeString("owner", String.Empty, i.Owner.ToString());
            rdata.writer.WriteAttributeString("creator", String.Empty, i.CreatorId);
            rdata.writer.WriteAttributeString("creatordata", String.Empty, i.CreatorData);
            rdata.writer.WriteAttributeString("creationdate", String.Empty, i.CreationDate.ToString());
            rdata.writer.WriteAttributeString("invtype", String.Empty, i.InvType.ToString());
            rdata.writer.WriteAttributeString("assettype", String.Empty, i.AssetType.ToString());
            rdata.writer.WriteAttributeString("groupowned", String.Empty, i.GroupOwned.ToString());
            rdata.writer.WriteAttributeString("groupid", String.Empty, i.GroupID.ToString());
            rdata.writer.WriteAttributeString("saletype", String.Empty, i.SaleType.ToString());
            rdata.writer.WriteAttributeString("saleprice", String.Empty, i.SalePrice.ToString());
            rdata.writer.WriteAttributeString("flags", String.Empty, i.Flags.ToString());

            rdata.writer.WriteStartElement(String.Empty, "Permissions", String.Empty);
            rdata.writer.WriteAttributeString("current", String.Empty, i.CurrentPermissions.ToString("X"));
            rdata.writer.WriteAttributeString("next", String.Empty, i.NextPermissions.ToString("X"));
            rdata.writer.WriteAttributeString("group", String.Empty, i.GroupPermissions.ToString("X"));
            rdata.writer.WriteAttributeString("everyone", String.Empty, i.EveryOnePermissions.ToString("X"));
            rdata.writer.WriteAttributeString("base", String.Empty, i.BasePermissions.ToString("X"));
            rdata.writer.WriteEndElement();

            rdata.writer.WriteElementString("Asset", i.AssetID.ToString());

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
        /// <param name=rdata>HTTP service request work area</param>
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
                        TrashCan.ID   = UUID.Random();
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
                rdata.Fail(Rest.HttpStatusCodeServerError, "unable to create trash can");
            }

            return TrashCan;
        }

        /// <summary>
        /// Make sure that an unchanged folder is not unnecessarily
        /// processed.
        /// </summary>
        /// <param name=newf>Folder obtained from enclosed entity</param>
        /// <param name=oldf>Folder obtained from the user's inventory</param>
        private bool FolderHasChanged(InventoryFolderBase newf, InventoryFolderBase oldf)
        {
            return (newf.Name           != oldf.Name
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
        /// <param name=newf>Item obtained from enclosed entity</param>
        /// <param name=oldf>Item obtained from the user's inventory</param>
        private bool ItemHasChanged(InventoryItemBase newf, InventoryItemBase oldf)
        {
            return (newf.Name           != oldf.Name
                    || newf.Folder      != oldf.Folder
                    || newf.Description != oldf.Description
                    || newf.Owner       != oldf.Owner
                    || newf.CreatorId   != oldf.CreatorId
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
        /// <param name=rdata>HTTP service request work area</param>
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
                            case XmlNodeType.Element:
                                Rest.Log.DebugFormat("{0} StartElement: <{1}>",
                                                     MsgId, ic.xml.Name);

                                switch (ic.xml.Name)
                                {
                                    case "Folder":
                                        Rest.Log.DebugFormat("{0} Processing {1} element",
                                                             MsgId, ic.xml.Name);
                                        CollectFolder(ic);
                                        break;
                                    case "Item":
                                        Rest.Log.DebugFormat("{0} Processing {1} element",
                                                             MsgId, ic.xml.Name);
                                        CollectItem(ic);
                                        break;
                                    case "Asset":
                                        Rest.Log.DebugFormat("{0} Processing {1} element",
                                                             MsgId, ic.xml.Name);
                                        CollectAsset(ic);
                                        break;
                                    case "Permissions":
                                        Rest.Log.DebugFormat("{0} Processing {1} element",
                                                             MsgId, ic.xml.Name);
                                        CollectPermissions(ic);
                                        break;
                                    default:
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
                                    case "Folder":
                                        Rest.Log.DebugFormat("{0} Completing {1} element",
                                                             MsgId, ic.xml.Name);
                                        ic.Pop();
                                        break;
                                    case "Item":
                                        Rest.Log.DebugFormat("{0} Completing {1} element",
                                                             MsgId, ic.xml.Name);
                                        Validate(ic);
                                        break;
                                    case "Asset":
                                        Rest.Log.DebugFormat("{0} Completing {1} element",
                                                             MsgId, ic.xml.Name);
                                        break;
                                    case "Permissions":
                                        Rest.Log.DebugFormat("{0} Completing {1} element",
                                                             MsgId, ic.xml.Name);
                                        break;
                                    default:
                                        Rest.Log.DebugFormat("{0} Ignoring {1} element",
                                                             MsgId, ic.xml.Name);
                                        break;
                                    }
                                break;

                            default:
                                Rest.Log.DebugFormat("{0} Ignoring: <{1}>:<{2}>",
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
            result.ID       = UUID.Zero;
            result.Owner    = ic.UserID;
            result.ParentID = UUID.Zero; // Context
            result.Type     = (short) AssetType.Folder;
            result.Version  = 1;

            if (ic.xml.HasAttributes)
            {
                for (int i = 0; i < ic.xml.AttributeCount; i++)
                {
                    ic.xml.MoveToAttribute(i);
                    switch (ic.xml.Name)
                    {
                    case "name":
                        result.Name     =     ic.xml.Value;
                        break;
                    case "uuid":
                        result.ID       = new UUID(ic.xml.Value);
                        break;
                    case "parent":
                        result.ParentID = new UUID(ic.xml.Value);
                        break;
                    case "owner":
                        result.Owner    = new UUID(ic.xml.Value);
                        break;
                    case "type":
                        result.Type     =     Int16.Parse(ic.xml.Value);
                        break;
                    case "version":
                        result.Version  =     UInt16.Parse(ic.xml.Value);
                        break;
                    default:
                        Rest.Log.DebugFormat("{0} Folder: unrecognized attribute: {1}:{2}",
                                             MsgId, ic.xml.Name, ic.xml.Value);
                        ic.Fail(Rest.HttpStatusCodeBadRequest, String.Format("unrecognized attribute <{0}>",
                                 ic.xml.Name));
                        break;
                    }
                }
            }

            ic.xml.MoveToElement();

            // The client is relying upon the reconstitution process
            // to determine the parent's UUID based upon context. This
            // is necessary where a new folder may have been
            // introduced.

            if (result.ParentID == UUID.Zero)
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
                    ic.Fail(Rest.HttpStatusCodeBadRequest, "invalid parent");
                }
            }

            // This is a new folder, so no existing UUID is available
            // or appropriate

            if (result.ID == UUID.Zero)
            {
                result.ID = UUID.Random();
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
            result.ID          = UUID.Zero;
            result.Folder      = UUID.Zero;
            result.Owner       = ic.UserID;
            result.CreatorId   = ic.UserID.ToString();
            result.AssetID     = UUID.Zero;
            result.GroupID     = UUID.Zero;
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
                        case "name":
                            result.Name         =     ic.xml.Value;
                            break;
                        case "desc":
                            result.Description  =     ic.xml.Value;
                            break;
                        case "uuid":
                            result.ID           = new UUID(ic.xml.Value);
                            break;
                        case "folder":
                            result.Folder       = new UUID(ic.xml.Value);
                            break;
                        case "owner":
                            result.Owner        = new UUID(ic.xml.Value);
                            break;
                        case "invtype":
                            result.InvType      =     Int32.Parse(ic.xml.Value);
                            break;
                        case "creator":
                            result.CreatorId    =     ic.xml.Value;
                            break;
                        case "assettype":
                            result.AssetType    =     Int32.Parse(ic.xml.Value);
                            break;
                        case "groupowned":
                            result.GroupOwned   =     Boolean.Parse(ic.xml.Value);
                            break;
                        case "groupid":
                            result.GroupID      = new UUID(ic.xml.Value);
                            break;
                        case "flags":
                            result.Flags        =     UInt32.Parse(ic.xml.Value);
                            break;
                        case "creationdate":
                            result.CreationDate =     Int32.Parse(ic.xml.Value);
                            break;
                        case "saletype":
                            result.SaleType     =     Byte.Parse(ic.xml.Value);
                            break;
                        case "saleprice":
                            result.SalePrice    =     Int32.Parse(ic.xml.Value);
                            break;

                        default:
                            Rest.Log.DebugFormat("{0} Item: Unrecognized attribute: {1}:{2}",
                                                 MsgId, ic.xml.Name, ic.xml.Value);
                            ic.Fail(Rest.HttpStatusCodeBadRequest, String.Format("unrecognized attribute",
                                ic.xml.Name));
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

            string    name  = String.Empty;
            string    desc  = String.Empty;
            sbyte     type  = (sbyte) AssetType.Unknown;
            bool      temp  = false;
            bool     local  = false;

            // This is not a persistent attribute
            bool    inline  = false;

            UUID    uuid  = UUID.Zero;

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
                            uuid = new UUID(ic.xml.Value);
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
                                    String.Format("unrecognized attribute <{0}>", ic.xml.Name));
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
                    ic.Item.AssetID = new UUID(ic.xml.ReadElementContentAsString());
                    Rest.Log.DebugFormat("{0} Asset ID supplied: {1}", MsgId, ic.Item.AssetID);
                }
                else
                {
                    Rest.Log.DebugFormat("{0} LLUID unimbedded asset must be inline", MsgId);
                    ic.Fail(Rest.HttpStatusCodeBadRequest, "no context for asset");
                }
            }

            // Otherwise, generate an asset ID, store that into the item, and
            // create an entry in the asset list for the inlined asset. But
            // only if the size is non-zero.

            else
            {
                AssetBase asset = null;
                string b64string = null;

                // Generate a UUID if none were given, and generally none should
                // be. Ever.

                if (uuid == UUID.Zero)
                {
                    uuid = UUID.Random();
                }

                // Create AssetBase entity to hold the inlined asset

                asset = new AssetBase(uuid, name, type, UUID.Zero.ToString());

                asset.Description = desc;
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

                if (ic.Item != null && ic.Item.AssetID == UUID.Zero)
                {
                    ic.Item.AssetID = uuid;
                }

                ic.Push(asset);
            }
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
                        case "current":
                            ic.CurrentPermissions  = UInt32.Parse(ic.xml.Value, NumberStyles.HexNumber);
                            break;
                        case "next":
                            ic.NextPermissions     = UInt32.Parse(ic.xml.Value, NumberStyles.HexNumber);
                            break;
                        case "group":
                            ic.GroupPermissions    = UInt32.Parse(ic.xml.Value, NumberStyles.HexNumber);
                            break;
                        case "everyone":
                            ic.EveryOnePermissions = UInt32.Parse(ic.xml.Value, NumberStyles.HexNumber);
                            break;
                        case "base":
                            ic.BasePermissions     = UInt32.Parse(ic.xml.Value, NumberStyles.HexNumber);
                            break;
                        default:
                            Rest.Log.DebugFormat("{0} Permissions:  invalid attribute {1}:{2}",
                                                 MsgId,ic.xml.Name, ic.xml.Value);
                            ic.Fail(Rest.HttpStatusCodeBadRequest,
                                         String.Format("invalid attribute <{0}>", ic.xml.Name));
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
                ic.Fail(Rest.HttpStatusCodeBadRequest, "request parse error");
            }

            // Every item is required to have a name (via REST anyway)

            if (ic.Item.Name == String.Empty)
            {
                Rest.Log.ErrorFormat("{0} An item name MUST be specified", MsgId);
                ic.Fail(Rest.HttpStatusCodeBadRequest, "item name required");
            }

            // An item MUST have an asset ID. AssetID should never be zero
            // here. It should always get set from the information stored
            // when the Asset element was processed.

            if (ic.Item.AssetID == UUID.Zero)
            {
                Rest.Log.ErrorFormat("{0} Unable to complete request", MsgId);
                Rest.Log.InfoFormat("{0} Asset information is missing", MsgId);
                ic.Fail(Rest.HttpStatusCodeBadRequest, "asset information required");
            }

            // If the item is new, then assign it an ID

            if (ic.Item.ID == UUID.Zero)
            {
                ic.Item.ID = UUID.Random();
            }

            // If the context is being implied, obtain the current
            // folder item's ID. If it was specified explicitly, make
            // sure that theparent folder exists.

            if (ic.Item.Folder == UUID.Zero)
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
                    ic.Fail(Rest.HttpStatusCodeBadRequest, "parent information required");
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
            ic.Item.GroupPermissions    = ic.GroupPermissions;
            ic.Item.NextPermissions     = ic.NextPermissions;

            // If no type was specified for this item, we can attempt to
            // infer something from the file type maybe. This is NOT as
            // good as having type be specified in the XML.

            if (ic.Item.AssetType == (int) AssetType.Unknown ||
                ic.Item.InvType   == (int) InventoryType.Unknown)
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
                        if (ic.Item.InvType == (int) InventoryType.Unknown)
                            ic.Item.InvType   = (int) InventoryType.Texture;
                        break;
                    case "jpg" :
                    case "jpeg" :
                        Rest.Log.DebugFormat("{0} Type {1} inferred",
                                             MsgId, parts[parts.Length - 1]);
                        if (ic.Item.AssetType == (int) AssetType.Unknown)
                            ic.Item.AssetType = (int) AssetType.ImageJPEG;
                        if (ic.Item.InvType == (int) InventoryType.Unknown)
                            ic.Item.InvType   = (int) InventoryType.Texture;
                        break;
                    case "tga" :
                        if (parts[parts.Length - 2].IndexOf("_texture") != -1)
                        {
                            if (ic.Item.AssetType == (int) AssetType.Unknown)
                                ic.Item.AssetType = (int) AssetType.TextureTGA;
                            if (ic.Item.InvType == (int) AssetType.Unknown)
                                ic.Item.InvType   = (int) InventoryType.Texture;
                        }
                        else
                        {
                            if (ic.Item.AssetType == (int) AssetType.Unknown)
                                ic.Item.AssetType = (int) AssetType.ImageTGA;
                            if (ic.Item.InvType == (int) InventoryType.Unknown)
                                ic.Item.InvType   = (int) InventoryType.Snapshot;
                        }
                        break;
                    default :
                        Rest.Log.DebugFormat("{0} Asset/Inventory type could not be inferred for {1}",
                               MsgId,ic.Item.Name);
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

                temp = LoadTGAClass.LoadTGA(tgadata);
                try
                {
                    ic.Asset.Data = OpenJPEG.EncodeFromImage(temp, true);
                }
                catch (DllNotFoundException)
                {
                    Rest.Log.ErrorFormat("OpenJpeg is not installed correctly on this system.   Asset Data is empty for {0}", ic.Item.Name);
                    ic.Asset.Data = new Byte[0];
                }
                catch (IndexOutOfRangeException)
                {
                    Rest.Log.ErrorFormat("OpenJpeg was unable to encode this.   Asset Data is empty for {0}", ic.Item.Name);
                    ic.Asset.Data = new Byte[0];
                }
                catch (Exception)
                {
                    Rest.Log.ErrorFormat("OpenJpeg was unable to encode this.   Asset Data is empty for {0}", ic.Item.Name);
                    ic.Asset.Data = new Byte[0];
                }
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

            internal UUID                                uuid = UUID.Zero;
            internal bool                       HaveInventory = false;
            internal ICollection<InventoryFolderImpl> folders = null;
            internal ICollection<InventoryItemBase>     items = null;
            internal UserProfileData              userProfile = null;
            internal InventoryFolderBase                 root = null;
            internal bool                             timeout = false;
            internal Timer             watchDog = new Timer();

            internal InventoryRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }

            internal void startWD(double interval)
            {
                Rest.Log.DebugFormat("{0} Setting watchdog", MsgId);
                watchDog.Elapsed  += new ElapsedEventHandler(OnTimeOut);
                watchDog.Interval  = interval;
                watchDog.AutoReset = false;
                watchDog.Enabled   = true;
                lock (watchDog)
                    watchDog.Start();
                
            }

            internal void stopWD()
            {
                Rest.Log.DebugFormat("{0} Reset watchdog", MsgId);
                lock (watchDog)
                    watchDog.Stop();
            }

            /// <summary>
            /// This is the callback method required by the inventory watchdog. The
            /// requestor issues an inventory request and then blocks until the
            /// request completes, or this method signals the monitor.
            /// </summary>

            private void OnTimeOut(object sender, ElapsedEventArgs args)
            {
                Rest.Log.DebugFormat("{0} Asynchronous inventory update timed-out", MsgId);
                // InventoryRequestData rdata = (InventoryRequestData) sender;
                lock (this)
                {
                    this.folders = null;
                    this.items   = null;
                    this.HaveInventory = false;
                    this.timeout = true;
                    Monitor.Pulse(this);
                }
            }

            /// <summary>
            /// This is the callback method required by inventory services. The
            /// requestor issues an inventory request and then blocks until this
            /// method signals the monitor.
            /// </summary>

            internal void GetUserInventory(ICollection<InventoryFolderImpl> folders, ICollection<InventoryItemBase> items)
            {
                Rest.Log.DebugFormat("{0} Asynchronously updating inventory data", MsgId);
                lock (this)
                {
                    if (watchDog.Enabled)
                    {
                        this.stopWD();
                    }
                    this.folders = folders;
                    this.items   = items;
                    this.HaveInventory = true;
                    this.timeout = false;
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
            internal /*static*/ const uint DefaultGroup    = 0x0;

            internal uint      CurrentPermissions  = 0x00;
            internal uint      NextPermissions     = 0x00;
            internal uint      BasePermissions     = 0x00;
            internal uint      EveryOnePermissions = 0x00;
            internal uint      GroupPermissions    = 0x00;

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
                GroupPermissions    = DefaultGroup;
                EveryOnePermissions = DefaultEveryOne;
            }

            internal UUID Parent()
            {
                if (stk.Count != 0)
                {
                    return stk.Peek().ID;
                }
                else
                {
                    return UUID.Zero;
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

            internal void Fail(int code, string addendum)
            {
                rdata.Fail(code, addendum);
            }
        }
    }
}
