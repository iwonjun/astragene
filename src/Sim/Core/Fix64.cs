using System;

namespace RtsGame.Sim.Core;

/// <summary>Signed Q32.32. Arithmetic truncates toward zero and throws on overflow.</summary>
public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
{
    public const long Scale = 1L << 32;
    public long Raw { get; }
    private Fix64(long raw) { Raw = raw; }
    public static Fix64 FromRaw(long raw) => new(raw);
    public static Fix64 FromInt(int value) => new((long)value * Scale);
    public static Fix64 FromRatio(long numerator, long denominator)
    {
        if (denominator == 0) throw new DivideByZeroException();
        return new(checked((long)((Int128)numerator * Scale / denominator)));
    }
    public static Fix64 Zero => new(0);
    public static Fix64 One => new(Scale);
    public static Fix64 MinValue => new(long.MinValue);
    public static Fix64 MaxValue => new(long.MaxValue);
    public int FloorToInt() => checked((int)(Raw >> 32));
    public int TruncateToInt() => checked((int)(Raw / Scale));
    public static Fix64 operator +(Fix64 a, Fix64 b) => new(checked(a.Raw + b.Raw));
    public static Fix64 operator -(Fix64 a, Fix64 b) => new(checked(a.Raw - b.Raw));
    public static Fix64 operator -(Fix64 value) => new(checked(-value.Raw));
    public static Fix64 operator *(Fix64 a, Fix64 b) => new(checked((long)((Int128)a.Raw * b.Raw / Scale)));
    public static Fix64 operator /(Fix64 a, Fix64 b)
    {
        if (b.Raw == 0) throw new DivideByZeroException();
        return new(checked((long)((Int128)a.Raw * Scale / b.Raw)));
    }
    public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
    public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;
    public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;
    public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;
    public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;
    public bool Equals(Fix64 other) => Raw == other.Raw;
    public override bool Equals(object? other) => other is Fix64 value && Equals(value);
    public override int GetHashCode() => unchecked((int)Raw ^ (int)(Raw >> 32));
    public int CompareTo(Fix64 other) => Raw.CompareTo(other.Raw);
    public override string ToString() => Raw.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/4294967296";
}
