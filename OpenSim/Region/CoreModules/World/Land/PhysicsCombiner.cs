using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Land
{
    public class PhysicsCombiner : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "PhysicsCombiner";}  }
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public Type ReplacableInterface { get { return null; } }

        private Dictionary<UUID, RegionConnections> m_regions = new Dictionary<UUID, RegionConnections>();
        private bool enabledYN = true;
        public void Initialise(IConfigSource source)
        {
            
        }

        public void Close()
        {
            
        }

        public void AddRegion(Scene scene)
        {
            if (!enabledYN)
                return;

            RegionConnections regionConnections = new RegionConnections();
            regionConnections.ConnectedRegions = new List<RegionData>();
            regionConnections.RegionScene = scene;
            regionConnections.RegionLandChannel = scene.LandChannel;
            regionConnections.RegionId = scene.RegionInfo.originRegionID;
            regionConnections.X = scene.RegionInfo.RegionLocX;
            regionConnections.Y = scene.RegionInfo.RegionLocY;
            regionConnections.XEnd = (int)Constants.RegionSize;
            regionConnections.YEnd = (int)Constants.RegionSize;
            lock (m_regions)
            {
                bool connectedYN = false;

                foreach (RegionConnections conn in m_regions.Values)
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

                    // If we're one region over +x y
                    //xxx
                    //xxy
                    //xxx
                    if ((((int)conn.X * (int)Constants.RegionSize) + conn.XEnd
                        >= (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize)
                        >= (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = conn.YEnd;
                        extents.X = conn.XEnd + regionConnections.XEnd;

                        conn.UpdateExtents(extents);


                        m_log.DebugFormat("Scene: {0} to the west of Scene{1} Offset: {2}. Extents:{3}",
                                          conn.RegionScene.RegionInfo.RegionName,
                                          regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);
                        
                        
                        scene.BordersLocked = true;
                        conn.RegionScene.BordersLocked = true;

                        RegionData ConnectedRegion = new RegionData();
                        ConnectedRegion.Offset = offset;
                        ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
                        ConnectedRegion.RegionScene = scene;
                        conn.ConnectedRegions.Add(ConnectedRegion);

                        conn.RegionScene.PhysicsScene.Combine(null, Vector3.Zero, extents);
                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, Vector3.Zero);

                        lock (conn.RegionScene.EastBorders)
                            conn.RegionScene.EastBorders[0].BorderLine.Z += (int)Constants.RegionSize;

                        lock (conn.RegionScene.NorthBorders)
                            conn.RegionScene.NorthBorders[0].BorderLine.Y += (int)Constants.RegionSize;

                        lock (conn.RegionScene.SouthBorders)
                            conn.RegionScene.SouthBorders[0].BorderLine.Y += (int)Constants.RegionSize;

                        lock (scene.WestBorders)
                            scene.WestBorders[0].BorderLine.Z += (int)Constants.RegionSize; //auto teleport West

                        // Reset Terrain..  since terrain normally loads first.
                        //
                        scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
                        //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
                        
                        conn.RegionScene.BordersLocked = false;
                        scene.BordersLocked = false;
                        connectedYN = true;
                        break;
                    }



                    // If we're one region over x +y
                    //xyx
                    //xxx
                    //xxx
                    if ((((int)conn.X * (int)Constants.RegionSize)
                        >= (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize) + conn.YEnd
                        >= (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd + conn.YEnd;
                        extents.X = conn.XEnd;
                        conn.UpdateExtents(extents);


                        scene.BordersLocked = true;
                        conn.RegionScene.BordersLocked = true;

                        RegionData ConnectedRegion = new RegionData();
                        ConnectedRegion.Offset = offset;
                        ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
                        ConnectedRegion.RegionScene = scene;
                        conn.ConnectedRegions.Add(ConnectedRegion);

                        m_log.DebugFormat("Scene: {0} to the northeast of Scene{1} Offset: {2}. Extents:{3}",
                                         conn.RegionScene.RegionInfo.RegionName,
                                         regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);
                        conn.RegionScene.PhysicsScene.Combine(null,Vector3.Zero,extents);
                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, Vector3.Zero);

                        lock(conn.RegionScene.NorthBorders)
                            conn.RegionScene.NorthBorders[0].BorderLine.Z += (int)Constants.RegionSize;
                        lock(conn.RegionScene.EastBorders)
                            conn.RegionScene.EastBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                        lock(conn.RegionScene.WestBorders)
                            conn.RegionScene.WestBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                        lock(scene.SouthBorders)
                            scene.SouthBorders[0].BorderLine.Z += (int)Constants.RegionSize; //auto teleport south

                        // Reset Terrain..  since terrain normally loads first.
                        //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
                        scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
                        //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());

                        scene.BordersLocked = false;
                        conn.RegionScene.BordersLocked = false;

                        connectedYN = true;
                        break;
                    }

                    // If we're one region over +x +y
                    //xxy
                    //xxx
                    //xxx
                    if ((((int)conn.X * (int)Constants.RegionSize) + conn.YEnd
                        >= (regionConnections.X * (int)Constants.RegionSize))
                        && (((int)conn.Y * (int)Constants.RegionSize) + conn.YEnd
                        >= (regionConnections.Y * (int)Constants.RegionSize)))
                    {
                        Vector3 offset = Vector3.Zero;
                        offset.X = (((regionConnections.X * (int)Constants.RegionSize)) -
                                    ((conn.X * (int)Constants.RegionSize)));
                        offset.Y = (((regionConnections.Y * (int)Constants.RegionSize)) -
                                    ((conn.Y * (int)Constants.RegionSize)));

                        Vector3 extents = Vector3.Zero;
                        extents.Y = regionConnections.YEnd + conn.YEnd;
                        extents.X = regionConnections.XEnd + conn.XEnd;
                        conn.UpdateExtents(extents);

                        scene.BordersLocked = true;
                        conn.RegionScene.BordersLocked = true;

                        RegionData ConnectedRegion = new RegionData();
                        ConnectedRegion.Offset = offset;
                        ConnectedRegion.RegionId = scene.RegionInfo.originRegionID;
                        ConnectedRegion.RegionScene = scene;

                        conn.ConnectedRegions.Add(ConnectedRegion);

                        m_log.DebugFormat("Scene: {0} to the NorthEast of Scene{1} Offset: {2}. Extents:{3}",
                                         conn.RegionScene.RegionInfo.RegionName,
                                         regionConnections.RegionScene.RegionInfo.RegionName, offset, extents);
                        
                        conn.RegionScene.PhysicsScene.Combine(null, Vector3.Zero, extents);
                        scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset, Vector3.Zero);
                        lock(conn.RegionScene.NorthBorders)
                        if (conn.RegionScene.NorthBorders.Count == 1)// &&  2)
                        {
                            //compound border
                            // already locked above
                            conn.RegionScene.NorthBorders[0].BorderLine.Z += (int)Constants.RegionSize;

                            lock(conn.RegionScene.EastBorders)
                                conn.RegionScene.EastBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                            lock(conn.RegionScene.WestBorders)
                                conn.RegionScene.WestBorders[0].BorderLine.Y += (int)Constants.RegionSize;

                            

                        }
                        lock(scene.SouthBorders)
                            scene.SouthBorders[0].BorderLine.Z += (int)Constants.RegionSize; //auto teleport south
                        
                        lock(conn.RegionScene.EastBorders)
                        if (conn.RegionScene.EastBorders.Count == 1)// && conn.RegionScene.EastBorders.Count == 2)
                        {

                            conn.RegionScene.EastBorders[0].BorderLine.Z += (int)Constants.RegionSize;
                            lock(conn.RegionScene.NorthBorders)
                                conn.RegionScene.NorthBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                            lock(conn.RegionScene.SouthBorders)
                                conn.RegionScene.SouthBorders[0].BorderLine.Y += (int)Constants.RegionSize;

                            
                        }

                        lock (scene.WestBorders)
                            scene.WestBorders[0].BorderLine.Z += (int)Constants.RegionSize; //auto teleport West
/*  
                        else
                        {
                            conn.RegionScene.NorthBorders[0].BorderLine.Z += (int)Constants.RegionSize;
                            conn.RegionScene.EastBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                            conn.RegionScene.WestBorders[0].BorderLine.Y += (int)Constants.RegionSize;
                            scene.SouthBorders[0].BorderLine.Z += (int)Constants.RegionSize; //auto teleport south
                        }
*/
                        

                        // Reset Terrain..  since terrain normally loads first.
                        //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
                        scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
                        //conn.RegionScene.PhysicsScene.SetTerrain(conn.RegionScene.Heightmap.GetFloatsSerialised());
                        scene.BordersLocked = false;
                        conn.RegionScene.BordersLocked = false;

                        connectedYN = true;

                        //scene.PhysicsScene.Combine(conn.RegionScene.PhysicsScene, offset,extents);

                        break;
                    }


                }
                if (!connectedYN)
                {
                    RegionData rdata = new RegionData();
                    rdata.Offset = Vector3.Zero;
                    rdata.RegionId = scene.RegionInfo.originRegionID;
                    rdata.RegionScene = scene;
                    regionConnections.RegionLandChannel = scene.LandChannel;

                    LargeLandChannel lnd = new LargeLandChannel(rdata,scene.LandChannel,regionConnections.ConnectedRegions);
                    scene.LandChannel = lnd;
                    
                    m_regions.Add(scene.RegionInfo.originRegionID,regionConnections);
                }
                    
            }
        }

        public void RemoveRegion(Scene scene)
        {
            
        }

        public void RegionLoaded(Scene scene)
        {
            
        }

        public void PostInitialise()
        {
            
        }
        public void OnFrame()
        {
            
        }

       
        public RegionData GetRegionFromPosition(Vector3 pPosition)
        {
            pPosition = pPosition/(int) Constants.RegionSize;
            int OffsetX = (int) pPosition.X;
            int OffsetY = (int) pPosition.Y;
            foreach (RegionConnections regConn in m_regions.Values)
            {
                foreach (RegionData reg in regConn.ConnectedRegions)
                {
                    if (reg.Offset.X == OffsetX && reg.Offset.Y == OffsetY)
                        return reg;
                }
            }
            return new RegionData();
        }
    }
    public class RegionConnections
    {
        public UUID RegionId;
        public Scene RegionScene;
        public ILandChannel RegionLandChannel;
        public uint X;
        public uint Y;
        public int XEnd;
        public int YEnd;
        public List<RegionData> ConnectedRegions;
        public void UpdateExtents(Vector3 extents)
        {
            XEnd = (int)extents.X;
            YEnd = (int)extents.Y;
        }

    }

    public class RegionData
    {
        public UUID RegionId;
        public Scene RegionScene;
        public Vector3 Offset;
        
    }

    public class LargeLandChannel : ILandChannel
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private RegionData RegData;
        private ILandChannel RootRegionLandChannel;
        private readonly List<RegionData> RegionConnections;
        
        #region ILandChannel Members

        public LargeLandChannel(RegionData regData, ILandChannel rootRegionLandChannel,List<RegionData> regionConnections)
        {
            RegData = regData;
            RootRegionLandChannel = rootRegionLandChannel;
            RegionConnections = regionConnections;
        }

        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            m_log.DebugFormat("[LANDPARCELNEARPOINT]: {0}>", position);
            return RootRegionLandChannel.ParcelsNearPoint(position - RegData.Offset);
        }

        public List<ILandObject> AllParcels()
        {

            return RootRegionLandChannel.AllParcels();

        }

        public ILandObject GetLandObject(int x, int y)
        {
            m_log.DebugFormat("[BIGLANDTESTINT]: <{0},{1}>", x, y);

            if (x > 0 && x <= (int)Constants.RegionSize && y > 0 && y <= (int)Constants.RegionSize)
            {
                return RootRegionLandChannel.GetLandObject(x, y);
            }
            else
            {
                int offsetX = (x / (int)Constants.RegionSize);
                int offsetY = (x / (int)Constants.RegionSize);
                offsetX *= (int)Constants.RegionSize;
                offsetY *= (int)Constants.RegionSize;

                foreach (RegionData regionData in RegionConnections)
                {
                    if (regionData.Offset.X == offsetX && regionData.Offset.Y == offsetY)
                    {
                        return regionData.RegionScene.LandChannel.GetLandObject(x - offsetX, y - offsetY);
                    }
                }
                ILandObject obj = new LandObject(UUID.Zero, false, RegData.RegionScene);
                obj.landData.Name = "NO LAND";
                return obj;
            }
        }

        public ILandObject GetLandObject(int localID)
        {
            return RootRegionLandChannel.GetLandObject(localID);
        }

        public ILandObject GetLandObject(float x, float y)
        {
            m_log.DebugFormat("[BIGLANDTESTFLOAT]: <{0},{1}>", x, y);
            
            if (x > 0 && x <= (int)Constants.RegionSize && y > 0 && y <= (int)Constants.RegionSize)
            {
                return RootRegionLandChannel.GetLandObject(x, y);
            }
            else
            {
                int offsetX = (int)(x/(int) Constants.RegionSize);
                int offsetY = (int)(x/(int) Constants.RegionSize);
                offsetX *= (int) Constants.RegionSize;
                offsetY *= (int) Constants.RegionSize;

                foreach (RegionData regionData in RegionConnections)
                {
                    if (regionData.Offset.X == offsetX && regionData.Offset.Y == offsetY)
                    {
                        return regionData.RegionScene.LandChannel.GetLandObject(x - offsetX, y - offsetY);
                    }
                }
                ILandObject obj = new LandObject(UUID.Zero, false, RegData.RegionScene);
                obj.landData.Name = "NO LAND";
                return obj;
            }
        }

        public bool IsLandPrimCountTainted()
        {
            return RootRegionLandChannel.IsLandPrimCountTainted();
        }

        public bool IsForcefulBansAllowed()
        {
            return RootRegionLandChannel.IsForcefulBansAllowed();
        }

        public void UpdateLandObject(int localID, LandData data)
        {
            RootRegionLandChannel.UpdateLandObject(localID, data);
        }

        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            RootRegionLandChannel.ReturnObjectsInParcel(localID, returnType, agentIDs, taskIDs, remoteClient);
        }

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            RootRegionLandChannel.setParcelObjectMaxOverride(overrideDel);
        }

        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            RootRegionLandChannel.setSimulatorObjectMaxOverride(overrideDel);
        }

        public void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            RootRegionLandChannel.SetParcelOtherCleanTime(remoteClient, localID, otherCleanTime);
        }

        #endregion
    }
}
