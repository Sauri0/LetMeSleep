using System;
using LetMeSleep.Online;

public static class LobbyJoinPolicyChecks
{
    private static int checks;

    public static int Main()
    {
        Expect(LobbyCandidateRejection.None,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 16, 15, false, false, true),
            "valid candidate");
        Expect(LobbyCandidateRejection.InvalidDetails,
            LobbyJoinPolicy.Validate("", "host", "lms-unity-094-alfa-2", 8, 7, false, false, true),
            "missing local identity");
        Expect(LobbyCandidateRejection.InvalidDetails,
            LobbyJoinPolicy.Validate("guest", "", "lms-unity-094-alfa-2", 8, 7, false, false, true),
            "missing owner identity");
        Expect(LobbyCandidateRejection.SameDeviceIdentity,
            LobbyJoinPolicy.Validate("same", "same", "lms-unity-094-alfa-2", 8, 7, false, false, true),
            "own lobby");
        Expect(LobbyCandidateRejection.IncompatibleVersion,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-older", 8, 7, false, false, true),
            "protocol mismatch");
        Expect(LobbyCandidateRejection.LobbyFull,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 8, 0, false, false, true),
            "full lobby");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 17, 16, false, false, true),
            "oversized lobby");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 8, 7, true, false, true),
            "host migration enabled");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 8, 7, false, true, true),
            "RTC enabled");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.Validate("guest", "host", "lms-unity-094-alfa-2", 8, 7, false, false, false),
            "join by ID disabled");

        Console.WriteLine($"ONLINE_POLICY_CHECKS checks={checks} failures=0 native_sdk_loaded=false");
        return 0;
    }

    private static void Expect(LobbyCandidateRejection expected, LobbyCandidateRejection actual, string name)
    {
        checks++;
        if (actual != expected)
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}
