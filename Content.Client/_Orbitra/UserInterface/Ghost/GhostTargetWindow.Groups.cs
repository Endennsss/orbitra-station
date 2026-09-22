using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Shared.Ghost.Systems;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed partial class GhostTargetWindow
{
    private readonly Dictionary<NetEntity, TargetRow> _orbitraTargets = new();
    private readonly Dictionary<string, TargetGroup> _orbitraGroups = new();
    private readonly List<TargetRow> _orbitraOrdered = new();
    private IPrototypeManager _orbitraPrototypes = default!;
    private SpriteSystem _orbitraSprites = default!;
    private Content.Client.Ghost.GhostSystem _orbitraGhost = default!;
    private uint _orbitraRoundGeneration;

    private void InitializeOrbitraTargets()
    {
        _orbitraPrototypes = IoCManager.Resolve<IPrototypeManager>();
        OrbitraEntryWindow.Attach(this);
        foreach (var button in new[] { GhostnadoButton, WarpToRandomFollowedButton, WarpToRandomButton })
        {
            button.HorizontalExpand = true;
            button.HorizontalAlignment = HAlignment.Stretch;
            button.AddStyleClass(OrbitraButtonStyles.Secondary);
        }
        ButtonContainer.SeparationOverride = OrbitraUiMetrics.Medium;
    }

    /// <summary>Uses server metadata only; no target entity needs to be present in the client's PVS.</summary>
    private void UpdateOrbitraTargets(IEnumerable<GhostWarp> warps)
    {
        // GhostGui создаётся ещё до запуска ECS; системы нужны только при получении целей.
        var entities = IoCManager.Resolve<IEntityManager>();
        _orbitraSprites = entities.System<SpriteSystem>();
        _orbitraGhost = entities.System<Content.Client.Ghost.GhostSystem>();
        if (_orbitraRoundGeneration != _orbitraGhost.OrbitraRoundGeneration)
        {
            _orbitraRoundGeneration = _orbitraGhost.OrbitraRoundGeneration;
            foreach (var group in _orbitraGroups.Values)
                group.Root.Dispose();
            _orbitraGroups.Clear();
            _orbitraTargets.Clear();
            SearchBar.SetText("", true);
            GhostScroll.SetScrollValue(Vector2.Zero);
        }
        var remaining = new HashSet<NetEntity>(_orbitraTargets.Keys);
        var seen = new HashSet<NetEntity>();
        var departments = _orbitraPrototypes.EnumeratePrototypes<DepartmentPrototype>().ToList();
        departments.Sort(DepartmentUIComparer.Instance);
        _orbitraOrdered.Clear();
        foreach (var warp in warps)
        {
            if (!seen.Add(warp.Entity))
                continue;
            if (!_orbitraTargets.TryGetValue(warp.Entity, out var row))
            {
                row = new TargetRow(warp.Entity);
                row.Button.OnPressed += _ => WarpClicked?.Invoke(row.Entity);
                _orbitraTargets.Add(warp.Entity, row);
            }
            remaining.Remove(warp.Entity);
            JobPrototype? job = null;
            if (warp.Job is { } jobId)
                _orbitraPrototypes.TryIndex(jobId, out job);
            var department = departments.FirstOrDefault(d => d.Primary && job != null && d.Roles.Contains(job.ID))
                             ?? departments.FirstOrDefault(d => job != null && d.Roles.Contains(job.ID));
            var groupId = warp.IsWarpPoint ? "places" : department?.ID ?? "unassigned";
            var groupName = warp.IsWarpPoint ? Loc.GetString("orbitra-ghost-places") : department == null
                ? Loc.GetString("orbitra-ghost-unassigned") : Loc.GetString(department.Name);
            if (!_orbitraGroups.TryGetValue(groupId, out var group))
            {
                group = new TargetGroup(groupId, groupName, department);
                group.Header.OnPressed += _ =>
                {
                    group.Collapsed = !group.Collapsed;
                    FilterOrbitraTargets();
                };
                _orbitraGroups.Add(groupId, group);
                ButtonContainer.AddChild(group.Root);
            }
            row.Name = warp.CharacterName ?? warp.DisplayName;
            row.Job = job?.LocalizedName ?? (warp.IsWarpPoint ? "" : Loc.GetString("orbitra-ghost-no-job"));
            row.NameLabel.SetMessage(row.Name);
            row.JobLabel.Text = row.Job;
            row.JobLabel.Visible = !warp.IsWarpPoint;
            row.Button.ToolTip = $"{row.Name}\n{row.Job}";
            row.Group = group;
            row.Icon.Visible = !warp.IsWarpPoint;
            if (!warp.IsWarpPoint && _orbitraPrototypes.TryIndex<JobIconPrototype>(job?.Icon ?? "JobIconUnknown", out var icon))
                row.Icon.Texture = _orbitraSprites.Frame0(icon.Icon);
            if (row.Button.Parent != group.Rows)
            {
                row.Button.Orphan();
                group.Rows.AddChild(row.Button);
            }
            _orbitraOrdered.Add(row);
        }
        foreach (var id in remaining)
        {
            _orbitraTargets[id].Button.Dispose();
            _orbitraTargets.Remove(id);
        }
        _orbitraOrdered.Sort((a, b) =>
        {
            var compare = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            return compare != 0 ? compare : a.Entity.CompareTo(b.Entity);
        });
        var positions = new Dictionary<string, int>();
        foreach (var row in _orbitraOrdered)
        {
            positions.TryGetValue(row.Group.Id, out var position);
            row.Button.SetPositionInParent(position);
            positions[row.Group.Id] = position + 1;
        }
        var orderedGroups = _orbitraGroups.Values.OrderBy(g => g.Id == "places" ? 2 : g.Department == null ? 1 : 0)
            .ThenBy(g => g.Department!, DepartmentUIComparer.Instance).ToList();
        for (var i = 0; i < orderedGroups.Count; i++)
            orderedGroups[i].Root.SetPositionInParent(i);
        FilterOrbitraTargets();
    }

    private void FilterOrbitraTargets()
    {
        var query = _searchText.Trim();
        foreach (var group in _orbitraGroups.Values)
            group.Count = 0;
        foreach (var row in _orbitraTargets.Values)
        {
            var match = query.Length == 0 || row.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        row.Job.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        row.Group.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            row.Button.Visible = match;
            if (match)
                row.Group.Count++;
        }
        foreach (var group in _orbitraGroups.Values)
        {
            var expanded = query.Length > 0 || !group.Collapsed;
            group.Root.Visible = group.Count > 0;
            group.Rows.Visible = expanded;
            group.Header.Text = $"{(expanded ? "▼" : "▶")} {group.Name} · {group.Count}";
        }
    }

    private sealed class TargetGroup
    {
        public readonly string Id;
        public readonly string Name;
        public readonly DepartmentPrototype? Department;
        public readonly BoxContainer Root = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = OrbitraUiMetrics.Small };
        public readonly Button Header = new() { HorizontalExpand = true, TextAlign = Label.AlignMode.Left, CanKeyboardFocus = true };
        public readonly BoxContainer Rows = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        public bool Collapsed;
        public int Count;

        public TargetGroup(string id, string name, DepartmentPrototype? department)
        {
            Id = id;
            Name = name;
            Department = department;
            Header.AddStyleClass(OrbitraButtonStyles.Ghost);
            Header.AddStyleClass("OrbitraGroupHeader");
            Header.Label.HorizontalExpand = true;
            Header.Label.HorizontalAlignment = HAlignment.Stretch;
            Root.AddChild(Header);
            Root.AddChild(Rows);
        }
    }

    private sealed class TargetRow
    {
        public readonly NetEntity Entity;
        public readonly ContainerButton Button = new() { HorizontalExpand = true, CanKeyboardFocus = true, MinHeight = OrbitraUiMetrics.ElementHeight };
        public readonly TextureRect Icon = new() { SetSize = new Vector2(20), VerticalAlignment = VAlignment.Center, Stretch = TextureRect.StretchMode.KeepAspectCentered };
        public readonly RichTextLabel NameLabel = new() { HorizontalExpand = true };
        public readonly Label JobLabel = new() { HorizontalExpand = true, ClipText = true };
        public string Name = "";
        public string Job = "";
        public TargetGroup Group = default!;

        public TargetRow(NetEntity entity)
        {
            Entity = entity;
            Button.AddStyleClass(ContainerButton.StyleClassButton);
            Button.AddStyleClass(OrbitraButtonStyles.Secondary);
            OrbitraMotion.AttachButton(Button);
            var content = new BoxContainer { SeparationOverride = OrbitraUiMetrics.Small, MouseFilter = Control.MouseFilterMode.Ignore };
            var labels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 0 };
            JobLabel.AddStyleClass("OrbitraLobbyMuted");
            labels.AddChild(NameLabel);
            labels.AddChild(JobLabel);
            content.AddChild(Icon);
            content.AddChild(labels);
            Button.AddChild(content);
        }
    }
}
