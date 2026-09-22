using System.Diagnostics;

namespace Orbitra.DevHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var channel = Guid.NewGuid().ToString("N");
        var data = args.Contains("--self-contained")
            ? Path.Combine(AppContext.BaseDirectory, "user_data")
            : Path.Combine(GetDataRoot(), "Space Station 14", "data");
        var directory = Path.Combine(data, "_Orbitra", "dev_lobby");
        Directory.CreateDirectory(directory);
        var request = Path.Combine(directory, channel + ".request");
        var reply = Path.Combine(directory, channel + ".reply");
        using var cancellation = new CancellationTokenSource();
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Content.Client.dll"));
        foreach (var argument in args)
            info.ArgumentList.Add(argument);
        info.ArgumentList.Add("--cvar");
        info.ArgumentList.Add("orbitra.dev_lobby_channel=" + channel);
        using var client = Process.Start(info) ?? throw new InvalidOperationException("Cannot start dev client.");
        var output = client.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput());
        var errors = client.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
        var watcher = WatchAsync(request, reply, cancellation.Token);
        try
        {
            await client.WaitForExitAsync();
            await Task.WhenAll(output, errors);
            return client.ExitCode;
        }
        finally
        {
            cancellation.Cancel();
            try { await watcher; }
            catch (OperationCanceledException) { }
            OrbitraDevLobbyServer.StopOwnedServer();
            File.Delete(request);
            File.Delete(reply);
            File.Delete(reply + ".tmp");
        }
    }

    private static async Task WatchAsync(string request, string reply, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            if (File.Exists(request))
            {
                File.Delete(request);
                var result = await OrbitraDevLobbyServer.EnsureRunningAsync(cancellation) ?? "ready";
                await File.WriteAllTextAsync(reply + ".tmp", result, cancellation);
                File.Move(reply + ".tmp", reply, overwrite: true);
            }
            await Task.Delay(250, cancellation);
        }
    }

    private static string GetDataRoot()
    {
        if (OperatingSystem.IsLinux())
            return Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
                   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }
}
