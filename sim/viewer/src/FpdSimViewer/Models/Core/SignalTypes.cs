using System.Collections.Generic;

namespace FpdSimViewer.Models.Core;

public enum SignalValueKind
{
    Scalar,
    Vector,
}

public readonly record struct SignalValue
{
    private readonly uint _scalar;
    private readonly ushort[]? _vector;

    public SignalValue(uint scalar)
    {
        Kind = SignalValueKind.Scalar;
        _scalar = scalar;
        _vector = null;
    }

    public SignalValue(ushort[] vector)
    {
        Kind = SignalValueKind.Vector;
        _scalar = 0U;
        _vector = vector;
    }

    public SignalValueKind Kind { get; }

    public bool IsScalar => Kind == SignalValueKind.Scalar;

    public uint Scalar => IsScalar ? _scalar : 0U;

    public ushort[] Vector => !IsScalar && _vector is not null ? _vector : [];

    public static implicit operator SignalValue(uint value) => new(value);

    public static implicit operator SignalValue(ushort[] value) => new(value);
}

public sealed class SignalMap : Dictionary<string, SignalValue>
{
    public SignalMap()
    {
    }

    public SignalMap(IDictionary<string, SignalValue> dictionary)
        : base(dictionary)
    {
    }
}

public readonly record struct Mismatch(ulong Cycle, string SignalName, uint Expected, uint Actual);

public static class SignalHelpers
{
    public static uint GetScalar(SignalMap signals, string name, uint fallback = 0U)
    {
        return signals.TryGetValue(name, out var value) && value.IsScalar ? value.Scalar : fallback;
    }

    public static ushort[] GetVector(SignalMap signals, string name)
    {
        return signals.TryGetValue(name, out var value) && !value.IsScalar ? value.Vector : [];
    }
}
