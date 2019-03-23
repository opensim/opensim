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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Capabilities;

using ComponentAce.Compression.Libs.zlib;

using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using Nini.Config;

namespace OpenSim.Region.ClientStack.Linden
{
    public struct ModelPrimLimits
    {

    }

    public class ModelCost
    {

        // upload fee defaults
        // fees are normalized to 1.0
        // this parameters scale them to basic cost ( so 1.0 translates to 10 )

        public float ModelMeshCostFactor = 0.0f; // scale total cost relative to basic (excluding textures)
        public float ModelTextureCostFactor = 1.0f; // scale textures fee to basic.
        public float ModelMinCostFactor = 0.0f; // 0.5f; // minimum total model free excluding textures

        // itens costs in normalized values
        // ie will be multiplied by basicCost and factors above
        public float primCreationCost = 0.002f;  // extra cost for each prim creation overhead
        // weigthed size to normalized cost
        public float bytecost = 1e-5f;

        // mesh upload fees based on compressed data sizes
        // several data sections are counted more that once
        // to promote user optimization
        // following parameters control how many extra times they are added
        // to global size.
        // LOD meshs
        const float medSizeWth = 1f; // 2x
        const float lowSizeWth = 1.5f; // 2.5x
        const float lowestSizeWth = 2f; // 3x
        // favor potencially physical optimized meshs versus automatic decomposition
        const float physMeshSizeWth = 6f; // counts  7x
        const float physHullSizeWth = 8f; // counts  9x

        // stream cost area factors
        // more or less like SL
        const float highLodFactor = 17.36f;
        const float midLodFactor = 277.78f;
        const float lowLodFactor = 1111.11f;

        // physics cost is below, identical to SL, assuming shape type convex
        // server cost is below identical to SL assuming non scripted non physical object

        // internal
        const int bytesPerCoord = 6; // 3 coords, 2 bytes per each

        // control prims dimensions
        public float PrimScaleMin = 0.001f;
        public float NonPhysicalPrimScaleMax = 256f;
        public float PhysicalPrimScaleMax = 10f;
        public int ObjectLinkedPartsMax = 512;


        public ModelCost(Scene scene)
        {
            PrimScaleMin = scene.m_minNonphys;
            NonPhysicalPrimScaleMax = scene.m_maxNonphys;
            PhysicalPrimScaleMax = scene.m_maxPhys;
            ObjectLinkedPartsMax = scene.m_linksetCapacity;
        }

        public void Econfig(IConfig EconomyConfig)
        {
            ModelMeshCostFactor = EconomyConfig.GetFloat("MeshModelUploadCostFactor", ModelMeshCostFactor);
            ModelTextureCostFactor = EconomyConfig.GetFloat("MeshModelUploadTextureCostFactor", ModelTextureCostFactor);
            ModelMinCostFactor = EconomyConfig.GetFloat("MeshModelMinCostFactor", ModelMinCostFactor);
                    // next 2 are normalized so final cost is afected by modelUploadFactor above and normal cost
            primCreationCost = EconomyConfig.GetFloat("ModelPrimCreationCost", primCreationCost);
            bytecost = EconomyConfig.GetFloat("ModelMeshByteCost", bytecost);
        }

        // storage for a single mesh asset cost parameters
        private class ameshCostParam
        {
            // LOD sizes for size dependent streaming cost
            public int highLODSize;
            public int medLODSize;
            public int lowLODSize;
            public int lowestLODSize;
            public int highLODsides;
            // normalized fee based on compressed data sizes
            public float costFee;
            // physics cost
            public float physicsCost;
        }

