using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OpenSim.Data.Model.Economy;

public partial class OpenSimEconomyContext : DbContext
{
    public OpenSimEconomyContext(DbContextOptions<OpenSimEconomyContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Balance> Balances { get; set; }

    public virtual DbSet<Totalsale> Totalsales { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<Userinfo> Userinfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Balance>(entity =>
        {
            entity.HasKey(e => e.User).HasName("PRIMARY");

            entity
                .ToTable("balances", tb => tb.HasComment("Rev.4"))
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.User)
                .HasMaxLength(36)
                .HasColumnName("user");
            entity.Property(e => e.Balance1).HasColumnName("balance");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Type).HasColumnName("type");
        });

        modelBuilder.Entity<Totalsale>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("totalsales", tb => tb.HasComment("Rev.3"))
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .HasColumnName("UUID");
            entity.Property(e => e.ObjectUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("objectUUID");
            entity.Property(e => e.Time).HasColumnName("time");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.User)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("user");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity
                .ToTable("transactions", tb => tb.HasComment("Rev.12"))
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Uuid)
                .HasMaxLength(36)
                .HasColumnName("UUID");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CommonName)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("commonName");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ObjectName)
                .HasMaxLength(255)
                .HasColumnName("objectName");
            entity.Property(e => e.ObjectUuid)
                .HasMaxLength(36)
                .HasColumnName("objectUUID");
            entity.Property(e => e.Receiver)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("receiver");
            entity.Property(e => e.ReceiverBalance)
                .HasDefaultValueSql("'-1'")
                .HasColumnName("receiverBalance");
            entity.Property(e => e.RegionHandle)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("regionHandle");
            entity.Property(e => e.RegionUuid)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("regionUUID");
            entity.Property(e => e.Secure)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("secure");
            entity.Property(e => e.Sender)
                .IsRequired()
                .HasMaxLength(36)
                .HasColumnName("sender");
            entity.Property(e => e.SenderBalance)
                .HasDefaultValueSql("'-1'")
                .HasColumnName("senderBalance");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Time).HasColumnName("time");
            entity.Property(e => e.Type).HasColumnName("type");
        });

        modelBuilder.Entity<Userinfo>(entity =>
        {
            entity.HasKey(e => e.User).HasName("PRIMARY");

            entity
                .ToTable("userinfo", tb => tb.HasComment("Rev.3"))
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.User)
                .HasMaxLength(36)
                .HasColumnName("user");
            entity.Property(e => e.Avatar)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("avatar");
            entity.Property(e => e.Class).HasColumnName("class");
            entity.Property(e => e.Pass)
                .IsRequired()
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .HasColumnName("pass");
            entity.Property(e => e.Serverurl)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("serverurl");
            entity.Property(e => e.Simip)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("simip");
            entity.Property(e => e.Type).HasColumnName("type");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
