using System;

namespace RtsGame.Sim.Core;

public static class FixMath
{
    public static Fix64 Pi => Fix64.FromRaw(13493037705);
    public static Fix64 Tau => Fix64.FromRaw(26986075410);
    public static Fix64 HalfPi => Fix64.FromRaw(6746518852);
    public static Fix64 Abs(Fix64 x) => x.Raw < 0 ? -x : x;
    public static Fix64 Min(Fix64 a, Fix64 b) => a < b ? a : b;
    public static Fix64 Max(Fix64 a, Fix64 b) => a > b ? a : b;
    public static Fix64 Clamp(Fix64 x, Fix64 min, Fix64 max)
    {
        if (min > max) throw new ArgumentException("Invalid clamp interval.");
        return Min(Max(x, min), max);
    }
    public static Fix64 Sqrt(Fix64 value)
    {
        if (value.Raw < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return Fix64.FromRaw(checked((long)IntegerSqrt((UInt128)value.Raw << 32)));
    }
    internal static ulong IntegerSqrt(UInt128 n)
    {
        UInt128 result = 0;
        UInt128 bit = (UInt128)1 << 126;
        while (bit > n) bit >>= 2;
        while (bit != 0)
        {
            if (n >= result + bit) { n -= result + bit; result = (result >> 1) + bit; }
            else result >>= 1;
            bit >>= 2;
        }
        return checked((ulong)result);
    }
    public static Fix64 Sin(Fix64 radians) => Lookup(radians.Raw, 0);
    public static Fix64 Cos(Fix64 radians) => Lookup(radians.Raw, 256);
    private static Fix64 Lookup(long raw, int offset)
    {
        long phase = raw % Tau.Raw;
        if (phase < 0) phase += Tau.Raw;
        Int128 scaled = (Int128)phase * 1024;
        int index = ((int)(scaled / Tau.Raw) + offset) & 1023;
        long remainder = (long)(scaled % Tau.Raw);
        long a = TrigTables.Sin[index];
        long b = TrigTables.Sin[(index + 1) & 1023];
        return Fix64.FromRaw(a + (long)((Int128)(b - a) * remainder / Tau.Raw));
    }
    public static Fix64 Atan2(Fix64 y, Fix64 x)
    {
        if (x.Raw == 0 && y.Raw == 0) return Fix64.Zero;
        Int128 ax = x.Raw < 0 ? -(Int128)x.Raw : x.Raw;
        Int128 ay = y.Raw < 0 ? -(Int128)y.Raw : y.Raw;
        Int128 small = ax < ay ? ax : ay;
        Int128 large = ax > ay ? ax : ay;
        Int128 scaled = small * 1023;
        int index = (int)(scaled / large);
        long angle = TrigTables.Atan[index];
        if (index < 1023)
            angle += (long)((TrigTables.Atan[index + 1] - angle) * (scaled % large) / large);
        if (ay > ax) angle = HalfPi.Raw - angle;
        if (x.Raw < 0) angle = Pi.Raw - angle;
        if (y.Raw < 0) angle = -angle;
        return Fix64.FromRaw(angle);
    }
}
