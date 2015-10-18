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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Console;
using OpenSim.Region.PhysicsModules.SharedBase;
using Mono.Addins;

namespace OpenSim.Region.RegionCombinerModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionCombinerModule")]
    public class RegionCombinerModule : ISharedRegionModule, IRegionCombinerModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
//        private static string LogHeader = "[REGION COMBINER MODULE]";

        public string Name
        {
            get { return "RegionCombinerModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        /// <summary>
        /// Is this module enabled?
        /// </summary>
        private bool m_combineContiguousRegions = false;

        /// <summary>
        /// This holds the root regions for the megaregions.
        /// </summary>
        /// <remarks>
        /// Usually there is only ever one megaregion (and hence only one entry here).
        /// </remarks>
        private Dictionary<UUID, RegionConnections> m_regions = new Dictionary<UUID, RegionConnections>();

        /// <summary>
        /// The scenes that comprise the megaregion.
        /// </summary>
        private Dictionary<UUID, Scene> m_startingScenes = new Dictionary<UUID, Scene>();

        public void Initialise(IConfigSource source)
        {
            IConfig myConfig = source.Configs["Startup"];
            m_combineContiguousRegions = myConfig.GetBoolean("CombineContiguousRegions", false);
            if (m_combineContiguousRegions)
                m_log.ErrorFormat("[REGION COMBINER MODULE]: THIS MODULE IS BEING MARKED OBSOLETE AND MAY SOON BE REMOVED. PLEASE USE VARREGIONS INSTEAD.");

            MainConsole.Instance.Commands.AddCommand(
                "RegionCombinerModule", false, "fix-phantoms", "fix-phantoms",
                "Fixes phantom objects after an import to a megaregion or a change from a megaregion back to normal regions",
                FixPhantoms);
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_combineContiguousRegions)
                scene.RegisterModuleInterface<IRegionCombinerModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_startingScenes)
                m_startingScenes.Remove(scene.RegionInfo.originRegionID);
        }

        public void RegionLoaded(Scene scene)
        {
            lock (m_startingScenes)
                m_startingScenes.Add(scene.RegionInfo.originRegionID, scene);

            if (m_combineContiguousRegions)
            {
                RegionLoadedDoWork(scene);

                scene.EventManager.OnNewPresence += NewPresence;
            }
        }

        public bool IsRootForMegaregion(UUID regionId)
        {
            lock (m_regions)
                return m_regions.ContainsKey(regionId);
        }

        public Vector2 GetSizeOfMegaregion(UUID regionId)
        {
            lock (m_regions)
            {
                if (m_regions.ContainsKey(regionId))
                {
                    RegionConnections rootConn = m_regions[regionId];

                    return new Vector2((float)rootConn.XEnd, (float)rootConn.YEnd);
                }
            }

            throw new Exception(string.Format("Region with id {0} not found", regionId));
        }

        // Test to see if this postiion (relative to the region) is within the area covered
        //    by this megaregion.
        public bool PositionIsInMegaregion(UUID currentRegion, int xx, int yy)
        {
            bool ret = false;
            if (xx < 0 || yy < 0)
                return ret;

            foreach (RegionConnections rootRegion in m_regions.Values)
            {
                if (currentRegion == rootRegion.RegionId)
                {
                    // The caller is in the root region so this is an easy test
                    if (xx < rootRegion.XEnd && yy < rootRegion.YEnd)
                    {
                        ret = true;
                    }
                    break;
                }
                else
                {
                    // Maybe the caller is in one of the sub-regions
                    foreach (RegionData childRegion in rootRegion.ConnectedRegions)
                    {
                        if (currentRegion == childRegion.RegionId)
                        {
                            // This is a child. Diddle the offsets and check if in
                            Vector3 positionInMegaregion = childRegion.Offset;
                            positionInMegaregion.X += xx;
                            positionInMegaregion.Y += yy;
                            if (positionInMegaregion.X < rootRegion.XEnd && positionInMegaregion.Y < rootRegion.YEnd)
                            {
                                ret = true;
                            }
                            break;
                        }
                    }
                }
            }

            return ret;
        }

        private void NewPresence(ScenePresence presence)
        {
            if (presence.IsChildAgent)
            {
                byte[] throttleData;

                try
                {
                    throttleData = presence.ControllingClient.GetThrottlesPacked(1);
                } 
                catch (NotImplementedException)
                {
                    return;
                }

                if (throttleData == null)
                    return;

                if (throttleData.Length == 0)
                    return;

                if (throttleData.Length != 28)
                    return;

                byte[] adjData;
                int pos = 0;

                if (!BitConverter.IsLittleEndian)
                {
                    byte[] newData = new byte[7 * 4];
                    Buffer.BlockCopy(throttleData, 0, newData, 0, 7 * 4);

                    for (int i = 0; i < 7; i++)
                        Array.Reverse(newData, i * 4, 4);

                    adjData = newData;
                }
                else
                {
                    adjData = throttleData;
                }

                // 0.125f converts from bits to bytes
                int resend = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int land = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int wind = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int cloud = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int task = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int texture = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
                int asset = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f);
                // State is a subcategory of task that we allocate a percentage to


                //int total = resend + land + wind + cloud + task + texture + asset;

                byte[] data = new byte[7 * 4];
                int ii = 0;

                Buffer.BlockCopy(Utils.FloatToBytes(resend), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(land * 50), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(wind), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(cloud), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(task), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(texture), 0, data, ii, 4); ii += 4;
                Buffer.BlockCopy(Utils.FloatToBytes(asset), 0, data, ii, 4);

                try
                {
                    presence.ControllingClient.SetChildAgentThrottle(data);
                }
                catch (NotImplementedException)
                {
                    return;
                }
            }
        }

        private void RegionLoadedDoWork(Scene scene)
        {
/* 
            // For testing on a single instance
            if (scene.RegionInfo.RegionLocX == 1004 && scene.RegionInfo.RegionLocY == 1000)
                return;
            // 
*/

            RegionConnections newConn = new RegionConnections();
            newConn.ConnectedRegions = new List<RegionData>();
            newConn.RegionScene = scene;
            newConn.RegionLandChannel = scene.LandChannel;
            newConn.RegionId = scene.RegionInfo.originRegionID;
            newConn.X = scene.RegionInfo.RegionLocX;
            newConn.Y = scene.RegionInfo.RegionLocY;
            newConn.XEnd = scene.RegionInfo.RegionSizeX;
            newConn.YEnd = scene.RegionInfo.RegionSizeX;

            lock (m_regions)
            {
                bool connectedYN = false;

                foreach (RegionConnections rootConn in m_regions.Values)
                {
                    #region commented
                    /*
                    // If we're one region over +x +y
                    //xxy
                    //xxx
                    //xxx
                    if ((((int)conn.X * (int)Constants.RegionSize) + conn.XEnd 
                        == (regionConnections.X * (int)Constants.RegionSize)) 
                        && (((int)conn.Y * (int)Constants.RegionSize) - conn.YEnd 
                        == (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int) Constants.RegionSize)) -
                                    ((conn.X * (int) Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int) Constants.RegionSize)) -
                                    ((conn.Y * (int) Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd + conn.YEnd;
                        extents.X = conn.XEnd + conn.XEnd;

                        m_log.DebugFormat("Scene: {0} to the northwest of Scene{1}.  Offset: {2}.  Extents:{3}",
                                          conn.RegionScene.RegionInfo.RegionName,
                                          regionConnections.RegionScene.RegionInfo.RegionName,
                                          offset, extents);

                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, extents);
                            
                        connectedYN = true;
                        break;
                    }
                    */

                    /*
                    //If we're one region over x +y
                    //xxx
                    //xxx
                    //xyx
                    if ((((int)conn.X * (int)Constants.RegionSize)
                        == (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize) - conn.YEnd
                        == (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd + conn.YEnd;
                        extents.X = conn.XEnd;

                        m_log.DebugFormat("Scene: {0} to the north of Scene{1}.  Offset: {2}. Extents:{3}",
                                          conn.RegionScene.RegionInfo.RegionName,
                                          regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);

                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, extents);
                        connectedYN = true;
                        break;
                    }
                    */

                    /*
                    // If we're one region over -x +y
                    //xxx
                    //xxx
                    //yxx
                    if ((((int)conn.X * (int)Constants.RegionSize) - conn.XEnd
                        == (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize) - conn.YEnd
                        == (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd + conn.YEnd;
                        extents.X = conn.XEnd + conn.XEnd;

                        m_log.DebugFormat("Scene: {0} to the northeast of Scene.  Offset: {2}. Extents:{3}",
                                          conn.RegionScene.RegionInfo.RegionName,
                                          regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);

                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, extents);


                        connectedYN = true;
                        break;
                    }
                    */

                    /*
                    // If we're one region over -x y
                    //xxx
                    //yxx
                    //xxx
                    if ((((int)conn.X * (int)Constants.RegionSize) - conn.XEnd
                        == (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize)
                        == (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd;
                        extents.X = conn.XEnd + conn.XEnd;

                        m_log.DebugFormat("Scene: {0} to the east of Scene{1} Offset: {2}. Extents:{3}",
                                          conn.RegionScene.RegionInfo.RegionName,
                                          regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);

                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, extents);

                        connectedYN = true;
                        break;
                    }
                    */

                    /*
                        // If we're one region over -x -y
                        //yxx
                        //xxx
                        //xxx
                        if ((((int)conn.X * (int)Constants.RegionSize) - conn.XEnd
                            == (regionConnections.X * (int)Constants.RegionSize))
                            && (((int)conn.Y * (int)Constants.RegionSize) + conn.YEnd
                            == (regionConnections.Y * (int)Constants.RegionSize)))
                        {
                            Vector3 offset = Vector3.Zero;
                            offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                        ((conn.X * (int)Constants.RegionSize)));
                            offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                        ((conn.Y * (int)Constants.RegionSize)));

                            Vector3 extents = Vector3.Zero;
                            extents.Y = regionConnections.YEnd + conn.YEnd;
                            extents.X = conn.XEnd + conn.XEnd;

                            m_log.DebugFormat("Scene: {0} to the northeast of Scene{1} Offset: {2}. Extents:{3}",
                                              conn.RegionScene.RegionInfo.RegionName,
                                              regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);

                            scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, extents);

                            connectedYN = true;
                            break;
                        }
                        */
                    #endregion


                    // Check to see if this new region is adjacent to the root region.
                    // Note that we expect the regions to be combined from the root region outward
                    //    thus the requirement for the ordering in the configuration files.

                    // If we're one region over +x y (i.e. root region is to the west)
                    //xxx
                    //xxy
                    //xxx
                    if (rootConn.PosX + rootConn.XEnd >= newConn.PosX && rootConn.PosY >= newConn.PosY)
                    {
                        connectedYN = DoWorkForOneRegionOverPlusXY(rootConn, newConn, scene);
                        break;
                    }

                    // If we're one region over x +y (i.e. root region is to the south)
                    //xyx
                    //xxx
                    //xxx
                    if (rootConn.PosX >= newConn.PosX && rootConn.PosY + rootConn.YEnd >= newConn.PosY)
                    {
                        connectedYN = DoWorkForOneRegionOverPlusXY(rootConn, newConn, scene);
                        break;
                    }

                    // If we're one region over +x +y (i.e. root region is to the south-west)
                    //xxy
                    //xxx
                    //xxx
                    if (rootConn.PosX + rootConn.XEnd >= newConn.PosX && rootConn.PosY + rootConn.YEnd >= newConn.PosY)
                    {
                        connectedYN = DoWorkForOneRegionOverPlusXY(rootConn, newConn, scene);
                        break;
                    }
                }

                // If !connectYN means that this region is a root region
                if (!connectedYN)
                {
                    DoWorkForRootRegion(newConn, scene);
                }
            }
        }

        private bool DoWorkForOneRegionOverPlusXY(RegionConnections rootConn, RegionConnections newConn, Scene scene)
        {
            // Offset (in meters) from the base of this region to the base of the root region.
            Vector3 offset = Vector3.Zero;
            offset.X = newConn.PosX - rootConn.PosX;
            offset.Y = newConn.PosY - rootConn.PosY;

            // The new total size of the region (in meters)
            // We just extend the X and Y dimensions so the extent might temporarily include areas without regions.
            Vector3 extents = Vector3.Zero;
            extents.X = Math.Max(rootConn.XEnd, offset.X + newConn.RegionScene.RegionInfo.RegionSizeX);
            extents.Y = Math.Max(rootConn.YEnd, offset.Y + newConn.RegionScene.RegionInfo.RegionSizeY);

            rootConn.UpdateExtents(extents);

            m_log.DebugFormat(
                "[REGION COMBINER MODULE]: Root region {0} is to the west of region {1}, Offset: {2}, Extents: {3}",
                rootConn.RegionScene.RegionInfo.RegionName,
                newConn.RegionScene.RegionInfo.RegionName, offset, extents);

            RegionData ConnectedRegion = new RegionData();
            ConnectedRegion.Offset = offset;
            ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
            ConnectedRegion.RegionScene = scene;
            rootConn.ConnectedRegions.Add(ConnectedRegion);

            // Inform root region Physics about the extents of this region
            rootConn.RegionScene.PhysicsScene.Combine(null, Vector3.Zero, extents);

            // Inform Child region that it needs to forward it's terrain to the root region
            scene.PhysicsScene.Combine(rootConn.RegionScene.PhysicsScene, offset, Vector3.Zero);

            // Reset Terrain..  since terrain loads before we get here, we need to load 
            // it again so it loads in the root region
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());

            // Create a client event forwarder and add this region's events to the root region.
            if (rootConn.ClientEventForwarder != null)
                rootConn.ClientEventForwarder.AddSceneToEventForwarding(scene);

            return true;
        }

        /*
         * 20140215 radams1: The border stuff was removed and the addition of regions to the mega-regions
         *   was generalized. These functions are not needed for the generalized solution but left for reference.
        private bool DoWorkForOneRegionOverXPlusY(RegionConnections rootConn, RegionConnections newConn, Scene scene)
        {
            Vector3 offset = Vector3.Zero;
            offset.X = newConn.PosX - rootConn.PosX;
            offset.Y = newConn.PosY - rootConn.PosY;

            Vector3 extents = Vector3.Zero;
            extents.Y = newConn.YEnd + rootConn.YEnd;
            extents.X = rootConn.XEnd;
            rootConn.UpdateExtents(extents);

            RegionData ConnectedRegion = new RegionData();
            ConnectedRegion.Offset = offset;
            ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
            ConnectedRegion.RegionScene = scene;
            rootConn.ConnectedRegions.Add(ConnectedRegion);

            m_log.DebugFormat(
                "[REGION COMBINER MODULE]: Root region {0} is to the south of region {1}, Offset: {2}, Extents: {3}",
                rootConn.RegionScene.RegionInfo.RegionName,
                newConn.RegionScene.RegionInfo.RegionName, offset, extents);

            rootConn.RegionScene.PhysicsScene.Combine(null, Vector3.Zero, extents);
            scene.PhysicsScene.Combine(rootConn.RegionScene.PhysicsScene, offset, Vector3.Zero);

            // Reset Terrain..  since terrain normally loads first.
            //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());

            if (rootConn.ClientEventForwarder != null)
                rootConn.ClientEventForwarder.AddSceneToEventForwarding(scene);

            return true;
        }

        private bool DoWorkForOneRegionOverPlusXPlusY(RegionConnections rootConn, RegionConnections newConn, Scene scene)
        {
            Vector3 offset = Vector3.Zero;
            offset.X = newConn.PosX - rootConn.PosX;
            offset.Y = newConn.PosY - rootConn.PosY;

            Vector3 extents = Vector3.Zero;

            // We do not want to inflate the extents for regions strictly to the NE of the root region, since this
            // would double count regions strictly to the north and east that have already been added.
//            extents.Y = regionConnections.YEnd + conn.YEnd;
//            extents.X = regionConnections.XEnd + conn.XEnd;
//            conn.UpdateExtents(extents);

            extents.Y = rootConn.YEnd;
            extents.X = rootConn.XEnd;

            RegionData ConnectedRegion = new RegionData();
            ConnectedRegion.Offset = offset;
            ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
            ConnectedRegion.RegionScene = scene;

            rootConn.ConnectedRegions.Add(ConnectedRegion);

            m_log.DebugFormat(
                "[REGION COMBINER MODULE]: Region {0} is to the southwest of Scene {1}, Offset: {2}, Extents: {3}",
                rootConn.RegionScene.RegionInfo.RegionName,
                newConn.RegionScene.RegionInfo.RegionName, offset, extents);

            rootConn.RegionScene.PhysicsScene.Combine(null, Vector3.Zero, extents);
            scene.PhysicsScene.Combine(rootConn.RegionScene.PhysicsScene, offset, Vector3.Zero);

            // Reset Terrain..  since terrain normally loads first.
            //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());

            if (rootConn.ClientEventForwarder != null)
                rootConn.ClientEventForwarder.AddSceneToEventForwarding(scene);

            return true;

            //scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset,extents);
        }
         */

        private void DoWorkForRootRegion(RegionConnections rootConn, Scene scene)
        {
            m_log.DebugFormat("[REGION COMBINER MODULE]: Adding root region {0}", scene.RegionInfo.RegionName);

            RegionData rdata = new RegionData();
            rdata.Offset = Vector3.Zero;
            rdata.RegionId = scene.RegionInfo.originRegionID;
            rdata.RegionScene = scene;
            // save it's land channel
            rootConn.RegionLandChannel = scene.LandChannel;

            // Substitue our landchannel
            RegionCombinerLargeLandChannel lnd = new RegionCombinerLargeLandChannel(rdata, scene.LandChannel,
                                                            rootConn.ConnectedRegions);

            scene.LandChannel = lnd;

            // Forward the permissions modules of each of the connected regions to the root region
            lock (m_regions)
            {
                foreach (RegionData r in rootConn.ConnectedRegions)
                {
                    ForwardPermissionRequests(rootConn, r.RegionScene);
                }

                // Create the root region's Client Event Forwarder
                rootConn.ClientEventForwarder = new RegionCombinerClientEventForwarder(rootConn);
    
                // Sets up the CoarseLocationUpdate forwarder for this root region
                scene.EventManager.OnNewPresence += SetCoarseLocationDelegate;
    
                // Adds this root region to a dictionary of regions that are connectable
                m_regions.Add(scene.RegionInfo.originRegionID, rootConn);
            }
        }

        private void SetCoarseLocationDelegate(ScenePresence presence)
        {
            presence.SetSendCoarseLocationMethod(SendCoarseLocationUpdates);
        }

        // This delegate was refactored for non-combined regions.
        // This combined region version will not use the pre-compiled lists of locations and ids
        private void SendCoarseLocationUpdates(UUID sceneId, ScenePresence presence, List<Vector3> coarseLocations, List<UUID> avatarUUIDs)
        {
            RegionConnections connectiondata = null; 
            lock (m_regions)
            {
                if (m_regions.ContainsKey(sceneId))
                    connectiondata = m_regions[sceneId];
                else
                    return;
            }

            List<Vector3> CoarseLocations = new List<Vector3>();
            List<UUID> AvatarUUIDs = new List<UUID>();

            connectiondata.RegionScene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                if (sp.UUID != presence.UUID)
                {
                    CoarseLocations.Add(sp.AbsolutePosition);
                    AvatarUUIDs.Add(sp.UUID);
                }
            });

            DistributeCoarseLocationUpdates(CoarseLocations, AvatarUUIDs, connectiondata, presence);
        }

        private void DistributeCoarseLocationUpdates(List<Vector3> locations, List<UUID> uuids, 
                                                     RegionConnections connectiondata, ScenePresence rootPresence)
        {
            RegionData[] rdata = connectiondata.ConnectedRegions.ToArray();
            //List<IClientAPI> clients = new List<IClientAPI>();
            Dictionary<Vector2, RegionCoarseLocationStruct> updates = new Dictionary<Vector2, RegionCoarseLocationStruct>();
            
            // Root Region entry
            RegionCoarseLocationStruct rootupdatedata = new RegionCoarseLocationStruct();
            rootupdatedata.Locations = new List<Vector3>();
            rootupdatedata.Uuids = new List<UUID>();
            rootupdatedata.Offset = Vector2.Zero;

            rootupdatedata.UserAPI = rootPresence.ControllingClient;

            if (rootupdatedata.UserAPI != null)
                updates.Add(Vector2.Zero, rootupdatedata);

            //Each Region needs an entry or we will end up with dead minimap dots
            foreach (RegionData regiondata in rdata)
            {
                Vector2 offset = new Vector2(regiondata.Offset.X, regiondata.Offset.Y);
                RegionCoarseLocationStruct updatedata = new RegionCoarseLocationStruct();
                updatedata.Locations = new List<Vector3>();
                updatedata.Uuids = new List<UUID>();
                updatedata.Offset = offset;

                if (offset == Vector2.Zero)
                    updatedata.UserAPI = rootPresence.ControllingClient;
                else
                    updatedata.UserAPI = LocateUsersChildAgentIClientAPI(offset, rootPresence.UUID, rdata);

                if (updatedata.UserAPI != null)
                    updates.Add(offset, updatedata);
            }

            // go over the locations and assign them to an IClientAPI
            for (int i = 0; i < locations.Count; i++)
            //{locations[i]/(int) Constants.RegionSize;
            {
                Vector3 pPosition = new Vector3((int)locations[i].X / (int)Constants.RegionSize, 
                                                (int)locations[i].Y / (int)Constants.RegionSize, locations[i].Z);
                Vector2 offset = new Vector2(pPosition.X*(int) Constants.RegionSize,
                                             pPosition.Y*(int) Constants.RegionSize);
                
                if (!updates.ContainsKey(offset))
                {
                    // This shouldn't happen
                    RegionCoarseLocationStruct updatedata = new RegionCoarseLocationStruct();
                    updatedata.Locations = new List<Vector3>();
                    updatedata.Uuids = new List<UUID>();
                    updatedata.Offset = offset;
                    
                    if (offset == Vector2.Zero)
                        updatedata.UserAPI = rootPresence.ControllingClient;
                    else 
                        updatedata.UserAPI = LocateUsersChildAgentIClientAPI(offset, rootPresence.UUID, rdata);

                    updates.Add(offset,updatedata);
                }
                
                updates[offset].Locations.Add(locations[i]);
                updates[offset].Uuids.Add(uuids[i]);
            }

            // Send out the CoarseLocationupdates from their respective client connection based on where the avatar is
            foreach (Vector2 offset in updates.Keys)
            {
                if (updates[offset].UserAPI != null)
                {
                    updates[offset].UserAPI.SendCoarseLocationUpdate(updates[offset].Uuids,updates[offset].Locations);
                }
            }
        }

        /// <summary>
        /// Locates a the Client of a particular region in an Array of RegionData based on offset
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="uUID"></param>
        /// <param name="rdata"></param>
        /// <returns>IClientAPI or null</returns>
        private IClientAPI LocateUsersChildAgentIClientAPI(Vector2 offset, UUID uUID, RegionData[] rdata)
        {
            IClientAPI returnclient = null;
            foreach (RegionData r in rdata)
            {
                if (r.Offset.X == offset.X && r.Offset.Y == offset.Y)
                {
                    return r.RegionScene.SceneGraph.GetControllingClient(uUID);
                }
            }

            return returnclient;
        }

        public void PostInitialise()
        {
        }
        
