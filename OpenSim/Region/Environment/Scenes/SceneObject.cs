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
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneObject : EntityBase
    {
        private Encoding enc = Encoding.ASCII;
        private Dictionary<LLUUID, Primitive> ChildPrimitives = new Dictionary<LLUUID, Primitive>(); //list of all primitive id's that are part of this group
        public Primitive rootPrimitive;
        private new Scene m_world;
        protected ulong m_regionHandle;

        private bool physicsEnabled = false;
        private PhysicsScene m_PhysScene;
        private PhysicsActor m_PhysActor;

        private EventManager m_eventManager;

        public bool isSelected = false;

        public PhysicsScene PhysScene
        {
            get
            {
                return m_PhysScene;
            }
        }

        public PhysicsActor PhysActor
        {
            get
            {
                return m_PhysActor;
            }
        }

        public LLUUID rootUUID
        {
            get
            {
                this.m_uuid = this.rootPrimitive.m_uuid;
                return this.m_uuid;
            }
        }

        public uint rootLocalID
        {
            get
            {
                this.m_localId = this.rootPrimitive.LocalId;
                return this.LocalId;
            }
        }

        public int primCount
        {
            get
            {
                return this.ChildPrimitives.Count;
            }
        }

        public Dictionary<LLUUID, Primitive> Children
        {
            get
            {
                return this.ChildPrimitives;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObject(Scene world, EventManager eventManager, LLUUID ownerID, uint localID, LLVector3 pos, PrimitiveBaseShape shape)
        {
            m_regionHandle = world.RegionInfo.RegionHandle;
            m_world = world;
            m_eventManager = eventManager;

            this.Pos = pos;
            this.CreateRootFromShape(ownerID, localID, shape, pos);

            registerEvents();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Need a null constructor for duplication</remarks>
        public SceneObject()
        {

        }

        public void registerEvents()
        {
            m_eventManager.OnBackup += new EventManager.OnBackupDelegate(ProcessBackup);
            m_eventManager.OnParcelPrimCountUpdate += new EventManager.OnParcelPrimCountUpdateDelegate(ProcessParcelPrimCountUpdate);
        }

        public void unregisterEvents()
        {
            m_eventManager.OnBackup -= new EventManager.OnBackupDelegate(ProcessBackup);
            m_eventManager.OnParcelPrimCountUpdate -= new EventManager.OnParcelPrimCountUpdateDelegate(ProcessParcelPrimCountUpdate);
        }

        /// <summary>
        /// Processes backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessBackup(OpenSim.Region.Interfaces.IRegionDataStore datastore)
        {
            datastore.StoreObject(this);
        }

        /// <summary>
        /// Sends my primitive info to the land manager for it to keep tally of all of the prims!
        /// </summary>
        private void ProcessParcelPrimCountUpdate()
        {

            m_eventManager.TriggerParcelPrimCountAdd(this);         
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="agentID"></param>
        /// <param name="localID"></param>
        public void CreateRootFromShape(LLUUID agentID, uint localID, PrimitiveBaseShape shape, LLVector3 pos)
        {

            this.rootPrimitive = new Primitive(this.m_regionHandle, this.m_world, agentID, localID, true, this, this, shape, pos);
            this.m_children.Add(rootPrimitive);

            this.ChildPrimitives.Add(this.rootUUID, this.rootPrimitive);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void CreateFromBytes(byte[] data)
        {

        }

        /// <summary>
        /// Makes a copy of this SceneObject (and child primitives)
        /// </summary>
        /// <returns>A complete copy of the object</returns>
        public new SceneObject Copy()
        {
            SceneObject dupe = new SceneObject();

            dupe.m_world = this.m_world;
            dupe.m_eventManager = this.m_eventManager;
            dupe.m_regionHandle = this.m_regionHandle;
            Primitive newRoot = this.rootPrimitive.Copy(dupe, dupe);
            dupe.rootPrimitive = newRoot;

            dupe.m_children.Add(dupe.rootPrimitive);
            dupe.rootPrimitive.Pos = this.Pos;
            dupe.Rotation = this.Rotation;
            dupe.LocalId = m_world.PrimIDAllocate();

            dupe.registerEvents();
            return dupe;
        }

        /// <summary>
        /// 
        /// </summary>
        public void DeleteAllChildren()
        {
            this.m_children.Clear();
            this.ChildPrimitives.Clear();
            this.rootPrimitive = null;
            unregisterEvents();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primObject"></param>
        public void AddNewChildPrims(SceneObject primObject)
        {
            this.rootPrimitive.AddNewChildren(primObject);
        }

        public void AddChildToList(Primitive prim)
        {
            if (!this.ChildPrimitives.ContainsKey(prim.m_uuid))
            {
                this.ChildPrimitives.Add(prim.m_uuid, prim);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        public Primitive HasChildPrim(LLUUID primID)
        {
            if (this.ChildPrimitives.ContainsKey(primID))
            {
                return this.ChildPrimitives[primID];
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public Primitive HasChildPrim(uint localID)
        {
            Primitive returnPrim = null;
            foreach (Primitive prim in this.ChildPrimitives.Values)
            {
                if (prim.LocalId == localID)
                {
                    returnPrim = prim;
                    break;
                }
            }
            return returnPrim;
        }

        public void SendAllChildPrimsToClient(IClientAPI client)
        {
            this.rootPrimitive.SendFullUpdateForAllChildren(client);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void BackUp()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void GrapMovement(LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            this.rootPrimitive.Pos = pos;
            this.rootPrimitive.SendTerseUpdateForAllChildren(remoteClient);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void GetProperites(IClientAPI client)
        {
            ObjectPropertiesPacket proper = new ObjectPropertiesPacket();
            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = LLUUID.Zero;
            proper.ObjectData[0].CreationDate = (ulong)this.rootPrimitive.CreationDate;
            proper.ObjectData[0].CreatorID = this.rootPrimitive.CreatorID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = 0;
            proper.ObjectData[0].LastOwnerID = this.rootPrimitive.LastOwnerID;
            proper.ObjectData[0].ObjectID = this.rootUUID;
            proper.ObjectData[0].OwnerID = this.rootPrimitive.OwnerID;
            proper.ObjectData[0].TouchName = enc.GetBytes(this.rootPrimitive.TouchName + "\0");
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = enc.GetBytes(this.rootPrimitive.SitName + "\0");
            proper.ObjectData[0].Name = enc.GetBytes(this.rootPrimitive.Name + "\0");
            proper.ObjectData[0].Description = enc.GetBytes(this.rootPrimitive.Description + "\0");
            proper.ObjectData[0].OwnerMask = this.rootPrimitive.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = this.rootPrimitive.NextOwnerMask;
            proper.ObjectData[0].GroupMask = this.rootPrimitive.GroupMask;
            proper.ObjectData[0].EveryoneMask = this.rootPrimitive.EveryoneMask;
            proper.ObjectData[0].BaseMask = this.rootPrimitive.BaseMask;

            client.OutPacket(proper);
        }

    }
}
