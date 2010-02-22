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
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

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
                Rest.Log.InfoFormat("{0} Prefixing domain name ({1})", MsgId, qPrefix);
                qPrefix = String.Format("{0}{1}{2}", Rest.Prefix, Rest.UrlPathSeparator, qPrefix);
                Rest.Log.InfoFormat("{0} Fully qualified domain name is <{1}>", MsgId, qPrefix);
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
            Rest.Log.InfoFormat("{0} Asset services ({1}) closing down", MsgId, qPrefix);
        }

        // Properties

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        #region Interface

        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response, string prefix)
        {
            return (RequestData) new AssetRequestData(request, response, prefix);
        }

        // Asset Handler

        private void DoAsset(RequestData rparm)
        {
            if (!enabled) return;

            AssetRequestData rdata = (AssetRequestData) rparm;

            Rest.Log.DebugFormat("{0} REST Asset handler ({1}) ENTRY", MsgId, qPrefix);

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
                    rdata.Fail(Rest.HttpStatusCodeNotAuthorized, String.Format("user \"{0}\" could not be authenticated"));
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

            if (rdata.Parameters.Length > 0)
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
                    DoPost(rdata);
                    break;
                case "delete" :
                default :
                    Rest.Log.WarnFormat("{0} Asset: Method not supported: {1}",
                                        MsgId, rdata.method);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,String.Format("method <{0}> not supported", rdata.method));
                    break;
                }
            }
            else
            {
                Rest.Log.WarnFormat("{0} Asset: No agent information provided", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, "no agent information provided");
            }

            Rest.Log.DebugFormat("{0} REST Asset handler EXIT", MsgId);
        }

        #endregion Interface

        /// <summary>
        /// The only parameter we recognize is a UUID.If an asset with this identification is
        /// found, it's content, base-64 encoded, is returned to the client.
        /// </summary>

        private void DoGet(AssetRequestData rdata)
        {
            Rest.Log.DebugFormat("{0} REST Asset handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length == 1)
            {
                UUID uuid = new UUID(rdata.Parameters[0]);
                AssetBase asset = Rest.AssetServices.Get(uuid.ToString());

                if (asset != null)
                {
                    Rest.Log.DebugFormat("{0}  Asset located <{1}>", MsgId, rdata.Parameters[0]);

                    rdata.initXmlWriter();

                    rdata.writer.WriteStartElement(String.Empty,"Asset",String.Empty);

                    rdata.writer.WriteAttributeString("id", asset.ID);
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
                    rdata.Fail(Rest.HttpStatusCodeNotFound, "invalid parameters");
                }
            }

            rdata.Complete();
            rdata.Respond(String.Format("Asset <{0}> : Normal completion", rdata.method));

        }

        /// <summary>
        /// UPDATE existing item, if it exists. URI identifies the item in question.
        /// The only parameter we recognize is a UUID. The enclosed asset data (base-64 encoded)
        /// is decoded and stored in the database, identified by the supplied UUID.
        /// </summary>
        private void DoPut(AssetRequestData rdata)
        {
            bool modified = false;
            bool created  = false;

            AssetBase asset = null;

            Rest.Log.DebugFormat("{0} REST Asset handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length == 1)
            {

                rdata.initXmlReader();
                XmlReader xml = rdata.reader;

                if (!xml.ReadToFollowing("Asset"))
                {
                    Rest.Log.DebugFormat("{0} Invalid request data: <{1}>", MsgId, rdata.path);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,"invalid request data");
                }

                UUID uuid = new UUID(rdata.Parameters[0]);
                asset = Rest.AssetServices.Get(uuid.ToString());

                modified = (asset != null);
                created  = !modified;

                asset = new AssetBase(uuid, xml.GetAttribute("name"), SByte.Parse(xml.GetAttribute("type")), UUID.Zero.ToString());
                asset.Description = xml.GetAttribute("desc");
                asset.Local       = Int32.Parse(xml.GetAttribute("local")) != 0;
                asset.Temporary   = Int32.Parse(xml.GetAttribute("temporary")) != 0;
                asset.Data        = Convert.FromBase64String(xml.ReadElementContentAsString("Asset", ""));

                if (asset.ID != rdata.Parameters[0])
                {
                    Rest.Log.WarnFormat("{0} URI and payload disagree on UUID U:{1} vs P:{2}",
                                        MsgId, rdata.Parameters[0], asset.ID);
                }

                Rest.AssetServices.Store(asset);

            }
            else
            {
                Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound, "invalid parameters");
            }

            if (created)
            {
                rdata.appendStatus(String.Format("<p> Created asset {0}, UUID {1} <p>", asset.Name, asset.FullID));
                rdata.Complete(Rest.HttpStatusCodeCreated);
            }
            else
            {
                if (modified)
                {
                    rdata.appendStatus(String.Format("<p> Modified asset {0}, UUID {1} <p>", asset.Name, asset.FullID));
                    rdata.Complete(Rest.HttpStatusCodeOK);
                }
                else
                {
                    rdata.Complete(Rest.HttpStatusCodeNoContent);
                }
            }

            rdata.Respond(String.Format("Asset {0} : Normal completion", rdata.method));

        }

        /// <summary>
        /// CREATE new item, replace if it exists. URI identifies the context for the item in question.
        /// No parameters are required for POST, just thepayload.
        /// </summary>

        private void DoPost(AssetRequestData rdata)
        {

            bool modified = false;
            bool created  = false;

            Rest.Log.DebugFormat("{0} REST Asset handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length != 0)
            {
                Rest.Log.WarnFormat("{0} Parameters ignored <{1}>", MsgId, rdata.path);
                Rest.Log.InfoFormat("{0} POST of an asset has no parameters", MsgId, rdata.path);
            }

            rdata.initXmlReader();
            XmlReader xml = rdata.reader;

            if (!xml.ReadToFollowing("Asset"))
            {
                Rest.Log.DebugFormat("{0} Invalid request data: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeBadRequest,"invalid request data");
            }

            UUID uuid = new UUID(xml.GetAttribute("id"));
            AssetBase asset = Rest.AssetServices.Get(uuid.ToString());

            modified = (asset != null);
            created  = !modified;

            asset             = new AssetBase(uuid, xml.GetAttribute("name"), SByte.Parse(xml.GetAttribute("type")), UUID.Zero.ToString());
            asset.Description = xml.GetAttribute("desc");
            asset.Local       = Int32.Parse(xml.GetAttribute("local")) != 0;
            asset.Temporary   = Int32.Parse(xml.GetAttribute("temporary")) != 0;
            asset.Data        = Convert.FromBase64String(xml.ReadElementContentAsString("Asset", ""));

            Rest.AssetServices.Store(asset);

            if (created)
            {
                rdata.appendStatus(String.Format("<p> Created asset {0}, UUID {1} <p>", asset.Name, asset.FullID));
                rdata.Complete(Rest.HttpStatusCodeCreated);
            }
            else
            {
                if (modified)
                {
                    rdata.appendStatus(String.Format("<p> Modified asset {0}, UUID {1} <p>", asset.Name, asset.FullID));
                    rdata.Complete(Rest.HttpStatusCodeOK);
                }
                else
                {
                    rdata.Complete(Rest.HttpStatusCodeNoContent);
                }
            }

            rdata.Respond(String.Format("Asset {0} : Normal completion", rdata.method));

        }

        /// <summary>
        /// Asset processing has no special data area requirements.
        /// </summary>

        internal class AssetRequestData : RequestData
        {
            internal AssetRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }
        }
    }
}
