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

// The physical implementation of the terrain is wrapped in this class.
public abstract class BSTerrainPhys : IDisposable
{
    public enum TerrainImplementation
    {
        Heightmap   = 0,
        Mesh        = 1
    }

    protected BSScene m_physicsScene { get; private set; }
    // Base of the region in world coordinates. Coordinates inside the region are relative to this.
    public Vector3 TerrainBase { get; private set; }
    public uint ID { get; private set; }

    public BSTerrainPhys(BSScene physicsScene, Vector3 regionBase, uint id)
    {
        m_physicsScene = physicsScene;
        TerrainBase = regionBase;
        ID = id;
    }
    public abstract void Dispose();
    public abstract float GetTerrainHeightAtXYZ(Vector3 pos);
    public abstract float GetWaterLevelAtXYZ(Vector3 pos);
}

// ==========================================================================================
public sealed class BSTerrainManager : IDisposable
{
    static string LogHeader = "[BULLETSIM TERRAIN MANAGER]";

    // These height values are fractional so the odd values will be
    //     noticable when debugging.
    public const float HEIGHT_INITIALIZATION = 24.987f;
    public const float HEIGHT_INITIAL_LASTHEIGHT = 24.876f;
    public const float HEIGHT_GETHEIGHT_RET = 24.765f;
    public const float WATER_HEIGHT_GETHEIGHT_RET = 19.998f;

    // If the min and max height are equal, we reduce the min by this
    //    amount to make sure that a bounding box is built for the terrain.
    public const float HEIGHT_EQUAL_FUDGE = 0.2f;

    // Until the whole simulator is changed to pass us the region size, we rely on constants.
    public Vector3 DefaultRegionSize = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

    // The scene that I am part of
    private BSScene m_physicsScene { get; set; }

    // The ground plane created to keep thing from falling to infinity.
    private BulletBody m_groundPlane;

    // If doing mega-regions, if we're region zero we will be managing multiple
    //    region terrains since region zero does the physics for the whole mega-region.
    private Dictionary<Vector3, BSTerrainPhys> m_terrains;

    // Flags used to know when to recalculate the height.
    private bool m_terrainModified = false;

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

    public BSTerrainManager(BSScene physicsScene, Vector3 regionSize)
    {
        m_physicsScene = physicsScene;
        DefaultRegionSize = regionSize;

        m_terrains = new Dictionary<Vector3,BSTerrainPhys>();

        // Assume one region of default size
        m_worldOffset = Vector3.Zero;
        m_worldMax = new Vector3(DefaultRegionSize);
        MegaRegionParentPhysicsScene = null;
    }

    public void Dispose()
    {
        ReleaseGroundPlaneAndTerrain();
    }

    // Create the initial instance of terrain and the underlying ground plane.
    // This is called from the initialization routine so we presume it is
    //    safe to call Bullet in real time. We hope no one is moving prims around yet.
    public void CreateInitialGroundPlaneAndTerrain()
    {
        DetailLog("{0},BSTerrainManager.CreateInitialGroundPlaneAndTerrain,region={1}", BSScene.DetailLogZero, m_physicsScene.RegionName);
        // The ground plane is here to catch things that are trying to drop to negative infinity
        BulletShape groundPlaneShape = m_physicsScene.PE.CreateGroundPlaneShape(BSScene.GROUNDPLANE_ID, 1f, BSParam.TerrainCollisionMargin);
        Vector3 groundPlaneAltitude = new Vector3(0f, 0f, BSParam.TerrainGroundPlane);
        m_groundPlane = m_physicsScene.PE.CreateBodyWithDefaultMotionState(groundPlaneShape,
                                        BSScene.GROUNDPLANE_ID, groundPlaneAltitude, Quaternion.Identity);

        // Everything collides with the ground plane.
        m_groundPlane.collisionType = CollisionType.Groundplane;

        m_physicsScene.PE.AddObjectToWorld(m_physicsScene.World, m_groundPlane);
        m_physicsScene.PE.UpdateSingleAabb(m_physicsScene.World, m_groundPlane);

        // Ground plane does not move
        m_physicsScene.PE.ForceActivationState(m_groundPlane, ActivationState.DISABLE_SIMULATION);

        BSTerrainPhys initialTerrain = new BSTerrainHeightmap(m_physicsScene, Vector3.Zero, BSScene.TERRAIN_ID, DefaultRegionSize);
        lock (m_terrains)
        {
            // Build an initial terrain and put it in the world. This quickly gets replaced by the real region terrain.
            m_terrains.Add(Vector3.Zero, initialTerrain);
        }
    }

