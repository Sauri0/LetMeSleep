using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    internal sealed class GameplayBotNavigation
    {
        // DTO for the authored map schema. No dependency from Gameplay to Content.
#pragma warning disable 0649 // Populated by JsonUtility from the authored SpatialData.
        [Serializable] private sealed class Plan { public int schema_version; public string map_id; public Zone[] zones; public Portal[] portals; public Stair stair; }
        [Serializable] private sealed class Zone { public string id; public float[] min, max; }
        [Serializable] private sealed class Portal { public string id, from, to; public float[] center, normal; public float width, height; public bool door; }
        [Serializable] private sealed class Flight { public float[] clear_x; public float start_y, end_y, start_z, end_z; }
        [Serializable] private sealed class Stair { public string id, from, to; public Flight lower_flight, upper_flight; public Zone mid_landing; }
#pragma warning restore 0649
        private readonly UnityGameplayWorld world;
        private readonly BotRegion[] regions;
        private readonly BotPassage[] passages;
        private readonly Dictionary<string, GameplayDoor> doors = new Dictionary<string, GameplayDoor>();
        private readonly Dictionary<uint, BotPatrol> patrols = new Dictionary<uint, BotPatrol>();
        public GameplayBotNavigation(TextAsset data, string mapId, UnityGameplayWorld world)
        {
            this.world = world;
            var plan = JsonUtility.FromJson<Plan>(data.text);
            if (plan == null || plan.schema_version != 1 || plan.map_id != mapId || plan.zones == null || plan.portals == null)
                throw new ArgumentException("Bot navigation data must match the active map and schema 1.");
            regions = plan.zones.Select(z => new BotRegion(z.id, Point(z.min), Point(z.max))).ToArray();
            if (regions.Length == 0 || regions.Select(r => r.Id).Distinct().Count() != regions.Length)
                throw new ArgumentException("Bot navigation regions must have unique IDs.");
            var links = new List<BotPassage>();
            var known = new HashSet<string>(regions.Select(r => r.Id));
            foreach (var portal in plan.portals)
            {
                // Exterior/service voids without authored walkable regions are not routes.
                if (!known.Contains(portal.from) || !known.Contains(portal.to)) continue;
                if (portal.width < .3f || portal.height < .5f) continue;
                var center = Point(portal.center); var normal = Point(portal.normal).Normalized;
                if (normal.LengthSquared < .9f) throw new ArgumentException("Invalid portal normal.");
                links.Add(new BotPassage(portal.id, portal.from, portal.to,
                    new[] { center - normal * .55f, center, center + normal * .55f }));
                if (portal.door)
                {
                    // Missing/ambiguous authored door binding is closed, never an invisible shortcut.
                    var matches = world.Doors.Values.Where(d => d.name == portal.id).ToArray();
                    doors.Add(portal.id, matches.Length == 1 ? matches[0] : null);
                }
            }
            var stair = plan.stair;
            if (stair?.lower_flight != null && stair.upper_flight != null && stair.mid_landing != null && known.Contains(stair.from) && known.Contains(stair.to))
            {
                var lower = stair.lower_flight; var upper = stair.upper_flight;
                float lx = Mid(lower.clear_x), ux = Mid(upper.clear_x);
                float turnZ = (Point(stair.mid_landing.min).Z + Point(stair.mid_landing.max).Z) * .5f;
                links.Add(new BotPassage(stair.id, stair.from, stair.to, new[] {
                    new Float3(lx, lower.start_y + .85f, lower.start_z - .45f),
                    new Float3(lx, lower.end_y + .85f, lower.end_z),
                    new Float3(lx, lower.end_y + .85f, turnZ),
                    new Float3(ux, upper.start_y + .85f, turnZ),
                    new Float3(ux, upper.start_y + .85f, upper.start_z),
                    new Float3(ux, upper.end_y + .85f, upper.end_z),
                    new Float3(ux, upper.end_y + .85f, upper.end_z - .45f) }));
            }
            passages = links.ToArray();
        }
        public Float3 Explore(ActorSnapshot actor, uint tick)
        {
            if (actor.Role != PlayerRole.Mosquito) return Float3.Zero;
            if (!patrols.TryGetValue(actor.ActorId, out var patrol))
                patrols.Add(actor.ActorId, patrol = new BotPatrol(regions, passages, actor.ActorId));
            var local = world.MapRoot.InverseTransformPoint(actor.Position.ToUnity()).ToFloat();
            var direction = patrol.Direction(local, tick, IsOpen);
            return world.MapRoot.TransformVector(direction.ToUnity()).ToFloat();
        }
        private bool IsOpen(string passage) => !doors.TryGetValue(passage, out var door) || (door && Mathf.Abs(door.AngleRadians) >= 60 * Mathf.Deg2Rad);
        private static float Mid(float[] v)
        { if (v == null || v.Length != 2 || !MathEx.Finite(v[0]) || !MathEx.Finite(v[1])) throw new ArgumentException("Invalid stair width."); return (v[0] + v[1]) * .5f; }
        private static Float3 Point(float[] v)
        {
            if (v == null || v.Length != 3) throw new ArgumentException("Invalid navigation point.");
            var point = new Float3(v[0], v[1], v[2]); if (!point.IsFinite) throw new ArgumentException("Nonfinite navigation point."); return point;
        }
    }
}
