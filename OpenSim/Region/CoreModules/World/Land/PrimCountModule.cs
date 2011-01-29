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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Diagnostics;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World.Land
{
    public class PrimCountModule : IPrimCountModule, INonSharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;
        private Dictionary<UUID, PrimCounts> m_PrimCounts =
                new Dictionary<UUID, PrimCounts>();

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_Scene = scene;

            m_Scene.EventManager.OnParcelPrimCountAdd +=
                    OnParcelPrimCountAdd;
            m_Scene.EventManager.OnObjectBeingRemovedFromScene +=
                    OnObjectBeingRemovedFromScene;
            m_Scene.EventManager.OnParcelPrimCountTainted +=
                    OnParcelPrimCountTainted;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {            
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PrimCountModule"; }
        }

        private void OnParcelPrimCountAdd(SceneObjectGroup obj)
        {
        }

        private void OnObjectBeingRemovedFromScene(SceneObjectGroup obj)
        {
        }

        private void OnParcelPrimCountTainted()
        {
        }

        public void TaintPrimCount(ILandObject land)
        {
        }

        public void TaintPrimCount(int x, int y)
        {
        }

        public void TaintPrimCount()
        {
        }

        public IPrimCounts GetPrimCounts(UUID parcelID)
        {
            PrimCounts primCounts;

            lock (m_PrimCounts)
            {
                if (m_PrimCounts.TryGetValue(parcelID, out primCounts))
                    return primCounts;

                primCounts = new PrimCounts(parcelID, this);
                m_PrimCounts[parcelID] = primCounts;
            }
            return primCounts;
        }

        public int GetOwnerCount(UUID parcelID)
        {
            return 0;
        }

        public int GetGroupCount(UUID parcelID)
        {
            return 0;
        }

        public int GetOthersCount(UUID parcelID)
        {
            return 0;
        }

        public int GetSimulatorCount(UUID parcelID)
        {
            return 0;
        }

        public int GetUserCount(UUID parcelID, UUID userID)
        {
            return 0;
        }
    }

    public class PrimCounts : IPrimCounts
    {
        private PrimCountModule m_Parent;
        private UUID m_ParcelID;
        private UserPrimCounts m_UserPrimCounts;

        public PrimCounts (UUID parcelID, PrimCountModule parent)
        {
            m_ParcelID = parcelID;
            m_Parent = parent;

            m_UserPrimCounts = new UserPrimCounts(this);
        }

        public int Owner
        {
            get
            {
                return m_Parent.GetOwnerCount(m_ParcelID);
            }
        }

        public int Group
        {
            get
            {
                return m_Parent.GetGroupCount(m_ParcelID);
            }
        }

        public int Others
        {
            get
            {
                return m_Parent.GetOthersCount(m_ParcelID);
            }
        }

        public int Simulator
        {
            get
            {
                return m_Parent.GetSimulatorCount(m_ParcelID);
            }
        }

        public IUserPrimCounts Users
        {
            get
            {
                return m_UserPrimCounts;
            }
        }

        public int GetUserCount(UUID userID)
        {
            return m_Parent.GetUserCount(m_ParcelID, userID);
        }
    }

    public class UserPrimCounts : IUserPrimCounts
    {
        private PrimCounts m_Parent;

        public UserPrimCounts(PrimCounts parent)
        {
            m_Parent = parent;
        }

        public int this[UUID userID]
        {
            get
            {
                return m_Parent.GetUserCount(userID);
            }
        }
    }
}