    // Release all the terrain structures we might have allocated
    public void ReleaseGroundPlaneAndTerrain()
    {
        DetailLog("{0},BSTerrainManager.ReleaseGroundPlaneAndTerrain,region={1}", BSScene.DetailLogZero, m_physicsScene.RegionName);
        if (m_groundPlane.HasPhysicalBody)
        {
            if (m_physicsScene.PE.RemoveObjectFromWorld(m_physicsScene.World, m_groundPlane))
            {
                m_physicsScene.PE.DestroyObject(m_physicsScene.World, m_groundPlane);
            }
            m_groundPlane.Clear();
        }

        ReleaseTerrain();
    }

    // Release all the terrain we have allocated
    public void ReleaseTerrain()
    {
        lock (m_terrains)
        {
            foreach (KeyValuePair<Vector3, BSTerrainPhys> kvp in m_terrains)
            {
                kvp.Value.Dispose();
            }
            m_terrains.Clear();
        }
    }

    // The simulator wants to set a new heightmap for the terrain.
    public void SetTerrain(float[] heightMap) {
        float[] localHeightMap = heightMap;
        // If there are multiple requests for changes to the same terrain between ticks,
        //      only do that last one.
        m_physicsScene.PostTaintObject("TerrainManager.SetTerrain-"+ m_worldOffset.ToString(), 0, delegate()
        {
            if (m_worldOffset != Vector3.Zero && MegaRegionParentPhysicsScene != null)
            {
                // If a child of a mega-region, we shouldn't have any terrain allocated for us
                ReleaseGroundPlaneAndTerrain();
                // If doing the mega-prim stuff and we are the child of the zero region,
                //    the terrain is added to our parent
                if (MegaRegionParentPhysicsScene is BSScene)
                {
                    DetailLog("{0},SetTerrain.ToParent,offset={1},worldMax={2}", BSScene.DetailLogZero, m_worldOffset, m_worldMax);
                    ((BSScene)MegaRegionParentPhysicsScene).TerrainManager.AddMegaRegionChildTerrain(
                                        BSScene.CHILDTERRAIN_ID, localHeightMap, m_worldOffset, m_worldOffset + DefaultRegionSize);
                }
            }
            else
            {
                // If not doing the mega-prim thing, just change the terrain
                DetailLog("{0},SetTerrain.Existing", BSScene.DetailLogZero);

                UpdateTerrain(BSScene.TERRAIN_ID, localHeightMap, m_worldOffset, m_worldOffset + DefaultRegionSize);
            }
        });
    }

    // Another region is calling this region and passing a terrain.
    // A region that is not the mega-region root will pass its terrain to the root region so the root region
    //      physics engine will have all the terrains.
    private void AddMegaRegionChildTerrain(uint id, float[] heightMap, Vector3 minCoords, Vector3 maxCoords)
    {
        // Since we are called by another region's thread, the action must be rescheduled onto our processing thread.
        m_physicsScene.PostTaintObject("TerrainManager.AddMegaRegionChild" + minCoords.ToString(), id, delegate()
        {
            UpdateTerrain(id, heightMap, minCoords, maxCoords);
        });
    }

