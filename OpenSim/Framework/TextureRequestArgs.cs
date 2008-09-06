using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class TextureRequestArgs : EventArgs
    {
        private sbyte m_discardLevel;
        private uint m_packetNumber;
        private float m_priority;
        protected UUID m_requestedAssetID;

        public float Priority
        {
            get { return m_priority; }
            set { m_priority = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public uint PacketNumber
        {
            get { return m_packetNumber; }
            set { m_packetNumber = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public sbyte DiscardLevel
        {
            get { return m_discardLevel; }
            set { m_discardLevel = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public UUID RequestedAssetID
        {
            get { return m_requestedAssetID; }
            set { m_requestedAssetID = value; }
        }
    }
}
