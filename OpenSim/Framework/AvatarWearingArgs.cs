using System;
using System.Collections.Generic;
using libsecondlife;

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
            public LLUUID ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");
            public byte Type = 0;

            public Wearable(LLUUID itemId, byte type)
            {
                ItemID = itemId;
                Type = type;
            }
        }

        #endregion
    }
}