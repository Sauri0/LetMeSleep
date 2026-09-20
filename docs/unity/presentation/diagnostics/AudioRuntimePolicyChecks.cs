using System;
using System.Collections.Generic;
using LetMeSleep.Audio;

internal static class AudioRuntimePolicyChecks
{
    private static int checks;

    private static void Check(bool result, string label)
    {
        if (!result) throw new Exception(label);
        checks++;
    }

    public static void Main()
    {
        var events = new GameplayAudioEventGate();
        Check(!events.TryAccept(4, 9, 1), "event before snapshot is silent");
        events.BeginRound(4, 9);
        Check(events.TryAccept(4, 9, 1), "current event plays");
        Check(!events.TryAccept(4, 9, 1), "duplicate event is silent");
        Check(!events.TryAccept(3, 9, 2), "old epoch is silent");
        Check(!events.TryAccept(4, 8, 2), "old round is silent");
        events.Suspend();
        Check(!events.TryAccept(4, 9, 3), "suspended event is silent");
        events.ResumeRound(4, 9);
        Check(!events.TryAccept(4, 9, 1), "rebind does not replay accepted event");
        Check(events.TryAccept(4, 9, 3), "rebind accepts unseen current event");
        events.EndRound();
        Check(!events.TryAccept(4, 9, 4), "late result event is silent");
        events.BeginRound(5, 1);
        Check(events.TryAccept(5, 1, 1), "new round accepts same event id");

        var candidates = new List<MosquitoBuzzCandidate>
        {
            new MosquitoBuzzCandidate(9, 81f),
            new MosquitoBuzzCandidate(7, 1f),
            new MosquitoBuzzCandidate(3, 64f),
            new MosquitoBuzzCandidate(5, 4f),
            new MosquitoBuzzCandidate(1, -1f),
            new MosquitoBuzzCandidate(11, float.NaN),
            new MosquitoBuzzCandidate(12, float.PositiveInfinity),
            new MosquitoBuzzCandidate(2, 9f),
            new MosquitoBuzzCandidate(4, 16f),
            new MosquitoBuzzCandidate(6, 25f),
            new MosquitoBuzzCandidate(8, 36f),
            new MosquitoBuzzCandidate(10, 49f),
        };
        var selected = new HashSet<uint>();
        MosquitoBuzzPolicy.SelectNearest(candidates, selected);
        Check(selected.Count == MosquitoBuzzPolicy.MaximumVoices, "six nearest voices");
        Check(!selected.Contains(9), "out of range is silent");
        Check(!selected.Contains(1), "invalid distance is silent");
        Check(!selected.Contains(11) && !selected.Contains(12), "non-finite distance is silent");
        Check(selected.Contains(7) && selected.Contains(5), "nearest voices remain audible");
        Check(!selected.Contains(3) && !selected.Contains(10), "voice cap excludes farther candidates");
        Console.WriteLine($"PASS {checks} audio policy checks; pure policy, no Unity playback.");
    }
}
