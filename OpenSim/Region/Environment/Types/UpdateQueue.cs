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

        public UpdateQueue()
        {
            m_queue = new Queue<SceneObjectPart>();
            m_ids = new List<LLUUID>();
        }
    }
}
