/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Physics.Manager;
using OpenSim.Region;
using OpenSim.Region.Scenes;
using Avatar=OpenSim.Region.Scenes.Avatar;
using Primitive = OpenSim.Region.Scenes.Primitive;

namespace OpenSim.Region.Scripting
{
    public delegate void ScriptEventHandler(IScriptContext context);

    public class ScriptHandler : IScriptContext, IScriptEntity, IScriptReadonlyEntity
    {
        private Scene m_world;
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

        public ScriptHandler(Script script, Entity entity, Scene world)
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