        // calculates a mesh model costs
        // returns false on error, with a reason on parameter error
        // resources input LLSD request
        // basicCost input region assets upload cost
        // totalcost returns model total upload fee
        // meshcostdata returns detailed costs for viewer
        // avatarSkeleton if mesh includes a avatar skeleton
        // useAvatarCollider if we should use physics mesh for avatar
        public bool MeshModelCost(LLSDAssetResource resources, int basicCost, out int totalcost,
            LLSDAssetUploadResponseData meshcostdata, out string error, ref string warning)
        {
            totalcost = 0;
            error = string.Empty;

            bool avatarSkeleton = false;

            if (resources == null ||
                resources.instance_list == null ||
                resources.instance_list.Array.Count == 0)
            {
                error = "missing model information.";
                return false;
            }

            int numberInstances = resources.instance_list.Array.Count;

            if (ObjectLinkedPartsMax != 0 && numberInstances > ObjectLinkedPartsMax)
            {
                error = "Model would have more than " + ObjectLinkedPartsMax.ToString() + " linked prims";
                return false;
            }

            meshcostdata.model_streaming_cost = 0.0;
            meshcostdata.simulation_cost = 0.0;
            meshcostdata.physics_cost = 0.0;
            meshcostdata.resource_cost = 0.0;

            meshcostdata.upload_price_breakdown.mesh_instance = 0;
            meshcostdata.upload_price_breakdown.mesh_physics = 0;
            meshcostdata.upload_price_breakdown.mesh_streaming = 0;
            meshcostdata.upload_price_breakdown.model = 0;

            int itmp;

            // textures cost
            if (resources.texture_list != null && resources.texture_list.Array.Count > 0)
            {
                float textures_cost = (float)(resources.texture_list.Array.Count * basicCost);
                textures_cost *= ModelTextureCostFactor;

                itmp = (int)(textures_cost + 0.5f); // round
                meshcostdata.upload_price_breakdown.texture = itmp;
                totalcost += itmp;
            }

            // meshs assets cost
            float meshsfee = 0;
            int numberMeshs = 0;
            bool haveMeshs = false;

            bool curskeleton;
            bool curAvatarPhys;

            List<ameshCostParam> meshsCosts = new List<ameshCostParam>();

            if (resources.mesh_list != null && resources.mesh_list.Array.Count > 0)
            {
                numberMeshs = resources.mesh_list.Array.Count;

                for (int i = 0; i < numberMeshs; i++)
                {
                    ameshCostParam curCost = new ameshCostParam();
                    byte[] data = (byte[])resources.mesh_list.Array[i];

                    if (!MeshCost(data, curCost, out curskeleton, out curAvatarPhys, out error))
                    {
                        return false;
                    }

                    if (curskeleton)
                    {
                        if (avatarSkeleton)
                        {
                            error = "model can only contain a avatar skeleton";
                            return false;
                        }
                        avatarSkeleton = true;
                    }
                    meshsCosts.Add(curCost);
                    meshsfee += curCost.costFee;
                }
                haveMeshs = true;
            }

            // instances (prims) cost


            int mesh;
            int skipedSmall = 0;
            for (int i = 0; i < numberInstances; i++)
            {
                Hashtable inst = (Hashtable)resources.instance_list.Array[i];

                ArrayList ascale = (ArrayList)inst["scale"];
                Vector3 scale;
                double tmp;
                tmp = (double)ascale[0];
                scale.X = (float)tmp;
                tmp = (double)ascale[1];
                scale.Y = (float)tmp;
                tmp = (double)ascale[2];
                scale.Z = (float)tmp;

                if (scale.X < PrimScaleMin || scale.Y < PrimScaleMin || scale.Z < PrimScaleMin)
                {
                    skipedSmall++;
                    continue;
                }

                if (scale.X > NonPhysicalPrimScaleMax || scale.Y > NonPhysicalPrimScaleMax || scale.Z > NonPhysicalPrimScaleMax)
                {
                    error = "Model contains parts with sides larger than " + NonPhysicalPrimScaleMax.ToString() + "m. Please ajust scale";
                    return false;
                }
                int nfaces = 0;
                if(inst.Contains("face_list"))
                {
                     nfaces = ((ArrayList)inst["face_list"]).Count;
                }

                if (haveMeshs && inst.ContainsKey("mesh"))
                {
                    mesh = (int)inst["mesh"];

                    if (mesh >= numberMeshs)
                    {
                        error = "Incoerent model information.";
                        return false;
                    }

                    // streamming cost

                    float sqdiam = scale.LengthSquared();

                    ameshCostParam curCost = meshsCosts[mesh];
                    if(nfaces != curCost.highLODsides)
                        warning +="Warning: Uploaded number of faces ( "+ nfaces.ToString() +" ) does not match highlod number of faces ( "+ curCost.highLODsides.ToString() +" )\n";

                    float mesh_streaming = streamingCost(curCost, sqdiam);

                    meshcostdata.model_streaming_cost += mesh_streaming;
                    meshcostdata.physics_cost += curCost.physicsCost;
                }
                else // instance as no mesh ??
                {
                    // to do later if needed
                    meshcostdata.model_streaming_cost += 0.5f;
                    meshcostdata.physics_cost += 1.0f;
                }

                // assume unscripted and static prim server cost
                meshcostdata.simulation_cost += 0.5f;
                // charge for prims creation
                meshsfee += primCreationCost;
            }

            if (skipedSmall > 0)
            {
                if (skipedSmall > numberInstances / 2)
                {
                    error = "Model contains too many prims smaller than " + PrimScaleMin.ToString() +
                        "m minimum allowed size. Please check scalling";
                    return false;
                }
                else
                    warning += skipedSmall.ToString() + " of the requested " +numberInstances.ToString() +
                        " model prims will not upload because they are smaller than " + PrimScaleMin.ToString() +
                        "m minimum allowed size. Please check scalling ";
            }

            if (meshcostdata.physics_cost <= meshcostdata.model_streaming_cost)
                meshcostdata.resource_cost = meshcostdata.model_streaming_cost;
            else
                meshcostdata.resource_cost = meshcostdata.physics_cost;

            if (meshcostdata.resource_cost < meshcostdata.simulation_cost)
                meshcostdata.resource_cost = meshcostdata.simulation_cost;

            // scale cost
            // at this point a cost of 1.0 whould mean basic cost
            meshsfee *= ModelMeshCostFactor;

            if (meshsfee < ModelMinCostFactor)
                meshsfee = ModelMinCostFactor;

            // actually scale it to basic cost
            meshsfee *= (float)basicCost;

            meshsfee += 0.5f; // rounding

            totalcost += (int)meshsfee;

            // breakdown prices
            // don't seem to be in use so removed code for now

            return true;
        }

