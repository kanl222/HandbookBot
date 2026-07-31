using HandbookBot.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Data.EfCore;

/// <summary>
/// Контекст базы данных HandbookBot.
/// Connection string передаётся через конфигурацию при регистрации.
/// </summary>
public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options)
    {
    }

    public DbSet<PreparationEntity> Preparations => Set<PreparationEntity>();
    public DbSet<PharmacyEntity> Pharmacies => Set<PharmacyEntity>();
    public DbSet<FaqEntryEntity> FaqEntries => Set<FaqEntryEntity>();

    //protected override void OnModelCreating(ModelBuilder modelBuilder)
    //{
    //    modelBuilder.Entity<PreparationEntity>(e =>
    //    {
    //        e.HasKey(p => p.Id);
    //        e.Property(p => p.Name).IsRequired().HasMaxLength(256);
    //        e.Property(p => p.Price).HasPrecision(18, 2);
    //    });

    //    modelBuilder.Entity<PharmacyEntity>(e =>
    //    {
    //        e.HasKey(p => p.Id);
    //        e.Property(p => p.Name).IsRequired().HasMaxLength(256);
    //        e.Property(p => p.Address).IsRequired().HasMaxLength(512);
    //        e.Property(p => p.Contact).HasMaxLength(128);
    //    });

    //    modelBuilder.Entity<FaqEntryEntity>(e =>
    //    {
    //        e.HasKey(f => f.Id);
    //        e.Property(f => f.Question).IsRequired().HasMaxLength(512);
    //        e.Property(f => f.Answer).IsRequired().HasMaxLength(2048);
    //    });
    //}
}
