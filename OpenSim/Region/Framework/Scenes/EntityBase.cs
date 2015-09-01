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

using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using log4net;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    public abstract class EntityBase : ISceneEntity
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
        /// Signals whether this entity was in a scene but has since been removed from it.
        /// </summary>
        public bool IsDeleted { get; protected internal set; }

        protected Vector3 m_pos;

        /// <summary>
        /// Absolute position of this entity in a scene.
        /// </summary>
        public virtual Vector3 AbsolutePosition
        {
            get { return m_pos; }
            set
            {
                m_pos = value;
            }
        }

        protected Vector3 m_velocity;
        protected Vector3 m_rotationalvelocity;

        /// <summary>
        /// Current velocity of the entity.
        /// </summary>
        public virtual Vector3 Velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }

        protected uint m_localId;

        public virtual uint LocalId
        {
            get { return m_localId; }
            set
            {
                m_localId = value;
//                m_log.DebugFormat("[ENTITY BASE]: Set part {0} to local id {1}", Name, m_localId);
            }
        }

        /// <summary>
        /// Creates a new Entity (should not occur on it's own)
        /// </summary>
        public EntityBase()
        {
            m_name = "(basic entity)";
        }

        /// <summary>
        /// Performs any updates that need to be done at each frame, as opposed to immediately.
        /// These included scheduled updates and updates that occur due to physics processing.
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
