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
 * 
 */

using libsecondlife;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{

    public class RestAssetServices : IRest
    {

        private bool    enabled = false;
        private string  qPrefix = "assets";

        // A simple constructor is used to handle any once-only
        // initialization of working classes.

        public RestAssetServices()
        {

            Rest.Log.InfoFormat("{0} Asset services initializing", MsgId);
            Rest.Log.InfoFormat("{0} Using REST Implementation Version {1}", MsgId, Rest.Version);

            // If the handler specifies a relative path for its domain
            // then we must add the standard absolute prefix, e.g. /admin

            if (!qPrefix.StartsWith(Rest.UrlPathSeparator))
            {
                qPrefix = Rest.Prefix + Rest.UrlPathSeparator + qPrefix;
            }

            // Register interface using the fully-qualified prefix

            Rest.Plugin.AddPathHandler(DoAsset, qPrefix, Allocate);

            // Activate if all went OK

            enabled = true;

            Rest.Log.InfoFormat("{0} Asset services initialization complete", MsgId);

        }

        // Post-construction, pre-enabled initialization opportunity
        // Not currently exploited.

        public void Initialize()
        {
        }

        // Called by the plug-in to halt REST processing. Local processing is
        // disabled, and control blocks until all current processing has 
        // completed. No new processing will be started

        public void Close()
        {
            enabled = false;
            Rest.Log.InfoFormat("{0} Asset services closing down", MsgId);
        }

        // Properties

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        #region Interface

        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response)
        {
            return (RequestData) new AssetRequestData(request, response, qPrefix);
        }

        // Asset Handler

        private void DoAsset(RequestData rparm)
        {

            if (!enabled) return;

            AssetRequestData rdata = (AssetRequestData) rparm;

            Rest.Log.DebugFormat("{0} REST Asset handler ENTRY", MsgId);

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
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, 
                                         rdata.request.Headers.Get("Authorization"));
                }
                else
                {
                    Rest.Log.ErrorFormat("{0} User authentication failed", MsgId);
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, 
                                         rdata.request.Headers.Get("Authorization"));
                }
                throw (e);
            }

            // Remove the prefix and what's left are the parameters. If we don't have
            // the parameters we need, fail the request. Parameters do NOT include
            // any supplied query values.

            if (rdata.parameters.Length > 0)
            {
                switch (rdata.method)
                {
                case "get" :
                    DoGet(rdata);
                    break;
                case "put" :
                    DoPut(rdata);
                    break;
                case "post" :
                case "delete" :
                default :
                    Rest.Log.WarnFormat("{0} Asset: Method not supported: {1}", 
                                        MsgId, rdata.method);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                               Rest.HttpStatusDescBadRequest);
                    break;
                }
            }
            else
            {
                Rest.Log.WarnFormat("{0} Asset: No agent information provided", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, Rest.HttpStatusDescBadRequest);
            }

            Rest.Log.DebugFormat("{0} REST Asset handler EXIT", MsgId);

        }

        #endregion Interface

        private void DoGet(AssetRequestData rdata)
        {

            bool istexture = false;

            Rest.Log.DebugFormat("{0} REST Asset handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            // The only parameter we accept is an LLUUID for
            // the asset

            if (rdata.parameters.Length == 1)
            {

                LLUUID uuid = new LLUUID(rdata.parameters[0]);
                AssetBase asset = Rest.AssetServices.GetAsset(uuid, istexture);

                if (asset != null)
                {
                    
                    Rest.Log.DebugFormat("{0}  Asset located <{1}>", MsgId, rdata.parameters[0]);

                    rdata.initXmlWriter();

                    rdata.writer.WriteStartElement(String.Empty,"Asset",String.Empty);

                    rdata.writer.WriteAttributeString("id", asset.ID.ToString());
                    rdata.writer.WriteAttributeString("name", asset.Name);
                    rdata.writer.WriteAttributeString("desc", asset.Description);
                    rdata.writer.WriteAttributeString("type", asset.Type.ToString());
                    rdata.writer.WriteAttributeString("local", asset.Local.ToString());
                    rdata.writer.WriteAttributeString("temporary", asset.Temporary.ToString());

                    rdata.writer.WriteBase64(asset.Data,0,asset.Data.Length);

                    rdata.writer.WriteFullEndElement();

                }
                else
                {
                    Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeNotFound, 
                               Rest.HttpStatusDescNotFound);
                }
            }

            rdata.Complete();
            rdata.Respond("Asset " + rdata.method + ": Normal completion");

        }

        private void DoPut(AssetRequestData rdata)
        {
            Rest.Log.DebugFormat("{0} REST Asset handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            // The only parameter we accept is an LLUUID for
            // the asset

            if (rdata.parameters.Length == 1)
            {
                rdata.initXmlReader();
                XmlReader xml = rdata.reader;

                if (!xml.ReadToFollowing("Asset"))
                {
                    Rest.Log.DebugFormat("{0} Invalid request data: <{1}>", MsgId, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest, 
                               Rest.HttpStatusDescBadRequest);
                }

                AssetBase asset = new AssetBase();
                asset.ID = rdata.parameters[0];
                asset.Name = xml.GetAttribute("name");
                asset.Description = xml.GetAttribute("desc");
                asset.Type = SByte.Parse(xml.GetAttribute("type"));
                asset.Local = Int32.Parse(xml.GetAttribute("local")) != 0;
                asset.Temporary = Int32.Parse(xml.GetAttribute("temporary")) != 0;
                asset.Data = (new System.Text.ASCIIEncoding()).GetBytes(Rest.Base64ToString(xml.ReadElementContentAsString("Asset", "")));

                Rest.AssetServices.AddAsset(asset);
            }
            else
            {
                Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound, 
                           Rest.HttpStatusDescNotFound);
            }

            rdata.Complete();
            rdata.Respond("Asset " + rdata.method + ": Normal completion");

        }

        internal class AssetRequestData : RequestData
        {
            internal AssetRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }
        }

    }
}