//        /// <summary>
//        /// TODO:
//        /// </summary>
//        /// <param name="rdata"></param>
//        public void UnCombineRegion(RegionData rdata)
//        {
//            lock (m_regions)
//            {
//                if (m_regions.ContainsKey(rdata.RegionId))
//                {
//                    // uncombine root region and virtual regions
//                }
//                else
//                {
//                    foreach (RegionConnections r in m_regions.Values)
//                    {
//                        foreach (RegionData rd in r.ConnectedRegions)
//                        {
//                            if (rd.RegionId == rdata.RegionId)
//                            {
//                                // uncombine virtual region
//                            }
//                        }
//                    }
//                }
//            }
//        }

        public void ForwardPermissionRequests(RegionConnections BigRegion, Scene VirtualRegion)
        {
            if (BigRegion.PermissionModule == null)
                BigRegion.PermissionModule = new RegionCombinerPermissionModule(BigRegion.RegionScene);

            VirtualRegion.Permissions.OnBypassPermissions += BigRegion.PermissionModule.BypassPermissions;
            VirtualRegion.Permissions.OnSetBypassPermissions += BigRegion.PermissionModule.SetBypassPermissions;
            VirtualRegion.Permissions.OnPropagatePermissions += BigRegion.PermissionModule.PropagatePermissions;
            VirtualRegion.Permissions.OnGenerateClientFlags += BigRegion.PermissionModule.GenerateClientFlags;
            VirtualRegion.Permissions.OnAbandonParcel += BigRegion.PermissionModule.CanAbandonParcel;
            VirtualRegion.Permissions.OnReclaimParcel += BigRegion.PermissionModule.CanReclaimParcel;
            VirtualRegion.Permissions.OnDeedParcel += BigRegion.PermissionModule.CanDeedParcel;
            VirtualRegion.Permissions.OnDeedObject += BigRegion.PermissionModule.CanDeedObject;
            VirtualRegion.Permissions.OnIsGod += BigRegion.PermissionModule.IsGod;
            VirtualRegion.Permissions.OnDuplicateObject += BigRegion.PermissionModule.CanDuplicateObject;
            VirtualRegion.Permissions.OnDeleteObject += BigRegion.PermissionModule.CanDeleteObject; //MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnEditObject += BigRegion.PermissionModule.CanEditObject; //MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnEditParcelProperties += BigRegion.PermissionModule.CanEditParcelProperties; //MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnInstantMessage += BigRegion.PermissionModule.CanInstantMessage;
            VirtualRegion.Permissions.OnInventoryTransfer += BigRegion.PermissionModule.CanInventoryTransfer; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnIssueEstateCommand += BigRegion.PermissionModule.CanIssueEstateCommand; //FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnMoveObject += BigRegion.PermissionModule.CanMoveObject; //MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnObjectEntry += BigRegion.PermissionModule.CanObjectEntry;
            VirtualRegion.Permissions.OnReturnObjects += BigRegion.PermissionModule.CanReturnObjects; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnRezObject += BigRegion.PermissionModule.CanRezObject; //MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnRunConsoleCommand += BigRegion.PermissionModule.CanRunConsoleCommand;
            VirtualRegion.Permissions.OnRunScript += BigRegion.PermissionModule.CanRunScript; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnCompileScript += BigRegion.PermissionModule.CanCompileScript;
            VirtualRegion.Permissions.OnSellParcel += BigRegion.PermissionModule.CanSellParcel;
            VirtualRegion.Permissions.OnTakeObject += BigRegion.PermissionModule.CanTakeObject;
            VirtualRegion.Permissions.OnTakeCopyObject += BigRegion.PermissionModule.CanTakeCopyObject;
            VirtualRegion.Permissions.OnTerraformLand += BigRegion.PermissionModule.CanTerraformLand;
            VirtualRegion.Permissions.OnLinkObject += BigRegion.PermissionModule.CanLinkObject; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnDelinkObject += BigRegion.PermissionModule.CanDelinkObject; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnBuyLand += BigRegion.PermissionModule.CanBuyLand; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnViewNotecard += BigRegion.PermissionModule.CanViewNotecard; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnViewScript += BigRegion.PermissionModule.CanViewScript; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnEditNotecard += BigRegion.PermissionModule.CanEditNotecard; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnEditScript += BigRegion.PermissionModule.CanEditScript; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnCreateObjectInventory += BigRegion.PermissionModule.CanCreateObjectInventory; //NOT IMPLEMENTED HERE 
            VirtualRegion.Permissions.OnEditObjectInventory += BigRegion.PermissionModule.CanEditObjectInventory;//MAYBE FULLY IMPLEMENTED
            VirtualRegion.Permissions.OnCopyObjectInventory += BigRegion.PermissionModule.CanCopyObjectInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnDeleteObjectInventory += BigRegion.PermissionModule.CanDeleteObjectInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnResetScript += BigRegion.PermissionModule.CanResetScript;
            VirtualRegion.Permissions.OnCreateUserInventory += BigRegion.PermissionModule.CanCreateUserInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnCopyUserInventory += BigRegion.PermissionModule.CanCopyUserInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnEditUserInventory += BigRegion.PermissionModule.CanEditUserInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnDeleteUserInventory += BigRegion.PermissionModule.CanDeleteUserInventory; //NOT YET IMPLEMENTED
            VirtualRegion.Permissions.OnTeleport += BigRegion.PermissionModule.CanTeleport; //NOT YET IMPLEMENTED
        }

        #region console commands

        public void FixPhantoms(string module, string[] cmdparams)
        {
            List<Scene> scenes = new List<Scene>(m_startingScenes.Values);

            foreach (Scene s in scenes)
            {
                MainConsole.Instance.OutputFormat("Fixing phantoms for {0}", s.RegionInfo.RegionName);
                
                s.ForEachSOG(so => so.AbsolutePosition = so.AbsolutePosition);
            }
        }

        #endregion
    }
}
