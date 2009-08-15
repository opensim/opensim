using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SecurityCredential : ISecurityCredential
    {
        private readonly ISocialEntity m_owner;

        public SecurityCredential(ISocialEntity m_owner)
        {
            this.m_owner = m_owner;
        }

        public ISocialEntity owner
        {
            get { return m_owner; }
        }
    }
}
