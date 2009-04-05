using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SEUser : ISocialEntity
    {
        private readonly UUID m_uuid;
        private readonly string m_name;

        public SEUser(UUID uuid, string name)
        {
            m_uuid = uuid;
            m_name = name;
        }

        public UUID GlobalID
        {
            get { return m_uuid; }
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsUser
        {
            get { return true; }
        }
    }
}
