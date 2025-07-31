using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OpenSim.Data.Model.Identity;

public partial class IdentityContext : DbContext
{
    public IdentityContext(DbContextOptions<IdentityContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EfmigrationsHistory> EfmigrationsHistories { get; set; }

    public virtual DbSet<IdentityRole> IdentityRoles { get; set; }

    public virtual DbSet<IdentityRoleClaim> IdentityRoleClaims { get; set; }

    public virtual DbSet<IdentityUser> IdentityUsers { get; set; }

    public virtual DbSet<IdentityUserClaim> IdentityUserClaims { get; set; }

    public virtual DbSet<IdentityUserLogin> IdentityUserLogins { get; set; }

    public virtual DbSet<IdentityUserToken> IdentityUserTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<EfmigrationsHistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity
                .ToTable("__EFMigrationsHistory")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion)
                .IsRequired()
                .HasMaxLength(32);
        });

        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("Identity_Role")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 255 });

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<IdentityRoleClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("Identity_RoleClaims")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.RoleId, "IX_Identity_RoleClaims_RoleId");

            entity.Property(e => e.RoleId).IsRequired();

            entity.HasOne(d => d.Role).WithMany(p => p.IdentityRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<IdentityUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("Identity_User")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex").HasAnnotation("MySql:IndexPrefixLength", new[] { 255 });

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 255 });

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.LockoutEnd).HasMaxLength(6);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "IdentityUserRole",
                    r => r.HasOne<IdentityRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<IdentityUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j
                            .ToTable("Identity_UserRoles")
                            .UseCollation("utf8mb4_unicode_ci");
                        j.HasIndex(new[] { "RoleId" }, "IX_Identity_UserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<IdentityUserClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("Identity_UserClaims")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "IX_Identity_UserClaims_UserId");

            entity.Property(e => e.UserId).IsRequired();

            entity.HasOne(d => d.User).WithMany(p => p.IdentityUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<IdentityUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("Identity_UserLogins")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "IX_Identity_UserLogins_UserId");

            entity.Property(e => e.UserId).IsRequired();

            entity.HasOne(d => d.User).WithMany(p => p.IdentityUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<IdentityUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity
                .ToTable("Identity_UserTokens")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasOne(d => d.User).WithMany(p => p.IdentityUserTokens).HasForeignKey(d => d.UserId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
