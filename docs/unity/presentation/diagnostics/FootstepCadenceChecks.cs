using System;
using LetMeSleep.Presentation.Gameplay;

internal static class FootstepCadenceChecks
{
    private static int checks;
    private static void Check(bool result, string label)
    {
        if (!result) throw new Exception(label);
        checks++;
    }

    private static int Count(double speed, int hz)
    {
        var cadence = new FootstepCadence();
        int count = 0;
        for (int i = 1; i <= 10 * hz; i++)
            if (cadence.Advance(speed / hz, speed, 1.0 / hz, (double)i / hz, true)) count++;
        return count;
    }

    public static void Main()
    {
        foreach (int hz in new[] { 15, 30, 60 })
        {
            int walk = Count(3.1, hz), run = Count(5, hz);
            Console.WriteLine($"{hz} Hz: 10 s walk={walk}, run={run}");
            Check(walk >= 24 && walk <= 26, "walk cadence");
            Check(run >= 30 && run <= 35, "run cadence");
        }
        var c = new FootstepCadence();
        Check(!c.Advance(20, 5, 1.0 / 30, 1, true), "teleport silent");
        Check(!c.Advance(0.1, 3.1, 1.0, 2, true), "snapshot gap silent");
        Check(!c.Advance(0.1, 3.1, 0, 2, true), "duplicate silent");
        Check(!c.Advance(0.1, 3.1, -0.1, 2, true), "out of order silent");
        Check(!c.Advance(0.1, 3.1, 1.0 / 30, 3, false), "recovery silent");
        Check(!c.Advance(double.NaN, 3.1, 1.0 / 30, 3, true), "invalid silent");
        for (int i = 0; i < 90; i++)
            Check(!c.Advance(0, 3.1, 1.0 / 30, 4 + i / 30.0, true), "vertical-only silent");
        int burst = 0;
        for (int i = 0; i < 30; i++)
            if (c.Advance(5.0 / 30, 5, 1.0 / 30, 8, true)) burst++;
        Check(burst <= 1, "same-frame backlog cannot burst");
        c = new FootstepCadence();
        double last = -100;
        for (int i = 1; i <= 300; i++)
        {
            double speed = i < 100 ? 3.1 : i < 200 ? 5 : 1.55;
            double now = i / 30.0;
            if (!c.Advance(speed / 30, speed, 1.0 / 30, now, true)) continue;
            Check(now - last >= 0.28, "walk/run/crouch transition spacing");
            last = now;
        }
        Console.WriteLine($"PASS {checks} checks; pure policy, no audio or Unity playback.");
    }
}
