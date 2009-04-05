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

        public SIUser(UUID uuid, string name)
        {
            this.m_uuid = uuid;
            this.m_name = name;
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
