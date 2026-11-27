using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<(long Version, string Json)> GetOrbitraRoadmapAsync(string serverId)
    {
        await using var db = await GetDb();
        var roadmap = await db.DbContext.OrbitraRoadmap.AsNoTracking()
            .SingleOrDefaultAsync(row => row.ServerId == serverId);
        return roadmap == null ? (0, string.Empty) : (roadmap.Version, roadmap.Json);
    }

    public async Task<bool> TrySaveOrbitraRoadmapAsync(string serverId, long expectedVersion, string json)
    {
        await using var db = await GetDb();
        if (expectedVersion == 0)
        {
            var inserted = await db.DbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO orbitra_roadmap (server_id, version, json) VALUES ({serverId}, 1, {json})
                ON CONFLICT (server_id) DO NOTHING
                """);
            return inserted == 1;
        }

        var updated = await db.DbContext.OrbitraRoadmap
            .Where(row => row.ServerId == serverId && row.Version == expectedVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Version, row => row.Version + 1)
                .SetProperty(row => row.Json, json));
        return updated == 1;
    }
}
