using Microsoft.EntityFrameworkCore;

namespace MyLife.Server.Data;

public sealed class MyLifeDbContext(DbContextOptions<MyLifeDbContext> options) : DbContext(options)
{
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

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
    }
}
