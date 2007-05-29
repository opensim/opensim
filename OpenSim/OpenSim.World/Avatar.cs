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
        //private Dictionary<uint, ClientView> m_clientThreads;
        private string m_regionName;
        private ushort m_regionWaterHeight;
        private bool m_regionTerraform;
        private bool childAvatar = false;
        private RegionInfo regionData;

        public Avatar(IClientAPI TheClient, World world, Dictionary<uint, IClientAPI> clientThreads, RegionInfo regionDat)
        {
            m_world = world;
            // m_clientThreads = clientThreads;
            m_regionName = regionData.RegionName;
            m_regionHandle = regionData.RegionHandle;
            m_regionTerraform = regionData.RegionTerraform;
            m_regionWaterHeight = regionData.RegionWaterHeight;

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Avatar.cs - Loading details from grid (DUMMY)");
            ControllingClient = TheClient;
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

            /*
            //register for events
            ControllingClient.OnRequestWearables += new GenericCall(this.SendOurAppearance);
            ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.CompleteMovement);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.SendInitialPosition);
            ControllingClient.OnAgentUpdate += new GenericCall3(this.HandleAgentUpdate);
            ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
             * */
        }

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

        public void ChildStatusChange(bool status)
        {

        }

        public override void addForces()
        {

        }

        public static void SetupTemplate(string name)
        {

        }

        protected static void SetDefaultPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {



        }

        public void CompleteMovement()
        {

        }

        public void HandleAgentUpdate(Packet pack)
        {
            this.HandleUpdate((AgentUpdatePacket)pack);
        }

        public void HandleUpdate(AgentUpdatePacket pack)
        {

        }

        //really really should be moved somewhere else (RegionInfo.cs ?)
        public void SendRegionHandshake(World regionInfo)
        {

        }

        public static void LoadAnims()
        {

        }

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
