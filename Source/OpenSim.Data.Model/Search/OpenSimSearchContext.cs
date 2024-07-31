using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OpenSim.Data.Model.Search;

public partial class OpenSimSearchContext : DbContext
{
    public OpenSimSearchContext(DbContextOptions<OpenSimSearchContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Allparcel> Allparcels { get; set; }

    public virtual DbSet<Classified> Classifieds { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Hostsregister> Hostsregisters { get; set; }

    public virtual DbSet<Object> Objects { get; set; }

    public virtual DbSet<Parcel> Parcels { get; set; }

    public virtual DbSet<Parcelsale> Parcelsales { get; set; }

    public virtual DbSet<Popularplace> Popularplaces { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Allparcel>(entity =>
        {
            entity.HasKey(e => e.ParcelUuid).HasName("PRIMARY");

            entity
                .ToTable("allparcels")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.HasIndex(e => e.RegionUuid, "regionUUID");

            entity.Property(e => e.ParcelUuid)
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("parcelUUID");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.GroupUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("groupUUID");
            entity.Property(e => e.InfoUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("infoUUID");
            entity.Property(e => e.Landingpoint)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("landingpoint");
            entity.Property(e => e.OwnerUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("ownerUUID");
            entity.Property(e => e.Parcelarea).HasColumnName("parcelarea");
            entity.Property(e => e.Parcelname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("parcelname");
            entity.Property(e => e.RegionUuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("regionUUID");
        });

        modelBuilder.Entity<Classified>(entity =>
        {
            entity.HasKey(e => e.Classifieduuid).HasName("PRIMARY");

            entity
                .ToTable("classifieds")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.Classifieduuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("classifieduuid");
            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("category");
            entity.Property(e => e.Classifiedflags).HasColumnName("classifiedflags");
            entity.Property(e => e.Creationdate).HasColumnName("creationdate");
            entity.Property(e => e.Creatoruuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("creatoruuid");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Expirationdate).HasColumnName("expirationdate");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Parcelname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("parcelname");
            entity.Property(e => e.Parceluuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parceluuid");
            entity.Property(e => e.Parentestate).HasColumnName("parentestate");
            entity.Property(e => e.Posglobal)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("posglobal");
            entity.Property(e => e.Priceforlisting).HasColumnName("priceforlisting");
            entity.Property(e => e.Simname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("simname");
            entity.Property(e => e.Snapshotuuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("snapshotuuid");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Eventid).HasName("PRIMARY");

            entity
                .ToTable("events")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Coveramount).HasColumnName("coveramount");
            entity.Property(e => e.Covercharge).HasColumnName("covercharge");
            entity.Property(e => e.Creatoruuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("creatoruuid");
            entity.Property(e => e.DateUtc).HasColumnName("dateUTC");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.Eventflags).HasColumnName("eventflags");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.GlobalPos)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("globalPos");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Owneruuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("owneruuid");
            entity.Property(e => e.ParcelUuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parcelUUID");
            entity.Property(e => e.Simname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("simname");
        });

        modelBuilder.Entity<Hostsregister>(entity =>
        {
            entity.HasKey(e => new { e.Host, e.Port })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("hostsregister")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.Host).HasColumnName("host");
            entity.Property(e => e.Port).HasColumnName("port");
            entity.Property(e => e.Checked).HasColumnName("checked");
            entity.Property(e => e.Failcounter).HasColumnName("failcounter");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.Nextcheck).HasColumnName("nextcheck");
            entity.Property(e => e.Register).HasColumnName("register");
        });

        modelBuilder.Entity<Object>(entity =>
        {
            entity.HasKey(e => new { e.Objectuuid, e.Parceluuid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("objects")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.Objectuuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("objectuuid");
            entity.Property(e => e.Parceluuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parceluuid");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.Location)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Regionuuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("regionuuid");
        });

        modelBuilder.Entity<Parcel>(entity =>
        {
            entity.HasKey(e => new { e.RegionUuid, e.ParcelUuid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("parcels")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.HasIndex(e => e.Description, "description");

            entity.HasIndex(e => e.Dwell, "dwell");

            entity.HasIndex(e => e.Parcelname, "name");

            entity.HasIndex(e => e.Searchcategory, "searchcategory");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("regionUUID");
            entity.Property(e => e.ParcelUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parcelUUID");
            entity.Property(e => e.Build)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("build");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasColumnName("description");
            entity.Property(e => e.Dwell).HasColumnName("dwell");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.ImageUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("imageUUID");
            entity.Property(e => e.Infouuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .HasColumnName("infouuid");
            entity.Property(e => e.Landingpoint)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("landingpoint");
            entity.Property(e => e.Mature)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValueSql("'PG'")
                .HasColumnName("mature");
            entity.Property(e => e.Parcelname)
                .IsRequired()
                .HasColumnName("parcelname");
            entity.Property(e => e.Public)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("public");
            entity.Property(e => e.Script)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("script");
            entity.Property(e => e.Searchcategory)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("searchcategory");
        });

        modelBuilder.Entity<Parcelsale>(entity =>
        {
            entity.HasKey(e => new { e.RegionUuid, e.ParcelUuid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("parcelsales")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("regionUUID");
            entity.Property(e => e.ParcelUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parcelUUID");
            entity.Property(e => e.Area).HasColumnName("area");
            entity.Property(e => e.Dwell).HasColumnName("dwell");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.InfoUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("infoUUID");
            entity.Property(e => e.Landingpoint)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("landingpoint");
            entity.Property(e => e.Mature)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValueSql("'PG'")
                .HasColumnName("mature");
            entity.Property(e => e.Parcelname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("parcelname");
            entity.Property(e => e.Parentestate)
                .HasDefaultValueSql("'1'")
                .HasColumnName("parentestate");
            entity.Property(e => e.Saleprice).HasColumnName("saleprice");
        });

        modelBuilder.Entity<Popularplace>(entity =>
        {
            entity.HasKey(e => e.ParcelUuid).HasName("PRIMARY");

            entity
                .ToTable("popularplaces")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.ParcelUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parcelUUID");
            entity.Property(e => e.Dwell).HasColumnName("dwell");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.HasPicture).HasColumnName("has_picture");
            entity.Property(e => e.InfoUuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("infoUUID");
            entity.Property(e => e.Mature)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("mature");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.RegionUuid).HasName("PRIMARY");

            entity
                .ToTable("regions")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.Property(e => e.RegionUuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("regionUUID");
            entity.Property(e => e.GatekeeperUrl)
                .HasMaxLength(255)
                .HasColumnName("gatekeeperURL");
            entity.Property(e => e.Owner)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("owner");
            entity.Property(e => e.Owneruuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("owneruuid");
            entity.Property(e => e.Regionhandle)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("regionhandle");
            entity.Property(e => e.Regionname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("regionname");
            entity.Property(e => e.Url)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("url");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
