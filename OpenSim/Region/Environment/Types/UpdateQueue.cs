using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;

namespace OpenSim.Region.Environment.Types
{
    public class UpdateQueue
    {
        private Queue<SceneObjectPart> m_queue;

        private List<LLUUID> m_ids;

        public int Count
        {
            get { return m_queue.Count; }
        }

        public UpdateQueue()
        {
            m_queue = new Queue<SceneObjectPart>();
            m_ids = new List<LLUUID>();
        }

        public void Enqueue(SceneObjectPart part)
        {
            lock (m_ids)
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
            if (m_queue.Count > 0)
            {
                part = m_queue.Dequeue();
                lock (m_ids)
                {
                    m_ids.Remove(part.UUID);
                }
            }

            return part;
        }
    
    }
}
