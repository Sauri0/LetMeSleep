using System;
using LetMeSleep.Online;

public static class LobbyJoinPolicyChecks
{
    private static int checks;

    public static int Main()
    {
        Expect(LobbyCandidateRejection.None,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 16, false, false, true),
            "valid joined lobby");
        Expect(LobbyCandidateRejection.InvalidDetails,
            LobbyJoinPolicy.ValidateJoined("", "host", "lms-unity-094-alfa-2", 8, false, false, true),
            "missing local identity");
        Expect(LobbyCandidateRejection.InvalidDetails,
            LobbyJoinPolicy.ValidateJoined("guest", "", "lms-unity-094-alfa-2", 8, false, false, true),
            "missing owner identity");
        Expect(LobbyCandidateRejection.SameDeviceIdentity,
            LobbyJoinPolicy.ValidateJoined("same", "same", "lms-unity-094-alfa-2", 8, false, false, true),
            "own lobby");
        Expect(LobbyCandidateRejection.IncompatibleVersion,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-older", 8, false, false, true),
            "protocol mismatch");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 17, false, false, true),
            "oversized lobby");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 1, false, false, true),
            "undersized lobby");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 8, true, false, true),
            "host migration enabled");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 8, false, true, true),
            "RTC enabled");
        Expect(LobbyCandidateRejection.UnsafeLobbyConfiguration,
            LobbyJoinPolicy.ValidateJoined("guest", "host", "lms-unity-094-alfa-2", 8, false, false, false),
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
