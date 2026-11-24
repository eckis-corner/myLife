using Microsoft.EntityFrameworkCore;

namespace MyLife.Server.Data;

public sealed class MyLifeDbContext(DbContextOptions<MyLifeDbContext> options) : DbContext(options)
{
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasColumnName("key").HasMaxLength(200);
            entity.Property(x => x.Value).HasColumnName("value");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(x => x.IsAdmin).HasColumnName("is_admin");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Language).HasColumnName("language").HasMaxLength(10);
            entity.Property(x => x.AuthHash).HasColumnName("auth_hash").HasMaxLength(200);
            entity.Property(x => x.KdfSalt).HasColumnName("kdf_salt");
            entity.Property(x => x.KdfMemoryKiB).HasColumnName("kdf_memory_kib");
            entity.Property(x => x.KdfIterations).HasColumnName("kdf_iterations");
            entity.Property(x => x.KdfParallelism).HasColumnName("kdf_parallelism");
            entity.Property(x => x.WrappedVmk).HasColumnName("wrapped_vmk");
            entity.Property(x => x.WrappedVmkByRecovery).HasColumnName("wrapped_vmk_by_recovery");
            entity.Property(x => x.PublicEncryptionKey).HasColumnName("public_encryption_key");
            entity.Property(x => x.WrappedEncryptionPrivateKey).HasColumnName("wrapped_encryption_private_key");
            entity.Property(x => x.PublicSigningKey).HasColumnName("public_signing_key");
            entity.Property(x => x.WrappedSigningPrivateKey).HasColumnName("wrapped_signing_private_key");
            entity.Property(x => x.TotpSecret).HasColumnName("totp_secret");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.Version).IsRowVersion();
        });
    }
}
