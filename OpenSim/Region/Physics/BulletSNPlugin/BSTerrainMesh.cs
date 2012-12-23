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

namespace OpenSim.Region.Physics.BulletSNPlugin
{
public sealed class BSTerrainMesh : BSTerrainPhys
{
    static string LogHeader = "[BULLETSIM TERRAIN MESH]";

    private float[] m_savedHeightMap;
    int m_sizeX;
    int m_sizeY;

    BulletShape m_terrainShape;
    BulletBody m_terrainBody;

    public BSTerrainMesh(BSScene physicsScene, Vector3 regionBase, uint id, Vector3 regionSize) 
        : base(physicsScene, regionBase, id)
    {
    }

    public BSTerrainMesh(BSScene physicsScene, Vector3 regionBase, uint id /* parameters for making mesh */)
        : base(physicsScene, regionBase, id)
    {
    }

    // Create terrain mesh from a heightmap.
    public BSTerrainMesh(BSScene physicsScene, Vector3 regionBase, uint id, float[] initialMap, 
                                                    Vector3 minCoords, Vector3 maxCoords)
        : base(physicsScene, regionBase, id)
    {
        int indicesCount;
        int[] indices;
        int verticesCount;
        float[] vertices;

        m_savedHeightMap = initialMap;

        m_sizeX = (int)(maxCoords.X - minCoords.X);
        m_sizeY = (int)(maxCoords.Y - minCoords.Y);

        if (!BSTerrainMesh.ConvertHeightmapToMesh(PhysicsScene, initialMap,
                            m_sizeX, m_sizeY,
                            (float)m_sizeX, (float)m_sizeY,
                            Vector3.Zero, 1.0f,
                            out indicesCount, out indices, out verticesCount, out vertices))
        {
            // DISASTER!!
            PhysicsScene.DetailLog("{0},BSTerrainMesh.create,failedConversionOfHeightmap", ID);
            PhysicsScene.Logger.ErrorFormat("{0} Failed conversion of heightmap to mesh! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }
        PhysicsScene.DetailLog("{0},BSTerrainMesh.create,meshed,indices={1},indSz={2},vertices={3},vertSz={4}", 
                                ID, indicesCount, indices.Length, verticesCount, vertices.Length);

        m_terrainShape = new BulletShape(BulletSimAPI.CreateMeshShape2(PhysicsScene.World.ptr,
                                                    indicesCount, indices, verticesCount, vertices),
                                        BSPhysicsShapeType.SHAPE_MESH);
        if (!m_terrainShape.HasPhysicalShape)
        {
            // DISASTER!!
            PhysicsScene.DetailLog("{0},BSTerrainMesh.create,failedCreationOfShape", ID);
            physicsScene.Logger.ErrorFormat("{0} Failed creation of terrain mesh! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }

        Vector3 pos = regionBase;
        Quaternion rot = Quaternion.Identity;

        m_terrainBody = new BulletBody(id, BulletSimAPI.CreateBodyWithDefaultMotionState2( m_terrainShape.ptr, ID, pos, rot));
        if (!m_terrainBody.HasPhysicalBody)
        {
            // DISASTER!!
            physicsScene.Logger.ErrorFormat("{0} Failed creation of terrain body! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }

        // Set current terrain attributes
        BulletSimAPI.SetFriction2(m_terrainBody.ptr, BSParam.TerrainFriction);
        BulletSimAPI.SetHitFraction2(m_terrainBody.ptr, BSParam.TerrainHitFraction);
        BulletSimAPI.SetRestitution2(m_terrainBody.ptr, BSParam.TerrainRestitution);
        BulletSimAPI.SetCollisionFlags2(m_terrainBody.ptr, CollisionFlags.CF_STATIC_OBJECT);

        // Static objects are not very massive.
        BulletSimAPI.SetMassProps2(m_terrainBody.ptr, 0f, Vector3.Zero);

        // Put the new terrain to the world of physical objects
        BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, m_terrainBody.ptr, pos, rot);

        // Redo its bounding box now that it is in the world
        BulletSimAPI.UpdateSingleAabb2(PhysicsScene.World.ptr, m_terrainBody.ptr);

        m_terrainBody.collisionType = CollisionType.Terrain;
        m_terrainBody.ApplyCollisionMask();

        // Make it so the terrain will not move or be considered for movement.
        BulletSimAPI.ForceActivationState2(m_terrainBody.ptr, ActivationState.DISABLE_SIMULATION);
    }

    public override void Dispose()
    {
        if (m_terrainBody.HasPhysicalBody)
        {
            BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, m_terrainBody.ptr);
            // Frees both the body and the shape.
            BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, m_terrainBody.ptr);
        }
    }

    public override float GetTerrainHeightAtXYZ(Vector3 pos)
    {
        // For the moment use the saved heightmap to get the terrain height.
        // TODO: raycast downward to find the true terrain below the position.
        float ret = BSTerrainManager.HEIGHT_GETHEIGHT_RET;

        int mapIndex = (int)pos.Y * m_sizeY + (int)pos.X;
        try
        {
            ret = m_savedHeightMap[mapIndex];
        }
        catch
        {
            // Sometimes they give us wonky values of X and Y. Give a warning and return something.
            PhysicsScene.Logger.WarnFormat("{0} Bad request for terrain height. terrainBase={1}, pos={2}",
                                                LogHeader, TerrainBase, pos);
            ret = BSTerrainManager.HEIGHT_GETHEIGHT_RET;
        }
        return ret;
    }

    // The passed position is relative to the base of the region.
    public override float GetWaterLevelAtXYZ(Vector3 pos)
    {
        return PhysicsScene.SimpleWaterLevel;
    }

    // Convert the passed heightmap to mesh information suitable for CreateMeshShape2().
    // Return 'true' if successfully created.
    public static bool ConvertHeightmapToMesh(
                                BSScene physicsScene,
                                float[] heightMap, int sizeX, int sizeY,    // parameters of incoming heightmap
                                float extentX, float extentY,               // zero based range for output vertices
                                Vector3 extentBase,                         // base to be added to all vertices
                                float magnification,                        // number of vertices to create between heightMap coords
                                out int indicesCountO, out int[] indicesO,
                                out int verticesCountO, out float[] verticesO)
    {
        bool ret = false;

        int indicesCount = 0;
        int verticesCount = 0;
        int[] indices = new int[0];
        float[] vertices = new float[0];

        // Simple mesh creation which assumes magnification == 1.
        // TODO: do a more general solution that scales, adds new vertices and smoothes the result.

        // Create an array of vertices that is sizeX+1 by sizeY+1 (note the loop
        //    from zero to <= sizeX). The triangle indices are then generated as two triangles
        //    per heightmap point. There are sizeX by sizeY of these squares. The extra row and
        //    column of vertices are used to complete the triangles of the last row and column
        //    of the heightmap.
        try
        {
            // One vertice per heightmap value plus the vertices off the top and bottom edge.
            int totalVertices = (sizeX + 1) * (sizeY + 1);
            vertices = new float[totalVertices * 3];
            int totalIndices = sizeX * sizeY * 6;
            indices = new int[totalIndices];

            float magX = (float)sizeX / extentX;
            float magY = (float)sizeY / extentY;
            physicsScene.DetailLog("{0},BSTerrainMesh.ConvertHeightMapToMesh,totVert={1},totInd={2},extentBase={3},magX={4},magY={5}",
                                    BSScene.DetailLogZero, totalVertices, totalIndices, extentBase, magX, magY);
            float minHeight = float.MaxValue;
            // Note that sizeX+1 vertices are created since there is land between this and the next region.
            for (int yy = 0; yy <= sizeY; yy++)
            {
                for (int xx = 0; xx <= sizeX; xx++)     // Hint: the "<=" means we go around sizeX + 1 times
                {
                    int offset = yy * sizeX + xx;
                    // Extend the height with the height from the last row or column
                    if (yy == sizeY) offset -= sizeX;
                    if (xx == sizeX) offset -= 1;
                    float height = heightMap[offset];
                    minHeight = Math.Min(minHeight, height);
                    vertices[verticesCount + 0] = (float)xx * magX + extentBase.X;
                    vertices[verticesCount + 1] = (float)yy * magY + extentBase.Y;
                    vertices[verticesCount + 2] = height + extentBase.Z;
                    verticesCount += 3;
                }
            }
            verticesCount = verticesCount / 3;

            for (int yy = 0; yy < sizeY; yy++)
            {
                for (int xx = 0; xx < sizeX; xx++)
                {
                    int offset = yy * (sizeX + 1) + xx;
                    // Each vertices is presumed to be the upper left corner of a box of two triangles
                    indices[indicesCount + 0] = offset;
                    indices[indicesCount + 1] = offset + 1;
                    indices[indicesCount + 2] = offset + sizeX + 1; // accounting for the extra column
                    indices[indicesCount + 3] = offset + 1;
                    indices[indicesCount + 4] = offset + sizeX + 2;
                    indices[indicesCount + 5] = offset + sizeX + 1;
                    indicesCount += 6;
                }
            }

            ret = true;
        }
        catch (Exception e)
        {
            physicsScene.Logger.ErrorFormat("{0} Failed conversion of heightmap to mesh. For={1}/{2}, e={3}",
                                                LogHeader, physicsScene.RegionName, extentBase, e);
        }

        indicesCountO = indicesCount;
        indicesO = indices;
        verticesCountO = verticesCount;
        verticesO = vertices;

        return ret;
    }
}
}
