using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Orbitra.DevHost;

/// <summary>Prepares an isolated server when the normal output is incompatible with the client.</summary>
internal static class OrbitraDevServerBuild
{
    internal static bool IsCompatible(string clientDirectory, string serverDirectory)
    {
        if (!File.Exists(Path.Combine(serverDirectory, "Content.Server.dll")))
            return false;
        foreach (var name in new[] { "Content.Shared.dll", "Content.Shared.Database.dll", "Robust.Shared.dll" })
        {
            var client = Path.Combine(clientDirectory, name);
            var server = Path.Combine(serverDirectory, name);
            if (!File.Exists(client) || !File.Exists(server))
                return false;
            try
            {
                if (!ReadSchema(client).SequenceEqual(ReadSchema(server)))
                    return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
        return true;
    }

    private static string[] ReadSchema(string path)
    {
        // Байты DLL могут различаться из-за генераторов даже при одинаковом контракте.
        // Проверяем имена типов и полей; окончательную сетевую совместимость проверяет Robust.
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var entries = new List<string>();
        string Name(TypeDefinitionHandle handle)
        {
            var type = metadata.GetTypeDefinition(handle);
            return (type.GetDeclaringType().IsNil ? metadata.GetString(type.Namespace) : Name(type.GetDeclaringType()))
                + "." + metadata.GetString(type.Name);
        }
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            var name = Name(handle);
            if (name.Contains('<'))
                continue;
            entries.Add(name);
            foreach (var field in type.GetFields())
                entries.Add(name + ":" + metadata.GetString(metadata.GetFieldDefinition(field).Name));
        }
        entries.Sort(StringComparer.Ordinal);
        return entries.ToArray();
    }

    public static async Task<string?> PrepareAsync(string root, CancellationToken cancellation)
    {
        var client = AppContext.BaseDirectory;
        var normal = Path.Combine(root, "bin", "Content.Server");
        if (IsCompatible(client, normal))
            return Path.Combine(normal, "Content.Server.dll");
        var isolated = Path.Combine(root, "bin", "Orbitra.DevServer");
        if (IsCompatible(client, isolated))
            return Path.Combine(isolated, "Content.Server.dll");

        // Не перезаписываем DLL уже запущенного пользовательского сервера.
        Directory.CreateDirectory(isolated);
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "build", "Content.Server/Content.Server.csproj", "--configuration", "Debug", "--output", isolated })
            info.ArgumentList.Add(argument);
        using var build = Process.Start(info);
        if (build == null)
            return null;
        await using var output = new FileStream(Path.Combine(isolated, "build.log"), FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var errors = new FileStream(Path.Combine(isolated, "build-errors.log"), FileMode.Create, FileAccess.Write, FileShare.Read);
        var stdout = build.StandardOutput.BaseStream.CopyToAsync(output);
        var stderr = build.StandardError.BaseStream.CopyToAsync(errors);
        try
        {
            await build.WaitForExitAsync(cancellation);
            await Task.WhenAll(stdout, stderr);
            return build.ExitCode == 0 && IsCompatible(client, isolated)
                ? Path.Combine(isolated, "Content.Server.dll") : null;
        }
        finally
        {
            if (!build.HasExited)
            {
                build.Kill(entireProcessTree: true);
                await build.WaitForExitAsync();
            }
            await Task.WhenAll(stdout, stderr);
        }
    }
}
