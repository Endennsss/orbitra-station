using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Roadmap;

[Serializable, NetSerializable]
public enum AtlasStatus : byte { Planned, Active, Done }

[Serializable, NetSerializable]
public sealed record AtlasTask(string Title, AtlasStatus Status);

[Serializable, NetSerializable]
public sealed record AtlasContent(string Title, string Description, AtlasStatus Status, AtlasTask[] Tasks);

/// <summary>Содержит только ту версию обновления, которая доступна получателю.</summary>
[Serializable, NetSerializable]
public sealed record AtlasEntry(Guid Id, int Revision, AtlasContent Content, bool Published, bool Pending);

[Serializable, NetSerializable]
public sealed class OrbitraRoadmapState : EuiStateBase
{
    public AtlasEntry[] Entries = [];
    public bool CanEdit;
    public bool Loading;
    public string? Notice;
    public Guid ReplyTo;
    public bool Success;
}

[Serializable, NetSerializable]
public enum AtlasAction : byte { Save, Publish, Unpublish, MoveUp, MoveDown }

[Serializable, NetSerializable]
public sealed class AtlasChange : EuiMessageBase
{
    public Guid Id;
    public int Revision;
    public AtlasAction Action;
    public AtlasContent? Content;
    public Guid RequestId;
}

public static class AtlasLimits
{
    public const int Title = 100;
    public const int Description = 2000;
    public const int Tasks = 40;
    public const int TaskTitle = 120;
    public const int Entries = 100;

    public static bool Valid(AtlasContent? content)
    {
        if (content == null || string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > Title ||
            content.Description == null || content.Description.Length > Description || content.Status > AtlasStatus.Done ||
            content.Tasks == null || content.Tasks.Length > Tasks)
            return false;

        foreach (var task in content.Tasks)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > TaskTitle || task.Status > AtlasStatus.Done)
                return false;
        }
        return true;
    }
}
