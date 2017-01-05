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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public class EntityManager
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly DoubleDictionaryThreadAbortSafe<UUID, uint, EntityBase> m_entities
            = new DoubleDictionaryThreadAbortSafe<UUID, uint, EntityBase>();

        public int Count
        {
            get { return m_entities.Count; }
        }

        public void Add(EntityBase entity)
        {
            m_entities.Add(entity.UUID, entity.LocalId, entity);
        }

        public void Clear()
        {
            m_entities.Clear();
        }

        public bool ContainsKey(UUID id)
        {
            return m_entities.ContainsKey(id);
        }

        public bool ContainsKey(uint localID)
        {
            return m_entities.ContainsKey(localID);
        }

        public bool Remove(uint localID)
        {
            return m_entities.Remove(localID);
        }

        public bool Remove(UUID id)
        {
            return m_entities.Remove(id);
        }

        public EntityBase[] GetAllByType<T>()
        {
            List<EntityBase> tmp = new List<EntityBase>();

            ForEach(
                delegate(EntityBase entity)
                {
                    if (entity is T)
                        tmp.Add(entity);
                }
            );

            return tmp.ToArray();
        }

        public EntityBase[] GetEntities()
        {
            List<EntityBase> tmp = new List<EntityBase>(m_entities.Count);
            ForEach(delegate(EntityBase entity) { tmp.Add(entity); });
            return tmp.ToArray();
        }

        public void ForEach(Action<EntityBase> action)
        {
            m_entities.ForEach(action);
        }

        public EntityBase Find(Predicate<EntityBase> predicate)
        {
            return m_entities.FindValue(predicate);
        }

        public EntityBase this[UUID id]
        {
            get
            {
                EntityBase entity;
                m_entities.TryGetValue(id, out entity);
                return entity;
            }
            set
            {
                Add(value);
            }
        }

        public EntityBase this[uint localID]
        {
            get
            {
                EntityBase entity;
                m_entities.TryGetValue(localID, out entity);
                return entity;
            }
            set
            {
                Add(value);
            }
        }

        public bool TryGetValue(UUID key, out EntityBase obj)
        {
            return m_entities.TryGetValue(key, out obj);
        }

        public bool TryGetValue(uint key, out EntityBase obj)
        {
            return m_entities.TryGetValue(key, out obj);
        }
    }
}
