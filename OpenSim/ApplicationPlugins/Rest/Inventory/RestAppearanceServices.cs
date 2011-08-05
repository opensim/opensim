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
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{

    public class RestAppearanceServices : IRest
    {
//        private static readonly int PARM_USERID = 0;

        // private static readonly int PARM_PATH   = 1;

//        private bool       enabled = false;
        private string     qPrefix = "appearance";

        /// <summary>
        /// The constructor makes sure that the service prefix is absolute
        /// and the registers the service handler and the allocator.
        /// </summary>

        public RestAppearanceServices()
        {
            Rest.Log.InfoFormat("{0} User appearance services initializing", MsgId);
            Rest.Log.InfoFormat("{0} Using REST Implementation Version {1}", MsgId, Rest.Version);

            // If a relative path was specified for the handler's domain,
            // add the standard prefix to make it absolute, e.g. /admin

            if (!qPrefix.StartsWith(Rest.UrlPathSeparator))
            {
                Rest.Log.InfoFormat("{0} Domain is relative, adding absolute prefix", MsgId);
                qPrefix = String.Format("{0}{1}{2}", Rest.Prefix, Rest.UrlPathSeparator, qPrefix);
                qPrefix = String.Format("{0}{1}{2}", Rest.Prefix, Rest.UrlPathSeparator, qPrefix);
                Rest.Log.InfoFormat("{0} Domain is now <{1}>", MsgId, qPrefix);
            }

            // Register interface using the absolute URI.

            Rest.Plugin.AddPathHandler(DoAppearance,qPrefix,Allocate);

            // Activate if everything went OK

//            enabled = true;

            Rest.Log.InfoFormat("{0} User appearance services initialization complete", MsgId);
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
            Rest.Log.InfoFormat("{0} User appearance services closing down", MsgId);
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
            return (RequestData) new AppearanceRequestData(request, response, prefix);
        }

        /// <summary>
        /// This method is registered with the handler when this service provider
        /// is initialized. It is called whenever the plug-in identifies this service
        /// provider as the best match for a given request.
        /// It handles all aspects of inventory REST processing, i.e. /admin/inventory
        /// </summary>
        /// <param name=hdata>A consolidated HTTP request work area</param>

        private void DoAppearance(RequestData hdata)
        {
            // !!! REFACTORIMG PROBLEM. This needs rewriting for 0.7

            //AppearanceRequestData rdata = (AppearanceRequestData) hdata;

            //Rest.Log.DebugFormat("{0} DoAppearance ENTRY", MsgId);

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
            //    Rest.Log.WarnFormat("{0} Appearance: No user agent identifier specified", MsgId);
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
            //    Rest.Log.DebugFormat("{0} User profile obtained for agent {1} {2}",
            //                         MsgId, rdata.userProfile.FirstName, rdata.userProfile.SurName);
            //}
            //else
            //{
            //    Rest.Log.WarnFormat("{0} No user profile for {1}", MsgId, rdata.path);
            //    rdata.Fail(Rest.HttpStatusCodeNotFound, "unrecognized user identity");
            //}

            //// If we get to here, then we have effectively validated the user's

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
            //                             MsgId, rdata.method, rdata.path);
            //        rdata.Fail(Rest.HttpStatusCodeMethodNotAllowed, 
            //                   String.Format("{0} not supported", rdata.method));
            //        break;
            //}
        }

        #endregion Interface

        #region method-specific processing

        /// <summary>
        /// This method implements GET processing for user's appearance.
        /// </summary>
        /// <param name=rdata>HTTP service request work area</param>

//        private void DoGet(AppearanceRequestData rdata)
//        {
//            AvatarData adata = Rest.AvatarServices.GetAvatar(rdata.userProfile.ID);
//
//            if (adata == null)
//            {
//                rdata.Fail(Rest.HttpStatusCodeNoContent,
//                    String.Format("appearance data not found for user {0} {1}", 
//                      rdata.userProfile.FirstName, rdata.userProfile.SurName));
//            }
//            rdata.userAppearance = adata.ToAvatarAppearance(rdata.userProfile.ID);
//
//            rdata.initXmlWriter();
//
//            FormatUserAppearance(rdata);
//
//            // Indicate a successful request
//
//            rdata.Complete();
//
//            // Send the response to the user. The body will be implicitly
//            // constructed from the result of the XML writer.
//
//            rdata.Respond(String.Format("Appearance {0} Normal completion", rdata.method));
//        }

        /// <summary>
        /// POST adds NEW information to the user profile database.
        /// This effectively resets the appearance before applying those
        /// characteristics supplied in the request.
        /// </summary>

//        private void DoExtend(AppearanceRequestData rdata)
//        {
//
//            bool  created  = false;
//            bool  modified = false;
//            string newnode = String.Empty;
//
//            Rest.Log.DebugFormat("{0} POST ENTRY", MsgId);
//
//            //AvatarAppearance old = Rest.AvatarServices.GetUserAppearance(rdata.userProfile.ID);
//
//            rdata.userAppearance = new AvatarAppearance();
//
//            //  Although the following behavior is admitted by HTTP I am becoming 
//            //  increasingly doubtful that it is appropriate for REST. If I attempt to
//            //  add a new record, and it already exists, then it seems to me that the
//            //  attempt should fail, rather than update the existing record.
//            AvatarData adata = null;
//            if (GetUserAppearance(rdata))
//            {
//                modified = rdata.userAppearance != null;
//                created  = !modified;
//                adata = new AvatarData(rdata.userAppearance);
//                Rest.AvatarServices.SetAvatar(rdata.userProfile.ID, adata);
//            //    Rest.UserServices.UpdateUserProfile(rdata.userProfile);
//            }
//            else
//            {
//                created  = true;
//                adata = new AvatarData(rdata.userAppearance);
//                Rest.AvatarServices.SetAvatar(rdata.userProfile.ID, adata);
//             //   Rest.UserServices.UpdateUserProfile(rdata.userProfile);
//            }
//
//            if (created)
//            {
//                newnode = String.Format("{0} {1}", rdata.userProfile.FirstName,
//                                   rdata.userProfile.SurName);
//                // Must include a location header with a URI that identifies the new resource.
//
//                rdata.AddHeader(Rest.HttpHeaderLocation,String.Format("http://{0}{1}:{2}{3}{4}",
//                         rdata.hostname,rdata.port,rdata.path,Rest.UrlPathSeparator, newnode));
//                rdata.Complete(Rest.HttpStatusCodeCreated);
//
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
//            rdata.Respond(String.Format("Appearance {0} : Normal completion", rdata.method));
//
//        }

        /// <summary>
        /// This updates the user's appearance. not all aspects need to be provided,
        /// only those supplied will be changed.
        /// </summary>

//        private void DoUpdate(AppearanceRequestData rdata)
//        {
//
//            // REFACTORING PROBLEM This was commented out. It doesn't work for 0.7
//
//            //bool  created  = false;
//            //bool  modified = false;
//
//
//            //rdata.userAppearance = Rest.AvatarServices.GetUserAppearance(rdata.userProfile.ID);
//
//            //// If the user exists then this is considered a modification regardless
//            //// of what may, or may not be, specified in the payload.
//
//            //if (rdata.userAppearance != null)
//            //{
//            //    modified = true;
//            //    Rest.AvatarServices.UpdateUserAppearance(rdata.userProfile.ID, rdata.userAppearance);
//            //    Rest.UserServices.UpdateUserProfile(rdata.userProfile);
//            //}
//
//            //if (created)
//            //{
//            //    rdata.Complete(Rest.HttpStatusCodeCreated);
//            //}
//            //else
//            //{
//            //    if (modified)
//            //    {
//            //        rdata.Complete(Rest.HttpStatusCodeOK);
//            //    }
//            //    else
//            //    {
//            //        rdata.Complete(Rest.HttpStatusCodeNoContent);
//            //    }
//            //}
//
//            rdata.Respond(String.Format("Appearance {0} : Normal completion", rdata.method));
//
//        }

        /// <summary>
        /// Delete the specified user's appearance. This actually performs a reset
        /// to the default avatar appearance, if the info is already there. 
        /// Existing ownership is preserved. All prior updates are lost and can not
        /// be recovered.
        /// </summary>
//        private void DoDelete(AppearanceRequestData rdata)
//        {
//            AvatarData adata = Rest.AvatarServices.GetAvatar(rdata.userProfile.ID);
//
//            if (adata != null)
//            {
//                AvatarAppearance old = adata.ToAvatarAppearance(rdata.userProfile.ID);
//                rdata.userAppearance = new AvatarAppearance();
//                rdata.userAppearance.Owner = old.Owner;
//                adata = new AvatarData(rdata.userAppearance);
//
//                Rest.AvatarServices.SetAvatar(rdata.userProfile.ID, adata);
//
//                rdata.Complete();
//            }
//            else
//            {
//
//                rdata.Complete(Rest.HttpStatusCodeNoContent);
//            }
//
//            rdata.Respond(String.Format("Appearance {0} : Normal completion", rdata.method));
//        }

#endregion method-specific processing

        private bool GetUserAppearance(AppearanceRequestData rdata)
        {

            XmlReader xml;
            bool indata = false;

            rdata.initXmlReader();
            xml     = rdata.reader;

            while (xml.Read())
            {
                switch (xml.NodeType)
                {
                    case XmlNodeType.Element :
                        switch (xml.Name)
                        {
                            case "Appearance" :
                                if (xml.MoveToAttribute("Height"))
                                {
                                    rdata.userAppearance.AvatarHeight = (float) Convert.ToDouble(xml.Value);
                                    indata = true;
                                }
//                                if (xml.MoveToAttribute("Owner"))
//                                {
//                                    rdata.userAppearance.Owner = (UUID)xml.Value;
//                                    indata = true;
//                                }
                                if (xml.MoveToAttribute("Serial"))
                                {
                                    rdata.userAppearance.Serial = Convert.ToInt32(xml.Value);
                                    indata = true;
                                }
                            break;
/*
                            case "Body" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.BodyItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.BodyAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Skin" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.SkinItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.SkinAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Hair" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.HairItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.HairAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Eyes" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.EyesItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.EyesAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Shirt" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.ShirtItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.ShirtAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Pants" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.PantsItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.PantsAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Shoes" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.ShoesItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.ShoesAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Socks" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.SocksItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.SocksAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Jacket" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.JacketItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.JacketAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Gloves" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.GlovesItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.GlovesAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "UnderShirt" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.UnderShirtItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.UnderShirtAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "UnderPants" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.UnderPantsItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.UnderPantsAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
                            case "Skirt" :
                                if (xml.MoveToAttribute("Item"))
                                {
                                    rdata.userAppearance.SkirtItem = (UUID)xml.Value;
                                    indata = true;
                                }
                                if (xml.MoveToAttribute("Asset"))
                                {
                                    rdata.userAppearance.SkirtAsset = (UUID)xml.Value;
                                    indata = true;
                                }
                            break;
*/
                            case "Attachment" :
                                {

                                    int  ap;
                                    UUID asset;
                                    UUID item;

                                    if (xml.MoveToAttribute("AtPoint"))
                                    {
                                        ap = Convert.ToInt32(xml.Value);
                                        if (xml.MoveToAttribute("Asset"))
                                        {
                                            asset = new UUID(xml.Value);
                                            if (xml.MoveToAttribute("Asset"))
                                            {
                                                item = new UUID(xml.Value);
                                                rdata.userAppearance.SetAttachment(ap, item, asset);
                                                indata = true;
                                            }
                                        }
                                    }
                                }
                            break;
                            case "Texture" :
                                if (xml.MoveToAttribute("Default"))
                                {
                                    rdata.userAppearance.Texture = new Primitive.TextureEntry(new UUID(xml.Value));
                                    indata = true;
                                }
                            break;
                            case "Face" :
                                {
                                    uint index;
                                    if (xml.MoveToAttribute("Index"))
                                    {
                                        index = Convert.ToUInt32(xml.Value);
                                        if (xml.MoveToAttribute("Id"))
                                        {
                                            rdata.userAppearance.Texture.CreateFace(index).TextureID = new UUID(xml.Value);
                                            indata = true;
                                        }
                                    }
                                }
                            break;
                            case "VisualParameters" :
                                {
                                    xml.ReadContentAsBase64(rdata.userAppearance.VisualParams,
                                                            0, rdata.userAppearance.VisualParams.Length);
                                    indata = true;
                                }
                            break;
                        } 
                    break;
                }
            }

            return indata;

        }

        private void FormatPart(AppearanceRequestData rdata, string part, UUID item, UUID asset)
        {
            if (item != UUID.Zero || asset != UUID.Zero)
            {
                rdata.writer.WriteStartElement(part);
                if (item  != UUID.Zero)
                {
                    rdata.writer.WriteAttributeString("Item",item.ToString());
                }

                if (asset != UUID.Zero)
                {
                    rdata.writer.WriteAttributeString("Asset",asset.ToString());
                }
                rdata.writer.WriteEndElement();
            }
        }

        private void FormatUserAppearance(AppearanceRequestData rdata)
        {

            Rest.Log.DebugFormat("{0} FormatUserAppearance", MsgId);

            if (rdata.userAppearance != null)
            {

                Rest.Log.DebugFormat("{0} FormatUserAppearance: appearance object exists", MsgId);
                rdata.writer.WriteStartElement("Appearance");

                rdata.writer.WriteAttributeString("Height", rdata.userAppearance.AvatarHeight.ToString());
//                if (rdata.userAppearance.Owner != UUID.Zero)
//                    rdata.writer.WriteAttributeString("Owner", rdata.userAppearance.Owner.ToString());
                rdata.writer.WriteAttributeString("Serial", rdata.userAppearance.Serial.ToString());

/*
                FormatPart(rdata, "Body", rdata.userAppearance.BodyItem, rdata.userAppearance.BodyAsset);
                FormatPart(rdata, "Skin", rdata.userAppearance.SkinItem, rdata.userAppearance.SkinAsset);
                FormatPart(rdata, "Hair", rdata.userAppearance.HairItem, rdata.userAppearance.HairAsset);
                FormatPart(rdata, "Eyes", rdata.userAppearance.EyesItem, rdata.userAppearance.EyesAsset);

                FormatPart(rdata, "Shirt", rdata.userAppearance.ShirtItem, rdata.userAppearance.ShirtAsset);
                FormatPart(rdata, "Pants", rdata.userAppearance.PantsItem, rdata.userAppearance.PantsAsset);
                FormatPart(rdata, "Skirt", rdata.userAppearance.SkirtItem, rdata.userAppearance.SkirtAsset);
                FormatPart(rdata, "Shoes", rdata.userAppearance.ShoesItem, rdata.userAppearance.ShoesAsset);
                FormatPart(rdata, "Socks", rdata.userAppearance.SocksItem, rdata.userAppearance.SocksAsset);

                FormatPart(rdata, "Jacket", rdata.userAppearance.JacketItem, rdata.userAppearance.JacketAsset);
                FormatPart(rdata, "Gloves", rdata.userAppearance.GlovesItem, rdata.userAppearance.GlovesAsset);

                FormatPart(rdata, "UnderShirt", rdata.userAppearance.UnderShirtItem, rdata.userAppearance.UnderShirtAsset);
                FormatPart(rdata, "UnderPants", rdata.userAppearance.UnderPantsItem, rdata.userAppearance.UnderPantsAsset);
*/
                Rest.Log.DebugFormat("{0} FormatUserAppearance: Formatting attachments", MsgId);

                rdata.writer.WriteStartElement("Attachments");
                List<AvatarAttachment> attachments = rdata.userAppearance.GetAttachments();
                foreach (AvatarAttachment attach in attachments)
                {
                    rdata.writer.WriteStartElement("Attachment");
                    rdata.writer.WriteAttributeString("AtPoint", attach.AttachPoint.ToString());
                    rdata.writer.WriteAttributeString("Item", attach.ItemID.ToString());
                    rdata.writer.WriteAttributeString("Asset", attach.AssetID.ToString());
                    rdata.writer.WriteEndElement();
                }
                rdata.writer.WriteEndElement();

                Primitive.TextureEntry texture = rdata.userAppearance.Texture;

                if (texture != null && (texture.DefaultTexture != null || texture.FaceTextures != null))
                {
                    Rest.Log.DebugFormat("{0} FormatUserAppearance: Formatting textures", MsgId);

                    rdata.writer.WriteStartElement("Texture");

                    if (texture.DefaultTexture != null)
                    {
                        Rest.Log.DebugFormat("{0} FormatUserAppearance: Formatting default texture", MsgId);
                        rdata.writer.WriteAttributeString("Default",
                            texture.DefaultTexture.TextureID.ToString());
                    }

                    if (texture.FaceTextures != null)
                    {

                        Rest.Log.DebugFormat("{0} FormatUserAppearance: Formatting face textures", MsgId);

                        for (int i=0; i<texture.FaceTextures.Length;i++)
                        {
                            if (texture.FaceTextures[i] != null)
                            {
                                rdata.writer.WriteStartElement("Face");
                                rdata.writer.WriteAttributeString("Index", i.ToString());
                                rdata.writer.WriteAttributeString("Id",
                                        texture.FaceTextures[i].TextureID.ToString());
                                rdata.writer.WriteEndElement();
                            }
                        }
                    }

                    rdata.writer.WriteEndElement();
                }

                Rest.Log.DebugFormat("{0} FormatUserAppearance: Formatting visual parameters", MsgId);

                rdata.writer.WriteStartElement("VisualParameters");
                rdata.writer.WriteBase64(rdata.userAppearance.VisualParams,0,
                            rdata.userAppearance.VisualParams.Length);
                rdata.writer.WriteEndElement();
                rdata.writer.WriteFullEndElement();
            }

            Rest.Log.DebugFormat("{0} FormatUserAppearance: completed", MsgId);

            return;
        }

        #region appearance RequestData extension

        internal class AppearanceRequestData : RequestData
        {

            /// <summary>
            /// These are the inventory specific request/response state
            /// extensions.
            /// </summary>

            internal UUID                       uuid = UUID.Zero;
            internal UserProfileData     userProfile = null;
            internal AvatarAppearance userAppearance = null;

            internal AppearanceRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }

        }

        #endregion Appearance RequestData extension

    }
}
