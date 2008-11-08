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

using System.IO;
using System.Xml.Serialization;

namespace OpenSim.Framework.Communications.Cache
{
    public class FileAssetClient : AssetServerBase
    {
        private readonly string m_dir;
        private readonly XmlSerializer m_xs = new XmlSerializer(typeof(AssetBase));

        public FileAssetClient(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            m_dir = dir;
        }
        public override void StoreAsset(AssetBase asset)
        {
            byte[] idBytes = asset.FullID.Guid.ToByteArray();

            string cdir = m_dir + Path.DirectorySeparatorChar + idBytes[0]
                                + Path.DirectorySeparatorChar + idBytes[1];

            if (!Directory.Exists(m_dir + Path.DirectorySeparatorChar + idBytes[0]))
                Directory.CreateDirectory(m_dir + Path.DirectorySeparatorChar + idBytes[0]);

            if (!Directory.Exists(cdir))
                Directory.CreateDirectory(cdir);

            FileStream x = new FileStream(cdir + Path.DirectorySeparatorChar + asset.FullID + ".xml", FileMode.Create);
            m_xs.Serialize(x, asset);

            x.Flush();
            x.Close();
        }

        public override void UpdateAsset(AssetBase asset)
        {
            StoreAsset(asset);
        }

        protected override AssetBase GetAsset(AssetRequest req)
        {
            byte[] idBytes = req.AssetID.Guid.ToByteArray();

            string cdir = m_dir + Path.DirectorySeparatorChar + idBytes[0]
                                + Path.DirectorySeparatorChar + idBytes[1];
            if (File.Exists(cdir + Path.DirectorySeparatorChar + req.AssetID + ".xml"))
            {
                FileStream x = File.OpenRead(cdir + Path.DirectorySeparatorChar + req.AssetID + ".xml");
                AssetBase ret = (AssetBase) m_xs.Deserialize(x);
                x.Close();
                return ret;
            }
            return null;
        }
    }
}
