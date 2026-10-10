using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Draws a microphone above active speakers and a compact speaker list in the lower-right corner.
/// </summary>
internal sealed class OrbitraVoiceSpeakerOverlay : Overlay
{
    private const int MaxVisibleSpeakers = 4;
    private const int MaxNameLength = 28;
    // Карточка переживает короткие сетевые паузы и низкий FPS, не исчезая
    // между соседними голосовыми кадрами.
    private static readonly TimeSpan SpeakerHoldTime = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(120);

    private readonly IGameTiming _timing;
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IUserInterfaceManager _uiManager;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly Font _font;
    private readonly Font _fontBold;
    private readonly Texture _microphone;
    private readonly Dictionary<NetEntity, SpeakerEntry> _speakers = new();
    private readonly List<NetEntity> _expired = new();

    public OrbitraVoiceSpeakerOverlay(
        IGameTiming timing,
        IResourceCache resources,
        IEntityManager entities,
        IPlayerManager player,
        IUserInterfaceManager uiManager)
    {
        _timing = timing;
        _entities = entities;
        _player = player;
        _uiManager = uiManager;
        _transform = entities.System<SharedTransformSystem>();
        _sprite = entities.System<SpriteSystem>();
        _font = resources.NotoStack(size: 11);
        _fontBold = resources.NotoStack(variation: "Bold", size: 12);
        _microphone = resources.GetTexture("/Textures/_Orbitra/Interface/Icons/microphone.svg.192dpi.png");
        ZIndex = 250;
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public void ShowLocal(string name)
    {
        var normalizedName = NormalizeName(name);
        if (_speakers.TryGetValue(NetEntity.Invalid, out var speaker))
        {
            speaker.Name = normalizedName;
            speaker.Persistent = true;
            speaker.Mode = OrbitraVoiceTransmissionMode.Proximity;
            speaker.ChannelId = string.Empty;
            return;
        }

        _speakers[NetEntity.Invalid] = new SpeakerEntry(normalizedName, persistent: true);
    }

    public void HideLocal() => _speakers.Remove(NetEntity.Invalid);

    public void TouchSpeaker(NetEntity speakerId, string name, OrbitraVoiceTransmissionMode mode, string channelId)
    {
        if (speakerId == NetEntity.Invalid)
            return;

        var normalizedName = NormalizeName(name);
        if (_speakers.TryGetValue(speakerId, out var speaker))
        {
            if (!string.Equals(speaker.Name, normalizedName, StringComparison.Ordinal))
                speaker.Name = normalizedName;

            speaker.Mode = mode;
            speaker.ChannelId = channelId;
            speaker.ExpiresAt = _timing.RealTime + SpeakerHoldTime;
            return;
        }

        _speakers[speakerId] = new SpeakerEntry(normalizedName, persistent: false)
        {
            Mode = mode,
            ChannelId = channelId,
            ExpiresAt = _timing.RealTime + SpeakerHoldTime,
        };
    }

    public void Clear() => _speakers.Clear();

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        RemoveExpiredSpeakers();
        return args.Space switch
        {
            OverlaySpace.WorldSpaceBelowFOV => _speakers.Count != 0,
            _ => false,
        };
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.WorldSpaceBelowFOV:
                DrawWorld(args);
                break;
        }
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        var world = args.WorldHandle;
        var iconSize = new Vector2(18f / EyeManager.PixelsPerMeter);
        var iconColor = Color.White;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRotation);

        foreach (var (speakerId, speaker) in _speakers)
        {
            if (!TryGetSpeakerEntity(speakerId, out var entity) ||
                !_entities.TryGetComponent(entity, out TransformComponent? transform) ||
                transform.MapID != args.MapId)
            {
                continue;
            }

            // Ставим значок относительно верхней границы фактического спрайта,
            // чтобы он оставался над головой у персонажей разного роста.
            var worldPosition = _transform.GetWorldPosition(transform);
            var iconOffset = new Vector2(0f, 0.82f);
            if (_entities.TryGetComponent(entity, out SpriteComponent? sprite) && sprite.Visible)
            {
                var bounds = _sprite.GetLocalBounds((entity, sprite));
                // Bounds уже учитывают смещения слоёв, но не Offset самого SpriteComponent.
                // Используем центр и верхнюю границу спрайта, чтобы значок не уезжал к ногам
                // или в сторону из-за направления персонажа и предметов в руках.
                iconOffset = new Vector2(
                    sprite.Offset.X,
                    sprite.Offset.Y + bounds.Height / 2f + iconSize.Y / 2f + 0.1f);
            }

            var iconCenter = worldPosition + iconOffset;
            if (!args.WorldAABB.Contains(iconCenter))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPosition);
            world.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));
            world.DrawTextureRect(
                _microphone,
                Box2.CenteredAround(iconOffset, iconSize),
                iconColor.WithAlpha(GetAlpha(speaker)));
        }

        world.SetTransform(Matrix3x2.Identity);
    }

    internal void DrawScreen(DrawingHandleScreen screen, Vector2 viewportSize, float uiScale)
    {
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return;

        RemoveExpiredSpeakers();
        var remoteCount = CountRemoteSpeakers();
        if (remoteCount == 0)
            return;

        uiScale = Math.Max(1f, uiScale);
        var margin = 16f * uiScale;
        var rightEdge = GetHudRightEdge(viewportSize.X);
        var cardWidth = MathF.Min(240f * uiScale, rightEdge - margin);
        if (cardWidth < 120f * uiScale)
            return;

        const float cardHeight = 46f;
        var gap = 6f * uiScale;
        var availableHeight = MathF.Max(0f, viewportSize.Y - margin * 2f);
        var maxCardsByHeight = (int) MathF.Floor((availableHeight + gap) / (cardHeight * uiScale + gap));
        var visibleCount = Math.Min(MaxVisibleSpeakers, Math.Min(remoteCount, maxCardsByHeight));
        if (visibleCount <= 0)
            return;

        var bottom = viewportSize.Y - margin;
        var drawnCount = 0;
        var left = rightEdge - margin - cardWidth;

        foreach (var (speakerId, speaker) in _speakers)
        {
            if (speakerId == NetEntity.Invalid || drawnCount >= visibleCount)
                continue;

            var rect = OrbitraVoiceSpeakerLayout.CalculateCardRect(viewportSize, rightEdge, drawnCount, uiScale);
            DrawSpeakerCard(screen, rect.Left, rect.Top, rect.Width, rect.Height, uiScale, speaker);
            bottom = rect.Top;
            bottom -= gap;
            drawnCount++;
        }

        if (remoteCount > drawnCount && bottom - 30f * uiScale >= margin)
            DrawOverflowCard(screen, left, bottom - 30f * uiScale, cardWidth, uiScale, remoteCount - drawnCount);
    }

    private float GetHudRightEdge(float fallback)
    {
        var viewport = _uiManager.ActiveScreen?.FindControl<LayoutContainer>("ViewportContainer");
        if (viewport == null)
            return fallback;

        // В раздельном HUD ViewportContainer занимает левую часть экрана.
        // PopupRoot шире него, поэтому его правый край нельзя использовать
        // для карточки: иначе карточка попадает поверх панели чата.
        var popupLeft = _uiManager.PopupRoot.GlobalPixelPosition.X;
        var viewportRight = viewport.GlobalPixelRect.Right - popupLeft;
        return Math.Clamp(viewportRight, 0f, fallback);
    }

    private void RemoveExpiredSpeakers()
    {
        var now = _timing.RealTime;
        _expired.Clear();
        foreach (var (speakerId, speaker) in _speakers)
        {
            if (!speaker.Persistent && speaker.ExpiresAt <= now)
                _expired.Add(speakerId);
        }

        foreach (var speakerId in _expired)
            _speakers.Remove(speakerId);
    }

    private void DrawSpeakerCard(
        DrawingHandleScreen screen,
        float left,
        float top,
        float width,
        float height,
        float uiScale,
        SpeakerEntry speaker)
    {
        var alpha = GetAlpha(speaker);
        var background = OrbitraPalettes.PanelInset.WithAlpha(0.94f * alpha);
        var border = OrbitraPalettes.PanelBorder.WithAlpha(alpha);
        var iconColor = Color.White.WithAlpha(alpha);
        var textColor = OrbitraPalettes.Primary.Text.WithAlpha(alpha);
        var box = UIBox2.FromDimensions(new Vector2(left, top), new Vector2(width, height));

        screen.DrawRect(box, background);
        screen.DrawRect(box, border, filled: false);
        screen.DrawRect(
            UIBox2.FromDimensions(new Vector2(left, top), new Vector2(width, 2f * uiScale)),
            border);

        var iconSize = 20f * uiScale;
        screen.DrawTextureRect(
            _microphone,
            UIBox2.FromDimensions(new Vector2(left + 9f * uiScale, top + (height - iconSize) / 2f), new Vector2(iconSize)),
            iconColor);

        var name = FitNameToCard(screen, speaker.Name, width - 42f * uiScale, uiScale);
        var textSize = screen.GetDimensions(_fontBold, name, uiScale);
        var textPosition = new Vector2(left + 36f * uiScale, top + 8f * uiScale);
        screen.DrawString(_fontBold, textPosition, name, uiScale, textColor);

        if (speaker.Mode == OrbitraVoiceTransmissionMode.Radio)
        {
            var channel = string.IsNullOrWhiteSpace(speaker.ChannelId)
                ? Loc.GetString("orbitra-voice-chat-radio")
                : Loc.GetString("orbitra-voice-chat-radio-channel", ("channel", speaker.ChannelId));
            var channelSize = screen.GetDimensions(_font, channel, uiScale);
            screen.DrawString(_font, new Vector2(left + 36f * uiScale, top + height - channelSize.Y - 6f * uiScale), channel, uiScale, OrbitraPalettes.IconNormal.WithAlpha(alpha));
        }
    }

    private string FitNameToCard(DrawingHandleScreen screen, string name, float availableWidth, float uiScale)
    {
        if (screen.GetDimensions(_fontBold, name, uiScale).X <= availableWidth)
            return name;

        const string suffix = "…";
        while (name.Length > 1 && screen.GetDimensions(_fontBold, name + suffix, uiScale).X > availableWidth)
            name = name[..^1];

        return name + suffix;
    }

    private void DrawOverflowCard(DrawingHandleScreen screen, float left, float top, float width, float uiScale, int count)
    {
        var height = 28f * uiScale;
        var box = UIBox2.FromDimensions(new Vector2(left, top), new Vector2(width, height));
        var color = OrbitraPalettes.IconNormal.WithAlpha(0.94f);
        screen.DrawRect(box, OrbitraPalettes.PanelInset.WithAlpha(0.94f));
        screen.DrawRect(box, OrbitraPalettes.PanelBorder.WithAlpha(0.9f), filled: false);

        var text = Loc.GetString("orbitra-voice-chat-more", ("count", count));
        var textSize = screen.GetDimensions(_font, text, uiScale);
        screen.DrawString(_font, new Vector2(left + (width - textSize.X) / 2f, top + 7f * uiScale), text, uiScale, color);
    }

    private int CountRemoteSpeakers()
    {
        var count = 0;
        foreach (var speakerId in _speakers.Keys)
        {
            if (speakerId != NetEntity.Invalid)
                count++;
        }

        return count;
    }

    private bool TryGetSpeakerEntity(NetEntity speakerId, out EntityUid entity)
    {
        if (speakerId == NetEntity.Invalid)
        {
            if (_player.LocalEntity is not { } local || !_entities.EntityExists(local))
            {
                entity = EntityUid.Invalid;
                return false;
            }

            entity = local;
            return true;
        }

        if (!_entities.TryGetEntity(speakerId, out EntityUid? resolved) || resolved is not { } valid)
        {
            entity = EntityUid.Invalid;
            return false;
        }

        entity = valid;
        return true;
    }

    private float GetAlpha(SpeakerEntry speaker)
    {
        if (speaker.Persistent)
            return 1f;

        var remaining = speaker.ExpiresAt - _timing.RealTime;
        if (remaining >= FadeTime)
            return 1f;

        return Math.Clamp((float) (remaining.TotalSeconds / FadeTime.TotalSeconds), 0f, 1f);
    }

    private static string NormalizeName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? Loc.GetString("orbitra-voice-chat-unknown-speaker") : name.Trim();
        return name.Length <= MaxNameLength ? name : name[..(MaxNameLength - 1)] + "…";
    }

    private sealed class SpeakerEntry
    {
        public string Name;
        public OrbitraVoiceTransmissionMode Mode;
        public string ChannelId = string.Empty;
        public TimeSpan ExpiresAt;
        public bool Persistent;

        public SpeakerEntry(string name, bool persistent)
        {
            Name = name;
            Persistent = persistent;
            ExpiresAt = TimeSpan.Zero;
        }
    }
}
