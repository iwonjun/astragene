using System;

namespace RtsGame.Sim.Core;

public readonly struct Fix2 : IEquatable<Fix2>
{
    public Fix64 X { get; }
    public Fix64 Y { get; }
    public Fix2(Fix64 x, Fix64 y) { X = x; Y = y; }
    public static Fix2 Zero => new(Fix64.Zero, Fix64.Zero);
    public Fix64 LengthSquared => X * X + Y * Y;
    public Fix64 Length
    {
        get
        {
            Int128 x = X.Raw;
            Int128 y = Y.Raw;
            UInt128 squared = (UInt128)(x * x) + (UInt128)(y * y);
            return Fix64.FromRaw(checked((long)FixMath.IntegerSqrt(squared)));
        }
    }
    public Fix2 Normalized => this == Zero ? Zero : this / Length;
    public static Fix64 Dot(Fix2 a, Fix2 b) => a.X * b.X + a.Y * b.Y;
    public static Fix2 operator +(Fix2 a, Fix2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Fix2 operator -(Fix2 a, Fix2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Fix2 operator *(Fix2 a, Fix64 b) => new(a.X * b, a.Y * b);
    public static Fix2 operator /(Fix2 a, Fix64 b) => new(a.X / b, a.Y / b);
    public static bool operator ==(Fix2 a, Fix2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Fix2 a, Fix2 b) => !(a == b);
    public bool Equals(Fix2 other) => this == other;
    public override bool Equals(object? other) => other is Fix2 value && Equals(value);
    public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
}
