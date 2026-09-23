using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.VoiceEditMode
{
    /// <summary>Distance curve, graded occlusion and routing hysteresis of the proximity policy.</summary>
    public sealed class VoiceProximityPolicyTests
    {
        [TestCase(PlayerRole.Human, PlayerRole.Human, 12f)]
        [TestCase(PlayerRole.Human, PlayerRole.Mosquito, 12f)]
        [TestCase(PlayerRole.Mosquito, PlayerRole.Human, 8f)]
        [TestCase(PlayerRole.Mosquito, PlayerRole.Mosquito, 16f)]
        public void DistanceCurve_IsMonotonicSmoothAndSilentAtTheCut(PlayerRole speaker, PlayerRole listener, float expectedCut)
        {
            float cut = VoiceSpatialPolicy.CutoffDistance(speaker, listener);
            Assert.That(cut, Is.EqualTo(expectedCut));
            float previous = float.MaxValue, maxStep = 0f;
            for (float d = .1f; d < cut; d += .01f)
            {
                float g = VoiceSpatialPolicy.DistanceGain(d, cut);
                Assert.That(g, Is.LessThanOrEqualTo(previous + 1e-6f), $"Gain rises at {d:0.00} m");
                if (previous != float.MaxValue) maxStep = Math.Max(maxStep, previous - g);
                previous = g;
            }
            Assert.That(maxStep, Is.LessThan(.05f), "No jump: 1 cm never changes the gain by more than 0.05 (≈ 0.2 dB).");
            Assert.That(VoiceSpatialPolicy.DistanceGain(cut - .05f, cut), Is.LessThan(.003f), "Melts to silence with zero slope at the cut.");
            Assert.That(VoiceSpatialPolicy.DistanceGain(cut, cut), Is.EqualTo(0f), "Exactly zero voice at the cut.");
            Assert.That(VoiceSpatialPolicy.DistanceGain(cut + 3f, cut), Is.EqualTo(0f), "Exactly zero voice beyond the cut.");
        }

        [Test]
        public void DistanceCurve_IsComfortableNearAndLegibleFar()
        {
            float Db(float d) => 20f * (float)Math.Log10(Math.Max(1e-9, VoiceSpatialPolicy.DistanceGain(d, 12f)));
            Assert.That(Db(2f), Is.EqualTo(0f).Within(.01f), "0 dB at the 2 m reference.");
            Assert.That(Db(.3f), Is.EqualTo(VoiceSpatialPolicy.NearBoostDb).Within(.01f), "At the ear: +8 dB, capped.");
            Assert.That(Db(.3f) - Db(1f), Is.GreaterThanOrEqualTo(3f), "The ear is clearly closer than 1 m.");
            Assert.That(Db(3f), Is.InRange(-4f, -1.5f), "Conversation distance stays comfortable.");
            Assert.That(Db(1f) - Db(10f), Is.GreaterThanOrEqualTo(14f), "1 → 10 m reads as distance (≥ 14 dB).");
            Assert.That(Db(10f), Is.GreaterThan(-30f), "Still audible at 10 m.");
        }

        [Test]
        public void Occlusion_IsProportionalToTheBlockedShareAndWalls()
        {
            float Db(float g) => 20f * (float)Math.Log10(g);
            Assert.That(VoiceSpatialPolicy.OcclusionGain(0f, 0), Is.EqualTo(1f));
            Assert.That(Db(VoiceSpatialPolicy.OcclusionGain(.4f, 1)), Is.InRange(-3.5f, -2f), "Open door: partial.");
            Assert.That(Db(VoiceSpatialPolicy.OcclusionGain(1f, 1)), Is.LessThanOrEqualTo(-6f), "Wall: ≥ 6 dB.");
            Assert.That(Db(VoiceSpatialPolicy.OcclusionGain(1f, 2)), Is.LessThan(Db(VoiceSpatialPolicy.OcclusionGain(1f, 1)) - 3f), "Two walls: clearly more.");
            float open = VoiceSpatialPolicy.OcclusionLowPassHertz(0f, 1), door = VoiceSpatialPolicy.OcclusionLowPassHertz(.4f, 1), wall = VoiceSpatialPolicy.OcclusionLowPassHertz(1f, 1);
            Assert.That(open, Is.GreaterThan(door)); Assert.That(door, Is.GreaterThan(wall));
            Assert.That(wall, Is.LessThanOrEqualTo(1500f));
            var blocked = VoiceSpatialPolicy.Evaluate(PlayerRole.Human, false, Float3.Zero, PlayerRole.Human, false, new Float3(3f, 0, 0), true);
            Assert.That(blocked.Gain, Is.EqualTo(VoiceSpatialPolicy.DistanceGain(3f, 12f) * VoiceSpatialPolicy.OccludedGain).Within(1e-5f));
            Assert.That(blocked.LowPassHertz, Is.EqualTo(VoiceSpatialPolicy.OccludedLowPassHertz).Within(1f));
        }

        [Test]
        public void Routing_HasHysteresisButTheGainStaysZeroPastTheCut()
        {
            Assert.That(VoiceSpatialPolicy.RouteAudible(11.9f, 12f, false), Is.True);
            Assert.That(VoiceSpatialPolicy.RouteAudible(12.2f, 12f, false), Is.False, "Opens only inside the cut.");
            Assert.That(VoiceSpatialPolicy.RouteAudible(12.8f, 12f, true), Is.True, "Stays routed 1 m past the cut (no flapping).");
            Assert.That(VoiceSpatialPolicy.RouteAudible(13.1f, 12f, true), Is.False);
            Assert.That(VoiceSpatialPolicy.DistanceGain(12.5f, 12f), Is.Zero, "Routed but inaudible.");
            Assert.That(VoiceSpatialPolicy.AirLowPassHertz(2f, 12f), Is.EqualTo(5500f));
            Assert.That(VoiceSpatialPolicy.AirLowPassHertz(11f, 12f), Is.LessThan(4200f), "Far voices darken a little.");
        }
    }
}
