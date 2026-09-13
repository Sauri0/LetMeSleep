using System;
using LetMeSleep.Gameplay;
using NUnit.Framework;

public sealed class SurfaceVisualFrameChecks
{
    private static void Near(Float3 actual, Float3 expected) => Assert.That((actual - expected).Length, Is.LessThan(.0001f));

    [TestCase(.6f)] [TestCase(-.6f)]
    public void WallHeadingIncludesPitchInsteadOfOnlyBodyYaw(float pitch)
    {
        var wall = new Float3(-1, 0, 0); var aim = MathEx.Aim((float)Math.PI / 2, pitch);
        SurfaceVisualFrame.TryResolve(wall, aim, Float3.Zero, 10, out _, out var forward);
        Near(forward, pitch > 0 ? Float3.Up : -Float3.Up);
    }
    [Test]
    public void FloorWallCeilingTransportAndReverseKeepTheSameLocalHeading()
    {
        var up = Float3.Up; var forward = Float3.Forward;
        foreach (var normal in new[] { -Float3.Forward, -Float3.Up, -Float3.Forward, Float3.Up })
        {
            var carried = SurfaceVisualFrame.TransportForward(up, normal, forward);
            SurfaceVisualFrame.TryResolve(normal, normal, carried, 0, out up, out forward);
            Assert.That(Float3.Dot(up, forward), Is.EqualTo(0).Within(.0001f));
            Assert.That(forward.Length, Is.EqualTo(1).Within(.0001f));
            if (normal.Y == -1) Near(forward, -Float3.Forward);
        }
        Near(forward, Float3.Forward);
    }
    [Test]
    public void RotatingObjectNormalTransportIsReversibleAndFinite()
    {
        var tilted = new Float3(1, 2, 3).Normalized;
        var transported = SurfaceVisualFrame.TransportForward(Float3.Up, tilted, Float3.Forward);
        Near(SurfaceVisualFrame.TransportForward(tilted, Float3.Up, transported), Float3.Forward);
        Near(SurfaceVisualFrame.TransportForward(Float3.Up, -Float3.Up, Float3.Forward), Float3.Forward);
    }
    [Test]
    public void ConvexAndConcaveQuarterTurnsDoNotDiscardHeading()
    {
        foreach (float sign in new[] { -1f, 1f })
        {
            var normal = Float3.Forward * sign;
            var transported = SurfaceVisualFrame.TransportForward(Float3.Up, normal, Float3.Forward);
            Near(transported, Float3.Up * -sign);
        }
    }
    [Test]
    public void InvalidTransportHistoryCannotIntroduceNonfiniteBasis()
    {
        foreach (var bad in new[] { Float3.Zero, new Float3(float.NaN, 0, 0), new Float3(float.MaxValue, 0, 0) })
            Near(SurfaceVisualFrame.TransportForward(bad, Float3.Up, Float3.Forward), Float3.Zero);
    }

    [TestCase(0, 1, 0)] [TestCase(0, -1, 0)]
    [TestCase(1, 0, 0)] [TestCase(-1, 0, 0)]
    [TestCase(0, 0, 1)] [TestCase(0, 0, -1)]
    [TestCase(1, 2, 3)]
    public void SupportBasisPreservesProjectedYawAndFootDirection(float x, float y, float z)
    {
        var normal = new Float3(x, y, z);
        var yaw = MathEx.Aim(.71f, 0);
        Assert.That(SurfaceVisualFrame.TryResolve(normal, yaw, Float3.Zero, 1, out var up, out var forward), Is.True);
        Near(up, normal.Normalized);
        Near(forward, Float3.ProjectPlane(yaw, up).Normalized);
        Assert.That(Float3.Dot(up, forward), Is.EqualTo(0).Within(.0001f));
        Assert.That(Float3.Cross(up, forward).Length, Is.EqualTo(1).Within(.0001f));
        // Root offset plus model ground anchor lands on the support plane.
        Near(up * .057f + up * -.057f, Float3.Zero);
    }

    [Test]
    public void NearNormalNoiseRetainsHeadingOnBothSides()
    {
        var previous = Float3.Up;
        foreach (float jitter in new[] { -.02f, 0f, .02f, -.00001f })
        {
            Assert.That(SurfaceVisualFrame.TryResolve(new Float3(1, 0, 0), new Float3(1, 0, jitter).Normalized,
                previous, 10, out _, out var forward), Is.True);
            Near(forward, previous);
        }
    }

    [TestCase(1, 0, 0)] [TestCase(0, 1, 0)] [TestCase(0, -1, 0)] [TestCase(0, 0, 1)]
    public void NormalViewWithoutHistoryGetsFiniteTangent(float x, float y, float z)
    {
        var normal = new Float3(x, y, z);
        Assert.That(SurfaceVisualFrame.TryResolve(normal, normal, normal, 1, out var up, out var forward), Is.True);
        Assert.That(forward.IsFinite, Is.True);
        Assert.That(forward.Length, Is.EqualTo(1).Within(.0001f));
        Assert.That(Float3.Dot(up, forward), Is.EqualTo(0).Within(.0001f));
    }

    [Test]
    public void OppositeYawTurnsGraduallyInsteadOfFlipping()
    {
        SurfaceVisualFrame.TryResolve(Float3.Up, -Float3.Forward, Float3.Forward, .2f, out _, out var next);
        Assert.That(Math.Acos(Float3.Dot(next, Float3.Forward)), Is.EqualTo(.2).Within(.0001));
        Assert.That(next.X, Is.GreaterThan(0));
    }

    [Test]
    public void HeadingConvergesEquallyAtThirtyAndOneHundredTwentyFrames()
    {
        Float3 Simulate(int fps, int frames)
        {
            var result = Float3.Forward;
            for (int i = 0; i < frames; i++)
                SurfaceVisualFrame.TryResolve(Float3.Up, -Float3.Forward, result, 4f / fps, out _, out result);
            return result;
        }
        Near(Simulate(30, 6), Simulate(120, 24));
        Assert.That(Math.Acos(Float3.Dot(Simulate(30, 6), Float3.Forward)), Is.EqualTo(.8).Within(.0001));
        Near(Simulate(30, 30), -Float3.Forward);
        Near(Simulate(120, 120), -Float3.Forward);
    }

    [Test]
    public void DuplicateFrameDoesNotAdvanceHeading()
    {
        SurfaceVisualFrame.TryResolve(Float3.Up, -Float3.Forward, Float3.Forward, 0, out _, out var forward);
        Near(forward, Float3.Forward);
    }

    [Test]
    public void InvalidNormalsFailAndInvalidViewFallsBack()
    {
        foreach (var bad in new[] { Float3.Zero, new Float3(float.NaN, 1, 0), new Float3(0, float.PositiveInfinity, 0), new Float3(float.MaxValue, 0, 0) })
            Assert.That(SurfaceVisualFrame.TryResolve(bad, Float3.Forward, Float3.Zero, 1, out _, out _), Is.False);
        Assert.That(SurfaceVisualFrame.TryResolve(Float3.Up, new Float3(float.NaN, 0, 0), Float3.Forward,
            float.NaN, out _, out var forward), Is.True);
        Near(forward, Float3.Forward);
    }
}
