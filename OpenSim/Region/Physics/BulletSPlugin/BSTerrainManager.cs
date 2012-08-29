/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Text;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region.Physics.Manager;

using Nini.Config;
using log4net;

using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSTerrainManager
{
    static string LogHeader = "[BULLETSIM TERRAIN MANAGER]";

    // These height values are fractional so the odd values will be
    //     noticable when debugging.
    public const float HEIGHT_INITIALIZATION = 24.987f;
    public const float HEIGHT_INITIAL_LASTHEIGHT = 24.876f;
    public const float HEIGHT_GETHEIGHT_RET = 24.765f;

    // If the min and max height are equal, we reduce the min by this
    //    amount to make sure that a bounding box is built for the terrain.
    public const float HEIGHT_EQUAL_FUDGE = 0.2f;

    public const float TERRAIN_COLLISION_MARGIN = 0.2f;

    // The scene that I am part of
    BSScene m_physicsScene;

    // The ground plane created to keep thing from falling to infinity.
    private BulletBody m_groundPlane;
    
    // If doing mega-regions, if we're region zero we will be managing multiple
    //    region terrains since region zero does the physics for the whole mega-region.
    private Dictionary<Vector2, BulletBody> m_terrains;
    private Dictionary<Vector2, BulletHeightMapInfo> m_heightMaps;

    // True of the terrain has been modified.
    // Used to force recalculation of terrain height after terrain has been modified
    private bool m_terrainModified;

    // If we are doing mega-regions, terrains are added from TERRAIN_ID to m_terrainCount.
    // This is incremented before assigning to new region so it is the last ID allocated.
    private uint m_terrainCount = BSScene.CHILDTERRAIN_ID - 1;
    public uint HighestTerrainID { get {return m_terrainCount; } }

    // If doing mega-regions, this holds our offset from region zero of
    //     the mega-regions. "parentScene" points to the PhysicsScene of region zero.
    private Vector3 m_worldOffset = Vector3.Zero;
    public Vector2 WorldExtents = new Vector2((int)Constants.RegionSize, (int)Constants.RegionSize);
    private PhysicsScene m_parentScene = null;

    public BSTerrainManager(BSScene physicsScene)
    {
        m_physicsScene = physicsScene;
        m_terrains = new Dictionary<Vector2,BulletBody>();
        m_heightMaps = new Dictionary<Vector2,BulletHeightMapInfo>();
        m_terrainModified = false;
    }

    // Create the initial instance of terrain and the underlying ground plane.
    // The objects are allocated in the unmanaged space and the pointers are tracked
    //    by the managed code.
    // The terrains and the groundPlane are not added to the list of PhysObjects.
    // This is called from the initialization routine so we presume it is
    //    safe to call Bullet in real time. We hope no one is moving around prim yet.
    public void CreateInitialGroundPlaneAndTerrain()
    {
        // The ground plane is here to catch things that are trying to drop to negative infinity
        BulletShape groundPlaneShape = new BulletShape(BulletSimAPI.CreateGroundPlaneShape2(BSScene.GROUNDPLANE_ID, 1f, TERRAIN_COLLISION_MARGIN));
        m_groundPlane = new BulletBody(BSScene.GROUNDPLANE_ID, 
                        BulletSimAPI.CreateBodyWithDefaultMotionState2(groundPlaneShape.Ptr, Vector3.Zero, Quaternion.Identity));
        BulletSimAPI.AddObjectToWorld2(m_physicsScene.World.Ptr, m_groundPlane.Ptr);

        Vector3 minTerrainCoords = new Vector3(0f, 0f, HEIGHT_INITIALIZATION - HEIGHT_EQUAL_FUDGE);
        Vector3 maxTerrainCoords = new Vector3(Constants.RegionSize, Constants.RegionSize, HEIGHT_INITIALIZATION);
        int totalHeights = (int)maxTerrainCoords.X * (int)maxTerrainCoords.Y;
        float[] initialMap = new float[totalHeights];
        for (int ii = 0; ii < totalHeights; ii++)
        {
            initialMap[ii] = HEIGHT_INITIALIZATION;
        }
        CreateNewTerrainSegment(BSScene.TERRAIN_ID, initialMap, minTerrainCoords, maxTerrainCoords);
    }

    public void ReleaseGroundPlaneAndTerrain()
    {
        if (BulletSimAPI.RemoveObjectFromWorld2(m_physicsScene.World.Ptr, m_groundPlane.Ptr))
        {
            BulletSimAPI.DestroyObject2(m_physicsScene.World.Ptr, m_groundPlane.Ptr);
        }
        m_groundPlane.Ptr = IntPtr.Zero;

        foreach (KeyValuePair<Vector2, BulletBody> kvp in m_terrains)
        {
            if (BulletSimAPI.RemoveObjectFromWorld2(m_physicsScene.World.Ptr, kvp.Value.Ptr))
            {
                BulletSimAPI.DestroyObject2(m_physicsScene.World.Ptr, kvp.Value.Ptr);
                BulletSimAPI.ReleaseHeightMapInfo2(m_heightMaps[kvp.Key].Ptr);
            }
        }
        m_terrains.Clear();
        m_heightMaps.Clear();
    }

    // Create a new terrain description. This is used for mega-regions where
    //    the children of region zero give region zero all of the terrain
    //    segments since region zero does all the physics for the mega-region.
    // Call at taint time!!
    public void CreateNewTerrainSegment(uint id, float[] heightMap, Vector3 minCoords, Vector3 maxCoords)
    {
        // The Z coordinates are recalculated to be the min and max height of the terrain
        //    itself. The caller may have passed us the real region extent.
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        int hSize = heightMap.Length;
        for (int ii = 0; ii < hSize; ii++)
        {
            float height = heightMap[ii];
            if (height < minZ) minZ = height;
            if (height > maxZ) maxZ = height;
        }
        // If the terrain is flat, make a difference so we get a bounding box
        if (minZ == maxZ) 
            minZ -= HEIGHT_EQUAL_FUDGE;

        minCoords.Z = minZ;
        maxCoords.Z = maxZ;
        Vector2 terrainRegionBase = new Vector2(minCoords.X, minCoords.Y);

        // Create the heightmap data structure in the unmanaged space
        BulletHeightMapInfo mapInfo = new BulletHeightMapInfo(id, heightMap,
                            BulletSimAPI.CreateHeightMapInfo2(id, minCoords, maxCoords, heightMap, TERRAIN_COLLISION_MARGIN));
        mapInfo.terrainRegionBase = terrainRegionBase;
        mapInfo.minCoords = minCoords;
        mapInfo.maxCoords = maxCoords;
        mapInfo.minZ = minZ;
        mapInfo.maxZ = maxZ;
        mapInfo.sizeX = maxCoords.X - minCoords.X;
        mapInfo.sizeY = maxCoords.Y - minCoords.Y;

        Vector3 centerPos;
        centerPos.X = minCoords.X + (mapInfo.sizeX / 2f);
        centerPos.Y = minCoords.Y + (mapInfo.sizeY / 2f);
        centerPos.Z = minZ + (maxZ - minZ) / 2f;

        DetailLog("{0},BSScene.CreateNewTerrainSegment,call,minZ={1},maxZ={2},hMapPtr={3},minC={4},maxC={5}",
                    BSScene.DetailLogZero, minZ, maxZ, mapInfo.Ptr, minCoords, maxCoords);
        // Create the terrain shape from the mapInfo
        BulletShape terrainShape = new BulletShape(BulletSimAPI.CreateTerrainShape2(mapInfo.Ptr));

        BulletBody terrainBody = new BulletBody(id, BulletSimAPI.CreateBodyWithDefaultMotionState2(terrainShape.Ptr,
                       centerPos, Quaternion.Identity)); 

        BulletSimAPI.SetFriction2(terrainBody.Ptr, m_physicsScene.Params.terrainFriction);
        BulletSimAPI.SetHitFraction2(terrainBody.Ptr, m_physicsScene.Params.terrainHitFraction);
        BulletSimAPI.SetRestitution2(terrainBody.Ptr, m_physicsScene.Params.terrainRestitution);
        BulletSimAPI.SetCollisionFlags2(terrainBody.Ptr, CollisionFlags.CF_STATIC_OBJECT);
        BulletSimAPI.Activate2(terrainBody.Ptr, true);

        // Add the new terrain to the dynamics world
        BulletSimAPI.AddObjectToWorld2(m_physicsScene.World.Ptr, terrainBody.Ptr);
        BulletSimAPI.UpdateSingleAabb2(m_physicsScene.World.Ptr, terrainBody.Ptr);

        // Add the created terrain to the management set. If we are doing mega-regions,
        //    the terrains of our children will be added.
        m_terrains.Add(terrainRegionBase, terrainBody);
        m_heightMaps.Add(terrainRegionBase, mapInfo);

        m_terrainModified = true;
    }

    public void SetTerrain(float[] heightMap) {
        if (m_worldOffset != Vector3.Zero && m_parentScene != null)
        {
            // If doing the mega-prim stuff and we are the child of the zero region,
            //    the terrain is really added to our parent
            if (m_parentScene is BSScene)
            {
                ((BSScene)m_parentScene).TerrainManager.SetTerrain(heightMap, m_worldOffset);
            }
        }
        else
        {
            // if not doing the mega-prim thing, just change the terrain
            SetTerrain(heightMap, m_worldOffset);
        }
    }

    private void SetTerrain(float[] heightMap, Vector3 tOffset)
    {
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        Vector2 terrainRegionBase = new Vector2(tOffset.X, tOffset.Y);

        int heightMapSize = heightMap.Length;
        for (int ii = 0; ii < heightMapSize; ii++)
        {
            float height = heightMap[ii];
            if (height < minZ) minZ = height;
            if (height > maxZ) maxZ = height;
        }

        // The shape of the terrain is from its base to its extents.
        Vector3 minCoords, maxCoords;
        minCoords = tOffset;
        minCoords.Z = minZ;
        maxCoords = tOffset;
        maxCoords.X += Constants.RegionSize;
        maxCoords.Y += Constants.RegionSize;
        maxCoords.Z = maxZ;

        BulletBody terrainBody;
        BulletHeightMapInfo mapInfo;
        if (m_heightMaps.TryGetValue(terrainRegionBase, out mapInfo))
        {
            terrainBody = m_terrains[terrainRegionBase];
            // Copy heightMap local and compute some statistics.
            for (int ii = 0; ii < heightMapSize; ii++)
            {
                mapInfo.heightMap[ii] = heightMap[ii];
            }

            // If this is terrain we know about, it's easy to update
            m_physicsScene.TaintedObject("BSScene.SetTerrain:UpdateExisting", delegate()
            {
                DetailLog("{0},SetTerrain:UpdateExisting,baseX={1},baseY={2},minZ={3},maxZ={4}", 
                                    BSScene.DetailLogZero, tOffset.X, tOffset.Y, minZ, maxZ);
                // Fill the existing height map info with the new location and size information
                BulletSimAPI.FillHeightMapInfo2(mapInfo.Ptr, mapInfo.ID, minCoords, maxCoords, mapInfo.heightMap, TERRAIN_COLLISION_MARGIN);

                // Create a terrain shape based on the new info
                BulletShape terrainShape = new BulletShape(BulletSimAPI.CreateTerrainShape2(mapInfo.Ptr));

                // Swap the shape in the terrain body (this also deletes the old shape)
                bool success = BulletSimAPI.ReplaceBodyShape2(m_physicsScene.World.Ptr, terrainBody.Ptr, terrainShape.Ptr);

                if (!success)
                {
                    DetailLog("{0},SetTerrain:UpdateExisting,Failed", BSScene.DetailLogZero);
                    m_physicsScene.Logger.ErrorFormat("{0} Failed updating terrain heightmap. Region={1}",
                                    LogHeader, m_physicsScene.RegionName);
                    
                }
            });
        }
        else
        {
            // Our mega-prim child is giving us a new terrain to add to the phys world
            uint newTerrainID = ++m_terrainCount;

            m_physicsScene.TaintedObject("BSScene.SetTerrain:NewTerrain", delegate()
            {
                DetailLog("{0},SetTerrain:NewTerrain,baseX={1},baseY={2}", BSScene.DetailLogZero, tOffset.X, tOffset.Y);
                CreateNewTerrainSegment(newTerrainID, heightMap, minCoords, maxCoords);
            });
        }
    }

    // Someday we will have complex terrain with caves and tunnels
    public float GetTerrainHeightAtXYZ(Vector3 loc)
    {
        // For the moment, it's flat and convex
        return GetTerrainHeightAtXY(loc.X, loc.Y);
    }

    // Given an X and Y, find the height of the terrain.
    // Since we could be handling multiple terrains for a mega-region,
    //    the base of the region is calcuated assuming all regions are
    //    the same size and that is the default.
    // Once the heightMapInfo is found, we have all the information to
    //    compute the offset into the array.
    private float lastHeightTX = 999999f;
    private float lastHeightTY = 999999f;
    private float lastHeight = HEIGHT_INITIAL_LASTHEIGHT;
    public float GetTerrainHeightAtXY(float tX, float tY)
    {
        // You'd be surprized at the number of times this routine is called
        //    with the same parameters as last time.
        if (!m_terrainModified && lastHeightTX == tX && lastHeightTY == tY)
            return lastHeight;

        lastHeightTX = tX;
        lastHeightTY = tY;
        float ret = HEIGHT_GETHEIGHT_RET;

        int offsetX = ((int)(tX / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
        int offsetY = ((int)(tY / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
        Vector2 terrainBaseXY = new Vector2(offsetX, offsetY);

        BulletHeightMapInfo mapInfo;
        if (m_heightMaps.TryGetValue(terrainBaseXY, out mapInfo))
        {
            float regionX = tX - offsetX;
            float regionY = tY - offsetY;
            if (regionX > mapInfo.sizeX) regionX = 0;
            if (regionY > mapInfo.sizeY) regionY = 0;
            int mapIndex = (int)regionY * (int)mapInfo.sizeY + (int)regionX;
            ret = mapInfo.heightMap[mapIndex];
            m_terrainModified = false;
            DetailLog("{0},BSTerrainManager.GetTerrainHeightAtXY,bX={1},baseY={2},szX={3},szY={4},regX={5},regY={6},index={7},ht={8}",
                    BSScene.DetailLogZero, offsetX, offsetY, mapInfo.sizeX, mapInfo.sizeY, regionX, regionY, mapIndex, ret);
        }
        else
        {
            m_physicsScene.Logger.ErrorFormat("{0} GetTerrainHeightAtXY: terrain not found: x={1}, y={2}",
                LogHeader, tX, tY);
        }
        lastHeight = ret;
        return ret;
    }

    // Although no one seems to check this, I do support combining.
    public bool SupportsCombining()
    {
        return true;
    }
    // This call says I am a child to region zero in a mega-region. 'pScene' is that
    //    of region zero, 'offset' is my offset from regions zero's origin, and
    //    'extents' is the largest XY that is handled in my region.
    public void Combine(PhysicsScene pScene, Vector3 offset, Vector3 extents)
    {
        m_worldOffset = offset;
        WorldExtents = new Vector2(extents.X, extents.Y);
        m_parentScene = pScene;
    }

    // Unhook all the combining that I know about.
    public void UnCombine(PhysicsScene pScene)
    {
        // Just like ODE, for the moment a NOP
    }


    private void DetailLog(string msg, params Object[] args)
    {
        m_physicsScene.PhysicsLogging.Write(msg, args);
    }
}
}
