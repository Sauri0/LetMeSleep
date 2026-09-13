using System;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>One render-distance timeline, independent of Authority and Unity animation.</summary>
    public sealed class HumanLocomotionClock
    {
        public readonly struct Profile
        {
            public readonly double Speed, DistancePerCycle, ClipDuration;
            public Profile(double speed, double distancePerCycle, double clipDuration)
            {
                if (!Positive(speed) || !Positive(distancePerCycle) || !Positive(clipDuration))
                    throw new ArgumentOutOfRangeException(nameof(speed));
                Speed = speed; DistancePerCycle = distancePerCycle; ClipDuration = clipDuration;
            }
        }

        public readonly struct Contact
        {
            public readonly uint Generation;
            public readonly long Index;
            public readonly bool Left;
            public readonly double FrameFraction;
            public Contact(uint generation, long index, bool left, double fraction)
            { Generation = generation; Index = index; Left = left; FrameFraction = fraction; }
        }

        public readonly struct Sample
        {
            public readonly double Phase, CyclesPerSecond, Blend, LowerTime, UpperTime;
            public readonly int LowerProfile, UpperProfile;
            public Sample(double phase, double rate, double blend, int lower, int upper, Profile[] profiles)
            {
                Phase = phase; CyclesPerSecond = rate; Blend = blend;
                LowerProfile = lower; UpperProfile = upper;
                LowerTime = (phase % 1) * profiles[lower].ClipDuration;
                UpperTime = (phase % 1) * profiles[upper].ClipDuration;
            }
        }

        private readonly Profile[] profiles;
        private double x, z, phase;
        private int lastFrame = -1;
        private bool baseline, pending;
        private Contact contact;
        public uint Generation { get; private set; }
        public Sample Current { get; private set; }

        public HumanLocomotionClock(params Profile[] profiles)
        {
            if (profiles == null || profiles.Length == 0) throw new ArgumentException("Gait profiles are required.");
            for (int i = 0; i < profiles.Length; i++)
                if (!Positive(profiles[i].Speed) || !Positive(profiles[i].DistancePerCycle) ||
                    !Positive(profiles[i].ClipDuration) || (i > 0 && profiles[i].Speed <= profiles[i - 1].Speed))
                    throw new ArgumentException("Ordered, valid gait profiles are required.");
            this.profiles = (Profile[])profiles.Clone();
            Suspend();
        }

        // Resuming always establishes a new baseline; it cannot consume paused displacement.
        public void Suspend()
        {
            unchecked { Generation++; }
            baseline = pending = false; phase = 0; lastFrame = -1;
            Current = new Sample(0, 0, 0, 0, 0, profiles);
        }

        public Sample Advance(double positionX, double positionZ, double deltaSeconds,
            int frameId, bool eligible, bool discontinuity = false)
        {
            // A same-frame explicit reset must still invalidate contacts.
            if (discontinuity || !eligible || !Finite(positionX) || !Finite(positionZ) ||
                !Positive(deltaSeconds) || deltaSeconds > 0.25)
            {
                Suspend();
                return Current;
            }
            if (frameId == lastFrame) return Current;
            pending = false;
            if (!baseline || frameId < lastFrame)
            {
                if (baseline) Suspend();
                x = positionX; z = positionZ; baseline = true; lastFrame = frameId;
                return Current;
            }

            double dx = positionX - x, dz = positionZ - z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            x = positionX; z = positionZ; lastFrame = frameId;
            double speed = distance / deltaSeconds;
            if (!Finite(speed) || speed > profiles[profiles.Length - 1].Speed * 1.5)
            {
                Suspend();
                return Current;
            }
            if (speed < 0.05)
            {
                Current = new Sample(phase, 0, Current.Blend, Current.LowerProfile, Current.UpperProfile, profiles);
                return Current;
            }

            int lower = 0;
            while (lower + 1 < profiles.Length && speed >= profiles[lower + 1].Speed) lower++;
            int upper = Math.Min(lower + 1, profiles.Length - 1);
            double blend = lower == upper ? 0 : Math.Max(0, Math.Min(1,
                (speed - profiles[lower].Speed) / (profiles[upper].Speed - profiles[lower].Speed)));
            double stride = profiles[lower].DistancePerCycle +
                (profiles[upper].DistancePerCycle - profiles[lower].DistancePerCycle) * blend;
            double before = phase;
            phase += distance / stride;
            Current = new Sample(phase, speed / stride, blend, lower, upper, profiles);
            long oldIndex = (long)Math.Floor(before * 2 + 1e-9);
            long newIndex = (long)Math.Floor(phase * 2 + 1e-9);
            // A frame spanning multiple contacts is a gap, not a backlog to replay.
            if (newIndex == oldIndex + 1)
            {
                double fraction = Math.Max(0, Math.Min(1, (newIndex * 0.5 - before) / (phase - before)));
                contact = new Contact(Generation, newIndex, (newIndex & 1) == 0, fraction);
                pending = true;
            }
            return Current;
        }

        public bool TryConsumeContact(out Contact value)
        {
            value = contact;
            bool result = pending;
            pending = false;
            return result;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Positive(double value) => Finite(value) && value > 0;
    }
}
