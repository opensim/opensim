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

namespace OpenSim.Region.Framework.Scenes
{
    public class EntityManager : IEnumerable<EntityBase>
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<UUID,EntityBase> m_eb_uuid = new Dictionary<UUID, EntityBase>();
        private readonly Dictionary<uint, EntityBase> m_eb_localID = new Dictionary<uint, EntityBase>();
        //private readonly Dictionary<UUID, ScenePresence> m_pres_uuid = new Dictionary<UUID, ScenePresence>();
        private System.Threading.ReaderWriterLockSlim m_lock = new System.Threading.ReaderWriterLockSlim();

        [Obsolete("Use Add() instead.")]
        public void Add(UUID id, EntityBase eb)
        {
            Add(eb);
        }

        public void Add(EntityBase entity)
        {
            m_lock.EnterWriteLock();
            try
            {
                try
                {
                    m_eb_uuid.Add(entity.UUID, entity);
                    m_eb_localID.Add(entity.LocalId, entity);
                }
                catch(Exception e)
                {
                    m_log.ErrorFormat("Add Entity failed: {0}", e.Message);
                }
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        public void InsertOrReplace(EntityBase entity)
        {
            m_lock.EnterWriteLock();
            try
            {
                try
                {
                    m_eb_uuid[entity.UUID] = entity;
                    m_eb_localID[entity.LocalId] = entity;
                }
                catch(Exception e)
                {
                    m_log.ErrorFormat("Insert or Replace Entity failed: {0}", e.Message);
                }
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            m_lock.EnterWriteLock();
            try
            {
                m_eb_uuid.Clear();
                m_eb_localID.Clear();
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                return m_eb_uuid.Count;
            }
        }

        public bool ContainsKey(UUID id)
        {
            try
            {
                return m_eb_uuid.ContainsKey(id);
            }
            catch
            {
                return false;
            }
        }

        public bool ContainsKey(uint localID)
        {
            try
            {
                return m_eb_localID.ContainsKey(localID);
            }
            catch
            {
                return false;
            }
        }

        public bool Remove(uint localID)
        {
            m_lock.EnterWriteLock();
            try
            {
                try
                {
                    bool a = false;
                    EntityBase entity;
                    if (m_eb_localID.TryGetValue(localID, out entity))
                        a = m_eb_uuid.Remove(entity.UUID);

                    bool b = m_eb_localID.Remove(localID);
                    return a && b;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Remove Entity failed for {0}", localID, e);
                    return false;
                }
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        public bool Remove(UUID id)
        {
            m_lock.EnterWriteLock();
            try
            {
                try 
                {
                    bool a = false;
                    EntityBase entity;
                    if (m_eb_uuid.TryGetValue(id, out entity))
                        a = m_eb_localID.Remove(entity.LocalId);

                    bool b = m_eb_uuid.Remove(id);
                    return a && b;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Remove Entity failed for {0}", id, e);
                    return false;
                }
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        public List<EntityBase> GetAllByType<T>()
        {
            List<EntityBase> tmp = new List<EntityBase>();

            m_lock.EnterReadLock();
            try
            {
                try
                {
                    foreach (KeyValuePair<UUID, EntityBase> pair in m_eb_uuid)
                    {
                        if (pair.Value is T)
                        {
                            tmp.Add(pair.Value);
                        }
                    }
                }
                catch (Exception e) 
                {
                    m_log.ErrorFormat("GetAllByType failed for {0}", e);
                    tmp = null;
                }
            }
            finally
            {
                m_lock.ExitReadLock();
            }

            return tmp;
        }

        public List<EntityBase> GetEntities()
        {
            m_lock.EnterReadLock();
            try
            {
                return new List<EntityBase>(m_eb_uuid.Values);
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        public EntityBase this[UUID id]
        {
            get
            {
                m_lock.EnterReadLock();
                try
                {
                    EntityBase entity;
                    if (m_eb_uuid.TryGetValue(id, out entity))
                        return entity;
                    else
                        return null;
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
            }
            set
            {
                InsertOrReplace(value);
            }
        }

        public EntityBase this[uint localID]
        {
            get
            {
                m_lock.EnterReadLock();
                try
                {
                    EntityBase entity;
                    if (m_eb_localID.TryGetValue(localID, out entity))
                        return entity;
                    else
                        return null;
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
            }
            set
            {
                InsertOrReplace(value);
            }
        }

        public bool TryGetValue(UUID key, out EntityBase obj)
        {
            m_lock.EnterReadLock();
            try
            {
                return m_eb_uuid.TryGetValue(key, out obj);
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        public bool TryGetValue(uint key, out EntityBase obj)
        {
            m_lock.EnterReadLock();
            try
            {
                return m_eb_localID.TryGetValue(key, out obj);
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        /// <summary>
        /// This could be optimised to work on the list 'live' rather than making a safe copy and iterating that.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<EntityBase> GetEnumerator()
        {
            return GetEntities().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }
}
