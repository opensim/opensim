using System;
using System.Collections.Generic;
using System.Text;
using Axiom.MathLib;
using OpenSim.Physics.Manager;
using OpenSim.types;
using libsecondlife;
using OpenSim.RegionServer.world.scripting;

namespace OpenSim.world
{
    public abstract class Entity : IScriptReadonlyEntity
    {
        public libsecondlife.LLUUID uuid;
        public uint localid;
        public LLVector3 velocity;
        public Quaternion rotation;
        protected List<Entity> children;
        protected LLVector3 m_pos;
        protected PhysicsActor _physActor;
        protected World m_world;
        protected string m_name;

        /// <summary>
        /// 
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual LLVector3 Pos
        {
            get
            {
                if (this._physActor != null)
                {
                    m_pos.X = _physActor.Position.X;
                    m_pos.Y = _physActor.Position.Y;
                    m_pos.Z = _physActor.Position.Z;
                }

                return m_pos;
            }
            set
            {
                if (this._physActor != null)
                {
                    try
                    {
                        lock (this.m_world.SyncRoot)
                        {

                            this._physActor.Position = new PhysicsVector(value.X, value.Y, value.Z);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                m_pos = value;
            }
        }

        /// <summary>
        /// Creates a new Entity (should not occur on it's own)
        /// </summary>
        public Entity()
        {
            uuid = new libsecondlife.LLUUID();
            localid = 0;
            m_pos = new LLVector3();
            velocity = new LLVector3();
            rotation = new Quaternion();
            m_name = "(basic entity)";
            children = new List<Entity>();
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void addForces()
        {
        	foreach (Entity child in children)
            {
                child.addForces();
            }
        }

        /// <summary>
        /// Performs any updates that need to be done at each frame. This function is overridable from it's children.
        /// </summary>
        public virtual void update() {
            // Do any per-frame updates needed that are applicable to every type of entity
            foreach (Entity child in children)
            {
                child.update();
            }
        }

        /// <summary>
        /// Returns a mesh for this object and any dependents
        /// </summary>
        /// <returns>The mesh of this entity tree</returns>
        public virtual Mesh getMesh()
        {
            Mesh mesh = new Mesh();

            foreach (Entity child in children)
            {
                mesh += child.getMesh();
            }

            return mesh;
        }
        
        /// <summary>
        /// Called at a set interval to inform entities that they should back themsleves up to the DB 
        /// </summary>
        public virtual void BackUp()
        {
        	
        }

        /// <summary>
        /// Infoms the entity that the land (heightmap) has changed
        /// </summary>
        public virtual void LandRenegerated()
        {

        }
    }
}
