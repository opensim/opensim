/*
 * Copyright (c) Contributors, http://opensimulator.org/
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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public class EntityList
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // we are intentionally using non generics here as testing has
        // shown synchronized collections are faster than manually
        // locked generics.

        private Hashtable m_obj_by_uuid;
        private Hashtable m_obj_by_local;

        private Hashtable m_pres_by_uuid;

        public EntityList()
        {
            m_obj_by_uuid = Hashtable.Synchronized(new Hashtable());
            m_obj_by_local = Hashtable.Synchronized(new Hashtable());
            m_pres_by_uuid = Hashtable.Synchronized(new Hashtable());
        }

        // Interface definition
        //
        // Add(SOG)
        // Add(SP)
        // RemoveObject(SOG)
        // RemovePresence(SP)
        // List()
        // ListObjects()
        // ListPresenes()
        // RemoveAll()
        // FindObject(UUID)
        // FindObject(int)
        // FindPresence(UUID)
        
        public void Add(SceneObjectGroup obj)
        {
            m_obj_by_uuid[obj.UUID] = obj;
            m_obj_by_local[obj.LocalId] = obj.UUID;
        }

        public void Add(ScenePresence pres)
        {
            m_pres_by_uuid[pres.UUID] = pres;
        }

        public SceneObjectGroup RemoveObject(UUID uuid)
        {
            SceneObjectGroup sog = null;
            try 
            {
                sog = (SceneObjectGroup)m_obj_by_uuid[uuid];
                m_obj_by_uuid.Remove(uuid);
                m_obj_by_local.Remove(sog.LocalId);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("RemoveObject failed for {0}", uuid, e);
                sog = null;
            }
            return sog;
        }
        
        public ScenePresence RemovePresence(UUID uuid)
        {
            ScenePresence sp = null;
            try 
            {
                sp = (ScenePresence)m_pres_by_uuid[uuid];
                m_pres_by_uuid.Remove(uuid);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("RemovePresence failed for {0}", uuid, e);
                sp = null;
            }
            return sp;   
        }

        public SceneObjectGroup FindObject(UUID uuid)
        {
            try
            {
                SceneObjectGroup sog = (SceneObjectGroup)m_obj_by_uuid[uuid];
                return sog;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("FindObject failed for {0}", uuid, e);
                return null;
            }
        }

        public SceneObjectGroup FindObject(int local)
        {
            try 
            {
                UUID uuid = (UUID)m_obj_by_local[local];
                SceneObjectGroup sog = (SceneObjectGroup)m_obj_by_uuid[uuid];
                return sog;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("FindObject failed for {0}", local, e);
                return null;
            }
        }

        public ScenePresence FindPresense(UUID uuid)
        {
            try
            {
                ScenePresence sp = (ScenePresence)m_pres_by_uuid[uuid];
                return sp;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
