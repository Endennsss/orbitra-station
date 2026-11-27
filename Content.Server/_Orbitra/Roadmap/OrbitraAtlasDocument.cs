using System.Linq;
using Content.Shared._Orbitra.Roadmap;

namespace Content.Server._Orbitra.Roadmap;

internal sealed record AtlasRecord(Guid Id, int Revision, AtlasContent Draft, AtlasContent? Public, bool Pending);

/// <summary>Порядок записей задаёт маршрут. Новая версия документа заменяет старую только после записи в БД.</summary>
internal sealed record OrbitraAtlasDocument(AtlasRecord[] Records)
{
    public AtlasEntry[] Read(bool editor)
    {
        return editor
            ? Records.Select(r => new AtlasEntry(r.Id, r.Revision, r.Draft, r.Public != null, r.Pending)).ToArray()
            : Records.Where(r => r.Public != null).Select(r => new AtlasEntry(r.Id, 0, r.Public!, true, false)).ToArray();
    }

    public string? TryChange(AtlasChange change, out OrbitraAtlasDocument updated)
    {
        updated = this;
        if (change.Id == Guid.Empty || change.RequestId == Guid.Empty || change.Action > AtlasAction.MoveDown)
            return "orbitra-atlas-invalid";

        var index = Array.FindIndex(Records, r => r.Id == change.Id);
        var original = index == -1 ? null : Records[index];
        if ((original?.Revision ?? 0) != change.Revision)
            return "orbitra-atlas-conflict";

        var records = Records.ToList();
        switch (change.Action)
        {
            case AtlasAction.Publish:
            case AtlasAction.Save:
                if (!AtlasLimits.Valid(change.Content))
                    return "orbitra-atlas-invalid";
                if (original == null && records.Count >= AtlasLimits.Entries)
                    return "orbitra-atlas-full";
                var publish = change.Action == AtlasAction.Publish;
                var entry = new AtlasRecord(change.Id, change.Revision + 1, change.Content!,
                    publish ? change.Content : original?.Public, !publish);
                if (index == -1)
                    records.Add(entry);
                else
                    records[index] = entry;
                break;
            case AtlasAction.Unpublish:
                if (original?.Public == null)
                    return "orbitra-atlas-invalid";
                records[index] = original with { Public = null, Pending = true, Revision = original.Revision + 1 };
                break;
            case AtlasAction.MoveUp:
            case AtlasAction.MoveDown:
                var neighbor = index + (change.Action == AtlasAction.MoveUp ? -1 : 1);
                if (original == null || neighbor < 0 || neighbor >= records.Count)
                    return "orbitra-atlas-invalid";
                var other = records[neighbor];
                records[index] = other with { Revision = other.Revision + 1 };
                records[neighbor] = original with { Revision = original.Revision + 1 };
                break;
        }

        updated = new OrbitraAtlasDocument(records.ToArray());
        return null;
    }
}
