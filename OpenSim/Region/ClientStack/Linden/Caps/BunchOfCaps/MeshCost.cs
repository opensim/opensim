// Proprietary code of Avination Virtual Limited
// (c) 2012 Melanie Thielker, Leal Duarte
//

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

namespace OpenSim.Region.ClientStack.Linden
{
    public class ModelCost
    {
        float ModelMinCost = 5.0f; // try to favor small meshs versus sculpts

        const float primCreationCost = 0.01f;  // 256 prims cost extra 2.56

        // weigthed size to money convertion
        const float bytecost = 1e-4f;

        // for mesh upload fees based on compressed data sizes
        // not using streaming physics and server costs as SL apparently does ??
        
        const float medSizeWth = 1f; // 2x
        const float lowSizeWth = 1.5f; // 2.5x
        const float lowestSizeWth = 2f; // 3x
        // favor potencial optimized meshs versus automatic decomposition
        const float physMeshSizeWth = 6f; // counts  7x
        const float physHullSizeWth = 8f; // counts  9x
        
        // stream cost size factors 
        const float highLodFactor = 17.36f;
        const float midLodFactor = 277.78f;
        const float lowLodFactor = 1111.11f;

        const int bytesPerCoord = 6; // 3 coords, 2 bytes per each

        private class ameshCostParam
        {
            public int highLODSize;
            public int medLODSize;
            public int lowLODSize;
            public int lowestLODSize;
            public float costFee;
            public float physicsCost;
        }

        public bool MeshModelCost(LLSDAssetResource resources, int basicCost, out int totalcost, LLSDAssetUploadResponseData meshcostdata, out string error)
        {
            totalcost = 0;
            error = string.Empty;
            
            if (resources == null ||
                resources.instance_list == null ||
                resources.instance_list.Array.Count == 0)
            {
                error = "Unable to upload mesh model. missing information.";
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
                int textures_cost = resources.texture_list.Array.Count;
                textures_cost *= basicCost;

                meshcostdata.upload_price_breakdown.texture = textures_cost;
                totalcost += textures_cost;
            }

            float meshsfee = 0;

            // meshs assets cost

            int numberMeshs = 0;
            List<ameshCostParam> meshsCosts = new List<ameshCostParam>();
            // a model could have no mesh actually
            if (resources.mesh_list != null && resources.mesh_list.Array.Count > 0)
            {
                numberMeshs = resources.mesh_list.Array.Count;
                
                for (int i = 0; i < numberMeshs; i++)
                {
                    ameshCostParam curCost = new ameshCostParam();
                    byte[] data = (byte[])resources.mesh_list.Array[i];

                    if (!MeshCost(data, curCost, out error))
                    {
                        return false;
                    }
                    meshsCosts.Add(curCost);
                    meshsfee += curCost.costFee;
                }
            }

            // instances (prims) cost
            int numberInstances = resources.instance_list.Array.Count;
            int mesh;
            for (int i = 0; i < numberInstances; i++)
            {
                Hashtable inst = (Hashtable)resources.instance_list.Array[i];

                // streamming cost
                // assume all instances have a mesh
                // but in general they can have normal prims
                // but for now that seems not suported
                // when they do, we will need to inspect pbs information 
                // and have cost funtions for all prims types
                // don't check for shape type none, since 
                // that could be used to upload meshs with low cost
                // changing later inworld

                ArrayList ascale = (ArrayList)inst["scale"];
                Vector3 scale;
                double tmp;
                tmp = (double)ascale[0];
                scale.X = (float)tmp;
                tmp = (double)ascale[1];
                scale.Y = (float)tmp;
                tmp = (double)ascale[2];
                scale.Z = (float)tmp;

                float sqdiam = scale.LengthSquared();

                mesh = (int)inst["mesh"];

                if(mesh >= numberMeshs)
                {
                    error = "Unable to upload mesh model. incoerent information.";
                    return false;
                }

                ameshCostParam curCost = meshsCosts[mesh];
                float mesh_streaming = streamingCost(curCost, sqdiam);

                meshcostdata.model_streaming_cost += mesh_streaming;

                meshcostdata.physics_cost += curCost.physicsCost;

                // unscripted and static prim server cost
                meshcostdata.simulation_cost += 0.5f;
                // charge for prims creation
                meshsfee += primCreationCost;
            }
            
            if (meshcostdata.physics_cost <= meshcostdata.model_streaming_cost)
                meshcostdata.resource_cost = meshcostdata.model_streaming_cost;
            else
                meshcostdata.resource_cost = meshcostdata.physics_cost;

            if (meshsfee < ModelMinCost)
                meshsfee = ModelMinCost;

            // scale cost with basic cost changes relative to 10
            meshsfee *= (float)basicCost / 10.0f;
            meshsfee += 0.5f; // rounding

            totalcost += (int)meshsfee;

            // breakdown prices
            // don't seem to be in use so removed code for now
            
            return true;
        }

        private bool MeshCost(byte[] data, ameshCostParam cost, out string error)
        {
            cost.highLODSize = 0;
            cost.medLODSize = 0;
            cost.lowLODSize = 0;
            cost.lowestLODSize = 0;
            cost.physicsCost = 0.0f;
            cost.costFee = 0.0f;

            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "Unable to upload mesh model. missing information.";
                return false;
            }

            OSD meshOsd = null;
            int start = 0;

            error = "Unable to upload mesh model. Invalid data";

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
                catch (Exception e)
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
                error = "Unable to upload mesh model. missing physics_convex block";
                return false;
            }

            if (!hulls(data, submesh_offset, hulls_size, out phys_hullsvertices, out phys_nhulls))
            {
                error = "Unable to upload mesh model. bad physics_convex block";
                return false;
            }

            submesh_offset = -1;
            
            // only look for LOD meshs sizes

            if (map.ContainsKey("high_lod"))
            {
                tmpmap = (OSDMap)map["high_lod"];
                // see at least if there is a offset for this one
                if (tmpmap.ContainsKey("offset"))
                    submesh_offset = tmpmap["offset"].AsInteger() + start;
                if (tmpmap.ContainsKey("size"))
                    highlod_size = tmpmap["size"].AsInteger();
            }

            if (submesh_offset < 0 || highlod_size <= 0)
            {
                error = "Unable to upload mesh model. missing high_lod";
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
            cost.medLODSize = medlod_size;
            cost.lowLODSize = lowlod_size;
            cost.lowestLODSize = lowestlod_size;

            submesh_offset = -1;

            if (map.ContainsKey("physics_mesh"))
            {
                tmpmap = (OSDMap)map["physics_mesh"];
                if (tmpmap.ContainsKey("offset"))
                    submesh_offset = tmpmap["offset"].AsInteger() + start;
                if (tmpmap.ContainsKey("size"))
                    physmesh_size = tmpmap["size"].AsInteger();

                if (submesh_offset >= 0 || physmesh_size > 0)
                {

                    if (!submesh(data, submesh_offset, physmesh_size, out phys_ntriangles))
                    {
                        error = "Unable to upload mesh model. parsing error";
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

        private bool submesh(byte[] data, int offset, int size, out int ntriangles)
        {
            ntriangles = 0;

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
            catch (Exception e)
            {
                return false;
            }

            OSDArray decodedMeshOsdArray = null;
            if ((!decodedMeshOsd is OSDArray))
                return false;

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
                }
            }

            return true;
        }

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
            catch (Exception e)
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
