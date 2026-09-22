namespace Content.Client._Orbitra.Particles;

/// <summary>Fixed five-shot window; consumes a single puff after a pause and never catches up a stall.</summary>
internal sealed class OrbitraMuzzleSmokeWindow
{
    private readonly double[] _times = new double[5];
    private int _next;
    private int _count;
    private double _last;
    private bool _pending;

    public void Record(double now)
    {
        _times[_next] = now;
        _next = (_next + 1) % _times.Length;
        _count = Math.Min(_times.Length, _count + 1);
        _last = now;
        _pending |= _count == _times.Length && now - _times[_next] <= 1.5;
    }

    public bool Consume(double now)
    {
        if (!_pending || now - _last < 0.2)
            return false;
        _pending = false;
        _count = 0;
        return now - _last <= 0.8;
    }

    public void Clear()
    {
        _count = 0;
        _pending = false;
    }
}
