using System;
using LetMeSleep.Presentation.Gameplay;

internal static class HumanLocomotionClockChecks
{
    private static int checks;
    private static HumanLocomotionClock NewClock() => new HumanLocomotionClock(
        new HumanLocomotionClock.Profile(1, 5.0 / 6, 1.4),
        new HumanLocomotionClock.Profile(1.55, 0.96875, 1.333333),
        new HumanLocomotionClock.Profile(3.1, 1.55, 1.1),
        new HumanLocomotionClock.Profile(5, 50.0 / 23, 0.8));
    private static void Check(bool pass, string label)
    { checks++; if (!pass) throw new Exception(label); }

    public static void Main()
    {
        foreach (int hz in new[] { 15, 30, 60, 144 })
        foreach (double speed in new[] { 1.0, 1.55, 3.1, 5.0 })
        {
            var c = NewClock();
            c.Advance(0, 0, 1.0 / hz, 0, true);
            int contacts = 0;
            bool lastLeft = true;
            for (int i = 1; i <= hz * 10; i++)
            {
                var sample = c.Advance(speed * i / hz, 0, 1.0 / hz, i, true);
                double before = sample.Phase;
                Check(c.Advance(999, 999, 1.0 / hz, i, true).Phase == before, "duplicate frame idempotent");
                if (c.TryConsumeContact(out var contact))
                {
                    contacts++;
                    Check(contact.Left != lastLeft, "alternating feet");
                    Check(contact.Index == contacts, "unique monotonic contact");
                    Check(contact.FrameFraction >= 0 && contact.FrameFraction <= 1, "crossing time bounded");
                    lastLeft = contact.Left;
                }
                Check(!c.TryConsumeContact(out _), "destructive single stream");
            }
            int expected = speed == 1 ? 24 : speed == 1.55 ? 32 : speed == 3.1 ? 40 : 46;
            Check(contacts == expected, $"{speed} at {hz}Hz expected{expected} got{contacts}");
            Console.WriteLine($"{hz}Hz, {speed}m/s, 10s = {contacts} contacts");
        }
        var clock = NewClock();
        clock.Advance(0, 0, 0.1, 0, true);
        var trot = clock.Advance(0.31, 0, 0.1, 1, true);
        double trotTime = trot.LowerProfile == 2 ? trot.LowerTime : trot.UpperTime;
        Check(Math.Abs(trotTime - 0.22) < 1e-6, "real imported duration1.1 used, not nominal.5");
        double phase = trot.Phase;
        var run = clock.Advance(0.81, 0, 0.1, 2, true);
        Check(Math.Abs(run.Phase - phase - 0.23) < 1e-6, "continuous phase at speed change");
        uint generation = clock.Generation;
        clock.Advance(100, 0, 0.1, 3, true);
        Check(clock.Generation != generation && !clock.TryConsumeContact(out _), "teleport invalidates");
        clock.Advance(100, 0, 0.1, 4, true);
        Check(clock.Current.Phase == 0, "silent baseline after teleport");
        clock.Advance(100.31, 0, 0.1, 5, true);
        clock.Suspend();
        clock.Advance(999, 0, 0.1, 6, true);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "pause consumes no displaced distance");
        clock.Advance(999.31, 0, 0.1, 7, false);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "recovery silent");
        clock.Advance(999.31, 0, 0.1, 8, true);
        clock.Advance(999.81, 0, 0.3, 9, true);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "long gap no backlog");
        clock.Advance(double.NaN, 0, 0.1, 10, true);
        Check(clock.Current.Phase == 0, "invalid pose silent");
        clock.Advance(0, 0, 0.1, 11, true);
        clock.Advance(0.775, 0, 0.25, 12, true);
        Check(clock.TryConsumeContact(out _), "contact queued");
        clock.Advance(0.775, 0, 0.1, 12, true, true);
        Check(!clock.TryConsumeContact(out _), "explicit same-frame cut discards contact");
        clock.Advance(0, 0, 0.1, 13, true);
        clock.Advance(1, 0, 0.2, 14, true);
        clock.Advance(2.25, 0, 0.25, 15, true);
        Check(!clock.TryConsumeContact(out _), "multiple crossings in gap discarded");
        clock.Advance(2.25, 0, 0, 16, true);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "zero simulation delta suspends");
        clock.Advance(2.25, 0, 0.1, 17, true);
        for (int frame = 18; frame < 30; frame++) clock.Advance(2.25, 0, 0.1, frame, true);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "stopped extrapolation silent");
        clock = NewClock();
        clock.Advance(50, 20, 0.1, 30, true);
        Check(clock.Current.Phase == 0 && !clock.TryConsumeContact(out _), "new visual instance independent baseline");
        bool rejected = false;
        try { new HumanLocomotionClock(default(HumanLocomotionClock.Profile)); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "invalid profile rejected");
        Console.WriteLine($"PASS {checks} checks. Pure clock only; no mesh, sound, Unity or G4 PASS.");
    }
}
