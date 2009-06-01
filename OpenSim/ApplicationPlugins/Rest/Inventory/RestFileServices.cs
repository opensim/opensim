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
using System.IO;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{
    public class RestFileServices : IRest
    {
        private bool    enabled = false;
        private string  qPrefix = "files";

        // A simple constructor is used to handle any once-only
        // initialization of working classes.

        public RestFileServices()
        {
            Rest.Log.InfoFormat("{0} File services initializing", MsgId);
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

            Rest.Plugin.AddPathHandler(DoFile, qPrefix, Allocate);

            // Activate if all went OK

            enabled = true;

            Rest.Log.InfoFormat("{0} File services initialization complete", MsgId);
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
            Rest.Log.InfoFormat("{0} File services ({1}) closing down", MsgId, qPrefix);
        }

        // Properties

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        #region Interface

        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response, string prefix)
        {
            return (RequestData) new FileRequestData(request, response, prefix);
        }

        // Asset Handler

        private void DoFile(RequestData rparm)
        {
            if (!enabled) return;

            FileRequestData rdata = (FileRequestData) rparm;

            Rest.Log.DebugFormat("{0} REST File handler ({1}) ENTRY", MsgId, qPrefix);

            // Now that we know this is a serious attempt to
            // access file data, we should find out who
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
                    DoDelete(rdata);
                    break;
                default :
                    Rest.Log.WarnFormat("{0} File: Method not supported: {1}",
                                        MsgId, rdata.method);
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,String.Format("method <{0}> not supported", rdata.method));
                    break;
                }
            }
            else
            {
                Rest.Log.WarnFormat("{0} File: No agent information provided", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, "no agent information provided");
            }

            Rest.Log.DebugFormat("{0} REST File handler EXIT", MsgId);

        }

        #endregion Interface

        /// <summary>
        /// The only parameter we recognize is a UUID.If an asset with this identification is
        /// found, it's content, base-64 encoded, is returned to the client.
        /// </summary>

        private void DoGet(FileRequestData rdata)
        {

            string path = String.Empty;

            Rest.Log.DebugFormat("{0} REST File handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length > 1)
            {
                try
                {
                    path = rdata.path.Substring(rdata.Parameters[0].Length+qPrefix.Length+2);
                    if (File.Exists(path))
                    {
                        Rest.Log.DebugFormat("{0}  File located <{1}>", MsgId, path);
                        Byte[] data = File.ReadAllBytes(path);
                        rdata.initXmlWriter();
                        rdata.writer.WriteStartElement(String.Empty,"File",String.Empty);
                        rdata.writer.WriteAttributeString("name", path);
                        rdata.writer.WriteBase64(data,0,data.Length);
                        rdata.writer.WriteFullEndElement();
                    }
                    else
                    {
                        Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, path);
                        rdata.Fail(Rest.HttpStatusCodeNotFound, String.Format("invalid parameters : {0}", path));
                    }
                }
                catch (Exception e)
                {
                    Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, e.Message);
                    rdata.Fail(Rest.HttpStatusCodeNotFound, String.Format("invalid parameters : {0} {1}", 
                                     path, e.Message));
                }
            }

            rdata.Complete();
            rdata.Respond(String.Format("File <{0}> : Normal completion", rdata.method));

        }

        /// <summary>
        /// UPDATE existing item, if it exists. URI identifies the item in question.
        /// The only parameter we recognize is a UUID. The enclosed asset data (base-64 encoded)
        /// is decoded and stored in the database, identified by the supplied UUID.
        /// </summary>
        private void DoPut(FileRequestData rdata)
        {
            bool modified = false;
            bool created  = false;
            string path   = String.Empty;

            Rest.Log.DebugFormat("{0} REST File handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length > 1)
            {
                try
                {
                    path = rdata.path.Substring(rdata.Parameters[0].Length+qPrefix.Length+2);
                    bool maymod = File.Exists(path);
                    
                    rdata.initXmlReader();
                    XmlReader xml = rdata.reader;

                    if (!xml.ReadToFollowing("File"))
                    {
                        Rest.Log.DebugFormat("{0} Invalid request data: <{1}>", MsgId, rdata.path);
                        rdata.Fail(Rest.HttpStatusCodeBadRequest,"invalid request data");
                    }

                    Byte[] data = Convert.FromBase64String(xml.ReadElementContentAsString("File", ""));

                    File.WriteAllBytes(path,data);
                    modified =   maymod;
                    created  = ! maymod;
                }
                catch (Exception e)
                {
                    Rest.Log.DebugFormat("{0} Exception during file processing : {1}", MsgId, 
                          e.Message);
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound, "invalid parameters");
            }

            if (created)
            {
                rdata.appendStatus(String.Format("<p> Created file {0} <p>", path));
                rdata.Complete(Rest.HttpStatusCodeCreated);
            }
            else
            {
                if (modified)
                {
                    rdata.appendStatus(String.Format("<p> Modified file {0} <p>", path));
                    rdata.Complete(Rest.HttpStatusCodeOK);
                }
                else
                {
                    rdata.Complete(Rest.HttpStatusCodeNoContent);
                }
            }

            rdata.Respond(String.Format("File {0} : Normal completion", rdata.method));

        }

        /// <summary>
        /// CREATE new item, replace if it exists. URI identifies the context for the item in question.
        /// No parameters are required for POST, just thepayload.
        /// </summary>

        private void DoPost(FileRequestData rdata)
        {

            bool modified = false;
            bool created  = false;
            string path   = String.Empty;

            Rest.Log.DebugFormat("{0} REST File handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length > 1)
            {
                try
                {
                    path = rdata.path.Substring(rdata.Parameters[0].Length+qPrefix.Length+2);
                    bool maymod = File.Exists(path);
                    
                    rdata.initXmlReader();
                    XmlReader xml = rdata.reader;

                    if (!xml.ReadToFollowing("File"))
                    {
                        Rest.Log.DebugFormat("{0} Invalid request data: <{1}>", MsgId, rdata.path);
                        rdata.Fail(Rest.HttpStatusCodeBadRequest,"invalid request data");
                    }

                    Byte[] data = Convert.FromBase64String(xml.ReadElementContentAsString("File", ""));

                    File.WriteAllBytes(path,data);
                    modified =   maymod;
                    created  = ! maymod;
                }
                catch (Exception e)
                {
                    Rest.Log.DebugFormat("{0} Exception during file processing : {1}", MsgId, 
                          e.Message);
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound, "invalid parameters");
            }

            if (created)
            {
                rdata.appendStatus(String.Format("<p> Created file {0} <p>", path));
                rdata.Complete(Rest.HttpStatusCodeCreated);
            }
            else
            {
                if (modified)
                {
                    rdata.appendStatus(String.Format("<p> Modified file {0} <p>", path));
                    rdata.Complete(Rest.HttpStatusCodeOK);
                }
                else
                {
                    rdata.Complete(Rest.HttpStatusCodeNoContent);
                }
            }

            rdata.Respond(String.Format("File {0} : Normal completion", rdata.method));

        }

        /// <summary>
        /// CREATE new item, replace if it exists. URI identifies the context for the item in question.
        /// No parameters are required for POST, just thepayload.
        /// </summary>

        private void DoDelete(FileRequestData rdata)
        {

            bool modified = false;
            bool created  = false;
            string path   = String.Empty;

            Rest.Log.DebugFormat("{0} REST File handler, Method = <{1}> ENTRY", MsgId, rdata.method);

            if (rdata.Parameters.Length > 1)
            {
                try
                {
                    path = rdata.path.Substring(rdata.Parameters[0].Length+qPrefix.Length+2);

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception e)
                {
                    Rest.Log.DebugFormat("{0} Exception during file processing : {1}", MsgId, 
                          e.Message);
                    rdata.Fail(Rest.HttpStatusCodeNotFound, String.Format("invalid parameters : {0} {1}",
                          path, e.Message));
                }
            }
            else
            {
                Rest.Log.DebugFormat("{0} Invalid parameters: <{1}>", MsgId, rdata.path);
                rdata.Fail(Rest.HttpStatusCodeNotFound, "invalid parameters");
            }

            if (created)
            {
                rdata.appendStatus(String.Format("<p> Created file {0} <p>", path));
                rdata.Complete(Rest.HttpStatusCodeCreated);
            }
            else
            {
                if (modified)
                {
                    rdata.appendStatus(String.Format("<p> Modified file {0} <p>", path));
                    rdata.Complete(Rest.HttpStatusCodeOK);
                }
                else
                {
                    rdata.Complete(Rest.HttpStatusCodeNoContent);
                }
            }

            rdata.Respond(String.Format("File {0} : Normal completion", rdata.method));

        }

        /// <summary>
        /// File processing has no special data area requirements.
        /// </summary>

        internal class FileRequestData : RequestData
        {
            internal FileRequestData(OSHttpRequest request, OSHttpResponse response, string prefix)
                : base(request, response, prefix)
            {
            }
        }
    }
}
