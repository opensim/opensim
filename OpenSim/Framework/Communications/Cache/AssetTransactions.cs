/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Capabilities;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications.Caches
{
    public class AgentAssetTransactions
    {
        // Fields
        public List<AssetCapsUploader> CapsUploaders = new List<AssetCapsUploader>();
        public List<NoteCardCapsUpdate> NotecardUpdaters = new List<NoteCardCapsUpdate>();
        public LLUUID UserID;
        public Dictionary<LLUUID, AssetXferUploader> XferUploaders = new Dictionary<LLUUID, AssetXferUploader>();

        // Methods
        public AgentAssetTransactions(LLUUID agentID)
        {
            this.UserID = agentID;
        }

        public AssetCapsUploader RequestCapsUploader()
        {
            AssetCapsUploader uploader = new AssetCapsUploader();
            this.CapsUploaders.Add(uploader);
            return uploader;
        }

        public NoteCardCapsUpdate RequestNoteCardUpdater()
        {
            NoteCardCapsUpdate update = new NoteCardCapsUpdate();
            this.NotecardUpdaters.Add(update);
            return update;
        }

        public AssetXferUploader RequestXferUploader(LLUUID transactionID)
        {
            AssetXferUploader uploader = new AssetXferUploader();
            this.XferUploaders.Add(transactionID, uploader);
            return uploader;
        }

        // Nested Types
        public class AssetCapsUploader
        {
            // Fields
            private BaseHttpServer httpListener;
            private LLUUID inventoryItemID;
            private string m_assetDescription = "";
            private string m_assetName = "";
            private LLUUID m_folderID;
            private LLUUID newAssetID;
            private bool SaveImages = false;
            private string uploaderPath = "";

            // Events
            public event UpLoadedTexture OnUpLoad;

            // Methods
            public void Initialise(string assetName, string assetDescription, LLUUID assetID, LLUUID inventoryItem, LLUUID folderID, string path, BaseHttpServer httpServer)
            {
                this.m_assetName = assetName;
                this.m_assetDescription = assetDescription;
                this.m_folderID = folderID;
                this.newAssetID = assetID;
                this.inventoryItemID = inventoryItem;
                this.uploaderPath = path;
                this.httpListener = httpServer;
            }

            private void SaveImageToFile(string filename, byte[] data)
            {
                FileStream output = File.Create(filename);
                BinaryWriter writer = new BinaryWriter(output);
                writer.Write(data);
                writer.Close();
                output.Close();
            }

            public string uploaderCaps(byte[] data, string path, string param)
            {
                LLUUID inventoryItemID = this.inventoryItemID;
                string text = "";
                LLSDAssetUploadComplete complete = new LLSDAssetUploadComplete();
                complete.new_asset = this.newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                this.httpListener.RemoveStreamHandler("POST", this.uploaderPath);
                if (this.SaveImages)
                {
                    this.SaveImageToFile(this.m_assetName + ".jp2", data);
                }
                if (this.OnUpLoad != null)
                {
                    this.OnUpLoad(this.m_assetName, "description", this.newAssetID, inventoryItemID, LLUUID.Zero, data);
                }
                return text;
            }
        }

        public class AssetXferUploader
        {
            // Fields
            public bool AddToInventory;
            public AssetBase Asset;
            public LLUUID InventFolder = LLUUID.Zero;
            private IClientAPI ourClient;
            public LLUUID TransactionID = LLUUID.Zero;
            public bool UploadComplete;
            public uint XferID;

            // Methods
            public void HandleXferPacket(uint xferID, uint packetID, byte[] data)
            {
                if (this.XferID == xferID)
                {
                    if (this.Asset.Data.Length > 1)
                    {
                        byte[] destinationArray = new byte[this.Asset.Data.Length + data.Length];
                        Array.Copy(this.Asset.Data, 0, destinationArray, 0, this.Asset.Data.Length);
                        Array.Copy(data, 0, destinationArray, this.Asset.Data.Length, data.Length);
                        this.Asset.Data = destinationArray;
                    }
                    else
                    {
                        byte[] buffer2 = new byte[data.Length - 4];
                        Array.Copy(data, 4, buffer2, 0, data.Length - 4);
                        this.Asset.Data = buffer2;
                    }
                    ConfirmXferPacketPacket newPack = new ConfirmXferPacketPacket();
                    newPack.XferID.ID = xferID;
                    newPack.XferID.Packet = packetID;
                    this.ourClient.OutPacket(newPack);
                    if ((packetID & 0x80000000) != 0)
                    {
                        this.SendCompleteMessage();
                    }
                }
            }

            public void Initialise(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data)
            {
                this.ourClient = remoteClient;
                this.Asset = new AssetBase();
                this.Asset.FullID = assetID;
                this.Asset.InvType = type;
                this.Asset.Type = type;
                this.Asset.Data = data;
                this.Asset.Name = "blank";
                this.Asset.Description = "empty";
                this.TransactionID = transaction;
                if (this.Asset.Data.Length > 2)
                {
                    this.SendCompleteMessage();
                }
                else
                {
                    this.ReqestStartXfer();
                }
            }

            protected void ReqestStartXfer()
            {
                this.UploadComplete = false;
                this.XferID = Util.GetNextXferID();
                RequestXferPacket newPack = new RequestXferPacket();
                newPack.XferID.ID = this.XferID;
                newPack.XferID.VFileType = this.Asset.Type;
                newPack.XferID.VFileID = this.Asset.FullID;
                newPack.XferID.FilePath = 0;
                newPack.XferID.Filename = new byte[0];
                this.ourClient.OutPacket(newPack);
            }

            protected void SendCompleteMessage()
            {
                this.UploadComplete = true;
                AssetUploadCompletePacket newPack = new AssetUploadCompletePacket();
                newPack.AssetBlock.Type = this.Asset.Type;
                newPack.AssetBlock.Success = true;
                newPack.AssetBlock.UUID = this.Asset.FullID;
                this.ourClient.OutPacket(newPack);
            }
        }

        public class NoteCardCapsUpdate
        {
            // Fields
            private BaseHttpServer httpListener;
            private LLUUID inventoryItemID;
            private string m_assetName = "";
            private LLUUID newAssetID;
            private bool SaveImages = false;
            private string uploaderPath = "";

            // Events
            public event UpLoadedTexture OnUpLoad;

            // Methods
            public void Initialise(LLUUID inventoryItem, string path, BaseHttpServer httpServer)
            {
                this.inventoryItemID = inventoryItem;
                this.uploaderPath = path;
                this.httpListener = httpServer;
                this.newAssetID = LLUUID.Random();
            }

            private void SaveImageToFile(string filename, byte[] data)
            {
                FileStream output = File.Create(filename);
                BinaryWriter writer = new BinaryWriter(output);
                writer.Write(data);
                writer.Close();
                output.Close();
            }

            public string uploaderCaps(byte[] data, string path, string param)
            {
                LLUUID inventoryItemID = this.inventoryItemID;
                string text = "";
                LLSDAssetUploadComplete complete = new LLSDAssetUploadComplete();
                complete.new_asset = this.newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                this.httpListener.RemoveStreamHandler("POST", this.uploaderPath);
                if (this.SaveImages)
                {
                    this.SaveImageToFile(this.m_assetName + "notecard.txt", data);
                }
                if (this.OnUpLoad != null)
                {
                    this.OnUpLoad(this.m_assetName, "description", this.newAssetID, inventoryItemID, LLUUID.Zero, data);
                }
                return text;
            }
        }
    }
}