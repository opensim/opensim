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
using System.Collections.Generic;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Agent.AssetTransaction
{
    public class AssetTransactionModule : IRegionModule, IAgentAssetTransactions
    {
        private readonly Dictionary<UUID, Scene> RegisteredScenes = new Dictionary<UUID, Scene>();
        private bool m_dumpAssetsToFile = false;
        private Scene m_scene = null;

        private AgentAssetTransactionsManager m_transactionManager;

        public AssetTransactionModule()
        {
            // System.Console.WriteLine("creating AgentAssetTransactionModule");
        }

        #region IAgentAssetTransactions Members

        public void HandleItemCreationFromTransaction(IClientAPI remoteClient, UUID transactionID, UUID folderID,
                                                      uint callbackID, string description, string name, sbyte invType,
                                                      sbyte type, byte wearableType, uint nextOwnerMask)
        {
            m_transactionManager.HandleItemCreationFromTransaction(remoteClient, transactionID, folderID, callbackID, description, name, invType, type,
                                                                   wearableType, nextOwnerMask);
        }

        public void HandleItemUpdateFromTransaction(IClientAPI remoteClient, UUID transactionID, InventoryItemBase item)
        {
            m_transactionManager.HandleItemUpdateFromTransaction(remoteClient, transactionID, item);
        }
        
        public void HandleTaskItemUpdateFromTransaction(
            IClientAPI remoteClient, SceneObjectPart part, UUID transactionID, TaskInventoryItem item)
        {
            m_transactionManager.HandleTaskItemUpdateFromTransaction(remoteClient, part, transactionID, item);
        }        

        public void RemoveAgentAssetTransactions(UUID userID)
        {
            m_transactionManager.RemoveAgentAssetTransactions(userID);
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.RegionID))
            {
                // System.Console.WriteLine("initialising AgentAssetTransactionModule");
                RegisteredScenes.Add(scene.RegionInfo.RegionID, scene);
                scene.RegisterModuleInterface<IAgentAssetTransactions>(this);

                scene.EventManager.OnNewClient += NewClient;
            }

            if (m_scene == null)
            {
                m_scene = scene;
                if (config.Configs["StandAlone"] != null)
                {
                    try
                    {
                        m_dumpAssetsToFile = config.Configs["StandAlone"].GetBoolean("dump_assets_to_file", false);
                        m_transactionManager = new AgentAssetTransactionsManager(m_scene, m_dumpAssetsToFile);
                    }
                    catch (Exception)
                    {
                        m_transactionManager = new AgentAssetTransactionsManager(m_scene, false);
                    }
                }
                else
                {
                    m_transactionManager = new AgentAssetTransactionsManager(m_scene, false);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AgentTransactionModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            client.OnAssetUploadRequest += m_transactionManager.HandleUDPUploadRequest;
            client.OnXferReceive += m_transactionManager.HandleXfer;
        }
    }
}
