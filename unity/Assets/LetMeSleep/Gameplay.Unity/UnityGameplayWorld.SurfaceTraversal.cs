using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld
    {
        // The caller already bounds contact displacement and tests the destination body.
        // These witnesses prove a local connected path on the actual meshes, not AABBs.
        private static bool WitnessMeshSurfacePath(Collider previous, Collider next,
            Vector3 from, Vector3 oldNormal, Vector3 to, Vector3 newNormal, int bridgesLeft = 2)
        {
            float cosine = Vector3.Dot(oldNormal, newNormal);
            if (cosine < -.98f) return false;
            if (cosine >= .98f)
            {
                // Spacing below the allowed 2mm seam ensures a larger open gap cannot
                // fit between witnesses. Typically 15 rays for one 30Hz crawl step.
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .0015f));
                if (steps > 120) return false;
                Vector3 priorPoint = from;
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
                    if (!MeshSurfaceHit(previous, point, oldNormal, out var hit) &&
                        !MeshSurfaceHit(next, point, newNormal, out hit)) return false;
                    // Parallel footprints alone cannot prove a small step. Its vertical
                    // riser must physically connect both contact heights.
                    if (i > 0 && Mathf.Abs(Vector3.Dot(hit.point - priorPoint, oldNormal)) > .00025f &&
                        !WitnessMeshRiser(previous, next, priorPoint, hit.point, oldNormal)) return false;
                    priorPoint = hit.point;
                }
                return true;
            }
            float determinant = 1 - cosine * cosine;
            if (determinant < .0001f) return false;
            // Nearest point on the intersection of the two witnessed contact planes.
            Vector3 midpoint = (from + to) * .5f;
            float oldDistance = Vector3.Dot(oldNormal, from - midpoint);
            float newDistance = Vector3.Dot(newNormal, to - midpoint);
            Vector3 seam = midpoint + ((oldDistance - cosine * newDistance) * oldNormal +
                (newDistance - cosine * oldDistance) * newNormal) / determinant;
            float oldLength = Vector3.Distance(from, seam), newLength = Vector3.Distance(to, seam);
            if (oldLength > .15f || newLength > .15f) return false;
            // Stay at most 1mm inside each face to avoid ambiguous triangle-edge ray hits.
            // Each face must reach the same seam; nearby disconnected planes fail here.
            Vector3 oldEnd = Vector3.MoveTowards(seam, from, Mathf.Min(.001f, oldLength * .5f));
            Vector3 newEnd = Vector3.MoveTowards(seam, to, Mathf.Min(.001f, newLength * .5f));
            if (MeshSurfaceSegment(previous, from, oldEnd, oldNormal) &&
                MeshSurfaceSegment(next, to, newEnd, newNormal)) return true;
            // Authored bevels replace the mathematical corner with an intermediate face.
            // Witness that face, then require physical joins on both sides recursively.
            // This never fills a missing bevel or changes the 2mm seam tolerance.
            if (bridgesLeft <= 0) return false;
            Vector3 bridgeNormal = (oldNormal + newNormal).normalized;
            if (bridgeNormal.sqrMagnitude < .9f) return false;
            foreach (var surface in new[] { previous, next })
            {
                if (!surface.Raycast(new Ray(seam + bridgeNormal * .035f, -bridgeNormal), out var bridge, .07f) ||
                    Vector3.Distance(bridge.point, seam) > .03f ||
                    Vector3.Dot(bridge.normal, oldNormal) >= .98f || Vector3.Dot(bridge.normal, newNormal) >= .98f ||
                    Vector3.Dot(bridge.normal, oldNormal) < -.1f || Vector3.Dot(bridge.normal, newNormal) < -.1f) continue;
                if (WitnessMeshSurfacePath(previous, surface, from, oldNormal, bridge.point, bridge.normal, bridgesLeft - 1) &&
                    WitnessMeshSurfacePath(surface, next, bridge.point, bridge.normal, to, newNormal, bridgesLeft - 1)) return true;
            }
            return false;
        }

        private static bool WitnessMeshRiser(Collider previous, Collider next,
            Vector3 from, Vector3 to, Vector3 planeNormal)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(to - from, planeNormal).normalized;
            if (tangent.sqrMagnitude < .5f) return false;
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(Vector3.Dot(to - from, planeNormal)) / .0015f));
            if (steps > 40) return false;
            for (int i = 0; i <= steps; i++)
            {
                // Avoid the ambiguous endpoints where the riser meets a flat face.
                float fraction = Mathf.Lerp(.05f, .95f, i / (float)steps);
                Vector3 point = Vector3.Lerp(from, to, fraction);
                if (!RiserAt(previous, point, tangent, planeNormal) &&
                    !RiserAt(next, point, tangent, planeNormal)) return false;
            }
            return true;
        }

        private static bool RiserAt(Collider collider, Vector3 point, Vector3 tangent, Vector3 planeNormal)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector3 rayNormal = tangent * direction;
                if (collider.Raycast(new Ray(point + rayNormal * .006f, -rayNormal), out var hit, .012f) &&
                    Vector3.Distance(hit.point, point) <= .002f &&
                    Mathf.Abs(Vector3.Dot(hit.normal, planeNormal)) < .1f &&
                    Vector3.Dot(hit.normal, rayNormal) > .2f) return true;
            }
            return false;
        }

        private static bool MeshSurfaceSegment(Collider surface, Vector3 from, Vector3 to, Vector3 normal)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .0015f));
            if (steps > 120) return false;
            for (int i = 0; i <= steps; i++)
                if (!MeshSurfaceAt(surface, Vector3.Lerp(from, to, i / (float)steps), normal)) return false;
            return true;
        }

        private static bool MeshSurfaceAt(Collider surface, Vector3 point, Vector3 normal)
            => MeshSurfaceHit(surface, point, normal, out _);

        private static bool MeshSurfaceHit(Collider surface, Vector3 point, Vector3 normal, out RaycastHit hit)
            => surface.Raycast(new Ray(point + normal * .006f, -normal), out hit, .012f) &&
                Vector3.Distance(hit.point, point) <= .002f && Vector3.Dot(hit.normal, normal) >= .98f;
    }
}
