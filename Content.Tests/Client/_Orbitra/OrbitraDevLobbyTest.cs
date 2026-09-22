#if !FULL_RELEASE
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Orbitra.DevHost;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra;

[TestFixture]
public sealed class OrbitraDevLobbyTest
{
    [Test]
    public void LaunchIsLoopbackOnlyAndDoesNotUseDefaultServerData()
    {
        var root = Path.GetFullPath(".");
        var info = OrbitraDevLobbyServer.CreateStartInfo(root, Path.Combine(root, "server with spaces.dll"));
        Assert.Multiple(() =>
        {
            Assert.That(info.UseShellExecute, Is.False);
            Assert.That(info.CreateNoWindow, Is.True);
            Assert.That(info.ArgumentList, Does.Contain("net.bindto=127.0.0.1"));
            Assert.That(info.ArgumentList, Does.Contain("status.bind=127.0.0.1:1214"));
            Assert.That(info.ArgumentList, Does.Contain("hub.advertise=false"));
            Assert.That(info.ArgumentList, Does.Contain("game.lobbyenabled=true"));
            Assert.That(info.ArgumentList, Does.Contain(Path.Combine(root, "bin", "OrbitraDevLobbyData")));
            Assert.That(info.ArgumentList[0], Is.EqualTo(Path.Combine(root, "server with spaces.dll")));
        });
    }

    [Test, Explicit("Starts and stops a real local dev server on port 1214.")]
    public async Task RealDevServerStartsWithLobby()
    {
        try
        {
            var error = await OrbitraDevLobbyServer.EnsureRunningAsync(CancellationToken.None);
            Assert.That(error, Is.Null);
            Assert.That(await OrbitraDevLobbyServer.EnsureRunningAsync(CancellationToken.None), Is.Null);
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
            var status = await http.GetStringAsync("http://127.0.0.1:1214/status");
            Assert.That(status, Does.Contain("Orbitra Dev Lobby"));
            Assert.That(status, Does.Contain("\"run_level\":0"));
        }
        finally
        {
            OrbitraDevLobbyServer.StopOwnedServer();
        }
    }

    [Test]
    public void ExistingLegacyDevDataIsReusedButNewPathsWin()
    {
        var temporary = Directory.CreateTempSubdirectory("orbitra-dev-migration-");
        try
        {
            var bin = Directory.CreateDirectory(Path.Combine(temporary.FullName, "bin")).FullName;
            var oldData = Directory.CreateDirectory(Path.Combine(bin, "LimeDevLobbyData")).FullName;
            var oldConfig = Path.Combine(bin, "lime_dev_lobby.toml");
            File.WriteAllText(oldConfig, "");
            var old = OrbitraDevLobbyServer.CreateStartInfo(temporary.FullName, "server.dll");
            Assert.That(old.ArgumentList, Does.Contain(oldData));
            Assert.That(old.ArgumentList, Does.Contain(oldConfig));
            var newData = Directory.CreateDirectory(Path.Combine(bin, "OrbitraDevLobbyData")).FullName;
            var newConfig = Path.Combine(bin, "orbitra_dev_lobby.toml");
            File.WriteAllText(newConfig, "");
            var current = OrbitraDevLobbyServer.CreateStartInfo(temporary.FullName, "server.dll");
            Assert.That(current.ArgumentList, Does.Contain(newData));
            Assert.That(current.ArgumentList, Does.Contain(newConfig));
            Assert.That(Directory.Exists(oldData), Is.True);
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    [Test]
    public void ServerCompatibilityRejectsStaleSharedAssemblies()
    {
        var temporary = Directory.CreateTempSubdirectory("orbitra-build-match-");
        try
        {
            var client = Directory.CreateDirectory(Path.Combine(temporary.FullName, "client")).FullName;
            var server = Directory.CreateDirectory(Path.Combine(temporary.FullName, "server")).FullName;
            File.WriteAllText(Path.Combine(server, "Content.Server.dll"), "server");
            foreach (var name in new[] { "Content.Shared.dll", "Content.Shared.Database.dll", "Robust.Shared.dll" })
            {
                File.Copy(typeof(OrbitraDevLobbyTest).Assembly.Location, Path.Combine(client, name));
                File.Copy(typeof(OrbitraDevLobbyTest).Assembly.Location, Path.Combine(server, name));
            }
            Assert.That(OrbitraDevServerBuild.IsCompatible(client, server), Is.True);
            File.Copy(typeof(string).Assembly.Location, Path.Combine(server, "Content.Shared.dll"), true);
            Assert.That(OrbitraDevServerBuild.IsCompatible(client, server), Is.False);
            Assert.That(OrbitraDevServerBuild.IsCompatible(client, temporary.FullName), Is.False);
        }
        finally
        {
            temporary.Delete(true);
        }
    }
}
#endif
