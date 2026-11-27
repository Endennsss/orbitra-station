using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Launcher;

/// <summary>Вид на причал при подключении: две снятые с карт сетки стыкуются по внешней кромке шлюзов.</summary>
public sealed partial class OrbitraDockingScene : Control
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IResourceCache _resources = default!;

    private const float Tile = 32f;
    private const float ApproachDistance = 8.5f;
    private const float LateralOffset = 0.92f;
    private const float AlignedAt = 0.78f;
    // Координаты в исходных изображениях, от левого верхнего угла.
    private static readonly Vector2 ShipPort = new(292, 112);
    private static readonly Vector2 BerthPort = new(32, 384);
    private static readonly Vector2 AftNozzle = new(4, 48);
    private static readonly Vector2 ForeNozzle = new(292, 48);

    private static readonly Vector2 UpperNozzle = new(52, 0);
    private static readonly Vector2 LowerNozzle = new(20, 224);

    // Концы девяти кадров opening из shuttle-base.rsi, далее в атласе идёт open.
    private static readonly float[] DoorFrameEnds = [0.10f, 0.15f, 0.20f, 0.23f, 0.26f, 0.29f, 0.34f, 0.39f, 0.49f];
    private const float ClampDelay = 0.12f;

    private readonly Texture _ship;
    private readonly Texture _berth;
    private readonly Texture _doors;
    private readonly Texture _sky;
    private readonly RSI.State _exhaust;
    private readonly RSI.State _nozzleLight;
    private float _position;
    private float _velocity;
    private float _target;
    private float _burn;
    private float _jetTime;
    private float _contactTime;
    private bool _stopped;

    public OrbitraDockingScene()
    {
        IoCManager.InjectDependencies(this);
        _ship = _resources.GetTexture("/Textures/_Orbitra/Interface/Docking/shuttle.png");
        _berth = _resources.GetTexture("/Textures/_Orbitra/Interface/Docking/port.png");
        _doors = _resources.GetTexture("/Textures/_Orbitra/Interface/Docking/doors.png");
        _sky = _resources.GetTexture("/Textures/Parallaxes/space_map2.png");
        var thruster = _resources.GetResource<RSIResource>("/Textures/Structures/Shuttles/thruster.rsi").RSI;
        _exhaust = thruster["thrust_burn_unshaded"];
        _nozzleLight = thruster["thrust"];
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public void Reset()
    {
        _position = _velocity = _target = _burn = _jetTime = _contactTime = 0;
        _stopped = false;
    }

    public void SetStage(ClientConnectionState stage)
    {
        if (_stopped || stage == ClientConnectionState.NotConnecting)
            return;

        _target = stage switch
        {
            ClientConnectionState.EstablishingConnection => 0.50f,
            ClientConnectionState.Handshake => 0.85f,
            ClientConnectionState.Connected => 1f,
            _ => 0f,
        };
        if (_config.GetCVar(CCVars.ReducedMotion))
            HoldPosition(_target);
    }

    public void Stop()
    {
        HoldPosition(_position);
        _stopped = true;
    }

    public void StopAtDock()
    {
        HoldPosition(1);
        _contactTime = ClampDelay + DoorFrameEnds[^1];
        _stopped = true;
    }

    private void HoldPosition(float position)
    {
        _position = _target = position;
        _velocity = _burn = 0;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize) =>
        new(0, Math.Clamp(availableSize.X * 0.38f, 88, 338));

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_stopped)
            return;

        if (_position == 1f)
        {
            var openedAt = ClampDelay + DoorFrameEnds[^1];
            if (_contactTime < openedAt)
                _contactTime = _config.GetCVar(CCVars.ReducedMotion) ? openedAt : MathF.Min(_contactTime + args.DeltaSeconds, openedAt);
            return;
        }

        if (_position == _target)
            return;
        if (_config.GetCVar(CCVars.ReducedMotion))
        {
            HoldPosition(_target);
            return;
        }

        // Критическое затухание сохраняет скорость при смене сетевого этапа прямо во время манёвра.
        var response = _target == 1f ? 6f : 4.6f;
        var dt = args.DeltaSeconds;
        var offset = _position - _target;
        var slope = _velocity + response * offset;
        var decay = MathF.Exp(-response * dt);
        _velocity = (_velocity - response * slope * dt) * decay;
        _position = _target + (offset + slope * dt) * decay;
        _burn = response * (response * (_target - _position) - 2 * _velocity);
        _jetTime = (_jetTime + dt) % _exhaust.TotalDelay;
        if (MathF.Abs(_target - _position) < 0.0006f)
            HoldPosition(_target);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (PixelWidth < 4 || PixelHeight < 4)
            return;

        var size = (Vector2) PixelSize;
        var p = _position;
        var scale = MathF.Min(size.X / (26 * Tile), size.Y / (10.5f * Tile)) * (1 + 0.12f * p);
        var berth = new Vector2(size.X * (0.8f - 0.16f * p), size.Y * (0.56f - 0.06f * p)).Rounded();
        var align = Math.Clamp(1 - p / AlignedAt, 0, 1);
        var ship = berth - new Vector2(ApproachDistance * (1 - p), LateralOffset * align * align) * (Tile * scale);

        // Далёкий фон сдвигается медленнее станции, когда камера сопровождает шаттл.
        var fieldWidth = MathF.Min(840, 800 * size.X / size.Y);
        var field = new Vector2(fieldWidth, fieldWidth * size.Y / size.X);
        var skyRect = UIBox2.FromDimensions(new Vector2(58 + 30 * p, 70), field);
        handle.DrawTextureRectRegion(_sky, UIBox2.FromDimensions(Vector2.Zero, size), skyRect);
        var viewport = handle.GetTransform();
        try
        {
            handle.SetTransform(Matrix3x2.CreateTranslation(-BerthPort) * Matrix3x2.CreateScale(scale)
                * Matrix3x2.CreateTranslation(berth) * viewport);
            handle.DrawTextureRect(_berth, UIBox2.FromDimensions(Vector2.Zero, _berth.Size));

            handle.SetTransform(Matrix3x2.CreateTranslation(-ShipPort) * Matrix3x2.CreateScale(scale)
                * Matrix3x2.CreateRotation(0.11f * align) * Matrix3x2.CreateTranslation(ship) * viewport);
            handle.DrawTextureRect(_ship, UIBox2.FromDimensions(Vector2.Zero, _ship.Size));
            DrawThrusters(handle, align);
            if (_position == 1f && _contactTime >= ClampDelay)
                DrawDoors(handle);
        }
        finally
        {
            handle.SetTransform(viewport);
        }
    }

    private void DrawDoors(DrawingHandleScreen handle)
    {
        // Обе створки запечены вместе с полом: после стыковки они точно замещают закрытые тайлы.
        var frame = 0;
        while (frame < DoorFrameEnds.Length && _contactTime >= ClampDelay + DoorFrameEnds[frame])
            frame++;

        var pair = new Vector2(2 * Tile, Tile);
        handle.DrawTextureRectRegion(_doors, UIBox2.FromDimensions(ShipPort - pair / 2, pair),
            UIBox2.FromDimensions(new Vector2(frame * pair.X, 0), pair));
    }

    private void DrawThrusters(DrawingHandleScreen handle, float align)
    {
        if (_velocity == 0)
            return;

        var frame = 0;
        var time = _jetTime;
        while (frame + 1 < _exhaust.DelayCount && time >= _exhaust.GetDelay(frame))
        {
            time -= _exhaust.GetDelay(frame);
            frame++;
        }

        if (_burn > 0)
            DrawJet(handle, frame, AftNozzle, -Vector2.UnitX, RsiDirection.East, _burn);
        else
            DrawJet(handle, frame, ForeNozzle, Vector2.UnitX, RsiDirection.West, -_burn);

        if (align <= 0)
            return;

        // Поперечное ускорение на кривой захода определяет, какой маневровый двигатель работает.
        var lateralBurn = 2 * LateralOffset / (ApproachDistance * AlignedAt)
            * (align * _burn - _velocity * _velocity / AlignedAt);
        if (lateralBurn > 0)
            DrawJet(handle, frame, UpperNozzle, -Vector2.UnitY, RsiDirection.South, lateralBurn);
        else
            DrawJet(handle, frame, LowerNozzle, Vector2.UnitY, RsiDirection.North, -lateralBurn);
    }

    private void DrawJet(DrawingHandleScreen handle, int frame, Vector2 nozzle, Vector2 outward, RsiDirection direction, float power)
    {
        if (power < 0.2f)
            return;

        var length = Tile * Math.Clamp(power / 2, 0.25f, 1f);
        var size = outward.X == 0 ? new Vector2(Tile, length) : new Vector2(length, Tile);
        var engine = nozzle - (Vector2.One + outward) * (Tile / 2);
        var plume = nozzle + outward * (length / 2) - size / 2;
        handle.DrawTextureRect(_nozzleLight.GetFrame(direction, frame), UIBox2.FromDimensions(engine, new Vector2(Tile)));
        handle.DrawTextureRect(_exhaust.GetFrame(direction, frame), UIBox2.FromDimensions(plume, size));
    }
}
