using System.Linq;
using System.Numerics;
using Content.Shared.Preferences;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyCharacterPreviewPanel
{
    /// <summary>Requests a saved profile relative to the selected slot.</summary>
    public event Action<int>? OrbitraProfileRequested;
    private int _orbitraDirection;
    private int _orbitraStep = 1;
    private int? _orbitraSlot;
    private int _orbitraSlotCount;
    private float _orbitraTransition = 1;
    private HumanoidCharacterProfile? _orbitraProfile;
    private bool _orbitraAwaitFrame;

    private void InitializeOrbitraPreview()
    {
        PreviousProfile.OnPressed += _ => RequestOrbitraProfile(-1);
        NextProfile.OnPressed += _ => RequestOrbitraProfile(1);
        RotateProfile.OnPressed += _ =>
        {
            _orbitraDirection = (_orbitraDirection + 1) % 4;
            ProfilePreviewSpriteView.OverrideDirection = _orbitraDirection switch
            {
                1 => Direction.East,
                2 => Direction.North,
                3 => Direction.West,
                _ => Direction.South,
            };
        };
    }

    internal void RequestOrbitraProfile(int step)
    {
        if (_orbitraTransition < 1 || _orbitraAwaitFrame || _orbitraSlotCount <= 1)
            return;
        _orbitraStep = step;
        OrbitraProfileRequested?.Invoke(step);
    }

    /// <summary>Updates labels without interpreting the localized profile summary.</summary>
    public void RefreshOrbitraProfile(HumanoidCharacterProfile profile, PlayerPreferences? preferences)
    {
        CharacterName.Text = string.IsNullOrWhiteSpace(profile.Name) ? Loc.GetString("orbitra-lobby-unnamed") : profile.Name;
        CharacterName.ToolTip = profile.Name;
        var slot = preferences?.SelectedCharacterIndex;
        var slots = preferences?.Characters.Keys.Order().ToArray() ?? [];
        var index = preferences == null ? -1 : Array.IndexOf(slots, preferences.SelectedCharacterIndex);
        ProfileCounter.Text = Loc.GetString("orbitra-lobby-profile", ("current", (index + 1).ToString("D2")), ("total", slots.Length.ToString("D2")));
        _orbitraSlotCount = slots.Length;
        PreviousProfile.Disabled = NextProfile.Disabled = _orbitraSlotCount <= 1 || _orbitraTransition < 1;
        if (slot == _orbitraSlot && _orbitraProfile != null && profile.WithName(_orbitraProfile.Name).Equals(_orbitraProfile) && ProfilePreviewSpriteView.PreviewDummy.IsValid())
        {
            ProfilePreviewSpriteView.SetName(profile.Name);
            _orbitraProfile = profile;
            return;
        }
        var animate = _orbitraSlot != null && slot != _orbitraSlot && _orbitraProfile != null &&
                      VisibleInTree && !IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.ReducedMotion);
        FinishOrbitraTransition();
        if (animate)
        {
            ProfilePreviewSpriteView.TransferOrbitraPreviewTo(OutgoingPreview);
            OutgoingPreview.Visible = true;
            _orbitraTransition = 0;
            _orbitraAwaitFrame = true;
        }
        ProfilePreviewSpriteView.LoadPreview(profile);
        _orbitraSlot = slot;
        _orbitraProfile = profile;
        ApplyOrbitraTransition();
        var species = IoCManager.Resolve<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
        Summary.Text = Loc.GetString("orbitra-lobby-character-details",
            ("species", Loc.GetString(species.Name)), ("age", profile.Age));
        PreviousProfile.Disabled = NextProfile.Disabled = _orbitraSlotCount <= 1 || _orbitraTransition < 1;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Высота даётся родителем после вычитания закреплённых действий, а не всем экраном.
        CrewCard.SetWidth = Math.Min(480, availableSize.X);
        VBox.SeparationOverride = availableSize.Y < 400 ? 8 : 12;
        var size = Math.Clamp(Math.Min(Math.Min(480, availableSize.X) - 168, availableSize.Y - 230), 32, 312);
        PreviewStage.SetSize = new Vector2(size);
        ProfilePreviewSpriteView.Scale = OutgoingPreview.Scale = new Vector2(size / 32f);
        return base.MeasureOverride(availableSize);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_orbitraAwaitFrame)
        {
            _orbitraAwaitFrame = false;
            return;
        }
        AdvanceOrbitraTransition(args.DeltaSeconds);
    }

    internal void AdvanceOrbitraTransition(float seconds)
    {
        if (_orbitraTransition >= 1)
            return;
        _orbitraTransition = IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.ReducedMotion)
            ? 1 : Math.Min(1, _orbitraTransition + seconds / 0.24f);
        ApplyOrbitraTransition();
        if (_orbitraTransition >= 1)
            FinishOrbitraTransition();
    }

    private void ApplyOrbitraTransition()
    {
        var t = _orbitraTransition;
        var smooth = t * t * (3 - 2 * t);
        PreviewStage.IncomingOffset = _orbitraStep * 48 * (1 - smooth);
        PreviewStage.OutgoingOffset = -_orbitraStep * 48 * smooth;
        ProfilePreviewSpriteView.Modulate = Color.White.WithAlpha(smooth);
        OutgoingPreview.Modulate = Color.White.WithAlpha(1 - smooth);
        PreviewStage.InvalidateArrange();
    }

    internal void FinishOrbitraTransition()
    {
        _orbitraTransition = 1;
        _orbitraAwaitFrame = false;
        OutgoingPreview.ClearPreview();
        OutgoingPreview.Visible = false;
        ApplyOrbitraTransition();
        PreviousProfile.Disabled = NextProfile.Disabled = _orbitraSlotCount <= 1;
    }

    /// <summary>Clears both client-only dummies when no profile is available.</summary>
    public void ClearOrbitraPreview()
    {
        FinishOrbitraTransition();
        ProfilePreviewSpriteView.ClearPreview();
        _orbitraProfile = null;
        _orbitraSlot = null;
    }

    protected override void ExitedTree()
    {
        ClearOrbitraPreview();
        base.ExitedTree();
    }
}
