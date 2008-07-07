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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;

namespace OpenSim.Grid.AssetServer
{
    public class GetAssetStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private OpenAsset_Main m_assetManager;
        private IAssetProvider m_assetProvider;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="assetManager"></param>
        /// <param name="assetProvider"></param>
        public GetAssetStreamHandler(OpenAsset_Main assetManager, IAssetProvider assetProvider)
            : base("GET", "/assets")
        {
            m_log.Info("[REST]: In Get Request");
            // m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);
            byte[] result = new byte[] {};

            string[] p = param.Split(new char[] {'/', '?', '&'}, StringSplitOptions.RemoveEmptyEntries);

            if (p.Length > 0)
            {
                LLUUID assetID = null;

                if (!LLUUID.TryParse(p[0], out assetID))
                {
                    m_log.InfoFormat(
                        "[REST]: GET:/asset ignoring request with malformed UUID {0}", p[0]);
                    return result;
                }

                if (StatsManager.AssetStats != null)
                    StatsManager.AssetStats.AddRequest();

                AssetBase asset = m_assetProvider.FetchAsset(assetID);
                if (asset != null)
                {
                    XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
                    MemoryStream ms = new MemoryStream();
                    XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                    xw.Formatting = Formatting.Indented;
                    xs.Serialize(xw, asset);
                    xw.Flush();

                    ms.Seek(0, SeekOrigin.Begin);
                    //StreamReader sr = new StreamReader(ms);

                    result = ms.GetBuffer();

                    m_log.InfoFormat(
                        "[REST]: GET:/asset found {0} with name {1}, size {2} bytes",
                        assetID, asset.Name, result.Length);

                    Array.Resize<byte>(ref result, (int) ms.Length);
                }
                else
                {
                    if (StatsManager.AssetStats != null)
                        StatsManager.AssetStats.AddNotFoundRequest();

                    m_log.InfoFormat("[REST]: GET:/asset failed to find {0}", assetID);
                }
            }

            return result;
        }
    }

    public class PostAssetStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private OpenAsset_Main m_assetManager;
        private IAssetProvider m_assetProvider;

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);

            LLUUID assetId;
            if (param.Length > 0)
                LLUUID.TryParse(param, out assetId);
            // byte[] txBuffer = new byte[4096];

            XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
            AssetBase asset = (AssetBase) xs.Deserialize(request);

            m_log.InfoFormat("[REST]: Creating asset {0}", asset.FullID);
            m_assetProvider.CreateAsset(asset);

            return new byte[] {};
        }

        public PostAssetStreamHandler(OpenAsset_Main assetManager, IAssetProvider assetProvider)
            : base("POST", "/assets")
        {
            // m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }
    }
}
