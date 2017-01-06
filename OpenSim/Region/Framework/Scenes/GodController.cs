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
using System.Xml;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Types;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public class GodController
    {
        ScenePresence m_scenePresence;
        Scene m_scene;
        protected bool m_allowGridGods;
        protected bool m_regionOwnerIsGod;
        protected bool m_regionManagerIsGod;
        protected bool m_parcelOwnerIsGod;
        protected bool m_forceGodModeAlwaysOn;
        protected bool m_allowGodActionsWithoutGodMode;

        protected bool m_viewerUiIsGod = false;

        protected int m_userLevel = 0;

        public GodController(Scene scene, ScenePresence sp)
        {
            m_scene = scene;
            m_scenePresence = sp;

            IConfigSource config = scene.Config;

            string[] sections = new string[] { "Startup", "Permissions" };

            // God level is based on UserLevel. Gods will have that
            // level grid-wide. Others may become god locally but grid
            // gods are god everywhere.
            m_allowGridGods =
                    Util.GetConfigVarFromSections<bool>(config,
                    "allow_grid_gods", sections, false);

            // The owner of a region is a god in his region only.
            m_regionOwnerIsGod =
                    Util.GetConfigVarFromSections<bool>(config,
                    "region_owner_is_god", sections, true);

            // Region managers are gods in the regions they manage.
            m_regionManagerIsGod =
                    Util.GetConfigVarFromSections<bool>(config,
                    "region_manager_is_god", sections, false);

            // Parcel owners are gods in their own parcels only.
            m_parcelOwnerIsGod =
                    Util.GetConfigVarFromSections<bool>(config,
                    "parcel_owner_is_god", sections, false);

            // God mode should be turned on in the viewer whenever
            // the user has god rights somewhere. They may choose
            // to turn it off again, though.
            m_forceGodModeAlwaysOn =
                    Util.GetConfigVarFromSections<bool>(config,
                    "automatic_gods", sections, false);

            // The user can execute any and all god functions, as
            // permitted by the viewer UI, without actually "godding
            // up". This is the default state in 0.8.2.
            m_allowGodActionsWithoutGodMode =
                    Util.GetConfigVarFromSections<bool>(config,
                    "implicit_gods", sections, false);

        }

        protected bool CanBeGod()
        {
            bool canBeGod = false;

            if (m_allowGridGods && m_userLevel > 0)
                canBeGod = true;

            if (m_regionOwnerIsGod && m_scene.RegionInfo.EstateSettings.IsEstateOwner(m_scenePresence.UUID))
                canBeGod = true;

            if (m_regionManagerIsGod && m_scene.Permissions.IsEstateManager(m_scenePresence.UUID))
                canBeGod = true;

            if (!canBeGod && m_parcelOwnerIsGod) // Skip expensive check if we're already god!
            {
                Vector3 pos = m_scenePresence.AbsolutePosition;
                ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
                if (parcel != null && parcel.LandData.OwnerID == m_scenePresence.UUID)
                    canBeGod = true;
            }

            return canBeGod;
        }

        protected void SyncViewerState()
        {
            bool canBeGod = CanBeGod();

            bool shoudBeGod = m_forceGodModeAlwaysOn ? canBeGod : (m_viewerUiIsGod && canBeGod);

            int godLevel = m_allowGridGods ? m_userLevel : 200;
            if (!shoudBeGod)
                godLevel = 0;

            if (m_viewerUiIsGod != shoudBeGod)
            {
                m_scenePresence.ControllingClient.SendAdminResponse(UUID.Zero, (uint)godLevel);
                m_viewerUiIsGod = shoudBeGod;
            }
        }

        public bool RequestGodMode(bool god)
        {
            if (!god)
            {
                if (m_viewerUiIsGod)
                    m_scenePresence.ControllingClient.SendAdminResponse(UUID.Zero, 0);

                m_viewerUiIsGod = false;

                return true;
            }

            if (!CanBeGod())
                return false;

            int godLevel = m_allowGridGods ? m_userLevel : 200;

            if (!m_viewerUiIsGod)
                m_scenePresence.ControllingClient.SendAdminResponse(UUID.Zero, (uint)godLevel);

            m_viewerUiIsGod = true;

            return true;
        }

        public OSD State()
        {
            OSDMap godMap = new OSDMap(2);

            godMap.Add("ViewerUiIsGod", OSD.FromBoolean(m_viewerUiIsGod));
            godMap.Add("UserLevel", OSD.FromInteger(m_userLevel));

            return godMap;
        }

        public void SetState(OSD state)
        {
            OSDMap s = (OSDMap)state;

            if (s.ContainsKey("ViewerUiIsGod"))
                m_viewerUiIsGod = s["ViewerUiIsGod"].AsBoolean();

            if (s.ContainsKey("UserLevel"))
                m_userLevel = s["UserLevel"].AsInteger();

            SyncViewerState();
        }

        public int UserLevel
        {
            get { return m_userLevel; }
            set { m_userLevel = UserLevel; }
        }

        public int GodLevel
        {
            get
            {
                int godLevel = m_allowGridGods ? m_userLevel : 200;
                if (!m_viewerUiIsGod)
                    godLevel = 0;

                return godLevel;
            }
        }

        public int EffectiveLevel
        {
            get
            {
                int godLevel = m_allowGridGods ? m_userLevel : 200;
                if (m_viewerUiIsGod)
                    return godLevel;

                if (m_allowGodActionsWithoutGodMode && CanBeGod())
                    return godLevel;

                return 0;
            }
        }
    }
}
