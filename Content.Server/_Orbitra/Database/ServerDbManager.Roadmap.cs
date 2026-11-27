using System.Threading.Tasks;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    /// <summary>Возвращает версию и содержимое дорожной карты сервера. До первой записи версия равна нулю.</summary>
    Task<(long Version, string Json)> GetOrbitraRoadmapAsync(string serverId);

    /// <summary>Сохраняет документ и увеличивает его версию, если ожидаемая версия ещё актуальна.</summary>
    Task<bool> TrySaveOrbitraRoadmapAsync(string serverId, long expectedVersion, string json);
}

public sealed partial class ServerDbManager
{
    public Task<(long Version, string Json)> GetOrbitraRoadmapAsync(string serverId)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetOrbitraRoadmapAsync(serverId));
    }

    public Task<bool> TrySaveOrbitraRoadmapAsync(string serverId, long expectedVersion, string json)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.TrySaveOrbitraRoadmapAsync(serverId, expectedVersion, json));
    }
}
