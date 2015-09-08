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
using OpenSim.Region.PhysicsModules.SharedBase;

using Nini.Config;
using log4net;

using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSTerrainHeightmap : BSTerrainPhys
{
    static string LogHeader = "[BULLETSIM TERRAIN HEIGHTMAP]";

    BulletHMapInfo m_mapInfo = null;

    // Constructor to build a default, flat heightmap terrain.
    public BSTerrainHeightmap(BSScene physicsScene, Vector3 regionBase, uint id, Vector3 regionSize)
        : base(physicsScene, regionBase, id)
    {
        Vector3 minTerrainCoords = new Vector3(0f, 0f, BSTerrainManager.HEIGHT_INITIALIZATION - BSTerrainManager.HEIGHT_EQUAL_FUDGE);
        Vector3 maxTerrainCoords = new Vector3(regionSize.X, regionSize.Y, BSTerrainManager.HEIGHT_INITIALIZATION);
        int totalHeights = (int)maxTerrainCoords.X * (int)maxTerrainCoords.Y;
        float[] initialMap = new float[totalHeights];
        for (int ii = 0; ii < totalHeights; ii++)
        {
            initialMap[ii] = BSTerrainManager.HEIGHT_INITIALIZATION;
        }
        m_mapInfo = new BulletHMapInfo(id, initialMap, regionSize.X, regionSize.Y);
        m_mapInfo.minCoords = minTerrainCoords;
        m_mapInfo.maxCoords = maxTerrainCoords;
        m_mapInfo.terrainRegionBase = TerrainBase;
        // Don't have to free any previous since we just got here.
        BuildHeightmapTerrain();
    }

    // This minCoords and maxCoords passed in give the size of the terrain (min and max Z
    //         are the high and low points of the heightmap).
    public BSTerrainHeightmap(BSScene physicsScene, Vector3 regionBase, uint id, float[] initialMap,
                                                    Vector3 minCoords, Vector3 maxCoords)
        : base(physicsScene, regionBase, id)
    {
        m_mapInfo = new BulletHMapInfo(id, initialMap, maxCoords.X - minCoords.X, maxCoords.Y - minCoords.Y);
        m_mapInfo.minCoords = minCoords;
        m_mapInfo.maxCoords = maxCoords;
        m_mapInfo.minZ = minCoords.Z;
        m_mapInfo.maxZ = maxCoords.Z;
        m_mapInfo.terrainRegionBase = TerrainBase;

        // Don't have to free any previous since we just got here.
        BuildHeightmapTerrain();
    }

    public override void Dispose()
    {
        ReleaseHeightMapTerrain();
    }

    // Using the information in m_mapInfo, create the physical representation of the heightmap.
    private void BuildHeightmapTerrain()
    {
        // Create the terrain shape from the mapInfo
        m_mapInfo.terrainShape = m_physicsScene.PE.CreateTerrainShape( m_mapInfo.ID,
                                new Vector3(m_mapInfo.sizeX, m_mapInfo.sizeY, 0), m_mapInfo.minZ, m_mapInfo.maxZ,
                                m_mapInfo.heightMap, 1f, BSParam.TerrainCollisionMargin);


        // The terrain object initial position is at the center of the object
        Vector3 centerPos;
        centerPos.X = m_mapInfo.minCoords.X + (m_mapInfo.sizeX / 2f);
        centerPos.Y = m_mapInfo.minCoords.Y + (m_mapInfo.sizeY / 2f);
        centerPos.Z = m_mapInfo.minZ + ((m_mapInfo.maxZ - m_mapInfo.minZ) / 2f);

        m_mapInfo.terrainBody = m_physicsScene.PE.CreateBodyWithDefaultMotionState(m_mapInfo.terrainShape,
                                m_mapInfo.ID, centerPos, Quaternion.Identity);

        // Set current terrain attributes
        m_physicsScene.PE.SetFriction(m_mapInfo.terrainBody, BSParam.TerrainFriction);
        m_physicsScene.PE.SetHitFraction(m_mapInfo.terrainBody, BSParam.TerrainHitFraction);
        m_physicsScene.PE.SetRestitution(m_mapInfo.terrainBody, BSParam.TerrainRestitution);
        m_physicsScene.PE.SetCollisionFlags(m_mapInfo.terrainBody, CollisionFlags.CF_STATIC_OBJECT);

        m_mapInfo.terrainBody.collisionType = CollisionType.Terrain;

        // Return the new terrain to the world of physical objects
        m_physicsScene.PE.AddObjectToWorld(m_physicsScene.World, m_mapInfo.terrainBody);

        // redo its bounding box now that it is in the world
        m_physicsScene.PE.UpdateSingleAabb(m_physicsScene.World, m_mapInfo.terrainBody);

        // Make it so the terrain will not move or be considered for movement.
        m_physicsScene.PE.ForceActivationState(m_mapInfo.terrainBody, ActivationState.DISABLE_SIMULATION);

        return;
    }

    // If there is information in m_mapInfo pointing to physical structures, release same.
    private void ReleaseHeightMapTerrain()
    {
        if (m_mapInfo != null)
        {
            if (m_mapInfo.terrainBody.HasPhysicalBody)
            {
                m_physicsScene.PE.RemoveObjectFromWorld(m_physicsScene.World, m_mapInfo.terrainBody);
                // Frees both the body and the shape.
                m_physicsScene.PE.DestroyObject(m_physicsScene.World, m_mapInfo.terrainBody);
            }
        }
        m_mapInfo = null;
    }

    // The passed position is relative to the base of the region.
    public override float GetTerrainHeightAtXYZ(Vector3 pos)
    {
        float ret = BSTerrainManager.HEIGHT_GETHEIGHT_RET;

        int mapIndex = (int)pos.Y * (int)m_mapInfo.sizeY + (int)pos.X;
        try
        {
            ret = m_mapInfo.heightMap[mapIndex];
        }
        catch
        {
            // Sometimes they give us wonky values of X and Y. Give a warning and return something.
            m_physicsScene.Logger.WarnFormat("{0} Bad request for terrain height. terrainBase={1}, pos={2}",
                                LogHeader, m_mapInfo.terrainRegionBase, pos);
            ret = BSTerrainManager.HEIGHT_GETHEIGHT_RET;
        }
        return ret;
    }

    // The passed position is relative to the base of the region.
    public override float GetWaterLevelAtXYZ(Vector3 pos)
    {
        return m_physicsScene.SimpleWaterLevel;
    }
}
}
