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

using NUnit.Framework;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Globalization;
using System.Threading;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class MundaneFrameworkTests : OpenSimTestCase
    {
        private bool m_RegionSettingsOnSaveEventFired;
        private bool m_RegionLightShareDataOnSaveEventFired;


        [Test]
        public void ChildAgentDataUpdate01()
        {
            // code coverage
            ChildAgentDataUpdate cadu = new ChildAgentDataUpdate();
            Assert.IsFalse(cadu.alwaysrun, "Default is false");
        }

        [Test]
        public void AgentPositionTest01()
        {
            UUID AgentId1 = UUID.Random();
            UUID SessionId1 = UUID.Random();
            uint CircuitCode1 = uint.MinValue;
            Vector3 Size1 = Vector3.UnitZ;
            Vector3 Position1 = Vector3.UnitX;
            Vector3 LeftAxis1 = Vector3.UnitY;
            Vector3 UpAxis1 = Vector3.UnitZ;
            Vector3 AtAxis1 = Vector3.UnitX;

            ulong RegionHandle1 = ulong.MinValue;
            byte[] Throttles1 = new byte[] {0, 1, 0};

            Vector3 Velocity1 = Vector3.Zero;
            float Far1 = 256;

            bool ChangedGrid1 = false;
            Vector3 Center1 = Vector3.Zero;

            AgentPosition position1 = new AgentPosition();
            position1.AgentID = AgentId1;
            position1.SessionID = SessionId1;
            position1.CircuitCode = CircuitCode1;
            position1.Size = Size1;
            position1.Position = Position1;
            position1.LeftAxis = LeftAxis1;
            position1.UpAxis = UpAxis1;
            position1.AtAxis = AtAxis1;
            position1.RegionHandle = RegionHandle1;
            position1.Throttles = Throttles1;
            position1.Velocity = Velocity1;
            position1.Far = Far1;
            position1.ChangedGrid = ChangedGrid1;
            position1.Center = Center1;

            ChildAgentDataUpdate cadu = new ChildAgentDataUpdate();
            cadu.AgentID = AgentId1.Guid;
            cadu.ActiveGroupID = UUID.Zero.Guid;
            cadu.throttles = Throttles1;
            cadu.drawdistance = Far1;
            cadu.Position = Position1;
            cadu.Velocity = Velocity1;
            cadu.regionHandle = RegionHandle1;
            cadu.cameraPosition = Center1;
            cadu.AVHeight = Size1.Z;

            AgentPosition position2 = new AgentPosition();
            position2.CopyFrom(cadu, position1.SessionID);

            Assert.IsTrue(
                position2.AgentID == position1.AgentID
                && position2.Size == position1.Size
                && position2.Position == position1.Position
                && position2.Velocity == position1.Velocity
                && position2.Center == position1.Center
                && position2.RegionHandle == position1.RegionHandle
                && position2.Far == position1.Far
               
                ,"Copy From ChildAgentDataUpdate failed");

            position2 = new AgentPosition();

            Assert.IsFalse(position2.AgentID == position1.AgentID, "Test Error, position2 should be a blank uninitialized AgentPosition");
            EntityTransferContext ctx = new EntityTransferContext();
            position2.Unpack(position1.Pack(ctx), null, ctx);

            Assert.IsTrue(position2.AgentID == position1.AgentID, "Agent ID didn't unpack the same way it packed");
            Assert.IsTrue(position2.Position == position1.Position, "Position didn't unpack the same way it packed");
            Assert.IsTrue(position2.Velocity == position1.Velocity, "Velocity didn't unpack the same way it packed");
            Assert.IsTrue(position2.SessionID == position1.SessionID, "SessionID didn't unpack the same way it packed");
            Assert.IsTrue(position2.CircuitCode == position1.CircuitCode, "CircuitCode didn't unpack the same way it packed");
            Assert.IsTrue(position2.LeftAxis == position1.LeftAxis, "LeftAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.UpAxis == position1.UpAxis, "UpAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.AtAxis == position1.AtAxis, "AtAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.RegionHandle == position1.RegionHandle, "RegionHandle didn't unpack the same way it packed");
            Assert.IsTrue(position2.ChangedGrid == position1.ChangedGrid, "ChangedGrid didn't unpack the same way it packed");
            Assert.IsTrue(position2.Center == position1.Center, "Center didn't unpack the same way it packed");
            Assert.IsTrue(position2.Size == position1.Size, "Size didn't unpack the same way it packed");

        }

        [Test]
        public void RegionSettingsTest01()
        {
            RegionSettings settings = new RegionSettings();
            settings.OnSave += RegionSaveFired;
            settings.Save();
            settings.OnSave -= RegionSaveFired;

//            string str = settings.LoadedCreationDate;
//            int dt = settings.LoadedCreationDateTime;
//            string id = settings.LoadedCreationID;
//            string time = settings.LoadedCreationTime;

            Assert.That(m_RegionSettingsOnSaveEventFired, "RegionSettings Save Event didn't Fire");
            
        }
        public void RegionSaveFired(RegionSettings settings)
        {
            m_RegionSettingsOnSaveEventFired = true;
        }
        
        [Test]
        public void InventoryItemBaseConstructorTest01()
        {
            InventoryItemBase b1 = new InventoryItemBase();
            Assert.That(b1.ID == UUID.Zero, "void constructor should create an inventory item with ID = UUID.Zero");
            Assert.That(b1.Owner == UUID.Zero, "void constructor should create an inventory item with Owner = UUID.Zero");

            UUID ItemID = UUID.Random();
            UUID OwnerID = UUID.Random();
            
            InventoryItemBase b2 = new InventoryItemBase(ItemID);
            Assert.That(b2.ID == ItemID, "ID constructor should create an inventory item with ID = ItemID");
            Assert.That(b2.Owner == UUID.Zero, "ID constructor  should create an inventory item with Owner = UUID.Zero");

            InventoryItemBase b3 = new InventoryItemBase(ItemID,OwnerID);
            Assert.That(b3.ID == ItemID, "ID,OwnerID constructor should create an inventory item with ID = ItemID");
            Assert.That(b3.Owner == OwnerID, "ID,OwnerID  constructor  should create an inventory item with Owner = OwnerID");

        }

        [Test]
        public void AssetMetaDataNonNullContentTypeTest01()
        {
            AssetMetadata assetMetadata = new AssetMetadata();
            assetMetadata.ContentType = "image/jp2";
            Assert.That(assetMetadata.Type == (sbyte)AssetType.Texture, "Content type should be AssetType.Texture");
            Assert.That(assetMetadata.ContentType == "image/jp2", "Text of content type should be image/jp2");
            UUID rndID = UUID.Random();
            assetMetadata.ID = rndID.ToString();
            Assert.That(assetMetadata.ID.ToLower() == rndID.ToString().ToLower(), "assetMetadata.ID Setter/Getter not Consistent");
            DateTime fixedTime = DateTime.Now;
            assetMetadata.CreationDate = fixedTime;
        }

        [Test]
        public void RegionLightShareDataCloneSaveTest01()
        {
            RegionLightShareData rlsd = new RegionLightShareData();
            rlsd.OnSave += RegionLightShareDataSaveFired;
            rlsd.Save();
            rlsd.OnSave -= RegionLightShareDataSaveFired;
            Assert.IsTrue(m_RegionLightShareDataOnSaveEventFired, "OnSave Event Never Fired");

            object o = rlsd.Clone();
            RegionLightShareData dupe = (RegionLightShareData) o;
            Assert.IsTrue(rlsd.sceneGamma == dupe.sceneGamma, "Memberwise Clone of RegionLightShareData failed");
        }
        public void RegionLightShareDataSaveFired(RegionLightShareData settings)
        {
            m_RegionLightShareDataOnSaveEventFired = true;
        }

        [Test]
        public void EstateSettingsMundateTests()
        {
            EstateSettings es = new EstateSettings();
            es.AddBan(null);
            UUID bannedUserId = UUID.Random();
            es.AddBan(new EstateBan()
                          {   BannedHostAddress = string.Empty,
                              BannedHostIPMask = string.Empty,
                              BannedHostNameMask = string.Empty,
                              BannedUserID = bannedUserId}
                          );
            Assert.IsTrue(es.IsBanned(bannedUserId, 32), "User Should be banned but is not.");
            Assert.IsFalse(es.IsBanned(UUID.Zero, 32), "User Should not be banned but is.");

            es.RemoveBan(bannedUserId);

            Assert.IsFalse(es.IsBanned(bannedUserId, 32), "User Should not be banned but is.");

            es.AddEstateManager(UUID.Zero);

            es.AddEstateManager(bannedUserId);
            Assert.IsTrue(es.IsEstateManagerOrOwner(bannedUserId), "bannedUserId should be EstateManager but isn't.");

            es.RemoveEstateManager(bannedUserId);
            Assert.IsFalse(es.IsEstateManagerOrOwner(bannedUserId), "bannedUserID is estateManager but shouldn't be");

            Assert.IsFalse(es.HasAccess(bannedUserId), "bannedUserID has access but shouldn't");

            es.AddEstateUser(bannedUserId);

            Assert.IsTrue(es.HasAccess(bannedUserId), "bannedUserID doesn't have access but should");
            es.RemoveEstateUser(bannedUserId);

            es.AddEstateManager(bannedUserId);

            Assert.IsTrue(es.HasAccess(bannedUserId), "bannedUserID doesn't have access but should");

            Assert.That(es.EstateGroups.Length == 0, "No Estate Groups Added..   so the array should be 0 length");

            es.AddEstateGroup(bannedUserId);

            Assert.That(es.EstateGroups.Length == 1, "1 Estate Groups Added..   so the array should be 1 length");

            Assert.That(es.EstateGroups[0] == bannedUserId,"User ID should be in EstateGroups");

        }

        [Test]
        public void InventoryFolderBaseConstructorTest01()
        {
            UUID uuid1 = UUID.Random();
            UUID uuid2 = UUID.Random();

            InventoryFolderBase fld = new InventoryFolderBase(uuid1);
            Assert.That(fld.ID == uuid1, "ID constructor failed to save value in ID field.");

            fld = new InventoryFolderBase(uuid1, uuid2);
            Assert.That(fld.ID == uuid1, "ID,Owner constructor failed to save value in ID field.");
            Assert.That(fld.Owner == uuid2, "ID,Owner constructor failed to save value in ID field.");
        }
        
        [Test]
        public void AsssetBaseConstructorTest01()
        {
            AssetBase abase = new AssetBase();
            Assert.IsNotNull(abase.Metadata, "void constructor of AssetBase should have created a MetaData element but didn't.");
            UUID itemID = UUID.Random();
            UUID creatorID = UUID.Random();
            abase = new AssetBase(itemID.ToString(), "test item", (sbyte) AssetType.Texture, creatorID.ToString());

            Assert.IsNotNull(abase.Metadata, "string,string,sbyte,string constructor of AssetBase should have created a MetaData element but didn't.");
            Assert.That(abase.ID == itemID.ToString(), "string,string,sbyte,string constructor failed to set ID property");
            Assert.That(abase.Metadata.CreatorID == creatorID.ToString(), "string,string,sbyte,string constructor failed to set Creator ID");


            abase = new AssetBase(itemID.ToString(), "test item", -1, creatorID.ToString());
            Assert.IsNotNull(abase.Metadata, "string,string,sbyte,string constructor of AssetBase with unknown type should have created a MetaData element but didn't.");
            Assert.That(abase.Metadata.Type == -1, "Unknown Type passed to string,string,sbyte,string constructor and was a known type when it came out again");

            AssetMetadata metts = new AssetMetadata();
            metts.FullID = itemID;
            metts.ID = string.Empty;
            metts.Name = "test item";
            abase.Metadata = metts;

            Assert.That(abase.ToString() == itemID.ToString(), "ToString is overriden to be fullID.ToString()");
            Assert.That(abase.ID == itemID.ToString(),"ID should be MetaData.FullID.ToString() when string.empty or null is provided to the ID property");
        }

        [Test]
        public void CultureSetCultureTest01()
        {
            CultureInfo ci = new CultureInfo("en-US", false);
            Culture.SetCurrentCulture();
            Assert.That(Thread.CurrentThread.CurrentCulture.Name == ci.Name, "SetCurrentCulture failed to set thread culture to en-US");

        }     
    }
}
