namespace FpdSimViewer.Engine;

public sealed class TraceCapture
{
    private readonly SimulationSnapshot?[] _buffer;
    private int _start;
    private int _count;

    public TraceCapture(int capacity = 4096)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        }

        _buffer = new SimulationSnapshot[capacity];
    }

    public int Count => _count;

    public void Record(SimulationSnapshot snapshot)
    {
        if (_count < _buffer.Length)
        {
            _buffer[(_start + _count) % _buffer.Length] = snapshot;
            _count++;
            return;
        }

        _buffer[_start] = snapshot;
        _start = (_start + 1) % _buffer.Length;
    }

    public SimulationSnapshot? GetAt(int index)
    {
        if (index < 0 || index >= _count)
        {
            return null;
        }

        return _buffer[(_start + index) % _buffer.Length];
    }

    public IReadOnlyList<SimulationSnapshot> GetRange(int start, int count)
    {
        if (start < 0 || count < 0 || start > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Range start/count is out of bounds.");
        }

        var available = Math.Min(count, _count - start);
        var range = new List<SimulationSnapshot>(available);
        for (var index = 0; index < available; index++)
        {
            var snapshot = GetAt(start + index);
            if (snapshot is not null)
            {
                range.Add(snapshot);
            }
        }

        return range;
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _start = 0;
        _count = 0;
    }
}
