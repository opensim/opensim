using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OpenSim.Data.Model.Core;

public partial class OpenSimCoreContext : DbContext
{
    public OpenSimCoreContext(DbContextOptions<OpenSimCoreContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AgentPref> AgentPrefs { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<Auth> Auths { get; set; }

    public virtual DbSet<Avatar> Avatars { get; set; }

    public virtual DbSet<Classified> Classifieds { get; set; }

    public virtual DbSet<EstateGroup> EstateGroups { get; set; }

    public virtual DbSet<EstateManager> EstateManagers { get; set; }

    public virtual DbSet<EstateMap> EstateMaps { get; set; }

    public virtual DbSet<EstateSetting> EstateSettings { get; set; }

    public virtual DbSet<EstateUser> EstateUsers { get; set; }

    public virtual DbSet<Estateban> Estatebans { get; set; }

    public virtual DbSet<Friend> Friends { get; set; }

    public virtual DbSet<Fsasset> Fsassets { get; set; }

    public virtual DbSet<GloebitSubscription> GloebitSubscriptions { get; set; }

    public virtual DbSet<GloebitTransaction> GloebitTransactions { get; set; }

    public virtual DbSet<GloebitUser> GloebitUsers { get; set; }

    public virtual DbSet<GridUser> GridUsers { get; set; }

    public virtual DbSet<HgTravelingDatum> HgTravelingData { get; set; }

    public virtual DbSet<ImOffline> ImOfflines { get; set; }

    public virtual DbSet<Inventoryfolder> Inventoryfolders { get; set; }

    public virtual DbSet<Inventoryitem> Inventoryitems { get; set; }

    public virtual DbSet<Migration> Migrations { get; set; }

    public virtual DbSet<MuteList> MuteLists { get; set; }

    public virtual DbSet<OsGroupsGroup> OsGroupsGroups { get; set; }

    public virtual DbSet<OsGroupsInvite> OsGroupsInvites { get; set; }

    public virtual DbSet<OsGroupsMembership> OsGroupsMemberships { get; set; }

    public virtual DbSet<OsGroupsNotice> OsGroupsNotices { get; set; }

    public virtual DbSet<OsGroupsPrincipal> OsGroupsPrincipals { get; set; }

    public virtual DbSet<OsGroupsRole> OsGroupsRoles { get; set; }

    public virtual DbSet<OsGroupsRolemembership> OsGroupsRolememberships { get; set; }

    public virtual DbSet<Presence> Presences { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<Token> Tokens { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<UserAlias> UserAliases { get; set; }

    public virtual DbSet<Userdatum> Userdata { get; set; }

    public virtual DbSet<Usernote> Usernotes { get; set; }

    public virtual DbSet<Userpick> Userpicks { get; set; }

    public virtual DbSet<Userprofile> Userprofiles { get; set; }

    public virtual DbSet<Usersetting> Usersettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AgentPref>(entity =>
        {
            entity.HasKey(e => e.PrincipalId).HasName("PRIMARY");

            entity
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID").IsUnique();

            entity.Property(e => e.PrincipalId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.AccessPrefs)
                .IsRequired()
                .HasMaxLength(2)
                .HasDefaultValueSql("'M'")
                .IsFixedLength();
            entity.Property(e => e.HoverHeight).HasColumnType("double(30,27)");
            entity.Property(e => e.Language)
                .IsRequired()
                .HasMaxLength(5)
                .HasDefaultValueSql("'en-us'")
                .IsFixedLength();
            entity.Property(e => e.LanguageIsPublic)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.PermNextOwner).HasDefaultValueSql("'532480'");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("assets")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.AccessTime)
                .HasDefaultValueSql("'0'")
                .HasColumnName("access_time");
            entity.Property(e => e.AssetFlags).HasColumnName("asset_flags");
            entity.Property(e => e.AssetType).HasColumnName("assetType");
            entity.Property(e => e.CreateTime)
                .HasDefaultValueSql("'0'")
                .HasColumnName("create_time");
            entity.Property(e => e.CreatorId)
                .IsRequired()
                .HasMaxLength(128)
                .HasDefaultValueSql("''")
                .HasColumnName("CreatorID");
            entity.Property(e => e.Data)
                .IsRequired()
                .HasColumnName("data");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("description");
            entity.Property(e => e.Local).HasColumnName("local");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.Temporary).HasColumnName("temporary");
        });

        modelBuilder.Entity<Auth>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("auth")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("UUID");
            entity.Property(e => e.AccountType)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValueSql("'UserAccount'")
                .HasColumnName("accountType");
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("passwordHash");
            entity.Property(e => e.PasswordSalt)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("passwordSalt");
            entity.Property(e => e.WebLoginKey)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("webLoginKey");
        });

        modelBuilder.Entity<Avatar>(entity =>
        {
            entity.HasKey(e => new { e.PrincipalId, e.Name })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID");

            entity.Property(e => e.PrincipalId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.Name).HasMaxLength(32);
            entity.Property(e => e.Value).HasColumnType("text");
        });

        modelBuilder.Entity<Classified>(entity =>
        {
            entity.HasKey(e => e.Classifieduuid).HasName("PRIMARY");

            entity
                .ToTable("classifieds")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

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

        modelBuilder.Entity<EstateGroup>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("estate_groups")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.EstateId, "EstateID");

            entity.Property(e => e.EstateId).HasColumnName("EstateID");
            entity.Property(e => e.Uuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<EstateManager>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("estate_managers")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.EstateId, "EstateID");

            entity.Property(e => e.EstateId).HasColumnName("EstateID");
            entity.Property(e => e.Uuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<EstateMap>(entity =>
        {
            entity.HasKey(e => e.RegionId).HasName("PRIMARY");

            entity
                .ToTable("estate_map")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.EstateId, "EstateID");

            entity.Property(e => e.RegionId)
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("RegionID");
            entity.Property(e => e.EstateId).HasColumnName("EstateID");
        });

        modelBuilder.Entity<EstateSetting>(entity =>
        {
            entity.HasKey(e => e.EstateId).HasName("PRIMARY");

            entity
                .ToTable("estate_settings")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.EstateId).HasColumnName("EstateID");
            entity.Property(e => e.AbuseEmail)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.AllowLandmark).HasDefaultValueSql("'1'");
            entity.Property(e => e.AllowParcelChanges).HasDefaultValueSql("'1'");
            entity.Property(e => e.AllowSetHome).HasDefaultValueSql("'1'");
            entity.Property(e => e.EstateName).HasMaxLength(64);
            entity.Property(e => e.EstateOwner)
                .IsRequired()
                .HasMaxLength(36);
            entity.Property(e => e.ParentEstateId).HasColumnName("ParentEstateID");
        });

        modelBuilder.Entity<EstateUser>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("estate_users")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.EstateId, "EstateID");

            entity.Property(e => e.EstateId).HasColumnName("EstateID");
            entity.Property(e => e.Uuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<Estateban>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("estateban")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.EstateId, "estateban_EstateID");

            entity.Property(e => e.BanTime).HasColumnName("banTime");
            entity.Property(e => e.BannedIp)
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnName("bannedIp");
            entity.Property(e => e.BannedIpHostMask)
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnName("bannedIpHostMask");
            entity.Property(e => e.BannedNameMask)
                .HasMaxLength(64)
                .HasColumnName("bannedNameMask");
            entity.Property(e => e.BannedUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("bannedUUID");
            entity.Property(e => e.BanningUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("banningUUID");
            entity.Property(e => e.EstateId).HasColumnName("EstateID");
        });

        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasKey(e => new { e.PrincipalId, e.Friend1 })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 36, 36 });

            entity
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID");

            entity.Property(e => e.PrincipalId)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("PrincipalID");
            entity.Property(e => e.Friend1).HasColumnName("Friend");
            entity.Property(e => e.Flags)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("'0'");
            entity.Property(e => e.Offered)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValueSql("'0'");
        });

        modelBuilder.Entity<Fsasset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.AccessTime, "idx_fsassets_access_time");
            
            entity
                .ToTable("fsassets")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.AccessTime).HasColumnName("access_time");
            entity.Property(e => e.AssetFlags).HasColumnName("asset_flags");
            entity.Property(e => e.CreateTime).HasColumnName("create_time");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("description");
            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(80)
                .IsFixedLength()
                .HasColumnName("hash");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("name");
            entity.Property(e => e.Type).HasColumnName("type");
        });

        modelBuilder.Entity<GloebitSubscription>(entity =>
        {
            entity.HasKey(e => new { e.ObjectId, e.AppKey, e.GlbApiUrl })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.CTime, "ix_cts");

            entity.HasIndex(e => e.ObjectId, "ix_oid");

            entity.HasIndex(e => e.SubscriptionId, "ix_sid");

            entity.HasIndex(e => new { e.SubscriptionId, e.GlbApiUrl }, "k_sub_api").IsUnique();

            entity.Property(e => e.ObjectId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("ObjectID");
            entity.Property(e => e.AppKey).HasMaxLength(64);
            entity.Property(e => e.GlbApiUrl).HasMaxLength(100);
            entity.Property(e => e.CTime)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("cTime");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Enabled).HasColumnName("enabled");
            entity.Property(e => e.ObjectName).HasMaxLength(64);
            entity.Property(e => e.SubscriptionId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("SubscriptionID");
        });

        modelBuilder.Entity<GloebitTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PRIMARY");

            entity
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.CTime, "ix_cts");

            entity.HasIndex(e => e.PayeeId, "ix_payeeid");

            entity.HasIndex(e => e.PayerId, "ix_payerid");

            entity.HasIndex(e => e.PartId, "ix_pid");

            entity.HasIndex(e => e.SubscriptionId, "ix_sid");

            entity.HasIndex(e => e.TransactionType, "ix_tt");

            entity.Property(e => e.TransactionId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("TransactionID");
            entity.Property(e => e.CTime)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("cTime");
            entity.Property(e => e.Canceled).HasColumnName("canceled");
            entity.Property(e => e.CategoryId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("CategoryID");
            entity.Property(e => e.Consumed).HasColumnName("consumed");
            entity.Property(e => e.Enacted).HasColumnName("enacted");
            entity.Property(e => e.EnactedTime)
                .HasColumnType("timestamp")
                .HasColumnName("enactedTime");
            entity.Property(e => e.FinishedTime)
                .HasColumnType("timestamp")
                .HasColumnName("finishedTime");
            entity.Property(e => e.PartDescription).HasMaxLength(128);
            entity.Property(e => e.PartId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PartID");
            entity.Property(e => e.PartName).HasMaxLength(64);
            entity.Property(e => e.PayeeId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PayeeID");
            entity.Property(e => e.PayeeName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.PayerId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PayerID");
            entity.Property(e => e.PayerName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.ResponseReason)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.ResponseStatus)
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(e => e.SubscriptionId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("SubscriptionID");
            entity.Property(e => e.TransactionTypeString)
                .IsRequired()
                .HasMaxLength(64);
        });

        modelBuilder.Entity<GloebitUser>(entity =>
        {
            entity.HasKey(e => new { e.AppKey, e.PrincipalId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.PrincipalId, "ix_gu_pid");

            entity.Property(e => e.AppKey)
                .HasMaxLength(36)
                .IsFixedLength();
            entity.Property(e => e.PrincipalId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.GloebitId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("GloebitID");
            entity.Property(e => e.GloebitToken)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength();
            entity.Property(e => e.LastSessionId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("LastSessionID");
        });

        modelBuilder.Entity<GridUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("GridUser")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.HomeLookAt)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("'<0,0,0>'")
                .IsFixedLength();
            entity.Property(e => e.HomePosition)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("'<0,0,0>'")
                .IsFixedLength();
            entity.Property(e => e.HomeRegionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("HomeRegionID");
            entity.Property(e => e.LastLookAt)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("'<0,0,0>'")
                .IsFixedLength();
            entity.Property(e => e.LastPosition)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("'<0,0,0>'")
                .IsFixedLength();
            entity.Property(e => e.LastRegionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("LastRegionID");
            entity.Property(e => e.Login)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("'0'")
                .IsFixedLength();
            entity.Property(e => e.Logout)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("'0'")
                .IsFixedLength();
            entity.Property(e => e.Online)
                .IsRequired()
                .HasMaxLength(5)
                .HasDefaultValueSql("'false'")
                .IsFixedLength();
        });

        modelBuilder.Entity<HgTravelingDatum>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PRIMARY");

            entity
                .ToTable("hg_traveling_data")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.UserId, "UserID");

            entity.Property(e => e.SessionId)
                .HasMaxLength(36)
                .HasColumnName("SessionID");
            entity.Property(e => e.ClientIpaddress)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("''")
                .HasColumnName("ClientIPAddress");
            entity.Property(e => e.GridExternalName)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.MyIpaddress)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValueSql("''")
                .HasColumnName("MyIPAddress");
            entity.Property(e => e.ServiceToken)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.Tmstamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("TMStamp");
            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("UserID");
        });

        modelBuilder.Entity<ImOffline>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("im_offline")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.FromId, "FromID");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID");

            entity.Property(e => e.Id)
                .HasColumnType("mediumint")
                .HasColumnName("ID");
            entity.Property(e => e.FromId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("FromID");
            entity.Property(e => e.Message)
                .IsRequired()
                .HasColumnType("text");
            entity.Property(e => e.PrincipalId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.Tmstamp)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("TMStamp");
        });

        modelBuilder.Entity<Inventoryfolder>(entity =>
        {
            entity.HasKey(e => e.FolderId).HasName("PRIMARY");

            entity
                .ToTable("inventoryfolders")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.AgentId, "inventoryfolders_agentid");

            entity.HasIndex(e => e.ParentFolderId, "inventoryfolders_parentFolderid");

            entity.Property(e => e.FolderId)
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("folderID");
            entity.Property(e => e.AgentId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("agentID");
            entity.Property(e => e.FolderName)
                .HasMaxLength(64)
                .HasColumnName("folderName");
            entity.Property(e => e.ParentFolderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parentFolderID");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Version).HasColumnName("version");
        });

        modelBuilder.Entity<Inventoryitem>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PRIMARY");

            entity
                .ToTable("inventoryitems")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.AvatarId, "inventoryitems_avatarid");

            entity.HasIndex(e => e.ParentFolderId, "inventoryitems_parentFolderid");

            entity.Property(e => e.InventoryId)
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("inventoryID");
            entity.Property(e => e.AssetId)
                .HasMaxLength(36)
                .HasColumnName("assetID");
            entity.Property(e => e.AssetType).HasColumnName("assetType");
            entity.Property(e => e.AvatarId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("avatarID");
            entity.Property(e => e.CreationDate).HasColumnName("creationDate");
            entity.Property(e => e.CreatorId)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("creatorID");
            entity.Property(e => e.Flags).HasColumnName("flags");
            entity.Property(e => e.GroupId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("groupID");
            entity.Property(e => e.GroupOwned).HasColumnName("groupOwned");
            entity.Property(e => e.InvType).HasColumnName("invType");
            entity.Property(e => e.InventoryBasePermissions).HasColumnName("inventoryBasePermissions");
            entity.Property(e => e.InventoryCurrentPermissions).HasColumnName("inventoryCurrentPermissions");
            entity.Property(e => e.InventoryDescription)
                .HasMaxLength(128)
                .HasColumnName("inventoryDescription");
            entity.Property(e => e.InventoryEveryOnePermissions).HasColumnName("inventoryEveryOnePermissions");
            entity.Property(e => e.InventoryGroupPermissions).HasColumnName("inventoryGroupPermissions");
            entity.Property(e => e.InventoryName)
                .HasMaxLength(64)
                .HasColumnName("inventoryName");
            entity.Property(e => e.InventoryNextPermissions).HasColumnName("inventoryNextPermissions");
            entity.Property(e => e.ParentFolderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("parentFolderID");
            entity.Property(e => e.SalePrice).HasColumnName("salePrice");
            entity.Property(e => e.SaleType).HasColumnName("saleType");
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

        modelBuilder.Entity<MuteList>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("MuteList")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.AgentId, "AgentID");

            entity.HasIndex(e => new { e.AgentId, e.MuteId, e.MuteName }, "AgentID_2").IsUnique();

            entity.Property(e => e.AgentId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("AgentID");
            entity.Property(e => e.MuteId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("MuteID");
            entity.Property(e => e.MuteName)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("''");
            entity.Property(e => e.MuteType).HasDefaultValueSql("'1'");
        });

        modelBuilder.Entity<OsGroupsGroup>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("PRIMARY");

            entity
                .ToTable("os_groups_groups")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.Name, "Name")
                .IsUnique()
                .HasAnnotation("MySql:FullTextIndex", true);

            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.Charter)
                .IsRequired()
                .HasColumnType("text")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.FounderId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("FounderID");
            entity.Property(e => e.InsigniaId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("InsigniaID");
            entity.Property(e => e.Location)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.OpenEnrollment)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.OwnerRoleId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("OwnerRoleID");
        });

        modelBuilder.Entity<OsGroupsInvite>(entity =>
        {
            entity.HasKey(e => e.InviteId).HasName("PRIMARY");

            entity
                .ToTable("os_groups_invites")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => new { e.GroupId, e.PrincipalId }, "PrincipalGroup").IsUnique();

            entity.Property(e => e.InviteId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("InviteID");
            entity.Property(e => e.GroupId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.PrincipalId)
                .IsRequired()
                .HasDefaultValueSql("''")
                .HasColumnName("PrincipalID");
            entity.Property(e => e.RoleId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("RoleID");
            entity.Property(e => e.Tmstamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("TMStamp");
        });

        modelBuilder.Entity<OsGroupsMembership>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.PrincipalId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("os_groups_membership")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID");

            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.PrincipalId)
                .HasDefaultValueSql("''")
                .HasColumnName("PrincipalID");
            entity.Property(e => e.AcceptNotices).HasDefaultValueSql("'1'");
            entity.Property(e => e.AccessToken)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.ListInProfile).HasDefaultValueSql("'1'");
            entity.Property(e => e.SelectedRoleId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("SelectedRoleID");
        });

        modelBuilder.Entity<OsGroupsNotice>(entity =>
        {
            entity.HasKey(e => e.NoticeId).HasName("PRIMARY");

            entity
                .ToTable("os_groups_notices")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.GroupId, "GroupID");

            entity.HasIndex(e => e.Tmstamp, "TMStamp");

            entity.Property(e => e.NoticeId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("NoticeID");
            entity.Property(e => e.AttachmentItemId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("AttachmentItemID");
            entity.Property(e => e.AttachmentName)
                .IsRequired()
                .HasMaxLength(128)
                .HasDefaultValueSql("''");
            entity.Property(e => e.AttachmentOwnerId)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("AttachmentOwnerID");
            entity.Property(e => e.FromName)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''");
            entity.Property(e => e.GroupId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.Message)
                .IsRequired()
                .HasColumnType("text")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.Tmstamp).HasColumnName("TMStamp");
        });

        modelBuilder.Entity<OsGroupsPrincipal>(entity =>
        {
            entity.HasKey(e => e.PrincipalId).HasName("PRIMARY");

            entity
                .ToTable("os_groups_principals")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.PrincipalId)
                .HasDefaultValueSql("''")
                .HasColumnName("PrincipalID");
            entity.Property(e => e.ActiveGroupId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("ActiveGroupID");
        });

        modelBuilder.Entity<OsGroupsRole>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.RoleId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("os_groups_roles")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.GroupId, "GroupID");

            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.RoleId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("RoleID");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
        });

        modelBuilder.Entity<OsGroupsRolemembership>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.RoleId, e.PrincipalId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity
                .ToTable("os_groups_rolemembership")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID");

            entity.Property(e => e.GroupId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("GroupID");
            entity.Property(e => e.RoleId)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("RoleID");
            entity.Property(e => e.PrincipalId)
                .HasDefaultValueSql("''")
                .HasColumnName("PrincipalID");
        });

        modelBuilder.Entity<Presence>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Presence")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.RegionId, "RegionID");

            entity.HasIndex(e => e.SessionId, "SessionID").IsUnique();

            entity.HasIndex(e => e.UserId, "UserID");

            entity.Property(e => e.LastSeen)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp");
            entity.Property(e => e.RegionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("RegionID");
            entity.Property(e => e.SecureSessionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("SecureSessionID");
            entity.Property(e => e.SessionId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("SessionID");
            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("UserID");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("regions")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.ScopeId, "ScopeID");

            entity.HasIndex(e => e.Flags, "flags");

            entity.HasIndex(e => new { e.EastOverrideHandle, e.WestOverrideHandle, e.SouthOverrideHandle, e.NorthOverrideHandle }, "overrideHandles");

            entity.HasIndex(e => e.RegionHandle, "regionHandle");

            entity.HasIndex(e => e.RegionName, "regionName");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .HasColumnName("uuid");
            entity.Property(e => e.Access)
                .HasDefaultValueSql("'1'")
                .HasColumnName("access");
            entity.Property(e => e.EastOverrideHandle).HasColumnName("eastOverrideHandle");
            entity.Property(e => e.Flags).HasColumnName("flags");
            entity.Property(e => e.LastSeen).HasColumnName("last_seen");
            entity.Property(e => e.LocX).HasColumnName("locX");
            entity.Property(e => e.LocY).HasColumnName("locY");
            entity.Property(e => e.LocZ).HasColumnName("locZ");
            entity.Property(e => e.NorthOverrideHandle).HasColumnName("northOverrideHandle");
            entity.Property(e => e.OriginUuid)
                .HasMaxLength(36)
                .HasColumnName("originUUID");
            entity.Property(e => e.OwnerUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .HasColumnName("owner_uuid");
            entity.Property(e => e.ParcelMapTexture)
                .HasMaxLength(36)
                .HasColumnName("parcelMapTexture");
            entity.Property(e => e.PrincipalId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.RegionAssetRecvKey)
                .HasMaxLength(128)
                .HasColumnName("regionAssetRecvKey");
            entity.Property(e => e.RegionAssetSendKey)
                .HasMaxLength(128)
                .HasColumnName("regionAssetSendKey");
            entity.Property(e => e.RegionAssetUri)
                .HasMaxLength(255)
                .HasColumnName("regionAssetURI");
            entity.Property(e => e.RegionDataUri)
                .HasMaxLength(255)
                .HasColumnName("regionDataURI");
            entity.Property(e => e.RegionHandle).HasColumnName("regionHandle");
            entity.Property(e => e.RegionMapTexture)
                .HasMaxLength(36)
                .HasColumnName("regionMapTexture");
            entity.Property(e => e.RegionName)
                .HasMaxLength(128)
                .HasColumnName("regionName");
            entity.Property(e => e.RegionRecvKey)
                .HasMaxLength(128)
                .HasColumnName("regionRecvKey");
            entity.Property(e => e.RegionSecret)
                .HasMaxLength(128)
                .HasColumnName("regionSecret");
            entity.Property(e => e.RegionSendKey)
                .HasMaxLength(128)
                .HasColumnName("regionSendKey");
            entity.Property(e => e.RegionUserRecvKey)
                .HasMaxLength(128)
                .HasColumnName("regionUserRecvKey");
            entity.Property(e => e.RegionUserSendKey)
                .HasMaxLength(128)
                .HasColumnName("regionUserSendKey");
            entity.Property(e => e.RegionUserUri)
                .HasMaxLength(255)
                .HasColumnName("regionUserURI");
            entity.Property(e => e.ScopeId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("ScopeID");
            entity.Property(e => e.ServerHttpPort).HasColumnName("serverHttpPort");
            entity.Property(e => e.ServerIp)
                .HasMaxLength(64)
                .HasColumnName("serverIP");
            entity.Property(e => e.ServerPort).HasColumnName("serverPort");
            entity.Property(e => e.ServerRemotingPort).HasColumnName("serverRemotingPort");
            entity.Property(e => e.ServerUri)
                .HasMaxLength(255)
                .HasColumnName("serverURI");
            entity.Property(e => e.SizeX).HasColumnName("sizeX");
            entity.Property(e => e.SizeY).HasColumnName("sizeY");
            entity.Property(e => e.SouthOverrideHandle).HasColumnName("southOverrideHandle");
            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.WestOverrideHandle).HasColumnName("westOverrideHandle");
        });

        modelBuilder.Entity<Token>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("tokens")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.Uuid, "UUID");

            entity.HasIndex(e => e.Token1, "token");

            entity.HasIndex(e => new { e.Uuid, e.Token1 }, "uuid_token").IsUnique();

            entity.HasIndex(e => e.Validity, "validity");

            entity.Property(e => e.Token1)
                .IsRequired()
                .HasColumnName("token");
            entity.Property(e => e.Uuid)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("UUID");
            entity.Property(e => e.Validity)
                .HasColumnType("datetime")
                .HasColumnName("validity");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity
                .HasNoKey()
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.Email, "Email");

            entity.HasIndex(e => e.FirstName, "FirstName");

            entity.HasIndex(e => e.LastName, "LastName");

            entity.HasIndex(e => new { e.FirstName, e.LastName }, "Name");

            entity.HasIndex(e => e.PrincipalId, "PrincipalID").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.Email).HasMaxLength(64);
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(e => e.PrincipalId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("PrincipalID");
            entity.Property(e => e.ScopeId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("ScopeID");
            entity.Property(e => e.ServiceUrls)
                .HasColumnType("text")
                .HasColumnName("ServiceURLs");
            entity.Property(e => e.UserTitle)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .UseCollation("utf8mb3_general_ci")
                .HasCharSet("utf8mb3");
        });

        modelBuilder.Entity<UserAlias>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("UserAlias")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => e.AliasId, "AliasID").IsUnique();

            entity.HasIndex(e => e.Id, "Id").IsUnique();

            entity.HasIndex(e => e.UserId, "UserID");

            entity.Property(e => e.AliasId)
                .IsRequired()
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("AliasID");
            entity.Property(e => e.Description).HasMaxLength(80);
            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                .IsFixedLength()
                .HasColumnName("UserID");
        });

        modelBuilder.Entity<Userdatum>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.TagId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("userdata")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength();
            entity.Property(e => e.TagId).HasMaxLength(64);
            entity.Property(e => e.DataKey).HasMaxLength(255);
            entity.Property(e => e.DataVal).HasMaxLength(255);
        });

        modelBuilder.Entity<Usernote>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("usernotes")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.HasIndex(e => new { e.Useruuid, e.Targetuuid }, "useruuid").IsUnique();

            entity.Property(e => e.Notes)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.Targetuuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("targetuuid");
            entity.Property(e => e.Useruuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("useruuid");
        });

        modelBuilder.Entity<Userpick>(entity =>
        {
            entity.HasKey(e => e.Pickuuid).HasName("PRIMARY");

            entity
                .ToTable("userpicks")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.Pickuuid)
                .HasMaxLength(36)
                .HasColumnName("pickuuid");
            entity.Property(e => e.Creatoruuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("creatoruuid");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Enabled)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("enabled");
            entity.Property(e => e.Gatekeeper)
                .HasMaxLength(255)
                .HasColumnName("gatekeeper");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Originalname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("originalname");
            entity.Property(e => e.Parceluuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("parceluuid");
            entity.Property(e => e.Posglobal)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("posglobal");
            entity.Property(e => e.Simname)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("simname");
            entity.Property(e => e.Snapshotuuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("snapshotuuid");
            entity.Property(e => e.Sortorder).HasColumnName("sortorder");
            entity.Property(e => e.Toppick)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("toppick");
            entity.Property(e => e.User)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<Userprofile>(entity =>
        {
            entity.HasKey(e => e.Useruuid).HasName("PRIMARY");

            entity
                .ToTable("userprofile")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.Useruuid)
                .HasMaxLength(36)
                .HasColumnName("useruuid");
            entity.Property(e => e.ProfileAboutText)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("profileAboutText");
            entity.Property(e => e.ProfileAllowPublish)
                .IsRequired()
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("profileAllowPublish");
            entity.Property(e => e.ProfileFirstImage)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("profileFirstImage");
            entity.Property(e => e.ProfileFirstText)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("profileFirstText");
            entity.Property(e => e.ProfileImage)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("profileImage");
            entity.Property(e => e.ProfileLanguages)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("profileLanguages");
            entity.Property(e => e.ProfileMaturePublish)
                .IsRequired()
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("profileMaturePublish");
            entity.Property(e => e.ProfilePartner)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("profilePartner");
            entity.Property(e => e.ProfileSkillsMask).HasColumnName("profileSkillsMask");
            entity.Property(e => e.ProfileSkillsText)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("profileSkillsText");
            entity.Property(e => e.ProfileUrl)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("profileURL");
            entity.Property(e => e.ProfileWantToMask).HasColumnName("profileWantToMask");
            entity.Property(e => e.ProfileWantToText)
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("profileWantToText");
        });

        modelBuilder.Entity<Usersetting>(entity =>
        {
            entity.HasKey(e => e.Useruuid).HasName("PRIMARY");

            entity
                .ToTable("usersettings")
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.Useruuid)
                .HasMaxLength(36)
                .HasColumnName("useruuid");
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.Imviaemail)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("imviaemail");
            entity.Property(e => e.Visible)
                .IsRequired()
                .HasColumnType("enum('true','false')")
                .HasColumnName("visible");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
