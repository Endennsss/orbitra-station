using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerControl
{
    [Dependency] private IResourceCache _resources = default!;

    private readonly Dictionary<ProtoId<DamageGroupPrototype>, (BoxContainer Row, Label Title, RichTextLabel Types)> _groups = new();
    private DamageSpecifier? _lastDamage;

    private void ResetDamage()
    {
        _lastDamage = null;
        DamageToggle.Visible = DamageToggle.Pressed = false;
        UpdateDisclosure();
        foreach (var group in _groups.Values)
            group.Row.Visible = false;
    }

    private void UpdateDisclosure()
    {
        GroupsContainer.Visible = DamageToggle.Visible && DamageToggle.Pressed;
        DamageToggle.Text = Loc.GetString(DamageToggle.Pressed ? "orbitra-analyzer-collapse-damage" : "orbitra-analyzer-expand-damage");
    }

    private void UpdateDamage(EntityUid patient)
    {
        if (!_entities.TryGetComponent<DamageableComponent>(patient, out var comp))
        {
            SetValue(DamageLabel, Loc.GetString("health-analyzer-window-entity-unknown-value-text"));
            if (_lastDamage != null)
                ResetDamage();
            return;
        }

        var system = _entities.System<DamageableSystem>();
        var damage = system.GetAllDamage((patient, comp));
        if (damage.Equals(_lastDamage))
            return;
        _lastDamage = damage;

        var total = system.GetTotalDamage((patient, comp));
        DamageLabel.Text = total.ToString();
        DamageToggle.Visible = total > FixedPoint2.Zero;
        UpdateDisclosure();

        var groups = system.GetDamagePerGroup((patient, comp));
        foreach (var (id, group) in _groups)
            group.Row.Visible = groups.GetValueOrDefault(id) > 0;

        var position = 0;
        foreach (var (id, amount) in groups.OrderByDescending(pair => pair.Value))
        {
            if (amount <= 0)
                break;
            var prototype = _prototypes.Index(id);
            if (!_groups.TryGetValue(id, out var group))
            {
                group = CreateGroup(id);
                _groups.Add(id, group);
                GroupsContainer.AddChild(group.Row);
            }

            group.Row.Visible = true;
            group.Row.SetPositionInParent(position++);
            SetValue(group.Title, Loc.GetString("health-analyzer-window-damage-group-text",
                ("damageGroup", prototype.LocalizedName), ("amount", amount)));
            var types = new List<string>();
            foreach (var type in prototype.DamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                    types.Add(Loc.GetString("health-analyzer-window-damage-type-text",
                        ("damageType", _prototypes.Index(type).LocalizedName), ("amount", value)));
            }
            var text = string.Join(" · ", types);
            if (group.Types.Text != text)
                group.Types.SetMessage(text);
        }
    }

    private (BoxContainer, Label, RichTextLabel) CreateGroup(ProtoId<DamageGroupPrototype> id)
    {
        var path = new ResPath("/Textures/Objects/Devices/health_analyzer.rsi");
        var state = id.Id.ToLowerInvariant();
        if (!_resources.GetResource<RSIResource>(path).RSI.TryGetState(state, out _))
            state = "unknown";

        var row = new BoxContainer { SeparationOverride = 8 };
        row.AddChild(new TextureRect
        {
            Texture = _entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(path, state)),
            SetSize = new Vector2(24), VerticalAlignment = VAlignment.Top,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        });
        var column = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 0 };
        var title = new Label();
        var types = new RichTextLabel { HorizontalExpand = true };
        column.AddChild(title);
        column.AddChild(types);
        row.AddChild(column);
        return (row, title, types);
    }
}
