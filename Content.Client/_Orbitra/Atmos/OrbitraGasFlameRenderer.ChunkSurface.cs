using System.Numerics;
using Content.Shared._Orbitra.Atmos;
using Content.Shared.Atmos;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Orbitra.Atmos;

internal sealed partial class OrbitraGasFlameRenderer
{
    private sealed class ChunkSurface : IDisposable
    {
        private static readonly Vector2i Size = new(OrbitraGasField.TextureSize, OrbitraGasField.TextureSize);
        private static readonly TextureLoadParameters TextureOptions = new()
        {
            Srgb = false,
            SampleParameters = new TextureSampleParameters { Filter = true },
        };

        private readonly GasOverlayChunk?[] _sources = new GasOverlayChunk?[9];
        private readonly Vector4[] _field = new Vector4[OrbitraGasField.FieldSize * OrbitraGasField.FieldSize];
        private readonly Rgba32[] _from = new Rgba32[Size.X * Size.Y];
        private readonly Rgba32[] _target = new Rgba32[Size.X * Size.Y];
        private readonly OwnedTexture _previousTexture;
        private readonly OwnedTexture _targetTexture;
        private readonly ShaderInstance _shader;
        private float _changedAt;
        private bool _hasFire;

        public ChunkSurface(IClyde clyde, ShaderPrototype prototype)
        {
            _previousTexture = clyde.CreateBlankTexture<Rgba32>(Size, "orbitra-flame-previous", TextureOptions);
            try
            {
                _targetTexture = clyde.CreateBlankTexture<Rgba32>(Size, "orbitra-flame-target", TextureOptions);
                try
                {
                    _shader = prototype.InstanceUnique();
                    _shader.SetParameter("previous_fire", _previousTexture);
                }
                catch
                {
                    _shader?.Dispose();
                    _targetTexture.Dispose();
                    throw;
                }
            }
            catch
            {
                _previousTexture.Dispose();
                throw;
            }
        }

        public bool MayBeVisible(float time) => _hasFire || Progress(time, _changedAt) < 1f;

        public void Update(Dictionary<Vector2i, GasOverlayChunk> chunks, Vector2i index, Span<Vector4> field, float time)
        {
            var changed = false;
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                var sourceIndex = (y + 1) * 3 + x + 1;
                chunks.TryGetValue(index + (x, y), out var chunk);
                changed |= !ReferenceEquals(_sources[sourceIndex], chunk);
                _sources[sourceIndex] = chunk;
            }

            if (!changed)
                return;

            _hasFire = _sources[4] is { } center && HasFire(center);
            field.Clear();
            var origin = index * OrbitraGasField.Tiles;
            foreach (var source in _sources)
            {
                if (source == null)
                    continue;

                var offset = source.Origin - origin + (1, 1);
                var tiles = new GasChunkEnumerator(source);
                while (tiles.MoveNext(out var gas))
                {
                    if (gas.FireState == 0)
                        continue;

                    var x = tiles.X + offset.X;
                    var y = tiles.Y + offset.Y;
                    if ((uint) x >= OrbitraGasField.FieldSize || (uint) y >= OrbitraGasField.FieldSize)
                        continue;

                    var strength = gas.FireState / 3f;
                    var tint = OrbitraAtmosVisual.Tint(gas.VisualState) / 7f;
                    var flow = OrbitraAtmosVisual.Flow(gas.VisualState);
                    var direction = flow == AtmosDirection.Invalid ? Vector2i.Zero : flow.CardinalToIntVec();
                    // Храним параметры с весом очага, чтобы пустые клетки не меняли цвет и наклон на краю.
                    field[y * OrbitraGasField.FieldSize + x] =
                        new Vector4(tint, 0.5f + direction.X * 0.5f, 0.5f + direction.Y * 0.5f, 1f) * strength;
                }
            }

            if (field.SequenceEqual(_field))
                return;

            OrbitraGasField.BlendInto(_from, _target, Progress(time, _changedAt));
            OrbitraGasField.Rasterize(field, _target);
            field.CopyTo(_field);
            _changedAt = time;
            _previousTexture.SetSubImage<Rgba32>(Vector2i.Zero, Size, _from);
            _targetTexture.SetSubImage<Rgba32>(Vector2i.Zero, Size, _target);
        }

        public void Draw(DrawingHandleWorld handle, Box2 bounds, float time, bool reducedMotion)
        {
            _shader.SetParameter("fire_origin", bounds.BottomLeft);
            _shader.SetParameter("fire_size", bounds.Size);
            _shader.SetParameter("transition", Progress(time, _changedAt));
            _shader.SetParameter("reduced_motion", reducedMotion);
            handle.UseShader(_shader);
            handle.DrawTextureRect(_targetTexture, bounds);
        }

        public void Dispose()
        {
            _shader.Dispose();
            _targetTexture.Dispose();
            _previousTexture.Dispose();
        }
    }
}
