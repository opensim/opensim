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

using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Method called when all the necessary assets for an archive request have been received.
    /// </summary>
    public delegate void AssetsRequestCallback(IDictionary<LLUUID, AssetBase> assets);
    
    /// <summary>
    /// Execute the write of an archive once we have received all the necessary data
    /// </summary>
    public class ArchiveWriteRequestExecution
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_savePath;
        protected string m_serializedEntities;        
        
        public ArchiveWriteRequestExecution(string serializedEntities, string savePath)
        {
            m_serializedEntities = serializedEntities;
            m_savePath = savePath;
        }
        
        protected internal void ReceivedAllAssets(IDictionary<LLUUID, AssetBase> assets)
        {
            m_log.DebugFormat("[ARCHIVER]: Received all {0} assets required", assets.Count);

            TarArchiveWriter archive = new TarArchiveWriter();

            archive.AddFile(ArchiveConstants.PRIMS_PATH, m_serializedEntities);
            
            AssetsArchiver assetsArchiver = new AssetsArchiver(assets);
            assetsArchiver.Archive(archive);

            archive.WriteTar(m_savePath);

            m_log.InfoFormat("[ARCHIVER]: Wrote out OpenSimulator archive {0}", m_savePath);
        }        
    }
}
