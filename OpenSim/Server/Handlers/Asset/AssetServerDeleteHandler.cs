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

using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Server.Handlers.Asset
{
    /// <summary>
    /// Remote deletes allowed.
    /// </summary>
    public enum AllowedRemoteDeleteTypes
    {
        None,
        MapTile,
        All
    }

    public class AssetServerDeleteHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_AssetService;

        /// <summary>
        /// Asset types that can be deleted remotely.
        /// </summary>
        private AllowedRemoteDeleteTypes m_allowedTypes;

        public AssetServerDeleteHandler(IAssetService service, AllowedRemoteDeleteTypes allowedTypes) :
                base("DELETE", "/assets")
        {
            m_AssetService = service;
            m_allowedTypes = allowedTypes;
        }

        public AssetServerDeleteHandler(IAssetService service, AllowedRemoteDeleteTypes allowedTypes, IServiceAuth auth) :
            base("DELETE", "/assets", auth)
        {
            m_AssetService = service;
            m_allowedTypes = allowedTypes;
        }
        protected override byte[] ProcessRequest(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            bool result = false;

            string[] p = SplitParams(path);

            if (p.Length > 0)
            {
                if (m_allowedTypes != AllowedRemoteDeleteTypes.None)
                {
                    string assetID = p[0];

                    AssetBase asset = m_AssetService.Get(assetID);
                    if (asset != null)
                    {
                        if (m_allowedTypes == AllowedRemoteDeleteTypes.All
                            || (int)(asset.Flags & AssetFlags.Maptile) != 0)
                        {
                            result = m_AssetService.Delete(assetID);
                        }
                        else
                        {
                            m_log.DebugFormat(
                                "[ASSET SERVER DELETE HANDLER]: Request to delete asset {0}, but type is {1} and allowed remote delete types are {2}",
                                assetID, (AssetFlags)asset.Flags, m_allowedTypes);
                        }
                    }
                }
            }

            XmlSerializer xs = new XmlSerializer(typeof(bool));
            return ServerUtils.SerializeResult(xs, result);
        }
    }
}