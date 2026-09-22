#if !FULL_RELEASE
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Orbitra.DevHost;

/// <summary>Starts a loopback-only development server; never restarts an existing server.</summary>
internal static class OrbitraDevLobbyServer
{
    public const string Address = "127.0.0.1:1214";
    private const string ServerName = "Orbitra Dev Lobby";
    private static readonly SemaphoreSlim LaunchLock = new(1, 1);
    private static Process? _ownedServer;

    public static async Task<string?> EnsureRunningAsync(CancellationToken cancellation)
    {
        Process? started = null;
        var ready = false;
        var entered = false;
        try
        {
            await LaunchLock.WaitAsync(cancellation).ConfigureAwait(false);
            entered = true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(2),
                MaxResponseContentBufferSize = 65536,
            };
            var status = await ProbeAsync(http, timeout.Token).ConfigureAwait(false);
            if (status == 1)
                return null;
            if (status == 2)
                return "orbitra-dev-lobby-port-busy";

            var root = FindRoot(AppContext.BaseDirectory);
            if (root == null)
                return "orbitra-dev-lobby-no-server";
            var dll = await OrbitraDevServerBuild.PrepareAsync(root, timeout.Token).ConfigureAwait(false);
            if (dll == null)
                return "orbitra-dev-lobby-build-failed";

            var info = CreateStartInfo(root, dll);
            cancellation.ThrowIfCancellationRequested();
            if (_ownedServer is { HasExited: true } previous)
            {
                previous.Dispose();
                _ownedServer = null;
            }
            started = Process.Start(info);
            if (started == null)
                return "orbitra-dev-lobby-start-failed";
            _ownedServer = started;
            while (!timeout.IsCancellationRequested)
            {
                if (started.HasExited)
                    return "orbitra-dev-lobby-start-failed";
                status = await ProbeAsync(http, timeout.Token).ConfigureAwait(false);
                if (status == 2)
                    return "orbitra-dev-lobby-port-busy";
                if (status == 1)
                {
                    ready = true;
                    return null;
                }
                await Task.Delay(500, timeout.Token).ConfigureAwait(false);
            }
            return "orbitra-dev-lobby-timeout";
        }
        catch (OperationCanceledException)
        {
            return "orbitra-dev-lobby-timeout";
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "orbitra-dev-lobby-start-failed";
        }
        finally
        {
            if (!ready && started != null)
            {
                Interlocked.CompareExchange(ref _ownedServer, null, started);
                StopServer(started);
            }
            if (entered)
                LaunchLock.Release();
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string root, string dll)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
        };
        info.ArgumentList.Add(dll);
        info.ArgumentList.Add("--config-file");
        var config = Path.Combine(root, "bin", "orbitra_dev_lobby.toml");
        var legacyConfig = Path.Combine(root, "bin", "lime_dev_lobby.toml");
        info.ArgumentList.Add(!File.Exists(config) && File.Exists(legacyConfig) ? legacyConfig : config);
        info.ArgumentList.Add("--data-dir");
        // Существующая база dev-лобби сохраняется без копирования или перезаписи.
        var data = Path.Combine(root, "bin", "OrbitraDevLobbyData");
        var legacyData = Path.Combine(root, "bin", "LimeDevLobbyData");
        info.ArgumentList.Add(!Directory.Exists(data) && Directory.Exists(legacyData) ? legacyData : data);
        foreach (var setting in new[]
                 {
                     "net.port=1214", "net.bindto=127.0.0.1", "net.upnp=false",
                     "status.enabled=true", "status.bind=127.0.0.1:1214", "hub.advertise=false",
                     $"game.hostname={ServerName}", "game.lobbyenabled=true", "game.lobbyduration=600",
                     "game.defaultpreset=sandbox", "game.map=Dev", "database.engine=sqlite",
                 })
        {
            info.ArgumentList.Add("--cvar");
            info.ArgumentList.Add(setting);
        }
        return info;
    }

    internal static string? FindRoot(string directory)
    {
        for (var parent = new DirectoryInfo(directory); parent != null; parent = parent.Parent)
            if (File.Exists(Path.Combine(parent.FullName, "Content.Server", "Content.Server.csproj")))
                return parent.FullName;
        return null;
    }

    private static async Task<int> ProbeAsync(HttpClient http, CancellationToken cancellation)
    {
        try
        {
            using var response = await http.GetAsync($"http://{Address}/status", cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return 2;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
            if (!json.RootElement.TryGetProperty("name", out var name) || name.GetString() != ServerName)
                return 2;
            return json.RootElement.TryGetProperty("run_level", out _) ? 1 : 0;
        }
        catch (HttpRequestException) { return 0; }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return 0; }
        catch (JsonException) { return 2; }
    }

    public static void StopOwnedServer()
    {
        var server = Interlocked.Exchange(ref _ownedServer, null);
        if (server == null)
            return;
        StopServer(server);
    }

    private static void StopServer(Process server)
    {
        try
        {
            if (!server.HasExited)
            {
                server.StandardInput.WriteLine("shutdown");
                if (!server.WaitForExit(2000))
                    server.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or System.ComponentModel.Win32Exception) { }
        finally { server.Dispose(); }
    }
}
#endif
