using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SecurityCredential : ISecurityCredential
    {
        private readonly ISocialEntity m_owner;
        private readonly Scene m_scene;

        public SecurityCredential(ISocialEntity m_owner)
        {
            this.m_owner = m_owner;
        }

        public ISocialEntity owner
        {
            get { return m_owner; }
        }

        public bool CanEditObject(IObject target)
        {
            return m_scene.Permissions.CanEditObject(target.GlobalID, m_owner.GlobalID);
        }

        public bool CanEditTerrain(int x, int y)
        {
            return m_scene.Permissions.CanTerraformLand(m_owner.GlobalID, new Vector3(x, y, 0));
        }
    }
}
