using Microsoft.EntityFrameworkCore;

namespace MyLife.Server.Data;

public sealed class MyLifeDbContext(DbContextOptions<MyLifeDbContext> options) : DbContext(options)
{
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Passkey> Passkeys => Set<Passkey>();

    public DbSet<VaultArea> Areas => Set<VaultArea>();

    public DbSet<VaultObject> Objects => Set<VaultObject>();

    public DbSet<VaultDocument> Documents => Set<VaultDocument>();

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    public DbSet<MailOutboxEntry> MailOutbox => Set<MailOutboxEntry>();

    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

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
            entity.Property(x => x.RecoveryAuthHash).HasColumnName("recovery_auth_hash").HasMaxLength(200);
            entity.Property(x => x.TotpSecret).HasColumnName("totp_secret");
            entity.Property(x => x.TotpLastStep).HasColumnName("totp_last_step");
            entity.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(64).HasDefaultValue("initial");
            entity.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(x => x.LockoutUntil).HasColumnName("lockout_until");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<Passkey>(entity =>
        {
            entity.ToTable("passkeys");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CredentialId).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.Passkeys).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => !x.User!.IsDeleted);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.CredentialId).HasColumnName("credential_id");
            entity.Property(x => x.PublicKey).HasColumnName("public_key");
            entity.Property(x => x.SignCount).HasColumnName("sign_count");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastUsedAt).HasColumnName("last_used_at");
        });

        modelBuilder.Entity<VaultArea>(entity =>
        {
            entity.ToTable("areas");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerId, x.AreaKey }).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.AreaKey).HasColumnName("area_key");
            entity.Property(x => x.WrappedAreaKey).HasColumnName("wrapped_area_key");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastReviewedAt).HasColumnName("last_reviewed_at");
        });

        modelBuilder.Entity<VaultObject>(entity =>
        {
            entity.ToTable("objects");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerId, x.AreaKey });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.AreaKey).HasColumnName("area_key");
            entity.Property(x => x.TypeKey).HasColumnName("type_key").HasMaxLength(50);
            entity.Property(x => x.Payload).HasColumnName("payload");
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<VaultDocument>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OwnerId);
            entity.HasIndex(x => x.StorageKey).IsUnique();
            entity.HasOne(x => x.Parent).WithMany(x => x.Documents).HasForeignKey(x => x.ObjectId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.ObjectId).HasColumnName("object_id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.WrappedFileKey).HasColumnName("wrapped_file_key");
            entity.Property(x => x.Metadata).HasColumnName("metadata");
            entity.Property(x => x.CipherSize).HasColumnName("cipher_size");
            entity.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(64);
            entity.Property(x => x.CipherSha256).HasColumnName("cipher_sha256");
            entity.Property(x => x.IsComplete).HasColumnName("is_complete");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerId, x.At });
            entity.HasIndex(x => x.At);
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.At).HasColumnName("at");
            entity.Property(x => x.ActorId).HasColumnName("actor_id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
            entity.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(50);
            entity.Property(x => x.TargetId).HasColumnName("target_id");
            entity.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
            entity.Property(x => x.Details).HasColumnName("details").HasMaxLength(500);
        });

        modelBuilder.Entity<MailOutboxEntry>(entity =>
        {
            entity.ToTable("mail_outbox");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.NextAttemptAt).HasFilter("sent_at IS NULL AND failed_at IS NULL");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(254);
            entity.Property(x => x.Template).HasColumnName("template").HasMaxLength(50);
            entity.Property(x => x.Language).HasColumnName("language").HasMaxLength(10);
            entity.Property(x => x.Parameters).HasColumnName("parameters");
            entity.Property(x => x.Attempts).HasColumnName("attempts");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(x => x.SentAt).HasColumnName("sent_at");
            entity.Property(x => x.FailedAt).HasColumnName("failed_at");
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(500);
        });

        modelBuilder.Entity<OneTimeToken>(entity =>
        {
            entity.ToTable("one_time_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Purpose });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Purpose).HasColumnName("purpose");
            entity.Property(x => x.TokenHash).HasColumnName("token_hash");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.UsedAt).HasColumnName("used_at");
        });
    }
}
