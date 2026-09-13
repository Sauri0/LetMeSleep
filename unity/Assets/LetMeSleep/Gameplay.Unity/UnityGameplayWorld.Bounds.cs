using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld : IGameplayBoundsWorld
    {
        private sealed class BoundsMemory
        {
            internal PlayerRole Role;
            internal Vector3[] Spawns;
            internal bool HasSafe, HasAttempt;
            internal Vector3 SafePoint;
            internal Collider Support;
            internal bool SafePointIsSupportLocal;
            internal uint LastAttempt;
            internal uint LastObservation;
            internal bool HasObservation;
            internal bool RescueUnsettled;
            internal uint LastStableTick;
            internal int StableTicks;
            internal BoundsRecoveryStatus Reported;
        }
        private readonly Dictionary<uint, BoundsMemory> boundsMemory = new Dictionary<uint, BoundsMemory>();
        private GameplayRecoveryVolume recoveryVolume;
        private GameplayRecoveryVolume.Settings recoverySettings;
        public bool BoundsRecoveryEnabled => recoveryVolume && recoveryVolume.isActiveAndEnabled;
        // Host diagnostics, deliberately separate from GameplayEvent/wire IDs.
        public event Action<uint, BoundsRecoveryStatus> BoundsRecoveryReported;

        public void EndBoundsRecovery() { boundsMemory.Clear(); recoveryVolume = null; recoverySettings = null; }

        public void BeginBoundsRecovery(IReadOnlyList<SpawnActor> roster)
        {
            boundsMemory.Clear();
            recoverySettings = null;
            recoveryVolume = MapRoot ? MapRoot.GetComponent<GameplayRecoveryVolume>() : null;
            if (!recoveryVolume || !recoveryVolume.isActiveAndEnabled) return;
            try { recoverySettings = new GameplayRecoveryVolume.Settings(recoveryVolume); }
            catch(ArgumentException exception) { Debug.LogWarning("LMS_BOUNDS_CONFIGURATION " + exception.Message,this); }
            foreach (var actor in roster)
            {
                var spawns = roster.Where(s => s.Role == actor.Role)
                    .OrderBy(s => s.SpawnId, StringComparer.Ordinal).ThenBy(s => s.ActorId)
                    .Select(s => s.Position.ToUnity()).ToArray();
                boundsMemory.Add(actor.ActorId, new BoundsMemory { Role = actor.Role, Spawns = spawns });
            }
        }

        public BoundsRecoveryResult CheckBounds(in BoundsRecoveryQuery query)
        {
            if (!recoveryVolume || !recoveryVolume.isActiveAndEnabled)
                return new BoundsRecoveryResult(BoundsRecoveryStatus.Disabled);
            if (!boundsMemory.TryGetValue(query.ActorId, out var memory) || memory.Role != query.Role
                || recoveryVolume.transform != MapRoot || recoverySettings == null || !query.Position.IsFinite
                || !MathEx.Finite(query.Height) || !MathEx.Finite(query.Radius)
                || query.Radius <= 0 || query.Height < 2 * query.Radius)
                return Report(query.ActorId, memory, BoundsRecoveryStatus.InvalidConfiguration);

            var position = query.Position.ToUnity();
            bool unsafePosition = !EntirelyInside(query, position, 0, false)
                || InFallZone(query.Role, position);
            if (!unsafePosition)
            {
                // A last-safe point must be supported when human, clear, wholly inside,
                // and revalidated later. A position on a removed/moved support is not trusted.
                if (!memory.HasObservation || memory.LastObservation != query.Tick)
                {
                    memory.HasObservation = true; memory.LastObservation = query.Tick;
                    bool restingOrControlled = query.Grounded || query.LifeState == LifeState.Flying
                        || query.LifeState == LifeState.Surface || query.LifeState == LifeState.ApproachingSurface;
                    if ((query.Role != PlayerRole.Human || query.Grounded) && restingOrControlled
                        && TrySafe(query, position, out var safe, out var support))
                    {
                        memory.HasSafe = true; memory.Support = support;
                        memory.SafePointIsSupportLocal = support;
                        memory.SafePoint = support ? support.transform.InverseTransformPoint(safe) : safe;
                        memory.StableTicks = memory.StableTicks > 0 && query.Tick == memory.LastStableTick + 1
                            ? memory.StableTicks + 1 : 1;
                        memory.LastStableTick = query.Tick;
                        if(memory.StableTicks >= 30) memory.RescueUnsettled = false;
                    }
                    else memory.StableTicks = 0;
                }
                memory.Reported = BoundsRecoveryStatus.NotRequired;
                return new BoundsRecoveryResult(BoundsRecoveryStatus.NotRequired);
            }
            // A validated destination that falls straight back out is not repeatedly teleported.
            // Wait for a real stable return or a new round/configuration; expose the failure.
            if (memory.RescueUnsettled)
                return Report(query.ActorId, memory, BoundsRecoveryStatus.UnstableRecovery);
            if (memory.HasAttempt && query.Tick - memory.LastAttempt < (uint)recoverySettings.RetryTicks)
                return new BoundsRecoveryResult(BoundsRecoveryStatus.RetryDeferred);
            memory.HasAttempt = true; memory.LastAttempt = query.Tick;
            if (memory.HasSafe)
            {
                bool supportValid = !memory.SafePointIsSupportLocal || IsSupport(memory.Support);
                var candidate = memory.Support ? memory.Support.transform.TransformPoint(memory.SafePoint) : memory.SafePoint;
                if (supportValid && TrySafe(query, candidate, out var safe, out _))
                    return Report(query.ActorId, memory, BoundsRecoveryStatus.Recovered, safe, NeedsGroundSupport(query));
            }
            var authored = query.Role == PlayerRole.Human ? recoverySettings.HumanSpawnPoints : recoverySettings.MosquitoSpawnPoints;
            for (int i = 0; i < (authored.Length > 0 ? authored.Length : memory.Spawns.Length); i++)
            {
                var candidate = authored.Length > 0 ? recoveryVolume.transform.TransformPoint(authored[i]) : memory.Spawns[i];
                if (TrySafe(query, candidate, out var safe, out _))
                    return Report(query.ActorId, memory, BoundsRecoveryStatus.Recovered, safe, NeedsGroundSupport(query));
            }
            return Report(query.ActorId, memory, BoundsRecoveryStatus.NoSafeDestination);
        }

        private BoundsRecoveryResult Report(uint actor, BoundsMemory memory, BoundsRecoveryStatus status,
            Vector3 point = default, bool grounded = false)
        {
            if (memory == null || memory.Reported != status || status == BoundsRecoveryStatus.Recovered)
            {
                if (memory != null) memory.Reported = status;
                if (memory != null && status == BoundsRecoveryStatus.Recovered)
                { memory.RescueUnsettled = true; memory.StableTicks = 0; }
                BoundsRecoveryReported?.Invoke(actor, status);
                if (status == BoundsRecoveryStatus.NoSafeDestination || status == BoundsRecoveryStatus.InvalidConfiguration
                    || status == BoundsRecoveryStatus.UnstableRecovery)
                    Debug.LogWarning($"LMS_BOUNDS_RECOVERY actor={actor} status={status}", this);
            }
            return new BoundsRecoveryResult(status, point.ToFloat(), grounded);
        }
        private bool InFallZone(PlayerRole role, Vector3 position)
        {
            var local = recoveryVolume.transform.InverseTransformPoint(position);
            var zones = role == PlayerRole.Human ? recoverySettings.HumanFallZones : recoverySettings.MosquitoFallZones;
            foreach (var zone in zones) if (zone.Contains(local)) return true;
            var polygons = role == PlayerRole.Human ? recoverySettings.HumanPolygons : recoverySettings.MosquitoPolygons;
            foreach (var zone in polygons) if (zone.Contains(local.ToFloat())) return true;
            return false;
        }
        private bool IsSupport(Collider c) => c && c.enabled && c.gameObject.activeInHierarchy
            && !c.isTrigger && IsWorldCollider(c) && !Actor(c);
        private bool OccupiesRecovery(Collider c, uint own) => c && c.enabled && c.gameObject.activeInHierarchy
            && IsWorldCollider(c) && (!Actor(c) || Actor(c).ActorId != own)
            && (!c.isTrigger || c.GetComponent<GameplayBodySurface>());

        private bool TrySafe(BoundsRecoveryQuery q, Vector3 point, out Vector3 safe, out Collider support)
        {
            safe = point; support = null;
            if (!GameplayRecoveryVolume.Finite(point) || InFallZone(q.Role, point)) return false;
            if (NeedsGroundSupport(q))
            {
                float reach = recoverySettings.SupportProbe;
                // Fallen insects cannot recover to another airborne waypoint. Project down
                // from that candidate only as far as this map's world-space safety bottom.
                float search = q.Role == PlayerRole.Human ? 2 * reach
                    : Mathf.Max(0, point.y + reach - SafetyWorldBottom());
                if(search <= 0) return false;
                var hits = Physics.RaycastAll(point + Vector3.up * reach, Vector3.down, search,
                    GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
                // Do not skip an unsafe/steep first solid and emerge below it on another floor.
                var hit = hits.FirstOrDefault(h => IsSupport(h.collider));
                if (!hit.collider || hit.normal.y < .55f || InFallZone(q.Role, hit.point)) return false;
                support = hit.collider;
                float footY = hit.point.y + .002f;
                safe.y = footY + (q.Role == PlayerRole.Human ? 0 : q.Radius);
                // A single centre ray can accept an edge or narrow unsupported ledge.
                float offset = q.Radius * .65f;
                foreach (var step in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    var p = new Vector3(safe.x, footY, safe.z) + step * offset;
                    if (!Physics.RaycastAll(p + Vector3.up * reach, Vector3.down, 2 * reach,
                        GeometryMask, QueryTriggerInteraction.Ignore).Any(h => IsSupport(h.collider)
                            && h.normal.y >= .55f && !InFallZone(q.Role, h.point))) return false;
                }
            }
            if (InFallZone(q.Role, safe) || !EntirelyInside(q, safe)) return false;
            var colliders = q.Role == PlayerRole.Human
                ? Physics.OverlapCapsule(safe + Vector3.up * q.Radius,
                    safe + Vector3.up * (q.Height - q.Radius), q.Radius, GeometryMask, QueryTriggerInteraction.Collide)
                : Physics.OverlapSphere(safe, q.Radius, GeometryMask, QueryTriggerInteraction.Collide);
            return !colliders.Any(c => OccupiesRecovery(c, q.ActorId));
        }
        private static bool NeedsGroundSupport(BoundsRecoveryQuery q) => q.Role == PlayerRole.Human
            || q.LifeState == LifeState.Falling || q.LifeState == LifeState.Stunned
            || q.LifeState == LifeState.Recovering;
        private float SafetyWorldBottom()
        {
            var b = recoverySettings.SafetyBounds; float bottom=float.PositiveInfinity;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
                bottom=Mathf.Min(bottom,recoveryVolume.transform.TransformPoint(corner).y);
            }
            return bottom;
        }
        private bool EntirelyInside(BoundsRecoveryQuery q, Vector3 point)
            => EntirelyInside(q, point, recoverySettings.InteriorMargin, true);
        private bool EntirelyInside(BoundsRecoveryQuery q, Vector3 point, float margin, bool rejectZoneCorners)
        {
            float r = q.Radius + margin;
            float low = q.Role == PlayerRole.Human ? -margin : -r;
            float high = q.Role == PlayerRole.Human ? q.Height + margin : r;
            for (int x = -1; x <= 1; x += 2)
            for (int y = 0; y <= 1; y++)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = point + new Vector3(x * r, y == 0 ? low : high, z * r);
                if (!recoverySettings.SafetyBounds.Contains(recoveryVolume.transform.InverseTransformPoint(corner))
                    || (rejectZoneCorners && InFallZone(q.Role, corner))) return false;
            }
            return true;
        }
    }
}
