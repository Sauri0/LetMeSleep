using System;
using System.Collections.Generic;
using LetMeSleep.Content.Environment;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    /// <summary>
    /// v0.3.0 decoration keep-out volumes of one Higgsfield map instance (MAPA-SISTEMAS "maps", decisions A26/A30:
    /// clean routes, detail concentrated off them). Built from the map's own data only:
    /// - human routes (SpatialData human_routes, or portal-to-portal and spawn-to-portal lines inside each zone when a
    ///   map only has zones/portals): capsules of <see cref="RouteRadius"/> from the floor to 2 m above it;
    /// - portals (doors and openings): boxes of the portal width/height, <see cref="PortalDepth"/> deep on both sides;
    /// - stairs (SpatialData stair flights);
    /// - human, mosquito and recovery spawns: <see cref="SpawnRadius"/> spheres;
    /// - objectives: LocalPosition and LocalApproachPoint (UseRadius + 0.3 m) and the TargetPath renderers.
    /// Pure queries (no physics); used by the editor decor builder/validator and by the PlayMode decor tests.
    /// </summary>
    public static class HiggsfieldDecorClearance
    {
        public const float RouteRadius = 0.6f;
        public const float RouteHeight = 2f;
        public const float PortalDepth = 0.6f;
        public const float SpawnRadius = 1f;
        public const float ObjectiveMargin = 0.3f;
        /// <summary>Flat, walkable decor (rugs, lily pads) may lie on routes, never in portals, spawns or objectives.</summary>
        public const float WalkableFlatHeight = 0.03f;

        public enum Kind { Route, Portal, Stair, Spawn, Objective, ObjectiveTarget }

        public sealed class Keepout
        {
            public Kind Kind;
            public string Id;
            public bool IsCapsule;
            public Vector3 A, B;      // capsule segment (world) when IsCapsule
            public float Radius;      // capsule / sphere radius
            public float MinY, MaxY;  // vertical extent of a route capsule
            public Bounds Box;        // world AABB otherwise (spheres use Box + Radius around its center)
            public bool IsSphere;
        }

        [Serializable] private sealed class Spatial
        {
            public Zone[] zones; public Portal[] portals; public Zone[] human_zones; public Portal[] human_portals; public Route[] human_routes;
            public Stair stair;
        }
        [Serializable] private sealed class Zone { public string id; public float[] min, max; }
        [Serializable] private sealed class Portal { public string id, from, to; public float[] center, normal; public float width, height; public bool door; }
        [Serializable] private sealed class Route { public string id; public RoutePoint[] points; }
        [Serializable] private sealed class RoutePoint { public float[] position; }
        [Serializable] private sealed class Stair { public string id; public Flight lower_flight, upper_flight; }
        [Serializable] private sealed class Flight { public float[] clear_x; public float start_y, end_y, start_z, end_z; }

        /// <summary>All keep-outs of <paramref name="map"/> in world space (the map may be anywhere in the scene).</summary>
        public static List<Keepout> Build(EnvironmentMapDefinition map)
        {
            if (!map) throw new ArgumentNullException(nameof(map));
            var result = new List<Keepout>();
            Transform root = map.transform;
            if (map.SpatialData)
            {
                var data = JsonUtility.FromJson<Spatial>(map.SpatialData.text);
                bool human = data.human_routes != null && data.human_routes.Length > 0;
                var zones = human ? data.human_zones ?? Array.Empty<Zone>() : data.zones ?? Array.Empty<Zone>();
                var portals = human ? data.human_portals ?? Array.Empty<Portal>() : data.portals ?? Array.Empty<Portal>();
                if (human)
                    foreach (var route in data.human_routes)
                        for (int i = 0; route.points != null && i + 1 < route.points.Length; i++)
                            AddRoute(result, root, route.id, V(route.points[i].position), V(route.points[i + 1].position), 0f);
                foreach (var portal in portals)
                {
                    Vector3 center = V(portal.center), normal = V(portal.normal).normalized;
                    Vector3 tangent = Vector3.Cross(Vector3.up, normal).normalized;
                    var local = new Bounds(center, Vector3.zero);
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float t in new[] { -1f, 1f })
                            foreach (float u in new[] { -1f, 1f })
                                local.Encapsulate(center + normal * (s * PortalDepth) + tangent * (t * (portal.width * .5f + .1f)) + Vector3.up * (u * portal.height * .5f));
                    result.Add(new Keepout { Kind = Kind.Portal, Id = portal.id, Box = WorldBox(root, local) });
                }
                // Zone graphs without explicit routes: people walk portal to portal (and from spawns to portals) across
                // each zone. Floor height = the zone floor (portal centers sit at mid door height).
                if (!human)
                {
                    var spawns = new List<Vector3>();
                    foreach (var point in map.HumanSpawnPoints ?? Array.Empty<Transform>()) if (point) spawns.Add(root.InverseTransformPoint(point.position));
                    foreach (var zone in zones)
                    {
                        var min = V(zone.min); var max = V(zone.max);
                        var nodes = new List<Vector3>();
                        foreach (var portal in portals)
                            if (portal.from == zone.id || portal.to == zone.id)
                            {
                                var c = V(portal.center); nodes.Add(new Vector3(c.x, min.y, c.z));
                            }
                        foreach (var s in spawns)
                            if (s.x >= min.x && s.x <= max.x && s.z >= min.z && s.z <= max.z && s.y >= min.y - .6f && s.y <= max.y)
                                nodes.Add(new Vector3(s.x, min.y, s.z));
                        for (int i = 0; i < nodes.Count; i++)
                            for (int j = i + 1; j < nodes.Count; j++)
                                AddRoute(result, root, zone.id, nodes[i], nodes[j], 0f);
                    }
                }
                if (data.stair != null)
                    foreach (var flight in new[] { data.stair.lower_flight, data.stair.upper_flight })
                    {
                        if (flight == null || flight.clear_x == null || flight.clear_x.Length != 2) continue;
                        var local = new Bounds();
                        local.SetMinMax(new Vector3(flight.clear_x[0] - .1f, Mathf.Min(flight.start_y, flight.end_y) - .1f, Mathf.Min(flight.start_z, flight.end_z) - .3f),
                                        new Vector3(flight.clear_x[1] + .1f, Mathf.Max(flight.start_y, flight.end_y) + RouteHeight + .2f, Mathf.Max(flight.start_z, flight.end_z) + .3f));
                        result.Add(new Keepout { Kind = Kind.Stair, Id = data.stair.id, Box = WorldBox(root, local) });
                    }
            }
            void Sphere(Kind kind, string id, Vector3 world, float radius) =>
                result.Add(new Keepout { Kind = kind, Id = id, IsSphere = true, Radius = radius, Box = new Bounds(world, Vector3.one * radius * 2f) });
            var humans = map.HumanSpawnPoints ?? Array.Empty<Transform>();
            for (int i = 0; i < humans.Length; i++) if (humans[i]) Sphere(Kind.Spawn, "human-" + i, humans[i].position, SpawnRadius);
            var mosquitoes = map.MosquitoSpawnPoints ?? Array.Empty<Transform>();
            for (int i = 0; i < mosquitoes.Length; i++) if (mosquitoes[i]) Sphere(Kind.Spawn, "mosquito-" + i, mosquitoes[i].position, SpawnRadius);
            var recovery = map.GetComponent<GameplayRecoveryVolume>();
            if (recovery)
            {
                for (int i = 0; i < recovery.HumanSpawnPoints.Length; i++) Sphere(Kind.Spawn, "recovery-human-" + i, root.TransformPoint(recovery.HumanSpawnPoints[i]), SpawnRadius);
                for (int i = 0; i < recovery.MosquitoSpawnPoints.Length; i++) Sphere(Kind.Spawn, "recovery-mosquito-" + i, root.TransformPoint(recovery.MosquitoSpawnPoints[i]), SpawnRadius);
            }
            var objectives = map.GetComponent<GameplayObjectiveCatalog>();
            if (objectives)
                foreach (var entry in objectives.Entries)
                {
                    float radius = entry.UseRadius + ObjectiveMargin;
                    Sphere(Kind.Objective, entry.ObjectiveId, root.TransformPoint(entry.LocalPosition), radius);
                    Sphere(Kind.Objective, entry.ObjectiveId + "/approach", root.TransformPoint(entry.LocalApproachPoint), radius);
                    if (string.IsNullOrEmpty(entry.TargetPath)) continue;
                    var target = root.Find(entry.TargetPath);
                    if (!target) continue;
                    foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                    {
                        var box = renderer.bounds; box.Expand(0.4f);
                        result.Add(new Keepout { Kind = Kind.ObjectiveTarget, Id = entry.ObjectiveId + "/target", Box = box });
                    }
                }
            return result;
        }

        private static void AddRoute(List<Keepout> result, Transform root, string id, Vector3 localA, Vector3 localB, float floorOffset)
        {
            Vector3 a = root.TransformPoint(localA), b = root.TransformPoint(localB);
            result.Add(new Keepout
            {
                Kind = Kind.Route, Id = id, IsCapsule = true, A = a, B = b, Radius = RouteRadius,
                MinY = Mathf.Min(a.y, b.y) - .15f + floorOffset, MaxY = Mathf.Max(a.y, b.y) + RouteHeight
            });
        }

        /// <summary>
        /// First keep-out intersected by a decor renderer's world bounds, or null when clear. Zone-graph routes are straight
        /// lines that may run along the outside of a wall: with <paramref name="separated"/> (a solid map surface between two
        /// points) a route on the other side of a wall does not reject decor inside the room.
        /// </summary>
        public static Keepout FirstViolation(Bounds bounds, IReadOnlyList<Keepout> keepouts, Func<Vector3, Vector3, bool> separated = null)
        {
            bool flat = bounds.size.y <= WalkableFlatHeight;
            foreach (var k in keepouts)
            {
                if (flat && k.Kind == Kind.Route) continue;
                if (k.IsCapsule)
                {
                    if (bounds.max.y < k.MinY || bounds.min.y > k.MaxY) continue;
                    if (SegmentBoxDistanceXZ(k.A, k.B, bounds) >= k.Radius) continue;
                    if (separated != null)
                    {
                        Vector3 ab = k.B - k.A;
                        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(bounds.center - k.A, ab) / ab.sqrMagnitude);
                        Vector3 onRoute = k.A + ab * t;
                        onRoute.y = Mathf.Clamp(bounds.center.y, k.MinY + .3f, k.MaxY - .3f);
                        if (separated(bounds.center, onRoute)) continue;
                    }
                    return k;
                }
                else if (k.IsSphere)
                {
                    if ((bounds.ClosestPoint(k.Box.center) - k.Box.center).sqrMagnitude < k.Radius * k.Radius) return k;
                }
                else if (bounds.Intersects(k.Box)) return k;
            }
            return null;
        }

        /// <summary>Distance in the XZ plane between segment AB and the XZ rectangle of <paramref name="box"/>.</summary>
        public static float SegmentBoxDistanceXZ(Vector3 a, Vector3 b, Bounds box)
        {
            var min = new Vector2(box.min.x, box.min.z); var max = new Vector2(box.max.x, box.max.z);
            var p = new Vector2(a.x, a.z); var q = new Vector2(b.x, b.z);
            if (Inside(p, min, max) || Inside(q, min, max)) return 0f;
            var corners = new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
            for (int i = 0; i < 4; i++) if (SegmentsIntersect(p, q, corners[i], corners[(i + 1) % 4])) return 0f;
            // Two disjoint convex sets: the closest pair has an endpoint of the segment or a corner of the rectangle.
            float best = Mathf.Min(PointRect(p, min, max), PointRect(q, min, max));
            foreach (var corner in corners) best = Mathf.Min(best, PointSegment(corner, p, q));
            return best;
        }

        private static float PointRect(Vector2 p, Vector2 min, Vector2 max) =>
            (new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y)) - p).magnitude;
        private static bool Inside(Vector2 p, Vector2 min, Vector2 max) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y;
        private static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = ab.sqrMagnitude < 1e-8f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool SegmentsIntersect(Vector2 p, Vector2 q, Vector2 r, Vector2 s)
        {
            Vector2 d1 = q - p, d2 = s - r; float den = Cross(d1, d2);
            if (Mathf.Abs(den) < 1e-9f) return false;
            float t = Cross(r - p, d2) / den, u = Cross(r - p, d1) / den;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        private static Bounds WorldBox(Transform root, Bounds local)
        {
            var box = new Bounds(root.TransformPoint(local.center), Vector3.zero);
            var e = local.extents;
            for (int i = 0; i < 8; i++)
                box.Encapsulate(root.TransformPoint(local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return box;
        }

        private static Vector3 V(float[] v) => v == null || v.Length < 3 ? Vector3.zero : new Vector3(v[0], v[1], v[2]);
    }
}
