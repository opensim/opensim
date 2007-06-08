using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using Axiom.MathLib;

namespace OpenSim.world
{
    public partial class Avatar : Entity
    {
        public static bool PhysicsEngineFlying = false;
        public static AvatarAnimations Animations;
        public string firstname;
        public string lastname;
        public IClientAPI ControllingClient;
        public LLUUID current_anim;
        public int anim_seq;
        private static libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock AvatarTemplate;
        private bool updateflag = false;
        private byte movementflag = 0;
        private List<NewForce> forcesList = new List<NewForce>();
        private short _updateCount = 0;
        private Axiom.MathLib.Quaternion bodyRot;
        private LLObject.TextureEntry avatarAppearanceTexture = null;
        private byte[] visualParams;
        private AvatarWearable[] Wearables;
        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
        private ulong m_regionHandle;
        private Dictionary<uint, IClientAPI> m_clientThreads;
        private bool childAvatar = false;

        private RegionInfo m_regInfo;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="theClient"></param>
        /// <param name="world"></param>
        /// <param name="clientThreads"></param>
        /// <param name="regionDat"></param>
        public Avatar(IClientAPI theClient, World world, Dictionary<uint, IClientAPI> clientThreads, RegionInfo reginfo)
        {

            m_world = world;
            m_clientThreads = clientThreads;
            this.uuid = theClient.AgentId;

            m_regInfo = reginfo;
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Avatar.cs - Loading details from grid (DUMMY)");
            ControllingClient = theClient;
            this.firstname = ControllingClient.FirstName;
            this.lastname = ControllingClient.LastName;
            localid = 8880000 + (this.m_world._localNumber++);
            Pos = ControllingClient.StartPos;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }
            Wearables = new AvatarWearable[13]; //should be 13 of these
            for (int i = 0; i < 13; i++)
            {
                Wearables[i] = new AvatarWearable();
            }
            this.Wearables[0].AssetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
            this.Wearables[0].ItemID = LLUUID.Random();

            this.avatarAppearanceTexture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            Console.WriteLine("avatar point 4");
            
            //register for events
            ControllingClient.OnRequestWearables += new GenericCall(this.SendOurAppearance);
            //ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.CompleteMovement);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.SendInitialPosition);
           /* ControllingClient.OnAgentUpdate += new GenericCall3(this.HandleAgentUpdate);
            ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
             * */
        }

        /// <summary>
        /// 
        /// </summary>
        public PhysicsActor PhysActor
        {
            set
            {
                this._physActor = value;
            }
            get
            {
                return _physActor;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        public void ChildStatusChange(bool status)
        {

        }
        
        /// <summary>
        /// 
        /// </summary>
        public override void addForces()
        {

        }

        /// <summary>
        ///  likely to removed very soon
        /// </summary>
        /// <param name="name"></param>
        public static void SetupTemplate(string name)
        {

        }

        /// <summary>
        /// likely to removed very soon
        /// </summary>
        /// <param name="objdata"></param>
        protected static void SetDefaultPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {



        }

        /// <summary>
        /// 
        /// </summary>
        public void CompleteMovement()
        {
            this.ControllingClient.MoveAgentIntoRegion(m_regInfo);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pack"></param>
        public void HandleAgentUpdate(Packet pack)
        {
            this.HandleUpdate((AgentUpdatePacket)pack);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pack"></param>
        public void HandleUpdate(AgentUpdatePacket pack)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public void SendRegionHandshake()
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            RegionHandshakePacket handshake = new RegionHandshakePacket();
            
            handshake.RegionInfo.BillableFactor = m_regInfo.estateSettings.billableFactor;
            handshake.RegionInfo.IsEstateManager = false;
            handshake.RegionInfo.TerrainHeightRange00 = m_regInfo.estateSettings.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = m_regInfo.estateSettings.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = m_regInfo.estateSettings.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = m_regInfo.estateSettings.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = m_regInfo.estateSettings.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = m_regInfo.estateSettings.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = m_regInfo.estateSettings.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = m_regInfo.estateSettings.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = (byte)m_regInfo.estateSettings.simAccess;
            handshake.RegionInfo.WaterHeight = m_regInfo.estateSettings.waterHeight;


            handshake.RegionInfo.RegionFlags = (uint)m_regInfo.estateSettings.regionFlags;

            handshake.RegionInfo.SimName = _enc.GetBytes(m_regInfo.estateSettings.waterHeight + "\0");
            handshake.RegionInfo.SimOwner = m_regInfo.MasterAvatarAssignedUUID;
            handshake.RegionInfo.TerrainBase0 = m_regInfo.estateSettings.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = m_regInfo.estateSettings.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = m_regInfo.estateSettings.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = m_regInfo.estateSettings.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = m_regInfo.estateSettings.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = m_regInfo.estateSettings.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = m_regInfo.estateSettings.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = m_regInfo.estateSettings.terrainDetail3;
            handshake.RegionInfo.CacheID = LLUUID.Random(); //I guess this is for the client to remember an old setting?

            this.ControllingClient.OutPacket(handshake);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void LoadAnims()
        {

        }
        
        /// <summary>
        /// 
        /// </summary>
        public override void LandRenegerated()
        {

        }


        public class NewForce
        {
            public float X;
            public float Y;
            public float Z;

            public NewForce()
            {

            }
        }
    }

}
