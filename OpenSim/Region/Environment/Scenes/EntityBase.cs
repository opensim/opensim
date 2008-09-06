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
using System.Runtime.Serialization;
using System.Security.Permissions;
using OpenMetaverse;

namespace OpenSim.Region.Environment.Scenes
{
    [Serializable]
    public abstract class EntityBase : ISerializable
    {
        /// <summary>
        /// The scene to which this entity belongs
        /// </summary>
        public Scene Scene
        {
            get { return m_scene; }
        }
        protected Scene m_scene;

        protected UUID m_uuid;

        public virtual UUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected string m_name;

        /// <summary>
        /// The name of this entity
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        /// <summary>
        /// Signals whether this group was in a scene but has since been deleted from it.
        /// </summary>
        public bool IsDeleted
        {
            get { return m_isDeleted; }
        }
        protected bool m_isDeleted;

        protected Vector3 m_pos;

        /// <summary>
        ///
        /// </summary>
        public virtual Vector3 AbsolutePosition
        {
            get { return m_pos; }
            set { m_pos = value; }
        }

        protected Vector3 m_velocity;
        protected Vector3 m_rotationalvelocity;

        /// <summary>
        ///
        /// </summary>
        public virtual Vector3 Velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }

        protected Quaternion m_rotation = new Quaternion(0f, 0f, 1f, 0f);

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
            m_uuid = UUID.Zero;

            m_pos = Vector3.Zero;
            m_velocity = Vector3.Zero;
            Rotation = Quaternion.Identity;
            m_name = "(basic entity)";
            m_rotationalvelocity = Vector3.Zero;
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

        protected EntityBase(SerializationInfo info, StreamingContext context)
        {
            //System.Console.WriteLine("EntityBase Deserialize BGN");

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            m_uuid = new UUID((Guid)info.GetValue("m_uuid", typeof(Guid)));
            m_name = (string)info.GetValue("m_name", typeof(string));

            m_pos
                = new Vector3(
                        (float)info.GetValue("m_pos.X", typeof(float)),
                        (float)info.GetValue("m_pos.Y", typeof(float)),
                        (float)info.GetValue("m_pos.Z", typeof(float)));

            m_velocity
                = new Vector3(
                        (float)info.GetValue("m_velocity.X", typeof(float)),
                        (float)info.GetValue("m_velocity.Y", typeof(float)),
                        (float)info.GetValue("m_velocity.Z", typeof(float)));

            m_rotationalvelocity
                = new Vector3(
                        (float)info.GetValue("m_rotationalvelocity.X", typeof(float)),
                        (float)info.GetValue("m_rotationalvelocity.Y", typeof(float)),
                        (float)info.GetValue("m_rotationalvelocity.Z", typeof(float)));

            m_rotation
                = new Quaternion(
                        (float)info.GetValue("m_rotation.X", typeof(float)),
                        (float)info.GetValue("m_rotation.Y", typeof(float)),
                        (float)info.GetValue("m_rotation.Z", typeof(float)),
                        (float)info.GetValue("m_rotation.W", typeof(float)));

            m_localId = (uint)info.GetValue("m_localId", typeof(uint));

            //System.Console.WriteLine("EntityBase Deserialize END");
        }

        [SecurityPermission(SecurityAction.LinkDemand,
            Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(
                        SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("m_uuid", m_uuid.Guid);
            info.AddValue("m_name", m_name);

            // Vector3
            info.AddValue("m_pos.X", m_pos.X);
            info.AddValue("m_pos.Y", m_pos.Y);
            info.AddValue("m_pos.Z", m_pos.Z);

            // Vector3
            info.AddValue("m_velocity.X", m_velocity.X);
            info.AddValue("m_velocity.Y", m_velocity.Y);
            info.AddValue("m_velocity.Z", m_velocity.Z);

            // Vector3
            info.AddValue("m_rotationalvelocity.X", m_rotationalvelocity.X);
            info.AddValue("m_rotationalvelocity.Y", m_rotationalvelocity.Y);
            info.AddValue("m_rotationalvelocity.Z", m_rotationalvelocity.Z);

            // Quaternion
            info.AddValue("m_rotation.X", m_rotation.X);
            info.AddValue("m_rotation.Y", m_rotation.Y);
            info.AddValue("m_rotation.Z", m_rotation.Z);
            info.AddValue("m_rotation.W", m_rotation.W);

            info.AddValue("m_localId", m_localId);
        }
    }

    //Nested Classes
    public class EntityIntersection
    {
        public Vector3 ipoint = new Vector3(0, 0, 0);
        public Vector3 normal = new Vector3(0, 0, 0);
        public Vector3 AAfaceNormal = new Vector3(0, 0, 0);
        public int face = -1;
        public bool HitTF = false;
        public SceneObjectPart obj;
        public float distance = 0;

        public EntityIntersection()
        {
        }

        public EntityIntersection(Vector3 _ipoint, Vector3 _normal, bool _HitTF)
        {
            ipoint = _ipoint;
            normal = _normal;
            HitTF = _HitTF;
        }
    }
}
