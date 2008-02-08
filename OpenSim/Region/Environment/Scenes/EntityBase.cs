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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using Axiom.Math;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes
{
    public abstract class EntityBase
    {
        protected Scene m_scene;
        
        public Scene Scene
        {
            get { return m_scene; }
        }

        public LLUUID m_uuid;

        public virtual LLUUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected string m_name;

        /// <summary>
        /// 
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        protected LLVector3 m_pos;

        /// <summary>
        /// 
        /// </summary>
        public virtual LLVector3 AbsolutePosition
        {
            get { return m_pos; }
            set { m_pos = value; }
        }

        protected LLVector3 m_velocity;
        protected LLVector3 m_rotationalvelocity;

        /// <summary>
        /// 
        /// </summary>
        public virtual LLVector3 Velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }

        protected Quaternion m_rotation = new Quaternion(0, 0, 1, 0);

        public virtual Quaternion Rotation
        {
            get { return m_rotation; }
            set { m_rotation = value; }
        }

        protected uint m_localId;

        public virtual uint LocalId
        {
            get { return m_localId; }
            set { m_localId = value; }
        }

        /// <summary>
        /// Creates a new Entity (should not occur on it's own)
        /// </summary>
        public EntityBase()
        {
            m_uuid = LLUUID.Zero;

            m_pos = new LLVector3();
            m_velocity = new LLVector3();
            Rotation = new Quaternion();
            m_name = "(basic entity)";
            m_rotationalvelocity = new LLVector3(0, 0, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        public abstract void UpdateMovement();

        /// <summary>
        /// Performs any updates that need to be done at each frame.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Copies the entity
        /// </summary>
        /// <returns></returns>
        public virtual EntityBase Copy()
        {
            return (EntityBase) MemberwiseClone();
        }


        public abstract void SetText(string text, Vector3 color, double alpha);
    }

    //Nested Classes
    public class EntityIntersection
    {
        public Vector3 ipoint = new Vector3(0, 0, 0);
        public float normal = 0;
        public bool HitTF = false;
        public SceneObjectPart obj;
        public float distance = 0;

        public EntityIntersection()
        {
        }

        public EntityIntersection(Vector3 _ipoint, float _normal, bool _HitTF)
        {
            ipoint = _ipoint;
            normal = _normal;
            HitTF = _HitTF;
        }
    }
}