        // single mesh asset cost
        private bool MeshCost(byte[] data, ameshCostParam cost,out bool skeleton, out bool avatarPhys, out string error)
        {
            cost.highLODSize = 0;
            cost.highLODsides = 0;
            cost.medLODSize = 0;
            cost.lowLODSize = 0;
            cost.lowestLODSize = 0;
            cost.physicsCost = 0.0f;
            cost.costFee = 0.0f;

            error = string.Empty;

            skeleton = false;
            avatarPhys = false;

            if (data == null || data.Length == 0)
            {
                error = "Missing model information.";
                return false;
            }

            OSD meshOsd = null;
            int start = 0;

            error = "Invalid model data";

            using (MemoryStream ms = new MemoryStream(data))
            {
                try
                {
                    OSD osd = OSDParser.DeserializeLLSDBinary(ms);
                    if (osd is OSDMap)
                        meshOsd = (OSDMap)osd;
                    else
                        return false;
                }
                catch
                {
                    return false;
                }
                start = (int)ms.Position;
            }

            OSDMap map = (OSDMap)meshOsd;
            OSDMap tmpmap;

            int highlod_size = 0;
            int medlod_size = 0;
            int lowlod_size = 0;
            int lowestlod_size = 0;
            int skin_size = 0;

            int hulls_size = 0;
            int phys_nhulls;
            int phys_hullsvertices = 0;

            int physmesh_size = 0;
            int phys_ntriangles = 0;

            int submesh_offset = -1;

            if (map.ContainsKey("skeleton"))
            {
                tmpmap = (OSDMap)map["skeleton"];
                if (tmpmap.ContainsKey("offset") && tmpmap.ContainsKey("size"))
                {
                    int sksize = tmpmap["size"].AsInteger();
                    if(sksize > 0)
                        skeleton = true;
                }
            }

            if (map.ContainsKey("physics_convex"))
            {
                tmpmap = (OSDMap)map["physics_convex"];
                if (tmpmap.ContainsKey("offset"))
                    submesh_offset = tmpmap["offset"].AsInteger() + start;
                if (tmpmap.ContainsKey("size"))
                    hulls_size = tmpmap["size"].AsInteger();
            }

            if (submesh_offset < 0 || hulls_size == 0)
            {
                error = "Missing physics_convex block";
                return false;
            }

            if (!hulls(data, submesh_offset, hulls_size, out phys_hullsvertices, out phys_nhulls))
            {
                error = "Bad physics_convex block";
                return false;
            }

            submesh_offset = -1;

            int nsides = 0;
            int lod_ntriangles = 0;

            if (map.ContainsKey("high_lod"))
            {
                tmpmap = (OSDMap)map["high_lod"];
                // see at least if there is a offset for this one
                if (tmpmap.ContainsKey("offset"))
                    submesh_offset = tmpmap["offset"].AsInteger() + start;
                if (tmpmap.ContainsKey("size"))
                    highlod_size = tmpmap["size"].AsInteger();

                if (submesh_offset >= 0 && highlod_size > 0)
                {
                    if (!submesh(data, submesh_offset, highlod_size, out lod_ntriangles, out nsides))
                    {
                        error = "Model data parsing error";
                        return false;
                    }
                }
            }

            if (submesh_offset < 0 || highlod_size <= 0)
            {
                error = "Missing high_lod block";
                return false;
            }

            bool haveprev = true;

            if (map.ContainsKey("medium_lod"))
            {
                tmpmap = (OSDMap)map["medium_lod"];
                if (tmpmap.ContainsKey("size"))
                    medlod_size = tmpmap["size"].AsInteger();
                else
                    haveprev = false;
            }

            if (haveprev && map.ContainsKey("low_lod"))
            {
                tmpmap = (OSDMap)map["low_lod"];
                if (tmpmap.ContainsKey("size"))
                    lowlod_size = tmpmap["size"].AsInteger();
                else
                    haveprev = false;
            }

            if (haveprev && map.ContainsKey("lowest_lod"))
            {
                tmpmap = (OSDMap)map["lowest_lod"];
                if (tmpmap.ContainsKey("size"))
                    lowestlod_size = tmpmap["size"].AsInteger();
            }

            if (map.ContainsKey("skin"))
            {
                tmpmap = (OSDMap)map["skin"];
                if (tmpmap.ContainsKey("size"))
                    skin_size = tmpmap["size"].AsInteger();
            }

            cost.highLODSize = highlod_size;
            cost.highLODsides = nsides;
            cost.medLODSize = medlod_size;
            cost.lowLODSize = lowlod_size;
            cost.lowestLODSize = lowestlod_size;

            submesh_offset = -1;

            tmpmap = null;
            if(map.ContainsKey("physics_mesh"))
                tmpmap = (OSDMap)map["physics_mesh"];
            else if (map.ContainsKey("physics_shape")) // old naming
                tmpmap = (OSDMap)map["physics_shape"];

            int phys_nsides = 0;
            if(tmpmap != null)
            {
                if (tmpmap.ContainsKey("offset"))
                    submesh_offset = tmpmap["offset"].AsInteger() + start;
                if (tmpmap.ContainsKey("size"))
                    physmesh_size = tmpmap["size"].AsInteger();

                if (submesh_offset >= 0 && physmesh_size > 0)
                {
                    if (!submesh(data, submesh_offset, physmesh_size, out phys_ntriangles, out phys_nsides))
                    {
                        error = "Model data parsing error";
                        return false;
                    }
                }
            }

            // upload is done in convex shape type so only one hull
            phys_hullsvertices++;
            cost.physicsCost = 0.04f * phys_hullsvertices;

            float sfee;

            sfee = data.Length; // start with total compressed data size

            // penalize lod meshs that should be more builder optimized
            sfee += medSizeWth * medlod_size;
            sfee += lowSizeWth * lowlod_size;
            sfee += lowestSizeWth * lowlod_size;

            // physics
            // favor potencial optimized meshs versus automatic decomposition
            if (physmesh_size != 0)
                sfee += physMeshSizeWth * (physmesh_size + hulls_size / 4); // reduce cost of mandatory convex hull
            else
                sfee += physHullSizeWth * hulls_size;

            // bytes to money
            sfee *= bytecost;

            cost.costFee = sfee;
            return true;
        }

