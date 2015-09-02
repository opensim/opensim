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
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules
{
    /// <summary>
    /// Enables Prim limits for parcel.
    /// </summary>
    /// <remarks>
    /// This module selectivly enables parcel prim limits.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "PrimLimitsModule")]
    public class PrimLimitsModule : INonSharedRegionModule
    {
        protected IDialogModule m_dialogModule;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_enabled;

        public string Name { get { return "PrimLimitsModule"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource config)
        {
            string permissionModules = Util.GetConfigVarFromSections<string>(config, "permissionmodules",
                new string[] { "Startup", "Permissions" }, "DefaultPermissionsModule"); 

            List<string> modules = new List<string>(permissionModules.Split(',').Select(m => m.Trim()));

            if(!modules.Contains("PrimLimitsModule"))
                return;

            m_log.DebugFormat("[PRIM LIMITS]: Initialized module");
            m_enabled = true;
        }
        
        public void Close()
        {
        }
        
        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
            {
                return;
            }
            scene.Permissions.OnRezObject += CanRezObject;
            scene.Permissions.OnObjectEntry += CanObjectEnter;
            scene.Permissions.OnDuplicateObject += CanDuplicateObject;

            m_log.DebugFormat("[PRIM LIMITS]: Region {0} added", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
            if (m_enabled)
            {
                return;
            }

            scene.Permissions.OnRezObject -= CanRezObject;
            scene.Permissions.OnObjectEntry -= CanObjectEnter;
            scene.Permissions.OnDuplicateObject -= CanDuplicateObject;
        }        
        
        public void RegionLoaded(Scene scene)
        {
            m_dialogModule = scene.RequestModuleInterface<IDialogModule>();
        }

        private bool CanRezObject(int objectCount, UUID ownerID, Vector3 objectPosition, Scene scene)
        {
            ILandObject lo = scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);

            string response = DoCommonChecks(objectCount, ownerID, lo, scene);

            if (response != null)
            {
                m_dialogModule.SendAlertToUser(ownerID, response);
                return false;
            }
            return true;
        }

        //OnDuplicateObject
        private bool CanDuplicateObject(int objectCount, UUID objectID, UUID ownerID, Scene scene, Vector3 objectPosition)
        {
            ILandObject lo = scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);

            string response = DoCommonChecks(objectCount, ownerID, lo, scene);

            if (response != null)
            {
                m_dialogModule.SendAlertToUser(ownerID, response);
                return false;
            }
            return true;
        }

        private bool CanObjectEnter(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene)
        {
            if (newPoint.X < -1f || newPoint.X > (scene.RegionInfo.RegionSizeX + 1) ||
                newPoint.Y < -1f || newPoint.Y > (scene.RegionInfo.RegionSizeY) )
                return true;

            SceneObjectPart obj = scene.GetSceneObjectPart(objectID);

            if (obj == null)
                return false;

            // Prim counts are determined by the location of the root prim.  if we're
            // moving a child prim, just let it pass
            if (!obj.IsRoot)
            {
                return true;
            }

            ILandObject newParcel = scene.LandChannel.GetLandObject(newPoint.X, newPoint.Y);

            if (newParcel == null)
                return true;

            Vector3 oldPoint = obj.GroupPosition;
            ILandObject oldParcel = scene.LandChannel.GetLandObject(oldPoint.X, oldPoint.Y);
            
            // The prim hasn't crossed a region boundry so we don't need to worry
            // about prim counts here
            if(oldParcel != null && oldParcel.Equals(newParcel))
            {
                return true;
            }

            int objectCount = obj.ParentGroup.PrimCount;
            int usedPrims = newParcel.PrimCounts.Total;
            int simulatorCapacity = newParcel.GetSimulatorMaxPrimCount();

            // TODO: Add Special Case here for temporary prims

            string response = DoCommonChecks(objectCount, obj.OwnerID, newParcel, scene);

            if (response != null)
            {
                m_dialogModule.SendAlertToUser(obj.OwnerID, response);
                return false;
            }
            return true;
        }

        private string DoCommonChecks(int objectCount, UUID ownerID, ILandObject lo, Scene scene)
        {
            string response = null;

            int simulatorCapacity = lo.GetSimulatorMaxPrimCount();
            if ((objectCount + lo.PrimCounts.Total) > simulatorCapacity)
            {
                response = "Unable to rez object because the parcel is too full";
            }
            else
            {
                int maxPrimsPerUser = scene.RegionInfo.MaxPrimsPerUser;
                if (maxPrimsPerUser >= 0)
                {
                    // per-user prim limit is set
                    if (ownerID != lo.LandData.OwnerID || lo.LandData.IsGroupOwned)
                    {
                        // caller is not the sole Parcel owner
                        EstateSettings estateSettings = scene.RegionInfo.EstateSettings;
                        if (ownerID != estateSettings.EstateOwner)
                        {
                            // caller is NOT the Estate owner
                            List<UUID> mgrs = new List<UUID>(estateSettings.EstateManagers);
                            if (!mgrs.Contains(ownerID))
                            {
                                // caller is not an Estate Manager
                                if ((lo.PrimCounts.Users[ownerID] + objectCount) >  maxPrimsPerUser)
                                {
                                    response = "Unable to rez object because you have reached your limit";
                                }
                            }
                        }
                    }
                }
            }
            return response;
        }
    }
}
