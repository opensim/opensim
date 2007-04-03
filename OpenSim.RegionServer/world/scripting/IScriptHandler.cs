using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Physics.Manager;
using OpenSim.world;
using Primitive=OpenSim.world.Primitive;

namespace OpenSim.RegionServer.world.scripting
{
    public delegate void ScriptEventHandler( IScriptContext context );
    
    public class ScriptHandler : IScriptContext
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

        public ScriptHandler( Script script, Entity entity, World world )
        {
            m_script = script;
            m_entity = entity;
            m_world = world;
        }

        #region IScriptContext Members

        bool IScriptContext.MoveTo(LLVector3 newPos)
        {
            if (m_entity is Primitive)
            {
                Primitive prim = m_entity as Primitive;
                // Of course, we really should have asked the physEngine if this is possible, and if not, returned false.
                prim.UpdatePosition( newPos );
                return true;
            }
            
            return false;
        }

        LLVector3 IScriptContext.GetPos()
        {
            return m_entity.position;
        }

        #endregion
    }

}
