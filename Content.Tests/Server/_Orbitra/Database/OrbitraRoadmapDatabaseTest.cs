using System.IO;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Server._Orbitra.Database;

[TestFixture]
public sealed class OrbitraRoadmapDatabaseTest
{
    [Test]
    public async Task CompetingChangesPreserveOneVersionAcrossReopen()
    {
        var directory = Directory.CreateTempSubdirectory("orbitra-roadmap-");
        try
        {
            var path = Path.Combine(directory.FullName, "roadmap.db");
            var db = OpenDatabase(path);
            Assert.That(await db.GetOrbitraRoadmapAsync("main"), Is.EqualTo((0L, "")));
            Assert.That(await db.TrySaveOrbitraRoadmapAsync("main", 1, "{}"), Is.False);
            Assert.That(await db.TrySaveOrbitraRoadmapAsync("other", 0, "{}"), Is.True);

            await SaveCompetingChanges(db, 0);
            var latest = await SaveCompetingChanges(db, 1);
            Assert.That(await db.TrySaveOrbitraRoadmapAsync("main", 0, "{}"), Is.False);
            Assert.That(await db.TrySaveOrbitraRoadmapAsync("main", 1, "{}"), Is.False);

            var reopened = OpenDatabase(path);
            Assert.That(await reopened.GetOrbitraRoadmapAsync("main"), Is.EqualTo((2L, latest)));
            Assert.That(await reopened.GetOrbitraRoadmapAsync("other"), Is.EqualTo((1L, "{}")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<string> SaveCompetingChanges(ServerDbBase db, long version)
    {
        var first = $$"""{"title":"План 'А'","edit":{{version}}}""";
        var second = $$"""{"title":"План Б","edit":{{version}}}""";
        var saved = await Task.WhenAll(
            Task.Run(() => db.TrySaveOrbitraRoadmapAsync("main", version, first)),
            Task.Run(() => db.TrySaveOrbitraRoadmapAsync("main", version, second)));
        Assert.That(saved[0], Is.Not.EqualTo(saved[1]));
        var winner = saved[0] ? first : second;
        Assert.That(await db.GetOrbitraRoadmapAsync("main"), Is.EqualTo((version + 1, winner)));
        return winner;
    }

    private static ServerDbSqlite OpenDatabase(string path)
    {
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connection.ToString()).Options;
        var config = new Mock<IConfigurationManager>();
        config.Setup(cfg => cfg.GetCVar(CCVars.DatabaseSqliteConcurrency)).Returns(2);
        return new ServerDbSqlite(() => options, false, config.Object, false,
            Mock.Of<ISawmill>(), Mock.Of<ISerializationManager>(), false);
    }
}
