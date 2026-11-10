using System.Numerics;
using Content.Shared.Atmos;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Orbitra.Atmos;

internal sealed partial class OrbitraGasRenderer
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
        private bool _hasGas;

        public ChunkSurface(IClyde clyde, ShaderPrototype prototype)
        {
            _previousTexture = clyde.CreateBlankTexture<Rgba32>(Size, "orbitra-gas-previous", TextureOptions);
            try
            {
                _targetTexture = clyde.CreateBlankTexture<Rgba32>(Size, "orbitra-gas-target", TextureOptions);
                try
                {
                    _shader = prototype.InstanceUnique();
                    _shader.SetParameter("map_atmosphere", false);
                    _shader.SetParameter("previous_density", _previousTexture);
                }
                catch
                {
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

        public bool MayBeVisible(float time) => _hasGas || Progress(time, _changedAt) < 1f;

        public void Update(Dictionary<Vector2i, GasOverlayChunk> chunks, Vector2i index, Vector3[] palette,
            Span<Vector4> field, float time)
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
                    var x = tiles.X + offset.X;
                    var y = tiles.Y + offset.Y;
                    if ((uint) x < OrbitraGasField.FieldSize && (uint) y < OrbitraGasField.FieldSize)
                        field[y * OrbitraGasField.FieldSize + x] = OrbitraGasField.Mix(gas.Opacity, palette);
                }
            }

            if (field.SequenceEqual(_field))
                return;

            // Перезапуск перехода начинается с уже отображаемого состояния.
            OrbitraGasField.BlendInto(_from, _target, Progress(time, _changedAt));
            OrbitraGasField.Rasterize(field, _target);
            field.CopyTo(_field);
            _hasGas = _sources[4] is { } center && HasGas(center);
            _changedAt = time;
            _previousTexture.SetSubImage<Rgba32>(Vector2i.Zero, Size, _from);
            _targetTexture.SetSubImage<Rgba32>(Vector2i.Zero, Size, _target);
        }

        public void Draw(DrawingHandleWorld handle, Matrix3x2 transform, Box2 bounds, float now, float driftTime)
        {
            SetCloudPosition(_shader, bounds, driftTime);
            _shader.SetParameter("transition", Progress(now, _changedAt));
            handle.SetTransform(transform);
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
