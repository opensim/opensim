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

        bool meshCreationSuccess = false;
        if (BSParam.TerrainMeshMagnification == 1)
        {
            // If a magnification of one, use the old routine that is tried and true.
            meshCreationSuccess = BSTerrainMesh.ConvertHeightmapToMesh(m_physicsScene,
                                            initialMap, m_sizeX, m_sizeY,       // input size
                                            Vector3.Zero,                       // base for mesh
                                            out indicesCount, out indices, out verticesCount, out vertices);
        }
        else
        {
            // Other magnifications use the newer routine
            meshCreationSuccess = BSTerrainMesh.ConvertHeightmapToMesh2(m_physicsScene,
                                            initialMap, m_sizeX, m_sizeY,       // input size
                                            BSParam.TerrainMeshMagnification,
                                            physicsScene.TerrainManager.DefaultRegionSize,
                                            Vector3.Zero,                       // base for mesh
                                            out indicesCount, out indices, out verticesCount, out vertices);
        }
        if (!meshCreationSuccess)
        {
            // DISASTER!!
            m_physicsScene.DetailLog("{0},BSTerrainMesh.create,failedConversionOfHeightmap,id={1}", BSScene.DetailLogZero, ID);
            m_physicsScene.Logger.ErrorFormat("{0} Failed conversion of heightmap to mesh! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }

        m_physicsScene.DetailLog("{0},BSTerrainMesh.create,meshed,id={1},indices={2},indSz={3},vertices={4},vertSz={5}",
                                BSScene.DetailLogZero, ID, indicesCount, indices.Length, verticesCount, vertices.Length);

        m_terrainShape = m_physicsScene.PE.CreateMeshShape(m_physicsScene.World, indicesCount, indices, verticesCount, vertices);
        if (!m_terrainShape.HasPhysicalShape)
        {
            // DISASTER!!
            m_physicsScene.DetailLog("{0},BSTerrainMesh.create,failedCreationOfShape,id={1}", BSScene.DetailLogZero, ID);
            m_physicsScene.Logger.ErrorFormat("{0} Failed creation of terrain mesh! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }

        Vector3 pos = regionBase;
        Quaternion rot = Quaternion.Identity;

        m_terrainBody = m_physicsScene.PE.CreateBodyWithDefaultMotionState(m_terrainShape, ID, pos, rot);
        if (!m_terrainBody.HasPhysicalBody)
        {
            // DISASTER!!
            m_physicsScene.Logger.ErrorFormat("{0} Failed creation of terrain body! base={1}", LogHeader, TerrainBase);
            // Something is very messed up and a crash is in our future.
            return;
        }
        physicsScene.PE.SetShapeCollisionMargin(m_terrainShape, BSParam.TerrainCollisionMargin);

        // Set current terrain attributes
        m_physicsScene.PE.SetFriction(m_terrainBody, BSParam.TerrainFriction);
        m_physicsScene.PE.SetHitFraction(m_terrainBody, BSParam.TerrainHitFraction);
        m_physicsScene.PE.SetRestitution(m_terrainBody, BSParam.TerrainRestitution);
        m_physicsScene.PE.SetContactProcessingThreshold(m_terrainBody, BSParam.TerrainContactProcessingThreshold);
        m_physicsScene.PE.SetCollisionFlags(m_terrainBody, CollisionFlags.CF_STATIC_OBJECT);

        // Static objects are not very massive.
        m_physicsScene.PE.SetMassProps(m_terrainBody, 0f, Vector3.Zero);

        // Put the new terrain to the world of physical objects
        m_physicsScene.PE.AddObjectToWorld(m_physicsScene.World, m_terrainBody);

        // Redo its bounding box now that it is in the world
        m_physicsScene.PE.UpdateSingleAabb(m_physicsScene.World, m_terrainBody);

        m_terrainBody.collisionType = CollisionType.Terrain;
        m_terrainBody.ApplyCollisionMask(m_physicsScene);

        if (BSParam.UseSingleSidedMeshes)
        {
            m_physicsScene.DetailLog("{0},BSTerrainMesh.settingCustomMaterial,id={1}", BSScene.DetailLogZero, id);
            m_physicsScene.PE.AddToCollisionFlags(m_terrainBody, CollisionFlags.CF_CUSTOM_MATERIAL_CALLBACK);
        }

        // Make it so the terrain will not move or be considered for movement.
        m_physicsScene.PE.ForceActivationState(m_terrainBody, ActivationState.DISABLE_SIMULATION);
    }

    public override void Dispose()
    {
        if (m_terrainBody.HasPhysicalBody)
        {
            m_physicsScene.PE.RemoveObjectFromWorld(m_physicsScene.World, m_terrainBody);
            // Frees both the body and the shape.
            m_physicsScene.PE.DestroyObject(m_physicsScene.World, m_terrainBody);
            m_terrainBody.Clear();
            m_terrainShape.Clear();
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
            m_physicsScene.Logger.WarnFormat("{0} Bad request for terrain height. terrainBase={1}, pos={2}",
                                                LogHeader, TerrainBase, pos);
            ret = BSTerrainManager.HEIGHT_GETHEIGHT_RET;
        }
        return ret;
    }

    // The passed position is relative to the base of the region.
    public override float GetWaterLevelAtXYZ(Vector3 pos)
    {
        return m_physicsScene.SimpleWaterLevel;
    }

    // Convert the passed heightmap to mesh information suitable for CreateMeshShape2().
    // Return 'true' if successfully created.
    public static bool ConvertHeightmapToMesh( BSScene physicsScene,
                                float[] heightMap, int sizeX, int sizeY,    // parameters of incoming heightmap
                                Vector3 extentBase,                         // base to be added to all vertices
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
            // One vertice per heightmap value plus the vertices off the side and bottom edge.
            int totalVertices = (sizeX + 1) * (sizeY + 1);
            vertices = new float[totalVertices * 3];
            int totalIndices = sizeX * sizeY * 6;
            indices = new int[totalIndices];

            if (physicsScene != null)
                physicsScene.DetailLog("{0},BSTerrainMesh.ConvertHeightMapToMesh,totVert={1},totInd={2},extentBase={3}",
                                    BSScene.DetailLogZero, totalVertices, totalIndices, extentBase);
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
                    vertices[verticesCount + 0] = (float)xx + extentBase.X;
                    vertices[verticesCount + 1] = (float)yy + extentBase.Y;
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
            if (physicsScene != null)
                physicsScene.Logger.ErrorFormat("{0} Failed conversion of heightmap to mesh. For={1}/{2}, e={3}",
                                                LogHeader, physicsScene.RegionName, extentBase, e);
        }

        indicesCountO = indicesCount;
        indicesO = indices;
        verticesCountO = verticesCount;
        verticesO = vertices;

        return ret;
    }

    private class HeightMapGetter
    {
        private float[] m_heightMap;
        private int m_sizeX;
        private int m_sizeY;
        public HeightMapGetter(float[] pHeightMap, int pSizeX, int pSizeY)
        {
            m_heightMap = pHeightMap;
            m_sizeX = pSizeX;
            m_sizeY = pSizeY;
        }
        // The heightmap is extended as an infinite plane at the last height
        public float GetHeight(int xx, int yy)
        {
            int offset = 0;
            // Extend the height with the height from the last row or column
            if (yy >= m_sizeY)
                if (xx >= m_sizeX)
                    offset = (m_sizeY - 1) * m_sizeX + (m_sizeX - 1);
                else
                    offset = (m_sizeY - 1) * m_sizeX + xx;
            else
                if (xx >= m_sizeX)
                    offset = yy * m_sizeX + (m_sizeX - 1);
                else
                    offset = yy * m_sizeX + xx;

            return m_heightMap[offset];
        }
    }

    // Convert the passed heightmap to mesh information suitable for CreateMeshShape2().
    // Version that handles magnification.
    // Return 'true' if successfully created.
    public static bool ConvertHeightmapToMesh2( BSScene physicsScene,
                                float[] heightMap, int sizeX, int sizeY,    // parameters of incoming heightmap
                                int magnification,                          // number of vertices per heighmap step
                                Vector3 extent,                             // dimensions of the output mesh
                                Vector3 extentBase,                         // base to be added to all vertices
                                out int indicesCountO, out int[] indicesO,
                                out int verticesCountO, out float[] verticesO)
    {
        bool ret = false;

        int indicesCount = 0;
        int verticesCount = 0;
        int[] indices = new int[0];
        float[] vertices = new float[0];

        HeightMapGetter hmap = new HeightMapGetter(heightMap, sizeX, sizeY);

        // The vertices dimension of the output mesh
        int meshX = sizeX * magnification;
        int meshY = sizeY * magnification;
        // The output size of one mesh step
        float meshXStep = extent.X / meshX;
        float meshYStep = extent.Y / meshY;

        // Create an array of vertices that is meshX+1 by meshY+1 (note the loop
        //    from zero to <= meshX). The triangle indices are then generated as two triangles
        //    per heightmap point. There are meshX by meshY of these squares. The extra row and
        //    column of vertices are used to complete the triangles of the last row and column
        //    of the heightmap.
        try
        {
            // Vertices for the output heightmap plus one on the side and bottom to complete triangles
            int totalVertices = (meshX + 1) * (meshY + 1);
            vertices = new float[totalVertices * 3];
            int totalIndices = meshX * meshY * 6;
            indices = new int[totalIndices];

            if (physicsScene != null)
                physicsScene.DetailLog("{0},BSTerrainMesh.ConvertHeightMapToMesh2,inSize={1},outSize={2},totVert={3},totInd={4},extentBase={5}",
                                    BSScene.DetailLogZero, new Vector2(sizeX, sizeY), new Vector2(meshX, meshY),
                                    totalVertices, totalIndices, extentBase);

            float minHeight = float.MaxValue;
            // Note that sizeX+1 vertices are created since there is land between this and the next region.
            // Loop through the output vertices and compute the mediun height in between the input vertices
            for (int yy = 0; yy <= meshY; yy++)
            {
                for (int xx = 0; xx <= meshX; xx++)     // Hint: the "<=" means we go around sizeX + 1 times
                {
                    float offsetY = (float)yy * (float)sizeY / (float)meshY;     // The Y that is closest to the mesh point
                    int stepY = (int)offsetY;
                    float fractionalY = offsetY - (float)stepY;
                    float offsetX = (float)xx * (float)sizeX / (float)meshX;     // The X that is closest to the mesh point
                    int stepX = (int)offsetX;
                    float fractionalX = offsetX - (float)stepX;

                    // physicsScene.DetailLog("{0},BSTerrainMesh.ConvertHeightMapToMesh2,xx={1},yy={2},offX={3},stepX={4},fractX={5},offY={6},stepY={7},fractY={8}",
                    //                 BSScene.DetailLogZero, xx, yy, offsetX, stepX, fractionalX, offsetY, stepY, fractionalY);

                    // get the four corners of the heightmap square the mesh point is in
                    float heightUL = hmap.GetHeight(stepX    , stepY    );
                    float heightUR = hmap.GetHeight(stepX + 1, stepY    );
                    float heightLL = hmap.GetHeight(stepX    , stepY + 1);
                    float heightLR = hmap.GetHeight(stepX + 1, stepY + 1);

                    // bilinear interplolation
                    float height = heightUL * (1 - fractionalX) * (1 - fractionalY)
                                 + heightUR * fractionalX       * (1 - fractionalY)
                                 + heightLL * (1 - fractionalX) * fractionalY
                                 + heightLR * fractionalX       * fractionalY;

                    // physicsScene.DetailLog("{0},BSTerrainMesh.ConvertHeightMapToMesh2,heightUL={1},heightUR={2},heightLL={3},heightLR={4},heightMap={5}",
                    //                 BSScene.DetailLogZero, heightUL, heightUR, heightLL, heightLR, height);

                    minHeight = Math.Min(minHeight, height);

                    vertices[verticesCount + 0] = (float)xx * meshXStep + extentBase.X;
                    vertices[verticesCount + 1] = (float)yy * meshYStep + extentBase.Y;
                    vertices[verticesCount + 2] = height + extentBase.Z;
                    verticesCount += 3;
                }
            }
            // The number of vertices generated
            verticesCount /= 3;

            // Loop through all the heightmap squares and create indices for the two triangles for that square
            for (int yy = 0; yy < meshY; yy++)
            {
                for (int xx = 0; xx < meshX; xx++)
                {
                    int offset = yy * (meshX + 1) + xx;
                    // Each vertices is presumed to be the upper left corner of a box of two triangles
                    indices[indicesCount + 0] = offset;
                    indices[indicesCount + 1] = offset + 1;
                    indices[indicesCount + 2] = offset + meshX + 1; // accounting for the extra column
                    indices[indicesCount + 3] = offset + 1;
                    indices[indicesCount + 4] = offset + meshX + 2;
                    indices[indicesCount + 5] = offset + meshX + 1;
                    indicesCount += 6;
                }
            }

            ret = true;
        }
        catch (Exception e)
        {
            if (physicsScene != null)
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
