using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Physics.Manager;
using OpenSim.world;
using Avatar=OpenSim.world.Avatar;
using Primitive = OpenSim.world.Primitive;

namespace OpenSim.RegionServer.world.scripting
{
    public delegate void ScriptEventHandler(IScriptContext context);

    public class ScriptHandler : IScriptContext, IScriptEntity, IScriptReadonlyEntity
    {
        private World m_world;
        private Script m_script;
        private Entity m_entity;

        public LLUUID ScriptId
        {
            get
            {
                return m_script.ScriptId;
            }
        }

        public void OnFrame()
        {
            m_script.OnFrame(this);
        }

        public ScriptHandler(Script script, Entity entity, World world)
        {
            m_script = script;
            m_entity = entity;
            m_world = world;
        }

        #region IScriptContext Members

        IScriptEntity IScriptContext.Entity
        {
            get
            {
                return this;
            }
        }

        bool IScriptContext.TryGetRandomAvatar(out IScriptReadonlyEntity avatar)
        {
            foreach (Entity entity in m_world.Entities.Values )
            {
                if( entity is Avatar )
                {
                    avatar = entity;
                    return true;
                }
            }
            
            avatar = null;
            return false;
        }

        #endregion

        #region IScriptEntity and IScriptReadonlyEntity Members

        public string Name
        {
            get
            {
                return m_entity.Name;
            }
        }

        public LLVector3 Pos
        {
            get
            {
                return m_entity.Pos;
            }

            set
            {
                if (m_entity is Primitive)
                {
                    Primitive prim = m_entity as Primitive;
                    // Of course, we really should have asked the physEngine if this is possible, and if not, returned false.
                   // prim.UpdatePosition( value );
                }
            }
        }

        #endregion
    }

}
