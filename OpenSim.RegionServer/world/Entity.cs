using System;
using System.Collections.Generic;
using System.Text;
using Axiom.MathLib;
using OpenSim.types;
using libsecondlife;

namespace OpenSim.world
{
    public class Entity
    {
        public libsecondlife.LLUUID uuid;
        public uint localid;
        public LLVector3 position;
        public LLVector3 velocity;
        public Quaternion rotation;
        protected string name;
        protected List<Entity> children;

        public Entity()
        {
            uuid = new libsecondlife.LLUUID();
            localid = 0;
            position = new LLVector3();
            velocity = new LLVector3();
            rotation = new Quaternion();
            name = "(basic entity)";
            children = new List<Entity>();
        }
        public virtual void addForces()
        {
        	foreach (Entity child in children)
            {
                child.addForces();
            }
        }
        public virtual void update() {
            // Do any per-frame updates needed that are applicable to every type of entity
            foreach (Entity child in children)
            {
                child.update();
            }
        }

        public virtual string getName()
        {
            return name;
        }

        public virtual Mesh getMesh()
        {
            Mesh mesh = new Mesh();

            foreach (Entity child in children)
            {
                mesh += child.getMesh();
            }

            return mesh;
        }
        
        public virtual void BackUp()
        {
        	
        }

        public virtual void LandRenegerated()
        {

        }
    }
}
