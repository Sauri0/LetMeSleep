using System.Collections.Generic;
using UnityEngine;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld
    {
        private readonly Dictionary<uint, Vector3> botDetours = new Dictionary<uint, Vector3>();
        private static readonly float[] BotYawSamples = { 0, 45, -45, 90, -90, 135, -135, 180 };
        private static readonly float[] BotFlightSamples = { 0, .65f, -.65f };
        private static readonly float[] BotGroundSamples = { 0 };
        internal void ResetBotSteering() => botDetours.Clear();
        internal Float3 SteerBot(ActorSnapshot self, Float3 desired)
        {
            bool human = self.Role == PlayerRole.Human;
            var direction = desired.ToUnity(); if (human) direction.y = 0;
            direction.Normalize(); if (direction.sqrMagnitude < .1f) return Float3.Zero;
            const float lookAhead = .65f;
            if (BotClearance(self, direction, lookAhead) >= lookAhead)
            { botDetours.Remove(self.ActorId); return direction.ToFloat(); }
            float bestScore = float.NegativeInfinity; var best = Vector3.zero;
            botDetours.TryGetValue(self.ActorId, out var previous);
            // Sweeps test actual map colliders. The motor remains the final collision authority.
            foreach (float yaw in BotYawSamples)
            foreach (float rise in human ? BotGroundSamples : BotFlightSamples)
            {
                var candidate = (Quaternion.AngleAxis(yaw, Vector3.up) * direction + Vector3.up * rise).normalized;
                float clearance = BotClearance(self, candidate, lookAhead);
                if (clearance < .12f) continue;
                float score = clearance * 3 + Vector3.Dot(direction, candidate) * .65f + Vector3.Dot(previous, candidate) * .45f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            botDetours[self.ActorId] = best; return best.ToFloat();
        }
        private float BotClearance(ActorSnapshot actor, Vector3 direction, float distance)
        {
            var position = actor.Position.ToUnity(); bool human = actor.Role == PlayerRole.Human;
            float radius = human ? .26f : .065f;
            var hits = human
                ? Physics.CapsuleCastAll(position + Vector3.up * .29f, position + Vector3.up * (1.46f - .64f * actor.CrouchFraction), radius, direction, distance, GeometryMask, QueryTriggerInteraction.Ignore)
                : Physics.SphereCastAll(position, radius, direction, distance, GeometryMask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            foreach (var hit in hits)
            {
                if (!IsWorldCollider(hit.collider) || hit.collider.GetComponentInParent<GameplayActorProxy>()) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return nearest;
        }
    }
}