    // If called for terrain has has not been previously allocated, a new terrain will be built
    //     based on the passed information. The 'id' should be either the terrain id or
    //     BSScene.CHILDTERRAIN_ID. If the latter, a new child terrain ID will be allocated and used.
    //     The latter feature is for creating child terrains for mega-regions.
    // If there is an existing terrain body, a new
    //     terrain shape is created and added to the body.
    //     This call is most often used to update the heightMap and parameters of the terrain.
    // (The above does suggest that some simplification/refactoring is in order.)
    // Called during taint-time.
    private void UpdateTerrain(uint id, float[] heightMap, Vector3 minCoords, Vector3 maxCoords)
    {
        // Find high and low points of passed heightmap.
        // The min and max passed in is usually the area objects can be in (maximum
        //     object height, for instance). The terrain wants the bounding box for the
        //     terrain so replace passed min and max Z with the actual terrain min/max Z.
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        foreach (float height in heightMap)
        {
            if (height < minZ) minZ = height;
            if (height > maxZ) maxZ = height;
        }
        if (minZ == maxZ)
        {
            // If min and max are the same, reduce min a little bit so a good bounding box is created.
            minZ -= BSTerrainManager.HEIGHT_EQUAL_FUDGE;
        }
        minCoords.Z = minZ;
        maxCoords.Z = maxZ;

        DetailLog("{0},BSTerrainManager.UpdateTerrain,call,id={1},minC={2},maxC={3}",
                            BSScene.DetailLogZero, id, minCoords, maxCoords);

        Vector3 terrainRegionBase = new Vector3(minCoords.X, minCoords.Y, 0f);

        lock (m_terrains)
        {
            BSTerrainPhys terrainPhys;
            if (m_terrains.TryGetValue(terrainRegionBase, out terrainPhys))
            {
                // There is already a terrain in this spot. Free the old and build the new.
                DetailLog("{0},BSTerrainManager.UpdateTerrain:UpdateExisting,call,id={1},base={2},minC={3},maxC={4}",
                                BSScene.DetailLogZero, id, terrainRegionBase, minCoords, maxCoords);

                // Remove old terrain from the collection
                m_terrains.Remove(terrainRegionBase);
                // Release any physical memory it may be using.
                terrainPhys.Dispose();

                if (MegaRegionParentPhysicsScene == null)
                {
                    // This terrain is not part of the mega-region scheme. Create vanilla terrain.
                    BSTerrainPhys newTerrainPhys = BuildPhysicalTerrain(terrainRegionBase, id, heightMap, minCoords, maxCoords);
                    m_terrains.Add(terrainRegionBase, newTerrainPhys);

                    m_terrainModified = true;
                }
                else
                {
                    // It's possible that Combine() was called after this code was queued.
                    // If we are a child of combined regions, we don't create any terrain for us.
                    DetailLog("{0},BSTerrainManager.UpdateTerrain:AmACombineChild,taint", BSScene.DetailLogZero);

                    // Get rid of any terrain that may have been allocated for us.
                    ReleaseGroundPlaneAndTerrain();

                    // I hate doing this, but just bail
                    return;
                }
            }
            else
            {
                // We don't know about this terrain so either we are creating a new terrain or
                //    our mega-prim child is giving us a new terrain to add to the phys world

                // if this is a child terrain, calculate a unique terrain id
                uint newTerrainID = id;
                if (newTerrainID >= BSScene.CHILDTERRAIN_ID)
                    newTerrainID = ++m_terrainCount;

                DetailLog("{0},BSTerrainManager.UpdateTerrain:NewTerrain,taint,newID={1},minCoord={2},maxCoord={3}",
                                            BSScene.DetailLogZero, newTerrainID, minCoords, maxCoords);
                BSTerrainPhys newTerrainPhys = BuildPhysicalTerrain(terrainRegionBase, id, heightMap, minCoords, maxCoords);
                m_terrains.Add(terrainRegionBase, newTerrainPhys);

                m_terrainModified = true;
            }
        }
    }

    // TODO: redo terrain implementation selection to allow other base types than heightMap.
    private BSTerrainPhys BuildPhysicalTerrain(Vector3 terrainRegionBase, uint id, float[] heightMap, Vector3 minCoords, Vector3 maxCoords)
    {
        m_physicsScene.Logger.DebugFormat("{0} Terrain for {1}/{2} created with {3}",
                                            LogHeader, m_physicsScene.RegionName, terrainRegionBase,
                                            (BSTerrainPhys.TerrainImplementation)BSParam.TerrainImplementation);
        BSTerrainPhys newTerrainPhys = null;
        switch ((int)BSParam.TerrainImplementation)
        {
            case (int)BSTerrainPhys.TerrainImplementation.Heightmap:
                newTerrainPhys = new BSTerrainHeightmap(m_physicsScene, terrainRegionBase, id,
                                            heightMap, minCoords, maxCoords);
                break;
            case (int)BSTerrainPhys.TerrainImplementation.Mesh:
                newTerrainPhys = new BSTerrainMesh(m_physicsScene, terrainRegionBase, id,
                                            heightMap, minCoords, maxCoords);
                break;
            default:
                m_physicsScene.Logger.ErrorFormat("{0} Bad terrain implementation specified. Type={1}/{2},Region={3}/{4}",
                                            LogHeader,
                                            (int)BSParam.TerrainImplementation,
                                            BSParam.TerrainImplementation,
                                            m_physicsScene.RegionName, terrainRegionBase);
                break;
        }
        return newTerrainPhys;
    }

