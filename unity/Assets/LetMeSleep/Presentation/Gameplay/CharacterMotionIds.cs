namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Stable CharacterView.MotionBinding IDs (index in CharacterContentBuilder's state lists, append-only).
    /// IDs 17+ (human) and 15+ (mosquito) exist only on prefabs built from the v0.3.0 expressive clips;
    /// callers check CharacterView.Motions before using them.
    /// </summary>
    public static class CharacterMotionIds
    {
        public const int HumanIdle = 0, HumanWalk = 1, HumanRun = 2, HumanCrouch = 3, HumanJump = 4, HumanLand = 5,
            HumanTurn = 6, HumanClap = 7, HumanHit = 8, HumanFall = 9, HumanFaint = 10, HumanRecover = 11,
            HumanSwat = 12, HumanBlink = 13, HumanFingerCurl = 14, HumanWalkSlow = 15, HumanTrot = 16,
            HumanJumpAir = 17, HumanFallAir = 18, HumanCrouchWalk = 19, HumanYawn = 20, HumanVictory = 21;

        public const int MosquitoIdle = 0, MosquitoHover = 1, MosquitoFly = 2, MosquitoBrake = 3, MosquitoPerchEnter = 4,
            MosquitoPerchIdle = 5, MosquitoSurfaceWalk = 6, MosquitoBiteStart = 7, MosquitoBiteLoop = 8,
            MosquitoDetach = 9, MosquitoHit = 10, MosquitoFall = 11, MosquitoRecover = 12, MosquitoLand = 13,
            MosquitoBite = 14, MosquitoStunnedLoop = 15;
    }
}
