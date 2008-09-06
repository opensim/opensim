using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AvatarWearingArgs : EventArgs
    {
        private List<Wearable> m_nowWearing = new List<Wearable>();

        /// <summary>
        ///
        /// </summary>
        public List<Wearable> NowWearing
        {
            get { return m_nowWearing; }
            set { m_nowWearing = value; }
        }

        #region Nested type: Wearable

        public class Wearable
        {
            public UUID ItemID = new UUID("00000000-0000-0000-0000-000000000000");
            public byte Type = 0;

            public Wearable(UUID itemId, byte type)
            {
                ItemID = itemId;
                Type = type;
            }
        }

        #endregion
    }
}
