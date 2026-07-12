using FluxGet.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FluxGet.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<DownloadTask> DownloadTasks { get; set; } = null!;
    
    public DbSet<DownloadChunk> DownloadChunks { get; set; } = null!;
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet",
            "downloads.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var speedHistoryConverter = new ValueConverter<List<double>, string>(
            v => v == null ? "" : string.Join(",", v),
            v => string.IsNullOrEmpty(v) ? new List<double>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse).ToList());
        
        modelBuilder.Entity<DownloadTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FilePath).IsRequired();
            entity.Property(e => e.SpeedHistory).HasConversion(speedHistoryConverter);
            entity.HasMany(e => e.Chunks)
                .WithOne(c => c.DownloadTask)
                .HasForeignKey(c => c.DownloadTaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.UrlHash);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.QueueOrder);
        });
        
        modelBuilder.Entity<DownloadChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DownloadTaskId);
            entity.HasIndex(e => e.Status);
        });
    }
}