    // Return 'true' of this position is somewhere in known physical terrain space
    public bool IsWithinKnownTerrain(Vector3 pos)
    {
        Vector3 terrainBaseXYZ;
        BSTerrainPhys physTerrain;
        return GetTerrainPhysicalAtXYZ(pos, out physTerrain, out terrainBaseXYZ);
    }

    // Return a new position that is over known terrain if the position is outside our terrain.
    public Vector3 ClampPositionIntoKnownTerrain(Vector3 pPos)
    {
        float edgeEpsilon = 0.1f;

        Vector3 ret = pPos;

        // First, base addresses are never negative so correct for that possible problem.
        if (ret.X < 0f || ret.Y < 0f)
        {
            ret.X = Util.Clamp<float>(ret.X, 0f, 1000000f);
            ret.Y = Util.Clamp<float>(ret.Y, 0f, 1000000f);
            DetailLog("{0},BSTerrainManager.ClampPositionToKnownTerrain,zeroingNegXorY,oldPos={1},newPos={2}",
                                        BSScene.DetailLogZero, pPos, ret);
        }

        // Can't do this function if we don't know about any terrain.
        if (m_terrains.Count == 0)
            return ret;

        int loopPrevention = 10;
        Vector3 terrainBaseXYZ;
        BSTerrainPhys physTerrain;
        while (!GetTerrainPhysicalAtXYZ(ret, out physTerrain, out terrainBaseXYZ))
        {
            // The passed position is not within a known terrain area.
            // NOTE that GetTerrainPhysicalAtXYZ will set 'terrainBaseXYZ' to the base of the unfound region.

            // Must be off the top of a region. Find an adjacent region to move into.
            //     The returned terrain is always 'lower'. That is, closer to <0,0>.
            Vector3 adjacentTerrainBase = FindAdjacentTerrainBase(terrainBaseXYZ);

            if (adjacentTerrainBase.X < terrainBaseXYZ.X)
            {
                // moving down into a new region in the X dimension. New position will be the max in the new base.
                ret.X = adjacentTerrainBase.X + DefaultRegionSize.X - edgeEpsilon;
            }
            if (adjacentTerrainBase.Y < terrainBaseXYZ.Y)
            {
                // moving down into a new region in the X dimension. New position will be the max in the new base.
                ret.Y = adjacentTerrainBase.Y + DefaultRegionSize.Y - edgeEpsilon;
            }
            DetailLog("{0},BSTerrainManager.ClampPositionToKnownTerrain,findingAdjacentRegion,adjacentRegBase={1},oldPos={2},newPos={3}",
                                        BSScene.DetailLogZero, adjacentTerrainBase, pPos, ret);

            if (loopPrevention-- < 0f)
            {
                // The 'while' is a little dangerous so this prevents looping forever if the
                //    mapping of the terrains ever gets messed up (like nothing at <0,0>) or
                //    the list of terrains is in transition.
                DetailLog("{0},BSTerrainManager.ClampPositionToKnownTerrain,suppressingFindAdjacentRegionLoop", BSScene.DetailLogZero);
                break;
            }
        }

        return ret;
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
    public float GetTerrainHeightAtXYZ(Vector3 pos)
    {
        float tX = pos.X;
        float tY = pos.Y;
        // You'd be surprized at the number of times this routine is called
        //    with the same parameters as last time.
        if (!m_terrainModified && (lastHeightTX == tX) && (lastHeightTY == tY))
            return lastHeight;
        m_terrainModified = false;

        lastHeightTX = tX;
        lastHeightTY = tY;
        float ret = HEIGHT_GETHEIGHT_RET;

        Vector3 terrainBaseXYZ;
        BSTerrainPhys physTerrain;
        if (GetTerrainPhysicalAtXYZ(pos, out physTerrain, out terrainBaseXYZ))
        {
            ret = physTerrain.GetTerrainHeightAtXYZ(pos - terrainBaseXYZ);
        }
        else
        {
            m_physicsScene.Logger.ErrorFormat("{0} GetTerrainHeightAtXY: terrain not found: region={1}, x={2}, y={3}",
                    LogHeader, m_physicsScene.RegionName, tX, tY);
            DetailLog("{0},BSTerrainManager.GetTerrainHeightAtXYZ,terrainNotFound,pos={1},base={2}",
                                BSScene.DetailLogZero, pos, terrainBaseXYZ);
        }

        lastHeight = ret;
        return ret;
    }

    public float GetWaterLevelAtXYZ(Vector3 pos)
    {
        float ret = WATER_HEIGHT_GETHEIGHT_RET;

        Vector3 terrainBaseXYZ;
        BSTerrainPhys physTerrain;
        if (GetTerrainPhysicalAtXYZ(pos, out physTerrain, out terrainBaseXYZ))
        {
            ret = physTerrain.GetWaterLevelAtXYZ(pos);
        }
        else
        {
            m_physicsScene.Logger.ErrorFormat("{0} GetWaterHeightAtXY: terrain not found: pos={1}, terrainBase={2}, height={3}",
                    LogHeader, m_physicsScene.RegionName, pos, terrainBaseXYZ, ret);
        }
        return ret;
    }

    // Given an address, return 'true' of there is a description of that terrain and output
    //    the descriptor class and the 'base' fo the addresses therein.
    private bool GetTerrainPhysicalAtXYZ(Vector3 pos, out BSTerrainPhys outPhysTerrain, out Vector3 outTerrainBase)
    {
        bool ret = false;

        Vector3 terrainBaseXYZ = Vector3.Zero;
        if (pos.X < 0f || pos.Y < 0f)
        {
            // We don't handle negative addresses so just make up a base that will not be found.
            terrainBaseXYZ = new Vector3(-DefaultRegionSize.X, -DefaultRegionSize.Y, 0f);
        }
        else
        {
            int offsetX = ((int)(pos.X / (int)DefaultRegionSize.X)) * (int)DefaultRegionSize.X;
            int offsetY = ((int)(pos.Y / (int)DefaultRegionSize.Y)) * (int)DefaultRegionSize.Y;
            terrainBaseXYZ = new Vector3(offsetX, offsetY, 0f);
        }

        BSTerrainPhys physTerrain = null;
        lock (m_terrains)
        {
            ret = m_terrains.TryGetValue(terrainBaseXYZ, out physTerrain);
        }
        outTerrainBase = terrainBaseXYZ;
        outPhysTerrain = physTerrain;
        return ret;
    }

    // Given a terrain base, return a terrain base for a terrain that is closer to <0,0> than
    //   this one. Usually used to return an out of bounds object to a known place.
    private Vector3 FindAdjacentTerrainBase(Vector3 pTerrainBase)
    {
        Vector3 ret = pTerrainBase;

        // Can't do this function if we don't know about any terrain.
        if (m_terrains.Count == 0)
            return ret;

        // Just some sanity
        ret.X = Util.Clamp<float>(ret.X, 0f, 1000000f);
        ret.Y = Util.Clamp<float>(ret.Y, 0f, 1000000f);
        ret.Z = 0f;

        lock (m_terrains)
        {
            // Once down to the <0,0> region, we have to be done.
            while (ret.X > 0f || ret.Y > 0f)
            {
                if (ret.X > 0f)
                {
                    ret.X = Math.Max(0f, ret.X - DefaultRegionSize.X);
                    DetailLog("{0},BSTerrainManager.FindAdjacentTerrainBase,reducingX,terrainBase={1}", BSScene.DetailLogZero, ret);
                    if (m_terrains.ContainsKey(ret))
                        break;
                }
                if (ret.Y > 0f)
                {
                    ret.Y = Math.Max(0f, ret.Y - DefaultRegionSize.Y);
                    DetailLog("{0},BSTerrainManager.FindAdjacentTerrainBase,reducingY,terrainBase={1}", BSScene.DetailLogZero, ret);
                    if (m_terrains.ContainsKey(ret))
                        break;
                }
            }
        }

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
        // Just like ODE, we don't do anything yet.
        DetailLog("{0},BSTerrainManager.UnCombine", BSScene.DetailLogZero);
    }


    private void DetailLog(string msg, params Object[] args)
    {
        m_physicsScene.PhysicsLogging.Write(msg, args);
    }
}
}
