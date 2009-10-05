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
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Region Server
    /// </summary>
    public class MySQLDataStore : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ConnectionString;

        private MySqlConnection m_Connection = null;

        public void Initialise(string connectionString)
        {
            m_ConnectionString = connectionString;

            m_Connection = new MySqlConnection(m_ConnectionString);

            m_Connection.Open();

            // Apply new Migrations
            //
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(m_Connection, assem, "RegionStore");
            m.Update();

            // Clean dropped attachments
            //
            MySqlCommand cmd = m_Connection.CreateCommand();
            cmd.CommandText = "delete from prims, primshapes using prims " +
                    "left join primshapes on prims.uuid = primshapes.uuid " +
                    "where PCode = 9 and State <> 0";
            ExecuteNonQuery(cmd);
            cmd.Dispose();
        }

        private IDataReader ExecuteReader(MySqlCommand c)
        {
            IDataReader r = null;
            bool errorSeen = false;

            while (true)
            {
                try
                {
                    r = c.ExecuteReader();
                }
                catch (Exception)
                {
                    Thread.Sleep(500);

                    m_Connection.Close();
                    m_Connection = (MySqlConnection) ((ICloneable)m_Connection).Clone();
                    m_Connection.Open();
                    c.Connection = m_Connection;

                    if (!errorSeen)
                    {
                        errorSeen = true;
                        continue;
                    }
                    throw;
                }

                break;
            }

            return r;
        }

        private void ExecuteNonQuery(MySqlCommand c)
        {
            bool errorSeen = false;

            while (true)
            {
                try
                {
                    c.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    Thread.Sleep(500);

                    m_Connection.Close();
                    m_Connection = (MySqlConnection) ((ICloneable)m_Connection).Clone();
                    m_Connection.Open();
                    c.Connection = m_Connection;

                    if (!errorSeen)
                    {
                        errorSeen = true;
                        continue;
                    }
                    throw;
                }

                break;
            }
        }

        public void Dispose() {}

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            uint flags = obj.RootPart.GetEffectiveObjectFlags();

            // Eligibility check
            //
            if ((flags & (uint)PrimFlags.Temporary) != 0)
                return;
            if ((flags & (uint)PrimFlags.TemporaryOnRez) != 0)
                return;

            lock (m_Connection)
            {
                MySqlCommand cmd = m_Connection.CreateCommand();

                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    cmd.Parameters.Clear();

                    cmd.CommandText = "replace into prims ("+
                            "UUID, CreationDate, "+
                            "Name, Text, Description, "+
                            "SitName, TouchName, ObjectFlags, "+
                            "OwnerMask, NextOwnerMask, GroupMask, "+
                            "EveryoneMask, BaseMask, PositionX, "+
                            "PositionY, PositionZ, GroupPositionX, "+
                            "GroupPositionY, GroupPositionZ, VelocityX, "+
                            "VelocityY, VelocityZ, AngularVelocityX, "+
                            "AngularVelocityY, AngularVelocityZ, "+
                            "AccelerationX, AccelerationY, "+
                            "AccelerationZ, RotationX, "+
                            "RotationY, RotationZ, "+
                            "RotationW, SitTargetOffsetX, "+
                            "SitTargetOffsetY, SitTargetOffsetZ, "+
                            "SitTargetOrientW, SitTargetOrientX, "+
                            "SitTargetOrientY, SitTargetOrientZ, "+
                            "RegionUUID, CreatorID, "+
                            "OwnerID, GroupID, "+
                            "LastOwnerID, SceneGroupID, "+
                            "PayPrice, PayButton1, "+
                            "PayButton2, PayButton3, "+
                            "PayButton4, LoopedSound, "+
                            "LoopedSoundGain, TextureAnimation, "+
                            "OmegaX, OmegaY, OmegaZ, "+
                            "CameraEyeOffsetX, CameraEyeOffsetY, "+
                            "CameraEyeOffsetZ, CameraAtOffsetX, "+
                            "CameraAtOffsetY, CameraAtOffsetZ, "+
                            "ForceMouselook, ScriptAccessPin, "+
                            "AllowedDrop, DieAtEdge, "+
                            "SalePrice, SaleType, "+
                            "ColorR, ColorG, ColorB, ColorA, "+
                            "ParticleSystem, ClickAction, Material, "+
                            "CollisionSound, CollisionSoundVolume, "+
                            "PassTouches, "+
                            "LinkNumber) values (" + "?UUID, "+
                            "?CreationDate, ?Name, ?Text, "+
                            "?Description, ?SitName, ?TouchName, "+
                            "?ObjectFlags, ?OwnerMask, ?NextOwnerMask, "+
                            "?GroupMask, ?EveryoneMask, ?BaseMask, "+
                            "?PositionX, ?PositionY, ?PositionZ, "+
                            "?GroupPositionX, ?GroupPositionY, "+
                            "?GroupPositionZ, ?VelocityX, "+
                            "?VelocityY, ?VelocityZ, ?AngularVelocityX, "+
                            "?AngularVelocityY, ?AngularVelocityZ, "+
                            "?AccelerationX, ?AccelerationY, "+
                            "?AccelerationZ, ?RotationX, "+
                            "?RotationY, ?RotationZ, "+
                            "?RotationW, ?SitTargetOffsetX, "+
                            "?SitTargetOffsetY, ?SitTargetOffsetZ, "+
                            "?SitTargetOrientW, ?SitTargetOrientX, "+
                            "?SitTargetOrientY, ?SitTargetOrientZ, "+
                            "?RegionUUID, ?CreatorID, ?OwnerID, "+
                            "?GroupID, ?LastOwnerID, ?SceneGroupID, "+
                            "?PayPrice, ?PayButton1, ?PayButton2, "+
                            "?PayButton3, ?PayButton4, ?LoopedSound, "+
                            "?LoopedSoundGain, ?TextureAnimation, "+
                            "?OmegaX, ?OmegaY, ?OmegaZ, "+
                            "?CameraEyeOffsetX, ?CameraEyeOffsetY, "+
                            "?CameraEyeOffsetZ, ?CameraAtOffsetX, "+
                            "?CameraAtOffsetY, ?CameraAtOffsetZ, "+
                            "?ForceMouselook, ?ScriptAccessPin, "+
                            "?AllowedDrop, ?DieAtEdge, ?SalePrice, "+
                            "?SaleType, ?ColorR, ?ColorG, "+
                            "?ColorB, ?ColorA, ?ParticleSystem, "+
                            "?ClickAction, ?Material, ?CollisionSound, "+
                            "?CollisionSoundVolume, ?PassTouches, ?LinkNumber)";

                    FillPrimCommand(cmd, prim, obj.UUID, regionUUID);

                    ExecuteNonQuery(cmd);

                    cmd.Parameters.Clear();

                    cmd.CommandText = "replace into primshapes ("+
                            "UUID, Shape, ScaleX, ScaleY, "+
                            "ScaleZ, PCode, PathBegin, PathEnd, "+
                            "PathScaleX, PathScaleY, PathShearX, "+
                            "PathShearY, PathSkew, PathCurve, "+
                            "PathRadiusOffset, PathRevolutions, "+
                            "PathTaperX, PathTaperY, PathTwist, "+
                            "PathTwistBegin, ProfileBegin, ProfileEnd, "+
                            "ProfileCurve, ProfileHollow, Texture, "+
                            "ExtraParams, State) values (?UUID, "+
                            "?Shape, ?ScaleX, ?ScaleY, ?ScaleZ, "+
                            "?PCode, ?PathBegin, ?PathEnd, "+
                            "?PathScaleX, ?PathScaleY, "+
                            "?PathShearX, ?PathShearY, "+
                            "?PathSkew, ?PathCurve, ?PathRadiusOffset, "+
                            "?PathRevolutions, ?PathTaperX, "+
                            "?PathTaperY, ?PathTwist, "+
                            "?PathTwistBegin, ?ProfileBegin, "+
                            "?ProfileEnd, ?ProfileCurve, "+
                            "?ProfileHollow, ?Texture, ?ExtraParams, "+
                            "?State)";

                    FillShapeCommand(cmd, prim);

                    ExecuteNonQuery(cmd);
                }
                cmd.Dispose();
            }
        }

        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            List<UUID> uuids = new List<UUID>();

            // Formerly, this used to check the region UUID.
            // That makes no sense, as we remove the contents of a prim
            // unconditionally, but the prim dependent on the region ID.
            // So, we would destroy an object and cause hard to detect
            // issues if we delete the contents only. Deleting it all may
            // cause the loss of a prim, but is cleaner.
            // It's also faster because it uses the primary key.
            //
            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select UUID from prims where SceneGroupID= ?UUID";
                    cmd.Parameters.AddWithValue("UUID", obj.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        while (reader.Read())
                            uuids.Add(new UUID(reader["UUID"].ToString()));
                    }

                    // delete the main prims
                    cmd.CommandText = "delete from prims where SceneGroupID= ?UUID";
                    ExecuteNonQuery(cmd);
                }
            }

            // there is no way this should be < 1 unless there is
            // a very corrupt database, but in that case be extra
            // safe anyway.
            if (uuids.Count > 0)
            {
                RemoveShapes(uuids);
                RemoveItems(uuids);
            }
        }

        /// <summary>
        /// Remove all persisted items of the given prim.
        /// The caller must acquire the necessrary synchronization locks
        /// </summary>
        /// <param name="uuid">the Item UUID</param>
        private void RemoveItems(UUID uuid)
        {
            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "delete from primitems where PrimID = ?PrimID";
                    cmd.Parameters.AddWithValue("PrimID", uuid.ToString());

                    ExecuteNonQuery(cmd);
                }
            }
        }

        /// <summary>
        /// Remove all persisted shapes for a list of prims
        /// The caller must acquire the necessrary synchronization locks
        /// </summary>
        /// <param name="uuids">the list of UUIDs</param>
        private void RemoveShapes(List<UUID> uuids)
        {
            lock (m_Connection)
            {
                string sql = "delete from primshapes where ";

                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    for (int i = 0; i < uuids.Count; i++)
                    {
                        if ((i + 1) == uuids.Count)
                        {// end of the list
                            sql += "(UUID = ?UUID" + i + ")";
                        }
                        else
                        {
                            sql += "(UUID = ?UUID" + i + ") or ";
                        }
                    }
                    cmd.CommandText = sql;

                    for (int i = 0; i < uuids.Count; i++)
                        cmd.Parameters.AddWithValue("UUID" + i, uuids[i].ToString());

                    ExecuteNonQuery(cmd);
                }
            }
        }

        /// <summary>
        /// Remove all persisted items for a list of prims
        /// The caller must acquire the necessrary synchronization locks
        /// </summary>
        /// <param name="uuids">the list of UUIDs</param>
        private void RemoveItems(List<UUID> uuids)
        {
            lock (m_Connection)
            {
                string sql = "delete from primitems where ";

                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    for (int i = 0; i < uuids.Count; i++)
                    {
                        if ((i + 1) == uuids.Count)
                        {
                            // end of the list
                            sql += "(PrimID = ?PrimID" + i + ")";
                        }
                        else
                        {
                            sql += "(PrimID = ?PrimID" + i + ") or ";
                        }
                    }
                    cmd.CommandText = sql;

                    for (int i = 0; i < uuids.Count; i++)
                        cmd.Parameters.AddWithValue("PrimID" + i, uuids[i].ToString());

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            UUID lastGroupID = UUID.Zero;
            Dictionary<UUID, SceneObjectGroup> objects = new Dictionary<UUID, SceneObjectGroup>();
            Dictionary<UUID, SceneObjectPart> prims = new Dictionary<UUID, SceneObjectPart>();
            SceneObjectGroup grp = null;

            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select *, " +
                        "case when prims.UUID = SceneGroupID " +
                        "then 0 else 1 end as sort from prims " +
                        "left join primshapes on prims.UUID = primshapes.UUID " +
                        "where RegionUUID = ?RegionUUID " +
                        "order by SceneGroupID asc, sort asc, LinkNumber asc";

                    cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            SceneObjectPart prim = BuildPrim(reader);
                            if (reader["Shape"] is DBNull)
                                prim.Shape = PrimitiveBaseShape.Default;
                            else
                                prim.Shape = BuildShape(reader);

                            prims[prim.UUID] = prim;

                            UUID groupID = new UUID(reader["SceneGroupID"].ToString());

                            if (groupID != lastGroupID) // New SOG
                            {
                                if (grp != null)
                                    objects[grp.UUID] = grp;

                                lastGroupID = groupID;

                                // There sometimes exist OpenSim bugs that 'orphan groups' so that none of the prims are
                                // recorded as the root prim (for which the UUID must equal the persisted group UUID).  In
                                // this case, force the UUID to be the same as the group UUID so that at least these can be
                                // deleted (we need to change the UUID so that any other prims in the linkset can also be 
                                // deleted).
                                if (prim.UUID != groupID && groupID != UUID.Zero)
                                {
                                    m_log.WarnFormat(
                                        "[REGION DB]: Found root prim {0} {1} at {2} where group was actually {3}.  Forcing UUID to group UUID",
                                        prim.Name, prim.UUID, prim.GroupPosition, groupID);

                                    prim.UUID = groupID;
                                }

                                grp = new SceneObjectGroup(prim);
                            }
                            else
                            {
                                // Black magic to preserve link numbers
                                //
                                int link = prim.LinkNum;

                                grp.AddPart(prim);

                                if (link != 0)
                                    prim.LinkNum = link;
                            }
                        }
                    }

                    if (grp != null)
                        objects[grp.UUID] = grp;
                }
            }

            // Instead of attempting to LoadItems on every prim,
            // most of which probably have no items... get a 
            // list from DB of all prims which have items and
            // LoadItems only on those
            List<SceneObjectPart> primsWithInventory = new List<SceneObjectPart>();
            lock (m_Connection)
            {
                using (MySqlCommand itemCmd = m_Connection.CreateCommand())
                {
                    itemCmd.CommandText = "select distinct primID from primitems";
                    using (IDataReader itemReader = ExecuteReader(itemCmd))
                    {
                        while (itemReader.Read())
                        {
                            if (!(itemReader["primID"] is DBNull))
                            {
                                UUID primID = new UUID(itemReader["primID"].ToString());
                                if (prims.ContainsKey(primID))
                                {
                                    primsWithInventory.Add(prims[primID]);
                                }
                            }
                        }
                    }
                }
            }

            foreach (SceneObjectPart prim in primsWithInventory)
                LoadItems(prim);

            m_log.DebugFormat("[REGION DB]: Loaded {0} objects using {1} prims", objects.Count, prims.Count);
            return new List<SceneObjectGroup>(objects.Values);
        }

        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim">The prim</param>
        private void LoadItems(SceneObjectPart prim)
        {
            lock (m_Connection)
            {
                List<TaskInventoryItem> inventory = new List<TaskInventoryItem>();

                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select * from primitems where PrimID = ?PrimID";
                    cmd.Parameters.AddWithValue("PrimID", prim.UUID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            TaskInventoryItem item = BuildItem(reader);

                            item.ParentID = prim.UUID; // Values in database are often wrong
                            inventory.Add(item);
                        }
                    }
                }

                prim.Inventory.RestoreInventoryItems(inventory);
            }
        }

        public void StoreTerrain(double[,] ter, UUID regionID)
        {
            m_log.Info("[REGION DB]: Storing terrain");

            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "delete from terrain where RegionUUID = ?RegionUUID";
                    cmd.Parameters.AddWithValue("RegionUUID", regionID.ToString());

                    ExecuteNonQuery(cmd);

                    cmd.CommandText = "insert into terrain (RegionUUID, " +
                        "Revision, Heightfield) values (?RegionUUID, " +
                        "1, ?Heightfield)";

                    cmd.Parameters.AddWithValue("Heightfield", SerializeTerrain(ter));

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public double[,] LoadTerrain(UUID regionID)
        {
            double[,] terrain = null;

            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select RegionUUID, Revision, Heightfield " +
                        "from terrain where RegionUUID = ?RegionUUID " +
                        "order by Revision desc limit 1";
                    cmd.Parameters.AddWithValue("RegionUUID", regionID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            int rev = Convert.ToInt32(reader["Revision"]);

                            terrain = new double[(int)Constants.RegionSize, (int)Constants.RegionSize];
                            terrain.Initialize();

                            using (MemoryStream mstr = new MemoryStream((byte[])reader["Heightfield"]))
                            {
                                using (BinaryReader br = new BinaryReader(mstr))
                                {
                                    for (int x = 0; x < (int)Constants.RegionSize; x++)
                                    {
                                        for (int y = 0; y < (int)Constants.RegionSize; y++)
                                        {
                                            terrain[x, y] = br.ReadDouble();
                                        }
                                    }
                                }

                                m_log.InfoFormat("[REGION DB]: Loaded terrain revision r{0}", rev);
                            }
                        }
                    }
                }
            }

            return terrain;
        }

        public void RemoveLandObject(UUID globalID)
        {
            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "delete from land where UUID = ?UUID";
                    cmd.Parameters.AddWithValue("UUID", globalID.ToString());

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public void StoreLandObject(ILandObject parcel)
        {
            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "replace into land (UUID, RegionUUID, " +
                        "LocalLandID, Bitmap, Name, Description, " +
                        "OwnerUUID, IsGroupOwned, Area, AuctionID, " +
                        "Category, ClaimDate, ClaimPrice, GroupUUID, " +
                        "SalePrice, LandStatus, LandFlags, LandingType, " +
                        "MediaAutoScale, MediaTextureUUID, MediaURL, " +
                        "MusicURL, PassHours, PassPrice, SnapshotUUID, " +
                        "UserLocationX, UserLocationY, UserLocationZ, " +
                        "UserLookAtX, UserLookAtY, UserLookAtZ, " +
                        "AuthbuyerID, OtherCleanTime, Dwell) values (" +
                        "?UUID, ?RegionUUID, " +
                        "?LocalLandID, ?Bitmap, ?Name, ?Description, " +
                        "?OwnerUUID, ?IsGroupOwned, ?Area, ?AuctionID, " +
                        "?Category, ?ClaimDate, ?ClaimPrice, ?GroupUUID, " +
                        "?SalePrice, ?LandStatus, ?LandFlags, ?LandingType, " +
                        "?MediaAutoScale, ?MediaTextureUUID, ?MediaURL, " +
                        "?MusicURL, ?PassHours, ?PassPrice, ?SnapshotUUID, " +
                        "?UserLocationX, ?UserLocationY, ?UserLocationZ, " +
                        "?UserLookAtX, ?UserLookAtY, ?UserLookAtZ, " +
                        "?AuthbuyerID, ?OtherCleanTime, ?Dwell)";

                    FillLandCommand(cmd, parcel.LandData, parcel.RegionUUID);

                    ExecuteNonQuery(cmd);

                    cmd.CommandText = "delete from landaccesslist where LandUUID = ?UUID";

                    ExecuteNonQuery(cmd);

                    cmd.Parameters.Clear();
                    cmd.CommandText = "insert into landaccesslist (LandUUID, " +
                            "AccessUUID, Flags) values (?LandUUID, ?AccessUUID, " +
                            "?Flags)";

                    foreach (ParcelManager.ParcelAccessEntry entry in parcel.LandData.ParcelAccessList)
                    {
                        FillLandAccessCommand(cmd, entry, parcel.LandData.GlobalID);
                        ExecuteNonQuery(cmd);
                        cmd.Parameters.Clear();
                    }
                }
            }
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            RegionSettings rs = null;

            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select * from regionsettings where regionUUID = ?RegionUUID";
                    cmd.Parameters.AddWithValue("regionUUID", regionUUID);

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        if (reader.Read())
                        {
                            rs = BuildRegionSettings(reader);
                            rs.OnSave += StoreRegionSettings;
                        }
                        else
                        {
                            rs = new RegionSettings();
                            rs.RegionUUID = regionUUID;
                            rs.OnSave += StoreRegionSettings;

                            StoreRegionSettings(rs);
                        }
                    }
                }
            }

            return rs;
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "replace into regionsettings (regionUUID, " +
                        "block_terraform, block_fly, allow_damage, " +
                        "restrict_pushing, allow_land_resell, " +
                        "allow_land_join_divide, block_show_in_search, " +
                        "agent_limit, object_bonus, maturity, " +
                        "disable_scripts, disable_collisions, " +
                        "disable_physics, terrain_texture_1, " +
                        "terrain_texture_2, terrain_texture_3, " +
                        "terrain_texture_4, elevation_1_nw, " +
                        "elevation_2_nw, elevation_1_ne, " +
                        "elevation_2_ne, elevation_1_se, " +
                        "elevation_2_se, elevation_1_sw, " +
                        "elevation_2_sw, water_height, " +
                        "terrain_raise_limit, terrain_lower_limit, " +
                        "use_estate_sun, fixed_sun, sun_position, " +
                        "covenant, Sandbox, sunvectorx, sunvectory, " +
                        "sunvectorz, loaded_creation_datetime, " +
                        "loaded_creation_id) values (?RegionUUID, ?BlockTerraform, " +
                        "?BlockFly, ?AllowDamage, ?RestrictPushing, " +
                        "?AllowLandResell, ?AllowLandJoinDivide, " +
                        "?BlockShowInSearch, ?AgentLimit, ?ObjectBonus, " +
                        "?Maturity, ?DisableScripts, ?DisableCollisions, " +
                        "?DisablePhysics, ?TerrainTexture1, " +
                        "?TerrainTexture2, ?TerrainTexture3, " +
                        "?TerrainTexture4, ?Elevation1NW, ?Elevation2NW, " +
                        "?Elevation1NE, ?Elevation2NE, ?Elevation1SE, " +
                        "?Elevation2SE, ?Elevation1SW, ?Elevation2SW, " +
                        "?WaterHeight, ?TerrainRaiseLimit, " +
                        "?TerrainLowerLimit, ?UseEstateSun, ?FixedSun, " +
                        "?SunPosition, ?Covenant, ?Sandbox, " +
                        "?SunVectorX, ?SunVectorY, ?SunVectorZ, " +
                        "?LoadedCreationDateTime, ?LoadedCreationID)";

                    FillRegionSettingsCommand(cmd, rs);

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            List<LandData> landData = new List<LandData>();

            lock (m_Connection)
            {
                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    cmd.CommandText = "select * from land where RegionUUID = ?RegionUUID";
                    cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            LandData newLand = BuildLandData(reader);
                            landData.Add(newLand);
                        }
                    }
                }

                using (MySqlCommand cmd = m_Connection.CreateCommand())
                {
                    foreach (LandData land in landData)
                    {
                        cmd.Parameters.Clear();
                        cmd.CommandText = "select * from landaccesslist where LandUUID = ?LandUUID";
                        cmd.Parameters.AddWithValue("LandUUID", land.GlobalID.ToString());

                        using (IDataReader reader = ExecuteReader(cmd))
                        {
                            while (reader.Read())
                            {
                                land.ParcelAccessList.Add(BuildLandAccessData(reader));
                            }
                        }
                    }
                }
            }

            return landData;
        }

        public void Shutdown()
        {
        }

        private SceneObjectPart BuildPrim(IDataReader row)
        {
            SceneObjectPart prim = new SceneObjectPart();
            prim.UUID = new UUID((String) row["UUID"]);
            // explicit conversion of integers is required, which sort
            // of sucks.  No idea if there is a shortcut here or not.
            prim.CreationDate = Convert.ToInt32(row["CreationDate"]);
            if (row["Name"] != DBNull.Value)
                prim.Name = (String)row["Name"];
            else
                prim.Name = string.Empty;
            // various text fields
            prim.Text = (String) row["Text"];
            prim.Color = Color.FromArgb(Convert.ToInt32(row["ColorA"]),
                                        Convert.ToInt32(row["ColorR"]),
                                        Convert.ToInt32(row["ColorG"]),
                                        Convert.ToInt32(row["ColorB"]));
            prim.Description = (String) row["Description"];
            prim.SitName = (String) row["SitName"];
            prim.TouchName = (String) row["TouchName"];
            // permissions
            prim.ObjectFlags = Convert.ToUInt32(row["ObjectFlags"]);
            prim.CreatorID = new UUID((String) row["CreatorID"]);
            prim.OwnerID = new UUID((String) row["OwnerID"]);
            prim.GroupID = new UUID((String) row["GroupID"]);
            prim.LastOwnerID = new UUID((String) row["LastOwnerID"]);
            prim.OwnerMask = Convert.ToUInt32(row["OwnerMask"]);
            prim.NextOwnerMask = Convert.ToUInt32(row["NextOwnerMask"]);
            prim.GroupMask = Convert.ToUInt32(row["GroupMask"]);
            prim.EveryoneMask = Convert.ToUInt32(row["EveryoneMask"]);
            prim.BaseMask = Convert.ToUInt32(row["BaseMask"]);
            // vectors
            prim.OffsetPosition = new Vector3(
                Convert.ToSingle(row["PositionX"]),
                Convert.ToSingle(row["PositionY"]),
                Convert.ToSingle(row["PositionZ"])
                );
            prim.GroupPosition = new Vector3(
                Convert.ToSingle(row["GroupPositionX"]),
                Convert.ToSingle(row["GroupPositionY"]),
                Convert.ToSingle(row["GroupPositionZ"])
                );
            prim.Velocity = new Vector3(
                Convert.ToSingle(row["VelocityX"]),
                Convert.ToSingle(row["VelocityY"]),
                Convert.ToSingle(row["VelocityZ"])
                );
            prim.AngularVelocity = new Vector3(
                Convert.ToSingle(row["AngularVelocityX"]),
                Convert.ToSingle(row["AngularVelocityY"]),
                Convert.ToSingle(row["AngularVelocityZ"])
                );
            prim.Acceleration = new Vector3(
                Convert.ToSingle(row["AccelerationX"]),
                Convert.ToSingle(row["AccelerationY"]),
                Convert.ToSingle(row["AccelerationZ"])
                );
            // quaternions
            prim.RotationOffset = new Quaternion(
                Convert.ToSingle(row["RotationX"]),
                Convert.ToSingle(row["RotationY"]),
                Convert.ToSingle(row["RotationZ"]),
                Convert.ToSingle(row["RotationW"])
                );
            prim.SitTargetPositionLL = new Vector3(
                Convert.ToSingle(row["SitTargetOffsetX"]),
                Convert.ToSingle(row["SitTargetOffsetY"]),
                Convert.ToSingle(row["SitTargetOffsetZ"])
                );
            prim.SitTargetOrientationLL = new Quaternion(
                Convert.ToSingle(row["SitTargetOrientX"]),
                Convert.ToSingle(row["SitTargetOrientY"]),
                Convert.ToSingle(row["SitTargetOrientZ"]),
                Convert.ToSingle(row["SitTargetOrientW"])
                );

            prim.PayPrice[0] = Convert.ToInt32(row["PayPrice"]);
            prim.PayPrice[1] = Convert.ToInt32(row["PayButton1"]);
            prim.PayPrice[2] = Convert.ToInt32(row["PayButton2"]);
            prim.PayPrice[3] = Convert.ToInt32(row["PayButton3"]);
            prim.PayPrice[4] = Convert.ToInt32(row["PayButton4"]);

            prim.Sound = new UUID(row["LoopedSound"].ToString());
            prim.SoundGain = Convert.ToSingle(row["LoopedSoundGain"]);
            prim.SoundFlags = 1; // If it's persisted at all, it's looped

            if (!(row["TextureAnimation"] is DBNull))
                prim.TextureAnimation = (Byte[])row["TextureAnimation"];
            if (!(row["ParticleSystem"] is DBNull))
                prim.ParticleSystem = (Byte[])row["ParticleSystem"];

            prim.RotationalVelocity = new Vector3(
                Convert.ToSingle(row["OmegaX"]),
                Convert.ToSingle(row["OmegaY"]),
                Convert.ToSingle(row["OmegaZ"])
                );

            prim.SetCameraEyeOffset(new Vector3(
                Convert.ToSingle(row["CameraEyeOffsetX"]),
                Convert.ToSingle(row["CameraEyeOffsetY"]),
                Convert.ToSingle(row["CameraEyeOffsetZ"])
                ));

            prim.SetCameraAtOffset(new Vector3(
                Convert.ToSingle(row["CameraAtOffsetX"]),
                Convert.ToSingle(row["CameraAtOffsetY"]),
                Convert.ToSingle(row["CameraAtOffsetZ"])
                ));

            if (Convert.ToInt16(row["ForceMouselook"]) != 0)
                prim.SetForceMouselook(true);

            prim.ScriptAccessPin = Convert.ToInt32(row["ScriptAccessPin"]);

            if (Convert.ToInt16(row["AllowedDrop"]) != 0)
                prim.AllowedDrop = true;

            if (Convert.ToInt16(row["DieAtEdge"]) != 0)
                prim.DIE_AT_EDGE = true;

            prim.SalePrice = Convert.ToInt32(row["SalePrice"]);
            prim.ObjectSaleType = unchecked((byte)Convert.ToSByte(row["SaleType"]));

            prim.Material = unchecked((byte)Convert.ToSByte(row["Material"]));

            if (!(row["ClickAction"] is DBNull))
                prim.ClickAction = unchecked((byte)Convert.ToSByte(row["ClickAction"]));

            prim.CollisionSound = new UUID(row["CollisionSound"].ToString());
            prim.CollisionSoundVolume = Convert.ToSingle(row["CollisionSoundVolume"]);
            
            if (Convert.ToInt16(row["PassTouches"]) != 0)
                prim.PassTouches = true;
            prim.LinkNum = Convert.ToInt32(row["LinkNumber"]);

            return prim;
        }


        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static TaskInventoryItem BuildItem(IDataReader row)
        {
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID        = new UUID((String)row["itemID"]);
            taskItem.ParentPartID  = new UUID((String)row["primID"]);
            taskItem.AssetID       = new UUID((String)row["assetID"]);
            taskItem.ParentID      = new UUID((String)row["parentFolderID"]);

            taskItem.InvType       = Convert.ToInt32(row["invType"]);
            taskItem.Type          = Convert.ToInt32(row["assetType"]);

            taskItem.Name          = (String)row["name"];
            taskItem.Description   = (String)row["description"];
            taskItem.CreationDate  = Convert.ToUInt32(row["creationDate"]);
            taskItem.CreatorID     = new UUID((String)row["creatorID"]);
            taskItem.OwnerID       = new UUID((String)row["ownerID"]);
            taskItem.LastOwnerID   = new UUID((String)row["lastOwnerID"]);
            taskItem.GroupID       = new UUID((String)row["groupID"]);

            taskItem.NextPermissions = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.CurrentPermissions     = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BasePermissions      = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryonePermissions  = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupPermissions     = Convert.ToUInt32(row["groupPermissions"]);
            taskItem.Flags         = Convert.ToUInt32(row["flags"]);

            return taskItem;
        }

        private static RegionSettings BuildRegionSettings(IDataReader row)
        {
            RegionSettings newSettings = new RegionSettings();

            newSettings.RegionUUID = new UUID((string) row["regionUUID"]);
            newSettings.BlockTerraform = Convert.ToBoolean(row["block_terraform"]);
            newSettings.AllowDamage = Convert.ToBoolean(row["allow_damage"]);
            newSettings.BlockFly = Convert.ToBoolean(row["block_fly"]);
            newSettings.RestrictPushing = Convert.ToBoolean(row["restrict_pushing"]);
            newSettings.AllowLandResell = Convert.ToBoolean(row["allow_land_resell"]);
            newSettings.AllowLandJoinDivide = Convert.ToBoolean(row["allow_land_join_divide"]);
            newSettings.BlockShowInSearch = Convert.ToBoolean(row["block_show_in_search"]);
            newSettings.AgentLimit = Convert.ToInt32(row["agent_limit"]);
            newSettings.ObjectBonus = Convert.ToDouble(row["object_bonus"]);
            newSettings.Maturity = Convert.ToInt32(row["maturity"]);
            newSettings.DisableScripts = Convert.ToBoolean(row["disable_scripts"]);
            newSettings.DisableCollisions = Convert.ToBoolean(row["disable_collisions"]);
            newSettings.DisablePhysics = Convert.ToBoolean(row["disable_physics"]);
            newSettings.TerrainTexture1 = new UUID((String) row["terrain_texture_1"]);
            newSettings.TerrainTexture2 = new UUID((String) row["terrain_texture_2"]);
            newSettings.TerrainTexture3 = new UUID((String) row["terrain_texture_3"]);
            newSettings.TerrainTexture4 = new UUID((String) row["terrain_texture_4"]);
            newSettings.Elevation1NW = Convert.ToDouble(row["elevation_1_nw"]);
            newSettings.Elevation2NW = Convert.ToDouble(row["elevation_2_nw"]);
            newSettings.Elevation1NE = Convert.ToDouble(row["elevation_1_ne"]);
            newSettings.Elevation2NE = Convert.ToDouble(row["elevation_2_ne"]);
            newSettings.Elevation1SE = Convert.ToDouble(row["elevation_1_se"]);
            newSettings.Elevation2SE = Convert.ToDouble(row["elevation_2_se"]);
            newSettings.Elevation1SW = Convert.ToDouble(row["elevation_1_sw"]);
            newSettings.Elevation2SW = Convert.ToDouble(row["elevation_2_sw"]);
            newSettings.WaterHeight = Convert.ToDouble(row["water_height"]);
            newSettings.TerrainRaiseLimit = Convert.ToDouble(row["terrain_raise_limit"]);
            newSettings.TerrainLowerLimit = Convert.ToDouble(row["terrain_lower_limit"]);
            newSettings.UseEstateSun = Convert.ToBoolean(row["use_estate_sun"]);
            newSettings.Sandbox = Convert.ToBoolean(row["sandbox"]);
            newSettings.SunVector = new Vector3 (
                                                 Convert.ToSingle(row["sunvectorx"]),
                                                 Convert.ToSingle(row["sunvectory"]),
                                                 Convert.ToSingle(row["sunvectorz"])
                                                 );
            newSettings.FixedSun = Convert.ToBoolean(row["fixed_sun"]);
            newSettings.SunPosition = Convert.ToDouble(row["sun_position"]);
            newSettings.Covenant = new UUID((String) row["covenant"]);

            newSettings.LoadedCreationDateTime = Convert.ToInt32(row["loaded_creation_datetime"]);
            
            if (row["loaded_creation_id"] is DBNull)
                newSettings.LoadedCreationID = "";
            else 
                newSettings.LoadedCreationID = (String) row["loaded_creation_id"];

            return newSettings;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static LandData BuildLandData(IDataReader row)
        {
            LandData newData = new LandData();

            newData.GlobalID = new UUID((String) row["UUID"]);
            newData.LocalID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.Bitmap = (Byte[]) row["Bitmap"];

            newData.Name = (String) row["Name"];
            newData.Description = (String) row["Description"];
            newData.OwnerID = new UUID((String)row["OwnerUUID"]);
            newData.IsGroupOwned = Convert.ToBoolean(row["IsGroupOwned"]);
            newData.Area = Convert.ToInt32(row["Area"]);
            newData.AuctionID = Convert.ToUInt32(row["AuctionID"]); //Unimplemented
            newData.Category = (ParcelCategory) Convert.ToInt32(row["Category"]);
                //Enum libsecondlife.Parcel.ParcelCategory
            newData.ClaimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.ClaimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.GroupID = new UUID((String) row["GroupUUID"]);
            newData.SalePrice = Convert.ToInt32(row["SalePrice"]);
            newData.Status = (ParcelStatus) Convert.ToInt32(row["LandStatus"]);
                //Enum. libsecondlife.Parcel.ParcelStatus
            newData.Flags = Convert.ToUInt32(row["LandFlags"]);
            newData.LandingType = Convert.ToByte(row["LandingType"]);
            newData.MediaAutoScale = Convert.ToByte(row["MediaAutoScale"]);
            newData.MediaID = new UUID((String) row["MediaTextureUUID"]);
            newData.MediaURL = (String) row["MediaURL"];
            newData.MusicURL = (String) row["MusicURL"];
            newData.PassHours = Convert.ToSingle(row["PassHours"]);
            newData.PassPrice = Convert.ToInt32(row["PassPrice"]);
            UUID authedbuyer = UUID.Zero;
            UUID snapshotID = UUID.Zero;

            UUID.TryParse((string)row["AuthBuyerID"], out authedbuyer);
            UUID.TryParse((string)row["SnapshotUUID"], out snapshotID);
            newData.OtherCleanTime = Convert.ToInt32(row["OtherCleanTime"]);
            newData.Dwell = Convert.ToInt32(row["Dwell"]);

            newData.AuthBuyerID = authedbuyer;
            newData.SnapshotID = snapshotID;
            try
            {
                newData.UserLocation =
                    new Vector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                                  Convert.ToSingle(row["UserLocationZ"]));
                newData.UserLookAt =
                    new Vector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                                  Convert.ToSingle(row["UserLookAtZ"]));
            }
            catch (InvalidCastException)
            {
                newData.UserLocation = Vector3.Zero;
                newData.UserLookAt = Vector3.Zero;
                m_log.ErrorFormat("[PARCEL]: unable to get parcel telehub settings for {1}", newData.Name);
            }

            newData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();

            return newData;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static ParcelManager.ParcelAccessEntry BuildLandAccessData(IDataReader row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new UUID((string) row["AccessUUID"]);
            entry.Flags = (AccessList) Convert.ToInt32(row["Flags"]);
            entry.Time = new DateTime();
            return entry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static Array SerializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(((int)Constants.RegionSize * (int)Constants.RegionSize) *sizeof (double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < (int)Constants.RegionSize; x++)
                for (int y = 0; y < (int)Constants.RegionSize; y++)
                {
                    double height = val[x, y];
                    if (height == 0.0)
                        height = double.Epsilon;

                    bw.Write(height);
                }

            return str.ToArray();
        }

        /// <summary>
        /// Fill the prim command with prim values
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        /// <param name="sceneGroupID"></param>
        /// <param name="regionUUID"></param>
        private void FillPrimCommand(MySqlCommand cmd, SceneObjectPart prim, UUID sceneGroupID, UUID regionUUID)
        {
            cmd.Parameters.AddWithValue("UUID", prim.UUID.ToString());
            cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());
            cmd.Parameters.AddWithValue("CreationDate", prim.CreationDate);
            cmd.Parameters.AddWithValue("Name", prim.Name);
            cmd.Parameters.AddWithValue("SceneGroupID", sceneGroupID.ToString());
                // the UUID of the root part for this SceneObjectGroup
            // various text fields
            cmd.Parameters.AddWithValue("Text", prim.Text);
            cmd.Parameters.AddWithValue("ColorR", prim.Color.R);
            cmd.Parameters.AddWithValue("ColorG", prim.Color.G);
            cmd.Parameters.AddWithValue("ColorB", prim.Color.B);
            cmd.Parameters.AddWithValue("ColorA", prim.Color.A);
            cmd.Parameters.AddWithValue("Description", prim.Description);
            cmd.Parameters.AddWithValue("SitName", prim.SitName);
            cmd.Parameters.AddWithValue("TouchName", prim.TouchName);
            // permissions
            cmd.Parameters.AddWithValue("ObjectFlags", prim.ObjectFlags);
            cmd.Parameters.AddWithValue("CreatorID", prim.CreatorID.ToString());
            cmd.Parameters.AddWithValue("OwnerID", prim.OwnerID.ToString());
            cmd.Parameters.AddWithValue("GroupID", prim.GroupID.ToString());
            cmd.Parameters.AddWithValue("LastOwnerID", prim.LastOwnerID.ToString());
            cmd.Parameters.AddWithValue("OwnerMask", prim.OwnerMask);
            cmd.Parameters.AddWithValue("NextOwnerMask", prim.NextOwnerMask);
            cmd.Parameters.AddWithValue("GroupMask", prim.GroupMask);
            cmd.Parameters.AddWithValue("EveryoneMask", prim.EveryoneMask);
            cmd.Parameters.AddWithValue("BaseMask", prim.BaseMask);
            // vectors
            cmd.Parameters.AddWithValue("PositionX", (double)prim.OffsetPosition.X);
            cmd.Parameters.AddWithValue("PositionY", (double)prim.OffsetPosition.Y);
            cmd.Parameters.AddWithValue("PositionZ", (double)prim.OffsetPosition.Z);
            cmd.Parameters.AddWithValue("GroupPositionX", (double)prim.GroupPosition.X);
            cmd.Parameters.AddWithValue("GroupPositionY", (double)prim.GroupPosition.Y);
            cmd.Parameters.AddWithValue("GroupPositionZ", (double)prim.GroupPosition.Z);
            cmd.Parameters.AddWithValue("VelocityX", (double)prim.Velocity.X);
            cmd.Parameters.AddWithValue("VelocityY", (double)prim.Velocity.Y);
            cmd.Parameters.AddWithValue("VelocityZ", (double)prim.Velocity.Z);
            cmd.Parameters.AddWithValue("AngularVelocityX", (double)prim.AngularVelocity.X);
            cmd.Parameters.AddWithValue("AngularVelocityY", (double)prim.AngularVelocity.Y);
            cmd.Parameters.AddWithValue("AngularVelocityZ", (double)prim.AngularVelocity.Z);
            cmd.Parameters.AddWithValue("AccelerationX", (double)prim.Acceleration.X);
            cmd.Parameters.AddWithValue("AccelerationY", (double)prim.Acceleration.Y);
            cmd.Parameters.AddWithValue("AccelerationZ", (double)prim.Acceleration.Z);
            // quaternions
            cmd.Parameters.AddWithValue("RotationX", (double)prim.RotationOffset.X);
            cmd.Parameters.AddWithValue("RotationY", (double)prim.RotationOffset.Y);
            cmd.Parameters.AddWithValue("RotationZ", (double)prim.RotationOffset.Z);
            cmd.Parameters.AddWithValue("RotationW", (double)prim.RotationOffset.W);

            // Sit target
            Vector3 sitTargetPos = prim.SitTargetPositionLL;
            cmd.Parameters.AddWithValue("SitTargetOffsetX", (double)sitTargetPos.X);
            cmd.Parameters.AddWithValue("SitTargetOffsetY", (double)sitTargetPos.Y);
            cmd.Parameters.AddWithValue("SitTargetOffsetZ", (double)sitTargetPos.Z);

            Quaternion sitTargetOrient = prim.SitTargetOrientationLL;
            cmd.Parameters.AddWithValue("SitTargetOrientW", (double)sitTargetOrient.W);
            cmd.Parameters.AddWithValue("SitTargetOrientX", (double)sitTargetOrient.X);
            cmd.Parameters.AddWithValue("SitTargetOrientY", (double)sitTargetOrient.Y);
            cmd.Parameters.AddWithValue("SitTargetOrientZ", (double)sitTargetOrient.Z);

            cmd.Parameters.AddWithValue("PayPrice", prim.PayPrice[0]);
            cmd.Parameters.AddWithValue("PayButton1", prim.PayPrice[1]);
            cmd.Parameters.AddWithValue("PayButton2", prim.PayPrice[2]);
            cmd.Parameters.AddWithValue("PayButton3", prim.PayPrice[3]);
            cmd.Parameters.AddWithValue("PayButton4", prim.PayPrice[4]);

            if ((prim.SoundFlags & 1) != 0) // Looped
            {
                cmd.Parameters.AddWithValue("LoopedSound", prim.Sound.ToString());
                cmd.Parameters.AddWithValue("LoopedSoundGain", prim.SoundGain);
            }
            else
            {
                cmd.Parameters.AddWithValue("LoopedSound", UUID.Zero);
                cmd.Parameters.AddWithValue("LoopedSoundGain", 0.0f);
            }

            cmd.Parameters.AddWithValue("TextureAnimation", prim.TextureAnimation);
            cmd.Parameters.AddWithValue("ParticleSystem", prim.ParticleSystem);

            cmd.Parameters.AddWithValue("OmegaX", (double)prim.RotationalVelocity.X);
            cmd.Parameters.AddWithValue("OmegaY", (double)prim.RotationalVelocity.Y);
            cmd.Parameters.AddWithValue("OmegaZ", (double)prim.RotationalVelocity.Z);

            cmd.Parameters.AddWithValue("CameraEyeOffsetX", (double)prim.GetCameraEyeOffset().X);
            cmd.Parameters.AddWithValue("CameraEyeOffsetY", (double)prim.GetCameraEyeOffset().Y);
            cmd.Parameters.AddWithValue("CameraEyeOffsetZ", (double)prim.GetCameraEyeOffset().Z);

            cmd.Parameters.AddWithValue("CameraAtOffsetX", (double)prim.GetCameraAtOffset().X);
            cmd.Parameters.AddWithValue("CameraAtOffsetY", (double)prim.GetCameraAtOffset().Y);
            cmd.Parameters.AddWithValue("CameraAtOffsetZ", (double)prim.GetCameraAtOffset().Z);

            if (prim.GetForceMouselook())
                cmd.Parameters.AddWithValue("ForceMouselook", 1);
            else
                cmd.Parameters.AddWithValue("ForceMouselook", 0);

            cmd.Parameters.AddWithValue("ScriptAccessPin", prim.ScriptAccessPin);

            if (prim.AllowedDrop)
                cmd.Parameters.AddWithValue("AllowedDrop", 1);
            else
                cmd.Parameters.AddWithValue("AllowedDrop", 0);

            if (prim.DIE_AT_EDGE)
                cmd.Parameters.AddWithValue("DieAtEdge", 1);
            else
                cmd.Parameters.AddWithValue("DieAtEdge", 0);

            cmd.Parameters.AddWithValue("SalePrice", prim.SalePrice);
            cmd.Parameters.AddWithValue("SaleType", unchecked((sbyte)(prim.ObjectSaleType)));

            byte clickAction = prim.ClickAction;
            cmd.Parameters.AddWithValue("ClickAction", unchecked((sbyte)(clickAction)));

            cmd.Parameters.AddWithValue("Material", unchecked((sbyte)(prim.Material)));

            cmd.Parameters.AddWithValue("CollisionSound", prim.CollisionSound.ToString());
            cmd.Parameters.AddWithValue("CollisionSoundVolume", prim.CollisionSoundVolume);

            if (prim.PassTouches)
                cmd.Parameters.AddWithValue("PassTouches", 1);
            else
                cmd.Parameters.AddWithValue("PassTouches", 0);

            cmd.Parameters.AddWithValue("LinkNumber", prim.LinkNum);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="taskItem"></param>
        private static void FillItemCommand(MySqlCommand cmd, TaskInventoryItem taskItem)
        {
            cmd.Parameters.AddWithValue("itemID", taskItem.ItemID);
            cmd.Parameters.AddWithValue("primID", taskItem.ParentPartID);
            cmd.Parameters.AddWithValue("assetID", taskItem.AssetID);
            cmd.Parameters.AddWithValue("parentFolderID", taskItem.ParentID);

            cmd.Parameters.AddWithValue("invType", taskItem.InvType);
            cmd.Parameters.AddWithValue("assetType", taskItem.Type);

            cmd.Parameters.AddWithValue("name", taskItem.Name);
            cmd.Parameters.AddWithValue("description", taskItem.Description);
            cmd.Parameters.AddWithValue("creationDate", taskItem.CreationDate);
            cmd.Parameters.AddWithValue("creatorID", taskItem.CreatorID);
            cmd.Parameters.AddWithValue("ownerID", taskItem.OwnerID);
            cmd.Parameters.AddWithValue("lastOwnerID", taskItem.LastOwnerID);
            cmd.Parameters.AddWithValue("groupID", taskItem.GroupID);
            cmd.Parameters.AddWithValue("nextPermissions", taskItem.NextPermissions);
            cmd.Parameters.AddWithValue("currentPermissions", taskItem.CurrentPermissions);
            cmd.Parameters.AddWithValue("basePermissions", taskItem.BasePermissions);
            cmd.Parameters.AddWithValue("everyonePermissions", taskItem.EveryonePermissions);
            cmd.Parameters.AddWithValue("groupPermissions", taskItem.GroupPermissions);
            cmd.Parameters.AddWithValue("flags", taskItem.Flags);
        }

        /// <summary>
        ///
        /// </summary>
        private static void FillRegionSettingsCommand(MySqlCommand cmd, RegionSettings settings)
        {
            cmd.Parameters.AddWithValue("RegionUUID", settings.RegionUUID.ToString());
            cmd.Parameters.AddWithValue("BlockTerraform", settings.BlockTerraform);
            cmd.Parameters.AddWithValue("BlockFly", settings.BlockFly);
            cmd.Parameters.AddWithValue("AllowDamage", settings.AllowDamage);
            cmd.Parameters.AddWithValue("RestrictPushing", settings.RestrictPushing);
            cmd.Parameters.AddWithValue("AllowLandResell", settings.AllowLandResell);
            cmd.Parameters.AddWithValue("AllowLandJoinDivide", settings.AllowLandJoinDivide);
            cmd.Parameters.AddWithValue("BlockShowInSearch", settings.BlockShowInSearch);
            cmd.Parameters.AddWithValue("AgentLimit", settings.AgentLimit);
            cmd.Parameters.AddWithValue("ObjectBonus", settings.ObjectBonus);
            cmd.Parameters.AddWithValue("Maturity", settings.Maturity);
            cmd.Parameters.AddWithValue("DisableScripts", settings.DisableScripts);
            cmd.Parameters.AddWithValue("DisableCollisions", settings.DisableCollisions);
            cmd.Parameters.AddWithValue("DisablePhysics", settings.DisablePhysics);
            cmd.Parameters.AddWithValue("TerrainTexture1", settings.TerrainTexture1.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture2", settings.TerrainTexture2.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture3", settings.TerrainTexture3.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture4", settings.TerrainTexture4.ToString());
            cmd.Parameters.AddWithValue("Elevation1NW", settings.Elevation1NW);
            cmd.Parameters.AddWithValue("Elevation2NW", settings.Elevation2NW);
            cmd.Parameters.AddWithValue("Elevation1NE", settings.Elevation1NE);
            cmd.Parameters.AddWithValue("Elevation2NE", settings.Elevation2NE);
            cmd.Parameters.AddWithValue("Elevation1SE", settings.Elevation1SE);
            cmd.Parameters.AddWithValue("Elevation2SE", settings.Elevation2SE);
            cmd.Parameters.AddWithValue("Elevation1SW", settings.Elevation1SW);
            cmd.Parameters.AddWithValue("Elevation2SW", settings.Elevation2SW);
            cmd.Parameters.AddWithValue("WaterHeight", settings.WaterHeight);
            cmd.Parameters.AddWithValue("TerrainRaiseLimit", settings.TerrainRaiseLimit);
            cmd.Parameters.AddWithValue("TerrainLowerLimit", settings.TerrainLowerLimit);
            cmd.Parameters.AddWithValue("UseEstateSun", settings.UseEstateSun);
            cmd.Parameters.AddWithValue("Sandbox", settings.Sandbox);
            cmd.Parameters.AddWithValue("SunVectorX", settings.SunVector.X);
            cmd.Parameters.AddWithValue("SunVectorY", settings.SunVector.Y);
            cmd.Parameters.AddWithValue("SunVectorZ", settings.SunVector.Z);
            cmd.Parameters.AddWithValue("FixedSun", settings.FixedSun);
            cmd.Parameters.AddWithValue("SunPosition", settings.SunPosition);
            cmd.Parameters.AddWithValue("Covenant", settings.Covenant.ToString());
            cmd.Parameters.AddWithValue("LoadedCreationDateTime", settings.LoadedCreationDateTime);
            cmd.Parameters.AddWithValue("LoadedCreationID", settings.LoadedCreationID);

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="land"></param>
        /// <param name="regionUUID"></param>
        private static void FillLandCommand(MySqlCommand cmd, LandData land, UUID regionUUID)
        {
            cmd.Parameters.AddWithValue("UUID", land.GlobalID.ToString());
            cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());
            cmd.Parameters.AddWithValue("LocalLandID", land.LocalID);

            // Bitmap is a byte[512]
            cmd.Parameters.AddWithValue("Bitmap", land.Bitmap);

            cmd.Parameters.AddWithValue("Name", land.Name);
            cmd.Parameters.AddWithValue("Description", land.Description);
            cmd.Parameters.AddWithValue("OwnerUUID", land.OwnerID.ToString());
            cmd.Parameters.AddWithValue("IsGroupOwned", land.IsGroupOwned);
            cmd.Parameters.AddWithValue("Area", land.Area);
            cmd.Parameters.AddWithValue("AuctionID", land.AuctionID); //Unemplemented
            cmd.Parameters.AddWithValue("Category", land.Category); //Enum libsecondlife.Parcel.ParcelCategory
            cmd.Parameters.AddWithValue("ClaimDate", land.ClaimDate);
            cmd.Parameters.AddWithValue("ClaimPrice", land.ClaimPrice);
            cmd.Parameters.AddWithValue("GroupUUID", land.GroupID.ToString());
            cmd.Parameters.AddWithValue("SalePrice", land.SalePrice);
            cmd.Parameters.AddWithValue("LandStatus", land.Status); //Enum. libsecondlife.Parcel.ParcelStatus
            cmd.Parameters.AddWithValue("LandFlags", land.Flags);
            cmd.Parameters.AddWithValue("LandingType", land.LandingType);
            cmd.Parameters.AddWithValue("MediaAutoScale", land.MediaAutoScale);
            cmd.Parameters.AddWithValue("MediaTextureUUID", land.MediaID.ToString());
            cmd.Parameters.AddWithValue("MediaURL", land.MediaURL);
            cmd.Parameters.AddWithValue("MusicURL", land.MusicURL);
            cmd.Parameters.AddWithValue("PassHours", land.PassHours);
            cmd.Parameters.AddWithValue("PassPrice", land.PassPrice);
            cmd.Parameters.AddWithValue("SnapshotUUID", land.SnapshotID.ToString());
            cmd.Parameters.AddWithValue("UserLocationX", land.UserLocation.X);
            cmd.Parameters.AddWithValue("UserLocationY", land.UserLocation.Y);
            cmd.Parameters.AddWithValue("UserLocationZ", land.UserLocation.Z);
            cmd.Parameters.AddWithValue("UserLookAtX", land.UserLookAt.X);
            cmd.Parameters.AddWithValue("UserLookAtY", land.UserLookAt.Y);
            cmd.Parameters.AddWithValue("UserLookAtZ", land.UserLookAt.Z);
            cmd.Parameters.AddWithValue("AuthBuyerID", land.AuthBuyerID);
            cmd.Parameters.AddWithValue("OtherCleanTime", land.OtherCleanTime);
            cmd.Parameters.AddWithValue("Dwell", land.Dwell);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="entry"></param>
        /// <param name="parcelID"></param>
        private static void FillLandAccessCommand(MySqlCommand cmd, ParcelManager.ParcelAccessEntry entry, UUID parcelID)
        {
            cmd.Parameters.AddWithValue("LandUUID", parcelID.ToString());
            cmd.Parameters.AddWithValue("AccessUUID", entry.AgentID.ToString());
            cmd.Parameters.AddWithValue("Flags", entry.Flags);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private PrimitiveBaseShape BuildShape(IDataReader row)
        {
            PrimitiveBaseShape s = new PrimitiveBaseShape();
            s.Scale = new Vector3(
                Convert.ToSingle(row["ScaleX"]),
                Convert.ToSingle(row["ScaleY"]),
                Convert.ToSingle(row["ScaleZ"])
                );
            // paths
            s.PCode = Convert.ToByte(row["PCode"]);
            s.PathBegin = Convert.ToUInt16(row["PathBegin"]);
            s.PathEnd = Convert.ToUInt16(row["PathEnd"]);
            s.PathScaleX = Convert.ToByte(row["PathScaleX"]);
            s.PathScaleY = Convert.ToByte(row["PathScaleY"]);
            s.PathShearX = Convert.ToByte(row["PathShearX"]);
            s.PathShearY = Convert.ToByte(row["PathShearY"]);
            s.PathSkew = Convert.ToSByte(row["PathSkew"]);
            s.PathCurve = Convert.ToByte(row["PathCurve"]);
            s.PathRadiusOffset = Convert.ToSByte(row["PathRadiusOffset"]);
            s.PathRevolutions = Convert.ToByte(row["PathRevolutions"]);
            s.PathTaperX = Convert.ToSByte(row["PathTaperX"]);
            s.PathTaperY = Convert.ToSByte(row["PathTaperY"]);
            s.PathTwist = Convert.ToSByte(row["PathTwist"]);
            s.PathTwistBegin = Convert.ToSByte(row["PathTwistBegin"]);
            // profile
            s.ProfileBegin = Convert.ToUInt16(row["ProfileBegin"]);
            s.ProfileEnd = Convert.ToUInt16(row["ProfileEnd"]);
            s.ProfileCurve = Convert.ToByte(row["ProfileCurve"]);
            s.ProfileHollow = Convert.ToUInt16(row["ProfileHollow"]);
            byte[] textureEntry = (byte[]) row["Texture"];
            s.TextureEntry = textureEntry;

            s.ExtraParams = (byte[]) row["ExtraParams"];

            s.State = Convert.ToByte(row["State"]);

            return s;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        private void FillShapeCommand(MySqlCommand cmd, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            cmd.Parameters.AddWithValue("UUID", prim.UUID.ToString());
            // shape is an enum
            cmd.Parameters.AddWithValue("Shape", 0);
            // vectors
            cmd.Parameters.AddWithValue("ScaleX", (double)s.Scale.X);
            cmd.Parameters.AddWithValue("ScaleY", (double)s.Scale.Y);
            cmd.Parameters.AddWithValue("ScaleZ", (double)s.Scale.Z);
            // paths
            cmd.Parameters.AddWithValue("PCode", s.PCode);
            cmd.Parameters.AddWithValue("PathBegin", s.PathBegin);
            cmd.Parameters.AddWithValue("PathEnd", s.PathEnd);
            cmd.Parameters.AddWithValue("PathScaleX", s.PathScaleX);
            cmd.Parameters.AddWithValue("PathScaleY", s.PathScaleY);
            cmd.Parameters.AddWithValue("PathShearX", s.PathShearX);
            cmd.Parameters.AddWithValue("PathShearY", s.PathShearY);
            cmd.Parameters.AddWithValue("PathSkew", s.PathSkew);
            cmd.Parameters.AddWithValue("PathCurve", s.PathCurve);
            cmd.Parameters.AddWithValue("PathRadiusOffset", s.PathRadiusOffset);
            cmd.Parameters.AddWithValue("PathRevolutions", s.PathRevolutions);
            cmd.Parameters.AddWithValue("PathTaperX", s.PathTaperX);
            cmd.Parameters.AddWithValue("PathTaperY", s.PathTaperY);
            cmd.Parameters.AddWithValue("PathTwist", s.PathTwist);
            cmd.Parameters.AddWithValue("PathTwistBegin", s.PathTwistBegin);
            // profile
            cmd.Parameters.AddWithValue("ProfileBegin", s.ProfileBegin);
            cmd.Parameters.AddWithValue("ProfileEnd", s.ProfileEnd);
            cmd.Parameters.AddWithValue("ProfileCurve", s.ProfileCurve);
            cmd.Parameters.AddWithValue("ProfileHollow", s.ProfileHollow);
            cmd.Parameters.AddWithValue("Texture", s.TextureEntry);
            cmd.Parameters.AddWithValue("ExtraParams", s.ExtraParams);
            cmd.Parameters.AddWithValue("State", s.State);
        }

        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            lock (m_Connection)
            {
                RemoveItems(primID);

                MySqlCommand cmd = m_Connection.CreateCommand();

                if (items.Count == 0)
                    return;

                cmd.CommandText = "insert into primitems ("+
                        "invType, assetType, name, "+
                        "description, creationDate, nextPermissions, "+
                        "currentPermissions, basePermissions, "+
                        "everyonePermissions, groupPermissions, "+
                        "flags, itemID, primID, assetID, "+
                        "parentFolderID, creatorID, ownerID, "+
                        "groupID, lastOwnerID) values (?invType, "+
                        "?assetType, ?name, ?description, "+
                        "?creationDate, ?nextPermissions, "+
                        "?currentPermissions, ?basePermissions, "+
                        "?everyonePermissions, ?groupPermissions, "+
                        "?flags, ?itemID, ?primID, ?assetID, "+
                        "?parentFolderID, ?creatorID, ?ownerID, "+
                        "?groupID, ?lastOwnerID)";

                foreach (TaskInventoryItem item in items)
                {
                    cmd.Parameters.Clear();

                    FillItemCommand(cmd, item);

                    ExecuteNonQuery(cmd);
                }
                
                cmd.Dispose();
            }
        }
    }
}
