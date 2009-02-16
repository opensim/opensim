/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Xml;
using ExtensionLoader;
using OpenMetaverse;
using HttpServer;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class OpenSimAssetFrontendPlugin : IAssetInventoryServerPlugin
    {
        AssetInventoryServer server;

        public OpenSimAssetFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            // Asset request
            server.HttpServer.AddHandler("get", null, @"^/assets/", AssetRequestHandler);

            // Asset creation
            server.HttpServer.AddHandler("post", null, @"^/assets/", AssetPostHandler);
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            Logger.Log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "AssetInventoryServer OpenSim asset frontend"; }
        }

        #endregion IPlugin implementation

        bool AssetRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID assetID;
            // Split the URL up to get the asset ID out
            string[] rawUrl = request.Uri.PathAndQuery.Split('/');

            if (rawUrl.Length >= 3 && rawUrl[2].Length >= 36 && UUID.TryParse(rawUrl[2].Substring(0, 36), out assetID))
            {
                Metadata metadata;
                byte[] assetData;
                BackendResponse dataResponse;

                if ((dataResponse = server.StorageProvider.TryFetchDataMetadata(assetID, out metadata, out assetData)) == BackendResponse.Success)
                {
                    MemoryStream stream = new MemoryStream();

                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    XmlWriter writer = XmlWriter.Create(stream, settings);

                    writer.WriteStartDocument();
                    writer.WriteStartElement("AssetBase");
                    writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                    writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
                    writer.WriteStartElement("FullID");
                    writer.WriteStartElement("Guid");
                    writer.WriteString(assetID.ToString());
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteStartElement("ID");
                    writer.WriteString(assetID.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("Data");
                    writer.WriteBase64(assetData, 0, assetData.Length);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Type");
                    writer.WriteValue(Utils.ContentTypeToSLAssetType(metadata.ContentType));
                    writer.WriteEndElement();
                    writer.WriteStartElement("Name");
                    writer.WriteString(metadata.Name);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Description");
                    writer.WriteString(metadata.Description);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Local");
                    writer.WriteValue(false);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Temporary");
                    writer.WriteValue(metadata.Temporary);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();

                    writer.Flush();
                    byte[] buffer = stream.GetBuffer();

                    response.Status = HttpStatusCode.OK;
                    response.ContentType = "application/xml";
                    response.ContentLength = stream.Length;
                    response.Body.Write(buffer, 0, (int)stream.Length);
                    response.Body.Flush();
                }
                else
                {
                    Logger.Log.WarnFormat("Failed to fetch asset data or metadata for {0}: {1}", assetID, dataResponse);
                    response.Status = HttpStatusCode.NotFound;
                }
            }
            else
            {
                Logger.Log.Warn("Unrecognized OpenSim asset request: " + request.Uri.PathAndQuery);
            }

            return true;
        }

        bool AssetPostHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            byte[] assetData = null;
            Metadata metadata = new Metadata();

            Logger.Log.Debug("Handling OpenSim asset upload");

            try
            {
                using (XmlReader reader = XmlReader.Create(request.Body))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("AssetBase");

                    reader.ReadStartElement("FullID");
                    UUID.TryParse(reader.ReadElementContentAsString("Guid", String.Empty), out metadata.ID);
                    reader.ReadEndElement();
                    reader.ReadStartElement("ID");
                    reader.Skip();
                    reader.ReadEndElement();

                    // HACK: Broken on Mono. https://bugzilla.novell.com/show_bug.cgi?id=464229
                    //int readBytes = 0;
                    //byte[] buffer = new byte[1024];
                    //MemoryStream stream = new MemoryStream();
                    //BinaryWriter writer = new BinaryWriter(stream);
                    //while ((readBytes = reader.ReadElementContentAsBase64(buffer, 0, buffer.Length)) > 0)
                    //    writer.Write(buffer, 0, readBytes);
                    //writer.Flush();
                    //assetData = stream.GetBuffer();
                    //Array.Resize<byte>(ref assetData, (int)stream.Length);

                    assetData = Convert.FromBase64String(reader.ReadElementContentAsString());

                    int type;
                    Int32.TryParse(reader.ReadElementContentAsString("Type", String.Empty), out type);
                    metadata.ContentType = Utils.SLAssetTypeToContentType(type);
                    metadata.Name = reader.ReadElementContentAsString("Name", String.Empty);
                    metadata.Description = reader.ReadElementContentAsString("Description", String.Empty);
                    Boolean.TryParse(reader.ReadElementContentAsString("Local", String.Empty), out metadata.Temporary);
                    Boolean.TryParse(reader.ReadElementContentAsString("Temporary", String.Empty), out metadata.Temporary);

                    reader.ReadEndElement();
                }

                if (assetData != null && assetData.Length > 0)
                {
                    metadata.SHA1 = OpenMetaverse.Utils.SHA1(assetData);
                    metadata.CreationDate = DateTime.Now;

                    BackendResponse storageResponse = server.StorageProvider.TryCreateAsset(metadata, assetData);

                    if (storageResponse == BackendResponse.Success)
                        response.Status = HttpStatusCode.Created;
                    else if (storageResponse == BackendResponse.NotFound)
                        response.Status = HttpStatusCode.NotFound;
                    else
                        response.Status = HttpStatusCode.InternalServerError;
                }
                else
                {
                    Logger.Log.Warn("AssetPostHandler called with no asset data");
                    response.Status = HttpStatusCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse POST data (expecting AssetBase): " + ex.Message);
                response.Status = HttpStatusCode.BadRequest;
            }

            Logger.Log.Debug("Finished handling OpenSim asset upload, Status: " + response.Status.ToString());
            return true;
        }
    }
}
