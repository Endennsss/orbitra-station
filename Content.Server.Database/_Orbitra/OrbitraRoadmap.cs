using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<OrbitraRoadmap> OrbitraRoadmap { get; set; } = null!;
}

public sealed class OrbitraRoadmap
{
    [Key]
    public required string ServerId { get; set; }
    public long Version { get; set; }
    public required string Json { get; set; }
}
