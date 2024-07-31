using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OpenSim.Data.Model.Region;

public partial class OpenSimRegionContext : DbContext
{
    public OpenSimRegionContext(DbContextOptions<OpenSimRegionContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Bakedterrain> Bakedterrains { get; set; }

    public virtual DbSet<Land> Lands { get; set; }

    public virtual DbSet<Landaccesslist> Landaccesslists { get; set; }

    public virtual DbSet<Migration> Migrations { get; set; }

    public virtual DbSet<Prim> Prims { get; set; }

    public virtual DbSet<Primitem> Primitems { get; set; }

    public virtual DbSet<Primshape> Primshapes { get; set; }

    public virtual DbSet<Regionban> Regionbans { get; set; }

    public virtual DbSet<Regionenvironment> Regionenvironments { get; set; }

    public virtual DbSet<Regionextra> Regionextras { get; set; }

    public virtual DbSet<Regionsetting> Regionsettings { get; set; }

    public virtual DbSet<Regionwindlight> Regionwindlights { get; set; }

    public virtual DbSet<SpawnPoint> SpawnPoints { get; set; }

    public virtual DbSet<Terrain> Terrains { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Bakedterrain>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("bakedterrain")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(255)
                .HasColumnName("RegionUUID");
        });

        modelBuilder.Entity<Land>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("land")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Uuid).HasColumnName("UUID");
            entity.Property(e => e.AnyAvsounds)
                .HasDefaultValueSql("'1'")
                .HasColumnName("AnyAVSounds");
            entity.Property(e => e.AuctionId).HasColumnName("AuctionID");
            entity.Property(e => e.AuthbuyerId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("AuthbuyerID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Environment)
                .HasColumnType("mediumtext")
                .HasColumnName("environment");
            entity.Property(e => e.GroupAvsounds)
                .HasDefaultValueSql("'1'")
                .HasColumnName("GroupAVSounds");
            entity.Property(e => e.GroupUuid)
                .HasMaxLength(255)
                .HasColumnName("GroupUUID");
            entity.Property(e => e.LocalLandId).HasColumnName("LocalLandID");
            entity.Property(e => e.MediaDescription)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.MediaSize)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("'0,0'");
            entity.Property(e => e.MediaTextureUuid)
                .HasMaxLength(255)
                .HasColumnName("MediaTextureUUID");
            entity.Property(e => e.MediaType)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValueSql("'none/none'");
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(255)
                .HasColumnName("MediaURL");
            entity.Property(e => e.MusicUrl)
                .HasMaxLength(255)
                .HasColumnName("MusicURL");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.OwnerUuid)
                .HasMaxLength(255)
                .HasColumnName("OwnerUUID");
            entity.Property(e => e.RegionUuid)
                .HasMaxLength(255)
                .HasColumnName("RegionUUID");
            entity.Property(e => e.SeeAvs)
                .HasDefaultValueSql("'1'")
                .HasColumnName("SeeAVs");
            entity.Property(e => e.SnapshotUuid)
                .HasMaxLength(255)
                .HasColumnName("SnapshotUUID");
        });

        modelBuilder.Entity<Landaccesslist>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("landaccesslist")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.AccessUuid)
                .HasMaxLength(255)
                .HasColumnName("AccessUUID");
            entity.Property(e => e.LandUuid)
                .HasMaxLength(255)
                .HasColumnName("LandUUID");
        });

        modelBuilder.Entity<Migration>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("migrations")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Version).HasColumnName("version");
        });

        modelBuilder.Entity<Prim>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("prims")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.RegionUuid, "prims_regionuuid");

            entity.HasIndex(e => e.SceneGroupId, "prims_scenegroupid");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("UUID");
            entity.Property(e => e.AccelerationX).HasDefaultValueSql("'0'");
            entity.Property(e => e.AccelerationY).HasDefaultValueSql("'0'");
            entity.Property(e => e.AccelerationZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.AngularVelocityX).HasDefaultValueSql("'0'");
            entity.Property(e => e.AngularVelocityY).HasDefaultValueSql("'0'");
            entity.Property(e => e.AngularVelocityZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.AttachedPosX).HasDefaultValueSql("'0'");
            entity.Property(e => e.AttachedPosY).HasDefaultValueSql("'0'");
            entity.Property(e => e.AttachedPosZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraAtOffsetX).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraAtOffsetY).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraAtOffsetZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraEyeOffsetX).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraEyeOffsetY).HasDefaultValueSql("'0'");
            entity.Property(e => e.CameraEyeOffsetZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.CollisionSound)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength();
            entity.Property(e => e.CreatorId)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("CreatorID");
            entity.Property(e => e.Density).HasDefaultValueSql("'1000'");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.DynAttrs).HasColumnType("text");
            entity.Property(e => e.Friction).HasDefaultValueSql("'0.6'");
            entity.Property(e => e.GravityModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.GroupPositionX).HasDefaultValueSql("'0'");
            entity.Property(e => e.GroupPositionY).HasDefaultValueSql("'0'");
            entity.Property(e => e.GroupPositionZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.KeyframeMotion).HasColumnType("blob");
            entity.Property(e => e.LastOwnerId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("LastOwnerID");
            entity.Property(e => e.Linksetdata)
                .HasColumnType("mediumtext")
                .HasColumnName("linksetdata");
            entity.Property(e => e.LoopedSound)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength();
            entity.Property(e => e.LoopedSoundGain).HasDefaultValueSql("'0'");
            entity.Property(e => e.Material).HasDefaultValueSql("'3'");
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(255)
                .HasColumnName("MediaURL");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.OmegaX).HasDefaultValueSql("'0'");
            entity.Property(e => e.OmegaY).HasDefaultValueSql("'0'");
            entity.Property(e => e.OmegaZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.OwnerId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("OwnerID");
            entity.Property(e => e.ParticleSystem).HasColumnType("blob");
            entity.Property(e => e.PhysInertia).HasColumnType("text");
            entity.Property(e => e.PositionX).HasDefaultValueSql("'0'");
            entity.Property(e => e.PositionY).HasDefaultValueSql("'0'");
            entity.Property(e => e.PositionZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.Pseudocrc)
                .HasDefaultValueSql("'0'")
                .HasColumnName("pseudocrc");
            entity.Property(e => e.RegionUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("RegionUUID");
            entity.Property(e => e.Restitution).HasDefaultValueSql("'0.5'");
            entity.Property(e => e.RezzerId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("RezzerID");
            entity.Property(e => e.RotationW).HasDefaultValueSql("'0'");
            entity.Property(e => e.RotationX).HasDefaultValueSql("'0'");
            entity.Property(e => e.RotationY).HasDefaultValueSql("'0'");
            entity.Property(e => e.RotationZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.SalePrice).HasDefaultValueSql("'10'");
            entity.Property(e => e.SceneGroupId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("SceneGroupID");
            entity.Property(e => e.SitName)
                .HasMaxLength(255)
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.SitTargetOffsetX).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOffsetY).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOffsetZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOrientW).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOrientX).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOrientY).HasDefaultValueSql("'0'");
            entity.Property(e => e.SitTargetOrientZ).HasDefaultValueSql("'0'");
            entity.Property(e => e.Sitactrange)
                .HasDefaultValueSql("'0'")
                .HasColumnName("sitactrange");
            entity.Property(e => e.Sopanims)
                .HasColumnType("blob")
                .HasColumnName("sopanims");
            entity.Property(e => e.Standtargetx)
                .HasDefaultValueSql("'0'")
                .HasColumnName("standtargetx");
            entity.Property(e => e.Standtargety)
                .HasDefaultValueSql("'0'")
                .HasColumnName("standtargety");
            entity.Property(e => e.Standtargetz)
                .HasDefaultValueSql("'0'")
                .HasColumnName("standtargetz");
            entity.Property(e => e.Text)
                .HasMaxLength(255)
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.TextureAnimation).HasColumnType("blob");
            entity.Property(e => e.TouchName)
                .HasMaxLength(255)
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.Vehicle).HasColumnType("text");
            entity.Property(e => e.VelocityX).HasDefaultValueSql("'0'");
            entity.Property(e => e.VelocityY).HasDefaultValueSql("'0'");
            entity.Property(e => e.VelocityZ).HasDefaultValueSql("'0'");
        });

        modelBuilder.Entity<Primitem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PRIMARY");

            entity
                .ToTable("primitems")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.PrimId, "primitems_primid");

            entity.Property(e => e.ItemId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("itemID");
            entity.Property(e => e.AssetId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("assetID");
            entity.Property(e => e.AssetType).HasColumnName("assetType");
            entity.Property(e => e.BasePermissions).HasColumnName("basePermissions");
            entity.Property(e => e.CreationDate).HasColumnName("creationDate");
            entity.Property(e => e.CreatorId)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("CreatorID");
            entity.Property(e => e.CurrentPermissions).HasColumnName("currentPermissions");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.EveryonePermissions).HasColumnName("everyonePermissions");
            entity.Property(e => e.Flags).HasColumnName("flags");
            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("groupID");
            entity.Property(e => e.GroupPermissions).HasColumnName("groupPermissions");
            entity.Property(e => e.InvType).HasColumnName("invType");
            entity.Property(e => e.LastOwnerId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("lastOwnerID");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.NextPermissions).HasColumnName("nextPermissions");
            entity.Property(e => e.OwnerId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("ownerID");
            entity.Property(e => e.ParentFolderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parentFolderID");
            entity.Property(e => e.PrimId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("primID");
        });

        modelBuilder.Entity<Primshape>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("primshapes")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("UUID");
            entity.Property(e => e.MatOvrd).HasColumnType("blob");
            entity.Property(e => e.Media).HasColumnType("text");
            entity.Property(e => e.Pcode).HasColumnName("PCode");
        });

        modelBuilder.Entity<Regionban>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("regionban")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.BannedIp)
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnName("bannedIp");
            entity.Property(e => e.BannedIpHostMask)
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnName("bannedIpHostMask");
            entity.Property(e => e.BannedUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("bannedUUID");
            entity.Property(e => e.RegionUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("regionUUID");
        });

        modelBuilder.Entity<Regionenvironment>(entity =>
        {
            entity.HasKey(e => e.RegionId).HasName("PRIMARY");

            entity
                .ToTable("regionenvironment")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RegionId)
                .HasMaxLength(36)
                .HasColumnName("region_id");
            entity.Property(e => e.LlsdSettings)
                .HasColumnType("mediumtext")
                .HasColumnName("llsd_settings");
        });

        modelBuilder.Entity<Regionextra>(entity =>
        {
            entity.HasKey(e => new { e.RegionId, e.Name })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("regionextra")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RegionId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("RegionID");
            entity.Property(e => e.Name).HasMaxLength(32);
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<Regionsetting>(entity =>
        {
            entity.HasKey(e => e.RegionUuid).HasName("PRIMARY");

            entity
                .ToTable("regionsettings")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("regionUUID");
            entity.Property(e => e.AgentLimit).HasColumnName("agent_limit");
            entity.Property(e => e.AllowDamage).HasColumnName("allow_damage");
            entity.Property(e => e.AllowLandJoinDivide).HasColumnName("allow_land_join_divide");
            entity.Property(e => e.AllowLandResell).HasColumnName("allow_land_resell");
            entity.Property(e => e.BlockFly).HasColumnName("block_fly");
            entity.Property(e => e.BlockSearch).HasColumnName("block_search");
            entity.Property(e => e.BlockShowInSearch).HasColumnName("block_show_in_search");
            entity.Property(e => e.BlockTerraform).HasColumnName("block_terraform");
            entity.Property(e => e.CacheId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("cacheID");
            entity.Property(e => e.Casino).HasColumnName("casino");
            entity.Property(e => e.Covenant)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("covenant");
            entity.Property(e => e.CovenantDatetime).HasColumnName("covenant_datetime");
            entity.Property(e => e.DisableCollisions).HasColumnName("disable_collisions");
            entity.Property(e => e.DisablePhysics).HasColumnName("disable_physics");
            entity.Property(e => e.DisableScripts).HasColumnName("disable_scripts");
            entity.Property(e => e.Elevation1Ne).HasColumnName("elevation_1_ne");
            entity.Property(e => e.Elevation1Nw).HasColumnName("elevation_1_nw");
            entity.Property(e => e.Elevation1Se).HasColumnName("elevation_1_se");
            entity.Property(e => e.Elevation1Sw).HasColumnName("elevation_1_sw");
            entity.Property(e => e.Elevation2Ne).HasColumnName("elevation_2_ne");
            entity.Property(e => e.Elevation2Nw).HasColumnName("elevation_2_nw");
            entity.Property(e => e.Elevation2Se).HasColumnName("elevation_2_se");
            entity.Property(e => e.Elevation2Sw).HasColumnName("elevation_2_sw");
            entity.Property(e => e.FixedSun).HasColumnName("fixed_sun");
            entity.Property(e => e.LoadedCreationDatetime).HasColumnName("loaded_creation_datetime");
            entity.Property(e => e.LoadedCreationId)
                .HasMaxLength(64)
                .HasColumnName("loaded_creation_id");
            entity.Property(e => e.MapTileId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("map_tile_ID");
            entity.Property(e => e.Maturity).HasColumnName("maturity");
            entity.Property(e => e.ObjectBonus).HasColumnName("object_bonus");
            entity.Property(e => e.ParcelTileId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("parcel_tile_ID");
            entity.Property(e => e.RestrictPushing).HasColumnName("restrict_pushing");
            entity.Property(e => e.SunPosition).HasColumnName("sun_position");
            entity.Property(e => e.Sunvectorx).HasColumnName("sunvectorx");
            entity.Property(e => e.Sunvectory).HasColumnName("sunvectory");
            entity.Property(e => e.Sunvectorz).HasColumnName("sunvectorz");
            entity.Property(e => e.TelehubObject)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'");
            entity.Property(e => e.TerrainLowerLimit).HasColumnName("terrain_lower_limit");
            entity.Property(e => e.TerrainRaiseLimit).HasColumnName("terrain_raise_limit");
            entity.Property(e => e.TerrainTexture1)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("terrain_texture_1");
            entity.Property(e => e.TerrainTexture2)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("terrain_texture_2");
            entity.Property(e => e.TerrainTexture3)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("terrain_texture_3");
            entity.Property(e => e.TerrainTexture4)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("terrain_texture_4");
            entity.Property(e => e.UseEstateSun).HasColumnName("use_estate_sun");
            entity.Property(e => e.WaterHeight).HasColumnName("water_height");
        });

        modelBuilder.Entity<Regionwindlight>(entity =>
        {
            entity.HasKey(e => e.RegionId).HasName("PRIMARY");

            entity
                .ToTable("regionwindlight")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RegionId)
                .HasMaxLength(36)
                .HasDefaultValueSql("'000000-0000-0000-0000-000000000000'")
                .HasColumnName("region_id");
            entity.Property(e => e.AmbientB)
                .HasDefaultValueSql("'0.34999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("ambient_b");
            entity.Property(e => e.AmbientG)
                .HasDefaultValueSql("'0.34999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("ambient_g");
            entity.Property(e => e.AmbientI)
                .HasDefaultValueSql("'0.34999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("ambient_i");
            entity.Property(e => e.AmbientR)
                .HasDefaultValueSql("'0.34999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("ambient_r");
            entity.Property(e => e.BigWaveDirectionX)
                .HasDefaultValueSql("'1.04999995'")
                .HasColumnType("float(9,8)")
                .HasColumnName("big_wave_direction_x");
            entity.Property(e => e.BigWaveDirectionY)
                .HasDefaultValueSql("'-0.41999999'")
                .HasColumnType("float(9,8)")
                .HasColumnName("big_wave_direction_y");
            entity.Property(e => e.BlueDensityB)
                .HasDefaultValueSql("'0.38000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("blue_density_b");
            entity.Property(e => e.BlueDensityG)
                .HasDefaultValueSql("'0.22000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("blue_density_g");
            entity.Property(e => e.BlueDensityI)
                .HasDefaultValueSql("'0.38000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("blue_density_i");
            entity.Property(e => e.BlueDensityR)
                .HasDefaultValueSql("'0.12000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("blue_density_r");
            entity.Property(e => e.BlurMultiplier)
                .HasDefaultValueSql("'0.04000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("blur_multiplier");
            entity.Property(e => e.CloudColorB)
                .HasDefaultValueSql("'0.41000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_color_b");
            entity.Property(e => e.CloudColorG)
                .HasDefaultValueSql("'0.41000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_color_g");
            entity.Property(e => e.CloudColorI)
                .HasDefaultValueSql("'0.41000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_color_i");
            entity.Property(e => e.CloudColorR)
                .HasDefaultValueSql("'0.41000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_color_r");
            entity.Property(e => e.CloudCoverage)
                .HasDefaultValueSql("'0.27000001'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_coverage");
            entity.Property(e => e.CloudDensity)
                .HasDefaultValueSql("'1.00000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_density");
            entity.Property(e => e.CloudDetailDensity)
                .HasDefaultValueSql("'0.12000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_detail_density");
            entity.Property(e => e.CloudDetailX)
                .HasDefaultValueSql("'1.00000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_detail_x");
            entity.Property(e => e.CloudDetailY)
                .HasDefaultValueSql("'0.52999997'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_detail_y");
            entity.Property(e => e.CloudScale)
                .HasDefaultValueSql("'0.41999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_scale");
            entity.Property(e => e.CloudScrollX)
                .HasDefaultValueSql("'0.2000000'")
                .HasColumnType("float(9,7)")
                .HasColumnName("cloud_scroll_x");
            entity.Property(e => e.CloudScrollXLock).HasColumnName("cloud_scroll_x_lock");
            entity.Property(e => e.CloudScrollY)
                .HasDefaultValueSql("'0.0100000'")
                .HasColumnType("float(9,7)")
                .HasColumnName("cloud_scroll_y");
            entity.Property(e => e.CloudScrollYLock).HasColumnName("cloud_scroll_y_lock");
            entity.Property(e => e.CloudX)
                .HasDefaultValueSql("'1.00000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_x");
            entity.Property(e => e.CloudY)
                .HasDefaultValueSql("'0.52999997'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("cloud_y");
            entity.Property(e => e.DensityMultiplier)
                .HasDefaultValueSql("'0.18000001'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("density_multiplier");
            entity.Property(e => e.DistanceMultiplier)
                .HasDefaultValueSql("'0.800000'")
                .HasColumnType("float(9,6) unsigned")
                .HasColumnName("distance_multiplier");
            entity.Property(e => e.DrawClassicClouds)
                .HasDefaultValueSql("'1'")
                .HasColumnName("draw_classic_clouds");
            entity.Property(e => e.EastAngle)
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("east_angle");
            entity.Property(e => e.FresnelOffset)
                .HasDefaultValueSql("'0.50000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("fresnel_offset");
            entity.Property(e => e.FresnelScale)
                .HasDefaultValueSql("'0.40000001'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("fresnel_scale");
            entity.Property(e => e.HazeDensity)
                .HasDefaultValueSql("'0.69999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("haze_density");
            entity.Property(e => e.HazeHorizon)
                .HasDefaultValueSql("'0.19000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("haze_horizon");
            entity.Property(e => e.HorizonB)
                .HasDefaultValueSql("'0.31999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("horizon_b");
            entity.Property(e => e.HorizonG)
                .HasDefaultValueSql("'0.25000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("horizon_g");
            entity.Property(e => e.HorizonI)
                .HasDefaultValueSql("'0.31999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("horizon_i");
            entity.Property(e => e.HorizonR)
                .HasDefaultValueSql("'0.25000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("horizon_r");
            entity.Property(e => e.LittleWaveDirectionX)
                .HasDefaultValueSql("'1.11000001'")
                .HasColumnType("float(9,8)")
                .HasColumnName("little_wave_direction_x");
            entity.Property(e => e.LittleWaveDirectionY)
                .HasDefaultValueSql("'-1.15999997'")
                .HasColumnType("float(9,8)")
                .HasColumnName("little_wave_direction_y");
            entity.Property(e => e.MaxAltitude)
                .HasDefaultValueSql("'1605'")
                .HasColumnName("max_altitude");
            entity.Property(e => e.NormalMapTexture)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'822ded49-9a6c-f61c-cb89-6df54f42cdf4'")
                .HasColumnName("normal_map_texture");
            entity.Property(e => e.ReflectionWaveletScale1)
                .HasDefaultValueSql("'2.0000000'")
                .HasColumnType("float(9,7) unsigned")
                .HasColumnName("reflection_wavelet_scale_1");
            entity.Property(e => e.ReflectionWaveletScale2)
                .HasDefaultValueSql("'2.0000000'")
                .HasColumnType("float(9,7) unsigned")
                .HasColumnName("reflection_wavelet_scale_2");
            entity.Property(e => e.ReflectionWaveletScale3)
                .HasDefaultValueSql("'2.0000000'")
                .HasColumnType("float(9,7) unsigned")
                .HasColumnName("reflection_wavelet_scale_3");
            entity.Property(e => e.RefractScaleAbove)
                .HasDefaultValueSql("'0.03000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("refract_scale_above");
            entity.Property(e => e.RefractScaleBelow)
                .HasDefaultValueSql("'0.20000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("refract_scale_below");
            entity.Property(e => e.SceneGamma)
                .HasDefaultValueSql("'1.0000000'")
                .HasColumnType("float(9,7) unsigned")
                .HasColumnName("scene_gamma");
            entity.Property(e => e.StarBrightness)
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("star_brightness");
            entity.Property(e => e.SunGlowFocus)
                .HasDefaultValueSql("'0.10000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_glow_focus");
            entity.Property(e => e.SunGlowSize)
                .HasDefaultValueSql("'1.75000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_glow_size");
            entity.Property(e => e.SunMoonColorB)
                .HasDefaultValueSql("'0.30000001'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_moon_color_b");
            entity.Property(e => e.SunMoonColorG)
                .HasDefaultValueSql("'0.25999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_moon_color_g");
            entity.Property(e => e.SunMoonColorI)
                .HasDefaultValueSql("'0.30000001'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_moon_color_i");
            entity.Property(e => e.SunMoonColorR)
                .HasDefaultValueSql("'0.23999999'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_moon_color_r");
            entity.Property(e => e.SunMoonPosition)
                .HasDefaultValueSql("'0.31700000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("sun_moon_position");
            entity.Property(e => e.UnderwaterFogModifier)
                .HasDefaultValueSql("'0.25000000'")
                .HasColumnType("float(9,8) unsigned")
                .HasColumnName("underwater_fog_modifier");
            entity.Property(e => e.WaterColorB)
                .HasDefaultValueSql("'64.000000'")
                .HasColumnType("float(9,6) unsigned")
                .HasColumnName("water_color_b");
            entity.Property(e => e.WaterColorG)
                .HasDefaultValueSql("'38.000000'")
                .HasColumnType("float(9,6) unsigned")
                .HasColumnName("water_color_g");
            entity.Property(e => e.WaterColorR)
                .HasDefaultValueSql("'4.000000'")
                .HasColumnType("float(9,6) unsigned")
                .HasColumnName("water_color_r");
            entity.Property(e => e.WaterFogDensityExponent)
                .HasDefaultValueSql("'4.0000000'")
                .HasColumnType("float(9,7) unsigned")
                .HasColumnName("water_fog_density_exponent");
        });

        modelBuilder.Entity<SpawnPoint>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("spawn_points")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.RegionId, "RegionID");

            entity.Property(e => e.RegionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("RegionID");
        });

        modelBuilder.Entity<Terrain>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("terrain")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(255)
                .HasColumnName("RegionUUID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
