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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Authorization
{
    public class AuthorizationService : IAuthorizationService
    {
        private enum AccessFlags
        {
            None = 0,               /* No restrictions */
            DisallowResidents = 1,  /* Only gods and managers*/
            DisallowForeigners = 2, /* Only local people */
        }

        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IUserManagement m_UserManagement;
//        private IGridService m_GridService;

        private Scene m_Scene;
        AccessFlags m_accessValue = AccessFlags.None;


        public AuthorizationService(IConfig config, Scene scene)
        {
            m_Scene = scene;
            m_UserManagement = scene.RequestModuleInterface<IUserManagement>();
//            m_GridService = scene.GridService;

            if (config != null)
            {
                string accessStr = config.GetString("Region_" + scene.RegionInfo.RegionName.Replace(' ', '_'), String.Empty);
                if (accessStr != string.Empty)
                {
                    try
                    {
                        m_accessValue = (AccessFlags)Enum.Parse(typeof(AccessFlags), accessStr);
                    }
                    catch (ArgumentException)
                    {
                        m_log.WarnFormat("[AuthorizationService]: {0} is not a valid access flag", accessStr);
                    }
                }
                m_log.DebugFormat("[AuthorizationService]: Region {0} access restrictions: {1}", m_Scene.RegionInfo.RegionName, m_accessValue);
            }

        }

        public bool IsAuthorizedForRegion(
            string user, string firstName, string lastName, string regionID, out string message)
        {
            // This should not happen
            if (m_Scene.RegionInfo.RegionID.ToString() != regionID)
            {
                m_log.WarnFormat("[AuthorizationService]: Service for region {0} received request to authorize for region {1}",
                    m_Scene.RegionInfo.RegionID, regionID);
                message = string.Format("Region {0} received request to authorize for region {1}", m_Scene.RegionInfo.RegionID, regionID);
                return false;
            }

            if (m_accessValue == AccessFlags.None)
            {
                message = "Authorized";
                return true;
            }

            UUID userID = new UUID(user);

            if ((m_accessValue & AccessFlags.DisallowForeigners) != 0)
            {
                if (!m_UserManagement.IsLocalGridUser(userID))
                {
                    message = "No foreign users allowed in this region";
                    return false;
                }
            }

            if ((m_accessValue & AccessFlags.DisallowResidents) != 0)
            {
                if (!(m_Scene.Permissions.IsGod(userID) || m_Scene.Permissions.IsAdministrator(userID)))
                {
                    message = "Only Admins and Managers allowed in this region";
                    return false;
                }
            }

            message = "Authorized";
            return true;
        }

    }
}