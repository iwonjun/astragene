using System;

namespace RtsGame.Sim.Core;

/// <summary>FNV-1a; integers use little-endian bytes. Never hash runtime object hashes.</summary>
public struct WorldHasher
{
    public const ulong Offset = 14695981039346656037UL;
    public const ulong Prime = 1099511628211UL;
    private ulong _value;
    private bool _initialized;
    public ulong Value => _initialized ? _value : Offset;
    public void AddByte(byte value)
    {
        _value = unchecked((Value ^ value) * Prime);
        _initialized = true;
    }
    public void Add(ReadOnlySpan<byte> bytes) { foreach (byte value in bytes) AddByte(value); }
    public void AddUInt64(ulong value) { for (int i = 0; i < 8; i++) { AddByte((byte)value); value >>= 8; } }
    public void AddInt64(long value) => AddUInt64(unchecked((ulong)value));
    public void AddInt32(int value) { uint bits = unchecked((uint)value); for (int i = 0; i < 4; i++) { AddByte((byte)bits); bits >>= 8; } }
    public void AddFix(Fix64 value) => AddInt64(value.Raw);
    public void AddFix2(Fix2 value) { AddFix(value.X); AddFix(value.Y); }
}