        // parses a LOD or physics mesh component
        private bool submesh(byte[] data, int offset, int size, out int ntriangles, out int nsides)
        {
            ntriangles = 0;
            nsides = 0;

            OSD decodedMeshOsd = new OSD();
            byte[] meshBytes = new byte[size];
            System.Buffer.BlockCopy(data, offset, meshBytes, 0, size);
            try
            {
                using (MemoryStream inMs = new MemoryStream(meshBytes))
                {
                    using (MemoryStream outMs = new MemoryStream())
                    {
                        using (ZOutputStream zOut = new ZOutputStream(outMs))
                        {
                            byte[] readBuffer = new byte[4096];
                            int readLen = 0;
                            while ((readLen = inMs.Read(readBuffer, 0, readBuffer.Length)) > 0)
                            {
                                zOut.Write(readBuffer, 0, readLen);
                            }
                            zOut.Flush();
                            outMs.Seek(0, SeekOrigin.Begin);

                            byte[] decompressedBuf = outMs.GetBuffer();
                            decodedMeshOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            OSDArray decodedMeshOsdArray = null;

            byte[] dummy;

            decodedMeshOsdArray = (OSDArray)decodedMeshOsd;
            foreach (OSD subMeshOsd in decodedMeshOsdArray)
            {
                if (subMeshOsd is OSDMap)
                {
                    OSDMap subtmpmap = (OSDMap)subMeshOsd;
                    if (subtmpmap.ContainsKey("NoGeometry") && ((OSDBoolean)subtmpmap["NoGeometry"]))
                        continue;

                    if (!subtmpmap.ContainsKey("Position"))
                        return false;

                    if (subtmpmap.ContainsKey("TriangleList"))
                    {
                        dummy = subtmpmap["TriangleList"].AsBinary();
                        ntriangles += dummy.Length / bytesPerCoord;
                    }
                    else
                        return false;
                    nsides++;
                }
            }

            return true;
        }

        // parses convex hulls component
        private bool hulls(byte[] data, int offset, int size, out int nvertices, out int nhulls)
        {
            nvertices = 0;
            nhulls = 1;

            OSD decodedMeshOsd = new OSD();
            byte[] meshBytes = new byte[size];
            System.Buffer.BlockCopy(data, offset, meshBytes, 0, size);
            try
            {
                using (MemoryStream inMs = new MemoryStream(meshBytes))
                {
                    using (MemoryStream outMs = new MemoryStream())
                    {
                        using (ZOutputStream zOut = new ZOutputStream(outMs))
                        {
                            byte[] readBuffer = new byte[4096];
                            int readLen = 0;
                            while ((readLen = inMs.Read(readBuffer, 0, readBuffer.Length)) > 0)
                            {
                                zOut.Write(readBuffer, 0, readLen);
                            }
                            zOut.Flush();
                            outMs.Seek(0, SeekOrigin.Begin);

                            byte[] decompressedBuf = outMs.GetBuffer();
                            decodedMeshOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            OSDMap cmap = (OSDMap)decodedMeshOsd;
            if (cmap == null)
                return false;

            byte[] dummy;

            // must have one of this
            if (cmap.ContainsKey("BoundingVerts"))
            {
                dummy = cmap["BoundingVerts"].AsBinary();
                nvertices = dummy.Length / bytesPerCoord;
            }
            else
                return false;

/* upload is done with convex shape type
            if (cmap.ContainsKey("HullList"))
            {
                dummy = cmap["HullList"].AsBinary();
                nhulls += dummy.Length;
            }


            if (cmap.ContainsKey("Positions"))
            {
                dummy = cmap["Positions"].AsBinary();
                nvertices = dummy.Length / bytesPerCoord;
            }
 */

            return true;
        }

        // returns streaming cost from on mesh LODs sizes in curCost and square of prim size length
        private float streamingCost(ameshCostParam curCost, float sqdiam)
        {
            // compute efective areas
            float ma = 262144f;

            float mh = sqdiam * highLodFactor;
            if (mh > ma)
                mh = ma;
            float mm = sqdiam * midLodFactor;
            if (mm > ma)
                mm = ma;

            float ml = sqdiam * lowLodFactor;
            if (ml > ma)
                ml = ma;

            float mlst = ma;

            mlst -= ml;
            ml -= mm;
            mm -= mh;

            if (mlst < 1.0f)
                mlst = 1.0f;
            if (ml < 1.0f)
                ml = 1.0f;
            if (mm < 1.0f)
                mm = 1.0f;
            if (mh < 1.0f)
                mh = 1.0f;

            ma = mlst + ml + mm + mh;

            // get LODs compressed sizes
            // giving 384 bytes bonus
            int lst = curCost.lowestLODSize - 384;
            int l = curCost.lowLODSize - 384;
            int m = curCost.medLODSize - 384;
            int h = curCost.highLODSize - 384;

            // use previus higher LOD size on missing ones
            if (m <= 0)
                m = h;
            if (l <= 0)
                l = m;
            if (lst <= 0)
                lst = l;

            // force minumum sizes
            if (lst < 16)
                lst = 16;
            if (l < 16)
                l = 16;
            if (m < 16)
                m = 16;
            if (h < 16)
                h = 16;

            // compute cost weighted by relative effective areas
            float cost = (float)lst * mlst + (float)l * ml + (float)m * mm + (float)h * mh;
            cost /= ma;

            cost *= 0.004f; // overall tunning parameter

            return cost;
        }
    }
}
