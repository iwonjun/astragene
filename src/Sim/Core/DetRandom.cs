using System;

namespace RtsGame.Sim.Core;

/// <summary>xorshift128+ with SplitMix64 seed expansion; state is part of world hashes.</summary>
public struct DetRandom
{
    public ulong State0 { get; private set; }
    public ulong State1 { get; private set; }
    public DetRandom(ulong seed)
    {
        State0 = SplitMix(ref seed);
        State1 = SplitMix(ref seed);
    }
    public DetRandom(ulong state0, ulong state1)
    {
        if ((state0 | state1) == 0) throw new ArgumentException("All-zero RNG state.");
        State0 = state0; State1 = state1;
    }
    private static ulong SplitMix(ref ulong seed)
    {
        unchecked
        {
            seed += 0x9E3779B97F4A7C15UL;
            ulong z = seed;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    public ulong NextUInt64()
    {
        if ((State0 | State1) == 0) throw new InvalidOperationException("RNG must be seeded.");
        ulong a = State0;
        ulong b = State1;
        State0 = b;
        a ^= a << 23;
        State1 = a ^ b ^ (a >> 17) ^ (b >> 26);
        return unchecked(State1 + b);
    }
    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        ulong bound = (ulong)exclusiveMax;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { value = NextUInt64(); } while (value < threshold);
        return (int)(value % bound);
    }
}
