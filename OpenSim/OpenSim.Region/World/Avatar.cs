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

namespace OpenSim.Region
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

        protected RegionInfo m_regionInfo;
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

            m_regionInfo = reginfo;
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Avatar.cs - Loading details from grid (DUMMY)");
            ControllingClient = theClient;
            this.firstname = ControllingClient.FirstName;
            this.lastname = ControllingClient.LastName;
            localid = this.m_world.NextLocalId;
            Pos = ControllingClient.StartPos;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }

            Wearables = AvatarWearable.DefaultWearables;
            
            this.avatarAppearanceTexture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            
            //register for events
            ControllingClient.OnRequestWearables += new GenericCall(this.SendOurAppearance);
            //ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.CompleteMovement);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.SendInitialPosition);
           /* ControllingClient.OnAgentUpdate += new GenericCall3(this.HandleAgentUpdate);
            ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
             */
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
            this.ControllingClient.MoveAgentIntoRegion(m_regionInfo);
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
