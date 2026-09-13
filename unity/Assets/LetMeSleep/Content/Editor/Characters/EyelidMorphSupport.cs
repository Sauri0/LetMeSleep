using System;

namespace LetMeSleep.Content.Characters.Editor
{
    // Pure topology policy, shared with the standalone diagnostic checks.
    internal static class EyelidMorphSupport
    {
        internal static bool[] Build(bool[] moving, int[] triangles)
        {
            if (moving == null || triangles == null || triangles.Length % 3 != 0)
                throw new ArgumentException("Expected vertex movement flags and triangle indices.");
            var allowed = (bool[])moving.Clone();
            for (int offset = 0; offset < triangles.Length; offset += 3)
            {
                int a = triangles[offset], b = triangles[offset + 1], c = triangles[offset + 2];
                if (a < 0 || b < 0 || c < 0 || a >= moving.Length || b >= moving.Length || c >= moving.Length)
                    throw new ArgumentException("Morph triangle index exceeds the vertex array.");
                // Read the original flags: do not flood through the whole head.
                if (moving[a] || moving[b] || moving[c])
                    allowed[a] = allowed[b] = allowed[c] = true;
            }
            return allowed;
        }

        internal static void Filter<T>(T[] deltas, bool[] allowed)
        {
            if (deltas == null || allowed == null || deltas.Length != allowed.Length)
                throw new ArgumentException("Morph delta and support arrays differ.");
            for (int i = 0; i < deltas.Length; i++)
                if (!allowed[i]) deltas[i] = default(T);
        }
    }
}
