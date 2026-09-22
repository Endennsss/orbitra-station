using System.Numerics;
using Content.Shared._Orbitra.Particles;

namespace Content.Client._Orbitra.Particles;

/// <summary>Bounded storage, independent of entities, render targets and camera coordinates.</summary>
internal sealed class OrbitraParticlePool
{
    internal struct Particle
    {
        public EntityUid Parent;
        public Vector2 Position;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Size;
        public OrbitraParticleEffectPrototype Effect;
        public bool Burst;
        public Color? Tint;
        public EntityUid? ImpactSource;
        public float? Opacity;
    }

    public readonly Particle[] Particles = new Particle[768];
    public int Count { get; private set; }
    public int Capacity { get; private set; } = 384;

    public void Configure(int capacity)
    {
        Capacity = Math.Clamp(capacity, 0, Particles.Length);
        Count = Math.Min(Count, Capacity);
        var marks = 0;
        for (var i = Count - 1; i >= 0; i--)
        {
            if (Particles[i].Effect.ImpactMark && ++marks > OrbitraBallistics.MarkBudget(Capacity))
                RemoveAt(i);
        }
        var excess = AmbientCount() - OrbitraDust.AmbientBudget(Capacity).Motes;
        for (var i = Count - 1; i >= 0 && excess > 0; i--)
        {
            if (!Particles[i].Effect.Ambient)
                continue;
            Particles[i] = Particles[--Count];
            excess--;
        }
    }

    public void Clear() => Count = 0;

    public void RemoveAt(int index) => Particles[index] = Particles[--Count];

    internal static int AdvanceEmission(ref float fraction, float elapsed, float rate)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(rate) || elapsed <= 0 || rate <= 0)
            return 0;
        fraction += Math.Min(elapsed, 0.05f) * rate;
        var count = (int) fraction;
        fraction -= count;
        return Math.Min(count, 32);
    }

    public bool Add(Particle particle)
    {
        if (particle.Effect.ImpactMark)
        {
            var total = 0;
            var onSource = 0;
            for (var i = 0; i < Count; i++)
            {
                ref var other = ref Particles[i];
                if (!other.Effect.ImpactMark)
                    continue;
                total++;
                if (other.ImpactSource != particle.ImpactSource)
                    continue;
                onSource++;
                if (Vector2.DistanceSquared(other.Position, particle.Position) < 0.0064f)
                    return false;
            }
            if (total >= OrbitraBallistics.MarkBudget(Capacity) || onSource >= 4 || Count >= Capacity)
                return false;
        }
        if (particle.Effect.Ambient && AmbientCount() >= OrbitraDust.AmbientBudget(Capacity).Motes)
            return false;
        if (Count < Capacity)
        {
            Particles[Count++] = particle;
            return true;
        }
        if (particle.Effect.Ambient)
            return false;
        // Следы не должны вытеснять рабочие эффекты, но сами уступают им место.
        for (var i = 0; i < Count; i++)
        {
            if (!Particles[i].Effect.ImpactMark)
                continue;
            Particles[i] = particle;
            return true;
        }
        // Фоновая пыль уступает место даже дыханию и дыму.
        for (var i = 0; i < Count; i++)
        {
            if (!Particles[i].Effect.Ambient)
                continue;
            Particles[i] = particle;
            return true;
        }
        if (!particle.Burst)
        {
            if (particle.Effect.Smoke)
                return false;
            // Сварка и прочая рабочая косметика важнее дыхания и дыма.
            for (var i = 0; i < Count; i++)
            {
                if (Particles[i].Burst || !Particles[i].Effect.Smoke)
                    continue;
                Particles[i] = particle;
                return true;
            }
            return false;
        }
        // Разовый удар вытесняет дым, затем непрерывную косметику, но не другие удары.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < Count; i++)
            {
                if (Particles[i].Burst || pass == 0 && !Particles[i].Effect.Smoke)
                    continue;
                Particles[i] = particle;
                return true;
            }
        }
        return false;
    }

    public int AmbientCount()
    {
        var count = 0;
        for (var i = 0; i < Count; i++)
            count += Particles[i].Effect.Ambient ? 1 : 0;
        return count;
    }

    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0)
            return;
        for (var i = Count - 1; i >= 0; i--)
        {
            ref var p = ref Particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                Particles[i] = Particles[--Count];
                continue;
            }
            p.Position += p.Velocity * dt;
            p.Velocity *= MathF.Exp(-p.Effect.Drag * dt);
        }
    }
}
