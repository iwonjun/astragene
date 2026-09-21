using System;
using System.Text;
using RtsGame.Sim.Core;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class CoreTests
{
    private static double Real(Fix64 value) => (double)value.Raw / Fix64.Scale;

    [Fact]
    public void ArithmeticGoldenAndRounding()
    {
        var a = Fix64.FromRatio(7, 2);
        var b = Fix64.FromRatio(-5, 4);
        Assert.Equal(9663676416L, (a + b).Raw);
        Assert.Equal(-18790481920L, (a * b).Raw);
        Assert.Equal(-12025908428L, (a / b).Raw);
        Assert.Equal(-2, b.FloorToInt());
        Assert.Equal(-1, b.TruncateToInt());
        Assert.Equal(0, (Fix64.FromRaw(-1) * Fix64.FromRaw(1)).Raw);
        Assert.Equal(Fix64.MinValue, Fix64.FromRatio(long.MinValue, Fix64.Scale));
        Assert.Equal(Fix64.One, Fix64.MaxValue / Fix64.MaxValue);
        Assert.Throws<OverflowException>(() => Fix64.MaxValue + Fix64.One);
        Assert.Throws<OverflowException>(() => -Fix64.MinValue);
        Assert.Throws<OverflowException>(() => Fix64.MaxValue * Fix64.FromInt(2));
        Assert.Throws<DivideByZeroException>(() => a / Fix64.Zero);
        Assert.Throws<DivideByZeroException>(() => Fix64.FromRatio(1, 0));
    }

    [Fact]
    public void SquareRootAccuracyAcrossRequiredRange()
    {
        for (int i = 0; i <= 1_000_000; i++)
        {
            var input = Fix64.FromRatio(i, 100);
            double error = Math.Abs(Real(FixMath.Sqrt(input)) - Math.Sqrt(i / 100.0));
            Assert.True(error < 0.001, $"sqrt({i}/100) error {error}");
        }
        Assert.Equal(6074000999L, FixMath.Sqrt(Fix64.FromInt(2)).Raw);
        Assert.Equal(65536L, FixMath.Sqrt(Fix64.FromRaw(1)).Raw);
        Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.Sqrt(Fix64.FromInt(-1)));
        Assert.InRange(Real(FixMath.Sqrt(Fix64.MaxValue)), 46340.95, 46340.96);
    }

    [Fact]
    public void TrigonometryGoldenQuadrantsAndAccuracy()
    {
        Assert.Equal(0L, FixMath.Sin(Fix64.Zero).Raw);
        Assert.Equal(Fix64.One, FixMath.Cos(Fix64.Zero));
        Assert.Equal(3373259426L, FixMath.Atan2(Fix64.One, Fix64.One).Raw);
        Assert.Equal(FixMath.Pi, FixMath.Atan2(Fix64.Zero, Fix64.FromInt(-1)));
        Assert.Equal(-FixMath.HalfPi, FixMath.Atan2(Fix64.FromInt(-1), Fix64.Zero));
        Assert.Equal(Fix64.Zero, FixMath.Atan2(Fix64.Zero, Fix64.Zero));
        for (int i = -10000; i <= 10000; i++)
        {
            var angle = Fix64.FromRatio(i, 100);
            Assert.InRange(Math.Abs(Real(FixMath.Sin(angle)) - Math.Sin(i / 100.0)), 0, 0.00001);
            Assert.InRange(Math.Abs(Real(FixMath.Cos(angle)) - Math.Cos(i / 100.0)), 0, 0.00001);
        }
        for (int x = -50; x <= 50; x++)
        for (int y = -50; y <= 50; y++)
            Assert.InRange(Math.Abs(Real(FixMath.Atan2(Fix64.FromInt(y), Fix64.FromInt(x))) - Math.Atan2(y, x)), 0, 0.000001);
        Assert.InRange(Real(FixMath.Atan2(Fix64.MinValue, Fix64.MinValue)), -2.356195, -2.356194);
        _ = FixMath.Cos(Fix64.MaxValue);
    }

    [Fact]
    public void VectorAndClockContracts()
    {
        var v = new Fix2(Fix64.FromInt(3), Fix64.FromInt(4));
        Assert.Equal(Fix64.FromInt(25), v.LengthSquared);
        Assert.Equal(Fix64.FromInt(5), v.Length);
        Assert.InRange(Real(v.Normalized.Length), 0.999999, 1.000001);
        Assert.Equal(Fix2.Zero, Fix2.Zero.Normalized);
        Assert.Equal(new Fix2(Fix64.One, Fix64.Zero), new Fix2(Fix64.FromRaw(1), Fix64.Zero).Normalized);
        var clock = new SimClock();
        for (int i = 0; i < 20; i++) clock.Advance();
        Assert.Equal(20, clock.Tick);
        Assert.Equal(10, clock.Turn);
        Assert.Equal(Fix64.One, clock.Time);
        Assert.Equal(Fix64.FromInt(4), FixMath.Clamp(Fix64.FromInt(5), Fix64.Zero, Fix64.FromInt(4)));
        Assert.Throws<ArgumentException>(() => FixMath.Clamp(Fix64.One, Fix64.One, Fix64.Zero));
    }

    [Fact]
    public void MillionRandomValuesAreIdenticalAndGoldenSeedIsStable()
    {
        ulong[] expected = { 10993463216891074725UL, 10493811622101777860UL, 15268851883089059143UL, 6580237555214349669UL, 13425723858421698840UL };
        var rng = new DetRandom(1);
        Assert.Equal(10451216379200822465UL, rng.State0);
        Assert.Equal(13757245211066428519UL, rng.State1);
        foreach (ulong value in expected) Assert.Equal(value, rng.NextUInt64());
        var a = new DetRandom(0);
        var b = new DetRandom(0);
        for (int i = 0; i < 1_000_000; i++) Assert.Equal(a.NextUInt64(), b.NextUInt64());
        var restored = new DetRandom(a.State0, a.State1);
        Assert.Equal(a.NextUInt64(), restored.NextUInt64());
        for (int i = 0; i < 10000; i++) Assert.InRange(a.NextInt(7), 0, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.NextInt(0));
        Assert.Throws<ArgumentException>(() => new DetRandom(0, 0));
        Assert.Throws<InvalidOperationException>(() => default(DetRandom).NextUInt64());
    }

    [Fact]
    public void FnvGoldenAndCanonicalByteOrder()
    {
        var hasher = new WorldHasher();
        Assert.Equal(0xcbf29ce484222325UL, hasher.Value);
        hasher.Add(Encoding.ASCII.GetBytes("hello"));
        Assert.Equal(0xa430d84680aabd0bUL, hasher.Value);
        var typed = new WorldHasher();
        typed.AddInt32(-1);
        typed.AddUInt64(0x0807060504030201UL);
        typed.AddFix(Fix64.One);
        var bytes = new WorldHasher();
        bytes.Add(new byte[] { 255,255,255,255, 1,2,3,4,5,6,7,8, 0,0,0,0,1,0,0,0 });
        Assert.Equal(bytes.Value, typed.Value);
    }
}
