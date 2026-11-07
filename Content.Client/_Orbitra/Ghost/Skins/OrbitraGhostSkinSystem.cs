// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Ghost.Skins;

/// <summary>Requests observer cosmetics and applies the server-selected skin on a separate sprite layer.</summary>
public sealed partial class OrbitraGhostSkinSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string NativeLayer = "ghostVariant";

    public event Action? SkinChanged;

    public ProtoId<OrbitraGhostSkinPrototype> SelectedSkin
    {
        get
        {
            if (TryComp<OrbitraGhostSkinComponent>(_player.LocalEntity, out var skin)
                && _prototypes.HasIndex(skin.Skin))
                return skin.Skin;

            return GetPreference();
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraGhostSkinComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraGhostSkinComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraGhostSkinComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<OrbitraGhostSkinComponent, AppearanceChangeEvent>(OnAppearance,
            after: [typeof(GenericVisualizerSystem)]);
        // Штатная GhostSystem уже владеет этой парой событий для GhostComponent.
        SubscribeLocalEvent<OrbitraGhostSkinComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<OrbitraGhostSkinComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnStartup(Entity<OrbitraGhostSkinComponent> ent, ref ComponentStartup args)
    {
        ApplySkin(ent);
        NotifyLocal(ent);
    }

    private void OnShutdown(Entity<OrbitraGhostSkinComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite)
            && TryComp<OrbitraGhostSkinVisualComponent>(ent, out var visual))
            RestoreNative((ent, sprite), visual);

        NotifyLocal(ent);
    }

    private void OnState(Entity<OrbitraGhostSkinComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplySkin(ent);
        NotifyLocal(ent);
    }

    private void OnAppearance(Entity<OrbitraGhostSkinComponent> ent, ref AppearanceChangeEvent args)
    {
        ApplySkin(ent);
    }

    private void OnAttached(Entity<OrbitraGhostSkinComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        // Повторно отправляем сохранённый выбор для нового призрака, не изменяя сетевой компонент локально.
        RequestSkin(GetPreference());
        SkinChanged?.Invoke();
    }

    private void OnDetached(Entity<OrbitraGhostSkinComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        SkinChanged?.Invoke();
    }

    /// <summary>Stores and requests a predefined skin for the currently controlled ghost.</summary>
    public bool RequestSkin(ProtoId<OrbitraGhostSkinPrototype> skin)
    {
        if (!CanRequestSkin(skin))
            return false;

        if (_cfg.GetCVar(OrbitraGhostSkinCVars.SelectedSkin) != skin.Id)
        {
            _cfg.SetCVar(OrbitraGhostSkinCVars.SelectedSkin, skin.Id);
            _cfg.SaveToFile();
        }
        RaiseNetworkEvent(new OrbitraSelectGhostSkinEvent(skin));
        return true;
    }

    private bool CanRequestSkin(ProtoId<OrbitraGhostSkinPrototype> skin)
    {
        return _net.IsConnected
            && HasComp<GhostComponent>(_player.LocalEntity)
            && !string.IsNullOrWhiteSpace(skin.Id)
            && _prototypes.HasIndex(skin);
    }

    private ProtoId<OrbitraGhostSkinPrototype> GetPreference()
    {
        var saved = new ProtoId<OrbitraGhostSkinPrototype>(_cfg.GetCVar(OrbitraGhostSkinCVars.SelectedSkin));
        return _prototypes.HasIndex(saved) ? saved : OrbitraGhostSkinComponent.DefaultSkin;
    }

    private void NotifyLocal(EntityUid uid)
    {
        if (uid == _player.LocalEntity)
            SkinChanged?.Invoke();
    }

    private void ApplySkin(Entity<OrbitraGhostSkinComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        Entity<SpriteComponent?> spriteEnt = (ent, sprite);
        if (!_prototypes.TryIndex(ent.Comp.Skin, out var skin) || skin.Sprite == null)
        {
            if (TryComp<OrbitraGhostSkinVisualComponent>(ent, out var existing))
                RestoreNative(spriteEnt, existing);
            return;
        }

        if (!_sprite.TryGetLayer(spriteEnt, NativeLayer, out var native, false))
            return;

        var visual = EnsureComp<OrbitraGhostSkinVisualComponent>(ent);
        if (!visual.Replacing)
        {
            visual.OriginalColor = sprite.Color;
            visual.OriginalVisible = native.Visible;
            visual.Replacing = true;
        }

        if (visual.AppliedSkin != ent.Comp.Skin)
        {
            var layer = _sprite.LayerMapReserve(spriteEnt, SkinLayer.Replacement);
            _sprite.LayerSetData(spriteEnt, layer, new PrototypeLayerData
            {
                Shader = "unshaded",
                Visible = true,
                Color = Color.White.WithAlpha(skin.Opacity),
            });
            _sprite.LayerSetSprite(spriteEnt, layer, skin.Sprite);
            visual.AppliedSkin = ent.Comp.Skin;
        }

        // Прозрачность задаётся только слою облика: исходный общий alpha призрака не умножается повторно.
        _sprite.SetColor(spriteEnt, Color.White);
        _sprite.LayerSetVisible(spriteEnt, NativeLayer, false);
    }

    private void RestoreNative(Entity<SpriteComponent?> sprite, OrbitraGhostSkinVisualComponent visual)
    {
        if (!visual.Replacing)
            return;

        if (_sprite.LayerExists(sprite, SkinLayer.Replacement))
            _sprite.LayerSetVisible(sprite, SkinLayer.Replacement, false);
        if (_sprite.LayerExists(sprite, NativeLayer))
            _sprite.LayerSetVisible(sprite, NativeLayer, visual.OriginalVisible);
        _sprite.SetColor(sprite, visual.OriginalColor);
        visual.Replacing = false;
        visual.AppliedSkin = null;
    }

    private enum SkinLayer : byte
    {
        Replacement,
    }
}
