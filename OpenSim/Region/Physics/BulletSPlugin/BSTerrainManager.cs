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

    public const float TERRAIN_COLLISION_MARGIN = 0.0f;

    // Until the whole simulator is changed to pass us the region size, we rely on constants.
    public Vector3 DefaultRegionSize = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

    // The scene that I am part of
    private BSScene PhysicsScene { get; set; }

    // The ground plane created to keep thing from falling to infinity.
    private BulletBody m_groundPlane;

    // If doing mega-regions, if we're region zero we will be managing multiple
    //    region terrains since region zero does the physics for the whole mega-region.
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
    private Vector3 m_worldOffset;
    // If the parent region (region 0), this is the extent of the combined regions
    //     relative to the origin of region zero
    private Vector3 m_worldMax;
    private PhysicsScene MegaRegionParentPhysicsScene { get; set; }

    public BSTerrainManager(BSScene physicsScene)
    {
        PhysicsScene = physicsScene;
        m_heightMaps = new Dictionary<Vector2,BulletHeightMapInfo>();
        m_terrainModified = false;

        // Assume one region of default size
        m_worldOffset = Vector3.Zero;
        m_worldMax = new Vector3(DefaultRegionSize);
        MegaRegionParentPhysicsScene = null;
    }

    // Create the initial instance of terrain and the underlying ground plane.
    // The objects are allocated in the unmanaged space and the pointers are tracked
    //    by the managed code.
    // The terrains and the groundPlane are not added to the list of PhysObjects.
    // This is called from the initialization routine so we presume it is
    //    safe to call Bullet in real time. We hope no one is moving prims around yet.
    public void CreateInitialGroundPlaneAndTerrain()
    {
        // The ground plane is here to catch things that are trying to drop to negative infinity
        BulletShape groundPlaneShape = new BulletShape(
                    BulletSimAPI.CreateGroundPlaneShape2(BSScene.GROUNDPLANE_ID, 1f, TERRAIN_COLLISION_MARGIN),
                    ShapeData.PhysicsShapeType.SHAPE_GROUNDPLANE);
        m_groundPlane = new BulletBody(BSScene.GROUNDPLANE_ID,
                        BulletSimAPI.CreateBodyWithDefaultMotionState2(groundPlaneShape.ptr, BSScene.GROUNDPLANE_ID,
                                                            Vector3.Zero, Quaternion.Identity));
        BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, m_groundPlane.ptr);
        // Ground plane does not move
        BulletSimAPI.ForceActivationState2(m_groundPlane.ptr, ActivationState.DISABLE_SIMULATION);
        // Everything collides with the ground plane.
        BulletSimAPI.SetCollisionFilterMask2(m_groundPlane.ptr,
                        (uint)CollisionFilterGroups.GroundPlaneFilter, (uint)CollisionFilterGroups.GroundPlaneMask);

        Vector3 minTerrainCoords = new Vector3(0f, 0f, HEIGHT_INITIALIZATION - HEIGHT_EQUAL_FUDGE);
        Vector3 maxTerrainCoords = new Vector3(DefaultRegionSize.X, DefaultRegionSize.Y, HEIGHT_INITIALIZATION);
        int totalHeights = (int)maxTerrainCoords.X * (int)maxTerrainCoords.Y;
        float[] initialMap = new float[totalHeights];
        for (int ii = 0; ii < totalHeights; ii++)
        {
            initialMap[ii] = HEIGHT_INITIALIZATION;
        }
        UpdateOrCreateTerrain(BSScene.TERRAIN_ID, initialMap, minTerrainCoords, maxTerrainCoords, true);
    }

    // Release all the terrain structures we might have allocated
    public void ReleaseGroundPlaneAndTerrain()
    {
        if (m_groundPlane.ptr != IntPtr.Zero)
        {
            if (BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, m_groundPlane.ptr))
            {
                BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, m_groundPlane.ptr);
            }
            m_groundPlane.ptr = IntPtr.Zero;
        }

        ReleaseTerrain();
    }

    // Release all the terrain we have allocated
    public void ReleaseTerrain()
    {
        foreach (KeyValuePair<Vector2, BulletHeightMapInfo> kvp in m_heightMaps)
        {
            if (BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, kvp.Value.terrainBody.ptr))
            {
                BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, kvp.Value.terrainBody.ptr);
                BulletSimAPI.ReleaseHeightMapInfo2(kvp.Value.Ptr);
            }
        }
        m_heightMaps.Clear();
    }

    // The simulator wants to set a new heightmap for the terrain.
    public void SetTerrain(float[] heightMap) {
        float[] localHeightMap = heightMap;
        PhysicsScene.TaintedObject("TerrainManager.SetTerrain", delegate()
        {
            if (m_worldOffset != Vector3.Zero && MegaRegionParentPhysicsScene != null)
            {
                // If a child of a mega-region, we shouldn't have any terrain allocated for us
                ReleaseGroundPlaneAndTerrain();
                // If doing the mega-prim stuff and we are the child of the zero region,
                //    the terrain is added to our parent
                if (MegaRegionParentPhysicsScene is BSScene)
                {
                    DetailLog("{0},SetTerrain.ToParent,offset={1},worldMax={2}",
                                    BSScene.DetailLogZero, m_worldOffset, m_worldMax);
                    ((BSScene)MegaRegionParentPhysicsScene).TerrainManager.UpdateOrCreateTerrain(BSScene.CHILDTERRAIN_ID,
                                    localHeightMap, m_worldOffset, m_worldOffset + DefaultRegionSize, true);
                }
            }
            else
            {
                // If not doing the mega-prim thing, just change the terrain
                DetailLog("{0},SetTerrain.Existing", BSScene.DetailLogZero);

                UpdateOrCreateTerrain(BSScene.TERRAIN_ID, localHeightMap,
                                    m_worldOffset, m_worldOffset + DefaultRegionSize, true);
            }
        });
    }

    // If called with no mapInfo for the terrain, this will create a new mapInfo and terrain
    //     based on the passed information. The 'id' should be either the terrain id or
    //     BSScene.CHILDTERRAIN_ID. If the latter, a new child terrain ID will be allocated and used.
    //     The latter feature is for creating child terrains for mega-regions.
    // If called with a mapInfo in m_heightMaps but the terrain has no body yet (mapInfo.terrainBody.Ptr == 0)
    //     then a new body and shape is created and the mapInfo is filled.
    //     This call is used for doing the initial terrain creation.
    // If called with a mapInfo in m_heightMaps and there is an existing terrain body, a new
    //     terrain shape is created and added to the body.
    //     This call is most often used to update the heightMap and parameters of the terrain.
    // The 'doNow' boolean says whether to do all the unmanaged activities right now (like when
    //     calling this routine from initialization or taint-time routines) or whether to delay
    //     all the unmanaged activities to taint-time.
    private void UpdateOrCreateTerrain(uint id, float[] heightMap, Vector3 minCoords, Vector3 maxCoords, bool inTaintTime)
    {
        DetailLog("{0},BSTerrainManager.UpdateOrCreateTerrain,call,minC={1},maxC={2},inTaintTime={3}",
                            BSScene.DetailLogZero, minCoords, maxCoords, inTaintTime);

        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        Vector2 terrainRegionBase = new Vector2(minCoords.X, minCoords.Y);

        int heightMapSize = heightMap.Length;
        for (int ii = 0; ii < heightMapSize; ii++)
        {
            float height = heightMap[ii];
            if (height < minZ) minZ = height;
            if (height > maxZ) maxZ = height;
        }

        // The shape of the terrain is from its base to its extents.
        minCoords.Z = minZ;
        maxCoords.Z = maxZ;

        BulletHeightMapInfo mapInfo;
        if (m_heightMaps.TryGetValue(terrainRegionBase, out mapInfo))
        {
            // If this is terrain we know about, it's easy to update

            mapInfo.heightMap = heightMap;
            mapInfo.minCoords = minCoords;
            mapInfo.maxCoords = maxCoords;
            mapInfo.minZ = minZ;
            mapInfo.maxZ = maxZ;
            mapInfo.sizeX = maxCoords.X - minCoords.X;
            mapInfo.sizeY = maxCoords.Y - minCoords.Y;
            DetailLog("{0},UpdateOrCreateTerrain:UpdateExisting,call,terrainBase={1},minC={2}, maxC={3}, szX={4}, szY={5}",
                        BSScene.DetailLogZero, terrainRegionBase, mapInfo.minCoords, mapInfo.maxCoords, mapInfo.sizeX, mapInfo.sizeY);

            BSScene.TaintCallback rebuildOperation = delegate()
            {
                if (MegaRegionParentPhysicsScene != null)
                {
                    // It's possible that Combine() was called after this code was queued.
                    // If we are a child of combined regions, we don't create any terrain for us.
                    DetailLog("{0},UpdateOrCreateTerrain:AmACombineChild,taint", BSScene.DetailLogZero);

                    // Get rid of any terrain that may have been allocated for us.
                    ReleaseGroundPlaneAndTerrain();

                    // I hate doing this, but just bail
                    return;
                }

                if (mapInfo.terrainBody.ptr != IntPtr.Zero)
                {
                    // Updating an existing terrain.
                    DetailLog("{0},UpdateOrCreateTerrain:UpdateExisting,taint,terrainBase={1},minC={2}, maxC={3}, szX={4}, szY={5}",
                                    BSScene.DetailLogZero, terrainRegionBase, mapInfo.minCoords, mapInfo.maxCoords, mapInfo.sizeX, mapInfo.sizeY);

                    // Remove from the dynamics world because we're going to mangle this object
                    BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, mapInfo.terrainBody.ptr);

                    // Get rid of the old terrain
                    BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, mapInfo.terrainBody.ptr);
                    BulletSimAPI.ReleaseHeightMapInfo2(mapInfo.Ptr);
                    mapInfo.Ptr = IntPtr.Zero;

                    /*
                    // NOTE: This routine is half here because I can't get the terrain shape replacement
                    //   to work. In the short term, the above three lines completely delete the old
                    //   terrain and the code below recreates one from scratch.
                    // Hopefully the Bullet community will help me out on this one.

                    // First, release the old collision shape (there is only one terrain)
                    BulletSimAPI.DeleteCollisionShape2(m_physicsScene.World.Ptr, mapInfo.terrainShape.Ptr);

                    // Fill the existing height map info with the new location and size information
                    BulletSimAPI.FillHeightMapInfo2(m_physicsScene.World.Ptr, mapInfo.Ptr, mapInfo.ID,
                                    mapInfo.minCoords, mapInfo.maxCoords, mapInfo.heightMap, TERRAIN_COLLISION_MARGIN);

                    // Create a terrain shape based on the new info
                    mapInfo.terrainShape = new BulletShape(BulletSimAPI.CreateTerrainShape2(mapInfo.Ptr));

                    // Stuff the shape into the existing terrain body
                    BulletSimAPI.SetBodyShape2(m_physicsScene.World.Ptr, mapInfo.terrainBody.Ptr, mapInfo.terrainShape.Ptr);
                    */
                }
                // else
                {
                    // Creating a new terrain.
                    DetailLog("{0},UpdateOrCreateTerrain:CreateNewTerrain,taint,baseX={1},baseY={2},minZ={3},maxZ={4}",
                                    BSScene.DetailLogZero, mapInfo.minCoords.X, mapInfo.minCoords.Y, minZ, maxZ);

                    mapInfo.ID = id;
                    mapInfo.Ptr = BulletSimAPI.CreateHeightMapInfo2(PhysicsScene.World.ptr, mapInfo.ID,
                        mapInfo.minCoords, mapInfo.maxCoords, mapInfo.heightMap, TERRAIN_COLLISION_MARGIN);

                    // Create the terrain shape from the mapInfo
                    mapInfo.terrainShape = new BulletShape(BulletSimAPI.CreateTerrainShape2(mapInfo.Ptr),
                                ShapeData.PhysicsShapeType.SHAPE_TERRAIN);

                    // The terrain object initial position is at the center of the object
                    Vector3 centerPos;
                    centerPos.X = minCoords.X + (mapInfo.sizeX / 2f);
                    centerPos.Y = minCoords.Y + (mapInfo.sizeY / 2f);
                    centerPos.Z = minZ + ((maxZ - minZ) / 2f);

                    mapInfo.terrainBody = new BulletBody(mapInfo.ID,
                            BulletSimAPI.CreateBodyWithDefaultMotionState2(mapInfo.terrainShape.ptr,
                                                        id, centerPos, Quaternion.Identity));
                }

                // Make sure the entry is in the heightmap table
                m_heightMaps[terrainRegionBase] = mapInfo;

                // Set current terrain attributes
                BulletSimAPI.SetFriction2(mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainFriction);
                BulletSimAPI.SetHitFraction2(mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainHitFraction);
                BulletSimAPI.SetRestitution2(mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainRestitution);
                BulletSimAPI.SetCollisionFlags2(mapInfo.terrainBody.ptr, CollisionFlags.CF_STATIC_OBJECT);

                // Return the new terrain to the world of physical objects
                BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, mapInfo.terrainBody.ptr);

                // redo its bounding box now that it is in the world
                BulletSimAPI.UpdateSingleAabb2(PhysicsScene.World.ptr, mapInfo.terrainBody.ptr);

                BulletSimAPI.SetCollisionFilterMask2(mapInfo.terrainBody.ptr,
                                    (uint)CollisionFilterGroups.TerrainFilter,
                                    (uint)CollisionFilterGroups.TerrainMask);

                // Make sure the new shape is processed.
                // BulletSimAPI.Activate2(mapInfo.terrainBody.ptr, true);
                BulletSimAPI.ForceActivationState2(mapInfo.terrainBody.ptr, ActivationState.DISABLE_SIMULATION);

                m_terrainModified = true;
            };

            // There is the option to do the changes now (we're already in 'taint time'), or
            //     to do the Bullet operations later.
            if (inTaintTime)
                rebuildOperation();
            else
                PhysicsScene.TaintedObject("BSScene.UpdateOrCreateTerrain:UpdateExisting", rebuildOperation);
        }
        else
        {
            // We don't know about this terrain so either we are creating a new terrain or
            //    our mega-prim child is giving us a new terrain to add to the phys world

            // if this is a child terrain, calculate a unique terrain id
            uint newTerrainID = id;
            if (newTerrainID >= BSScene.CHILDTERRAIN_ID)
                newTerrainID = ++m_terrainCount;

            float[] heightMapX = heightMap;
            Vector3 minCoordsX = minCoords;
            Vector3 maxCoordsX = maxCoords;

            DetailLog("{0},UpdateOrCreateTerrain:NewTerrain,call,id={1}, minC={2}, maxC={3}",
                            BSScene.DetailLogZero, newTerrainID, minCoords, minCoords);

            // Code that must happen at taint-time
            BSScene.TaintCallback createOperation = delegate()
            {
                DetailLog("{0},UpdateOrCreateTerrain:NewTerrain,taint,baseX={1},baseY={2}", BSScene.DetailLogZero, minCoords.X, minCoords.Y);
                // Create a new mapInfo that will be filled with the new info
                mapInfo = new BulletHeightMapInfo(id, heightMapX,
                        BulletSimAPI.CreateHeightMapInfo2(PhysicsScene.World.ptr, newTerrainID,
                                    minCoordsX, maxCoordsX, heightMapX, TERRAIN_COLLISION_MARGIN));
                // Put the unfilled heightmap info into the collection of same
                m_heightMaps.Add(terrainRegionBase, mapInfo);
                // Build the terrain
                UpdateOrCreateTerrain(newTerrainID, heightMap, minCoords, maxCoords, true);

                m_terrainModified = true;
            };

            // If already in taint-time, just call Bullet. Otherwise queue the operations for the safe time.
            if (inTaintTime)
                createOperation();
            else
                PhysicsScene.TaintedObject("BSScene.UpdateOrCreateTerrain:NewTerrain", createOperation);
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
    private float GetTerrainHeightAtXY(float tX, float tY)
    {
        // You'd be surprized at the number of times this routine is called
        //    with the same parameters as last time.
        if (!m_terrainModified && lastHeightTX == tX && lastHeightTY == tY)
            return lastHeight;

        lastHeightTX = tX;
        lastHeightTY = tY;
        float ret = HEIGHT_GETHEIGHT_RET;

        int offsetX = ((int)(tX / (int)DefaultRegionSize.X)) * (int)DefaultRegionSize.X;
        int offsetY = ((int)(tY / (int)DefaultRegionSize.Y)) * (int)DefaultRegionSize.Y;
        Vector2 terrainBaseXY = new Vector2(offsetX, offsetY);

        BulletHeightMapInfo mapInfo;
        if (m_heightMaps.TryGetValue(terrainBaseXY, out mapInfo))
        {
            float regionX = tX - offsetX;
            float regionY = tY - offsetY;
            int mapIndex = (int)regionY * (int)mapInfo.sizeY + (int)regionX;
            try
            {
                ret = mapInfo.heightMap[mapIndex];
            }
            catch
            {
                // Sometimes they give us wonky values of X and Y. Give a warning and return something.
                PhysicsScene.Logger.WarnFormat("{0} Bad request for terrain height. terrainBase={1}, x={2}, y={3}",
                                    LogHeader, terrainBaseXY, regionX, regionY);
                ret = HEIGHT_GETHEIGHT_RET;
            }
            // DetailLog("{0},BSTerrainManager.GetTerrainHeightAtXY,bX={1},baseY={2},szX={3},szY={4},regX={5},regY={6},index={7},ht={8}",
            //         BSScene.DetailLogZero, offsetX, offsetY, mapInfo.sizeX, mapInfo.sizeY, regionX, regionY, mapIndex, ret);
        }
        else
        {
            PhysicsScene.Logger.ErrorFormat("{0} GetTerrainHeightAtXY: terrain not found: region={1}, x={2}, y={3}",
                    LogHeader, PhysicsScene.RegionName, tX, tY);
        }
        m_terrainModified = false;
        lastHeight = ret;
        return ret;
    }

    // Although no one seems to check this, I do support combining.
    public bool SupportsCombining()
    {
        return true;
    }

    // This routine is called two ways:
    //    One with 'offset' and 'pScene' zero and null but 'extents' giving the maximum
    //        extent of the combined regions. This is to inform the parent of the size
    //        of the combined regions.
    //    and one with 'offset' as the offset of the child region to the base region,
    //        'pScene' pointing to the parent and 'extents' of zero. This informs the
    //        child of its relative base and new parent.
    public void Combine(PhysicsScene pScene, Vector3 offset, Vector3 extents)
    {
        m_worldOffset = offset;
        m_worldMax = extents;
        MegaRegionParentPhysicsScene = pScene;
        if (pScene != null)
        {
            // We are a child.
            // We want m_worldMax to be the highest coordinate of our piece of terrain.
            m_worldMax = offset + DefaultRegionSize;
        }
        DetailLog("{0},BSTerrainManager.Combine,offset={1},extents={2},wOffset={3},wMax={4}",
                        BSScene.DetailLogZero, offset, extents, m_worldOffset, m_worldMax);
    }

    // Unhook all the combining that I know about.
    public void UnCombine(PhysicsScene pScene)
    {
        // Just like ODE, for the moment a NOP
        DetailLog("{0},BSTerrainManager.UnCombine", BSScene.DetailLogZero);
    }


    private void DetailLog(string msg, params Object[] args)
    {
        PhysicsScene.PhysicsLogging.Write(msg, args);
    }
}
}
