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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;
using OpenMetaverse;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Types
{
    [Serializable]
    public class UpdateQueue : ISerializable
    {
        private Queue<SceneObjectPart> m_queue;

        private List<UUID> m_ids;

        private object m_syncObject = new object();

        public int Count
        {
            get { return m_queue.Count; }
        }

        public UpdateQueue()
        {
            m_queue = new Queue<SceneObjectPart>();
            m_ids = new List<UUID>();
        }

        public void Clear()
        {
            lock (m_syncObject)
            {
                m_ids.Clear();
                m_queue.Clear();
            }
        }

        public void Enqueue(SceneObjectPart part)
        {
            lock (m_syncObject)
            {
                if (!m_ids.Contains(part.UUID))
                {
                    m_ids.Add(part.UUID);
                    m_queue.Enqueue(part);
                }
            }
        }

        public SceneObjectPart Dequeue()
        {
            SceneObjectPart part = null;
            lock (m_syncObject)
            {
                if (m_queue.Count > 0)
                {
                    part = m_queue.Dequeue();
                    m_ids.Remove(part.UUID);
                }
            }

            return part;
        }

        protected UpdateQueue(SerializationInfo info, StreamingContext context)
        {
            //System.Console.WriteLine("UpdateQueue Deserialize BGN");

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            m_queue = (Queue<SceneObjectPart>)info.GetValue("m_queue", typeof(Queue<SceneObjectPart>));
            List<Guid> ids_work = (List<Guid>)info.GetValue("m_ids", typeof(List<Guid>));

            foreach (Guid guid in ids_work)
            {
                m_ids.Add(new UUID(guid));
            }

            //System.Console.WriteLine("UpdateQueue Deserialize END");
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

            List<Guid> ids_work = new List<Guid>();

            foreach (UUID uuid in m_ids)
            {
                ids_work.Add(uuid.Guid);
            }

            info.AddValue("m_queue", m_queue);
            info.AddValue("m_ids", ids_work);
        }
    }
}
