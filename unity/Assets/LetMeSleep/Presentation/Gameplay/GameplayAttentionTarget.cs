using System.Collections.Generic;
using LetMeSleep.Content.Characters;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    // Visual target only, after pose binding1100 and before facial writer1200.
    [DefaultExecutionOrder(1150)]
    [DisallowMultipleComponent]
    public sealed class GameplayAttentionTarget : MonoBehaviour
    {
        private GameplayRuntime game;
        private GameplayActorProxy proxy;
        private LobbyMovementRuntime lobby;
        private string player;
        private VisualAttentionRig attention;
        private ulong epoch, round;
        private uint rosterRevision;
        private bool identityCaptured;
        private readonly NearestAttentionPolicy<Transform> selection = new NearestAttentionPolicy<Transform>();
        private readonly List<NearestAttentionPolicy<Transform>.Candidate> candidates = new List<NearestAttentionPolicy<Transform>.Candidate>(16);
        private readonly Dictionary<Transform, Transform> lookTargets = new Dictionary<Transform, Transform>(16);

        public void Bind(GameplayRuntime runtime, GameplayActorProxy actor, VisualAttentionRig rig)
        {
            ClearSelection(); game = runtime; proxy = actor; attention = rig; lobby = null;
            identityCaptured = false;
            if (attention) attention.ClearLookTarget();
        }
        public void Bind(LobbyMovementRuntime runtime, string playerId, VisualAttentionRig rig)
        {
            ClearSelection(); lobby = runtime; player = playerId; attention = rig; game = null; proxy = null;
            epoch = runtime.SessionEpoch; rosterRevision = runtime.RosterRevision; identityCaptured = true;
            if (attention) attention.ClearLookTarget();
        }
        private void LateUpdate()
        {
            if (!attention || !attention.IsConfigured) { ClearSelection(); return; }
            candidates.Clear(); lookTargets.Clear();
            Vector3 direction;
            if (game)
            {
                if (!game.isActiveAndEnabled || !proxy || !proxy.gameObject.activeInHierarchy || game.World == null ||
                    !game.World.Actors.TryGetValue(proxy.ActorId, out var current) || current != proxy ||
                    !transform.IsChildOf(proxy.transform) || game.LatestSnapshot == null)
                { ClearSelection(); return; }
                var snapshot = game.LatestSnapshot;
                if (!identityCaptured) { epoch = snapshot.SessionEpoch; round = snapshot.RoundId; identityCaptured = true; }
                if (epoch != snapshot.SessionEpoch || round != snapshot.RoundId) { ClearSelection(); return; }
                global::LetMeSleep.Gameplay.ActorSnapshot self = null;
                foreach (var state in snapshot.Actors) if (state.ActorId == proxy.ActorId) { self = state; break; }
                if (self == null) { ClearSelection(); return; }
                direction = proxy.ActorId == game.LocalActorId ? game.LocalViewForward.ToUnity() : self.ViewForward.ToUnity();
                // Accepted session roster: at most16 actors, never scene/preview/global searches.
                foreach (var state in snapshot.Actors)
                {
                    if (state.ActorId == proxy.ActorId || !game.World.Actors.TryGetValue(state.ActorId, out var other) ||
                        !other || !other.gameObject.activeInHierarchy) continue;
                    var visual = other.GetComponentInChildren<ActorVisualBinding>();
                    if (!visual || !visual.isActiveAndEnabled || visual.ActorId != state.ActorId || !visual.View) continue;
                    AddCandidate(visual.transform, visual.View, transform.position, direction);
                }
            }
            else if (lobby)
            {
                if (!lobby.isActiveAndEnabled || !lobby.IsBound || lobby.SessionEpoch != epoch || lobby.RosterRevision != rosterRevision ||
                    !lobby.TryGetVisual(player, out var current) || !current || !current.activeInHierarchy ||
                    (current.transform != transform && !transform.IsChildOf(current.transform)))
                { ClearSelection(); return; }
                direction = current.transform.forward; // Actual interpolated yaw; lobby does not replicate pitch.
                var snapshot = lobby.LatestSnapshot;
                if (snapshot != null && snapshot.SessionEpoch == epoch && snapshot.RosterRevision == rosterRevision)
                    foreach (var pose in snapshot.Poses)
                    {
                        if (pose.PlayerId == player || !lobby.TryGetVisual(pose.PlayerId, out var other) || !other || !other.activeInHierarchy) continue;
                        var view = other.GetComponentInChildren<CharacterView>();
                        if (view) AddCandidate(other.transform, view, current.transform.position, direction);
                    }
            }
            else { ClearSelection(); return; }

            Transform selected = selection.Select(candidates, Time.unscaledTimeAsDouble);
            if (selected && lookTargets.TryGetValue(selected, out var target) && target)
                attention.SetLookTarget(target);
            else if (Finite(direction) && direction.sqrMagnitude > .001f)
                attention.SetLookPoint(attention.LookOrigin.position + direction.normalized * 8);
            else attention.ClearLookTarget();
        }
        private void AddCandidate(Transform root, CharacterView view, Vector3 origin, Vector3 direction)
        {
            if (!root || root == transform || transform.IsChildOf(root) || !root.gameObject.activeInHierarchy ||
                !Finite(direction) || direction.sqrMagnitude <= .001f) return;
            var facial = view.GetComponent<VisualAttentionRig>();
            Transform target = facial && facial.IsConfigured ? facial.LookOrigin : view.GetAnchor("CameraEye");
            if (!target) target = view.GetAnchor("CameraTarget");
            if (!target || !target.gameObject.activeInHierarchy || !target.IsChildOf(root)) target = root;
            Vector3 toTarget = target.position - attention.LookOrigin.position;
            if (!Finite(toTarget) || toTarget.sqrMagnitude <= .000001f) return;
            // Range uses comparable character roots; cone aims at the face, not human feet.
            float distanceSquared = (root.position - origin).sqrMagnitude;
            float forwardDot = Vector3.Dot(direction.normalized, toTarget.normalized);
            if (!NearestAttentionPolicy<Transform>.IsEligible(distanceSquared, forwardDot)) return;
            candidates.Add(new NearestAttentionPolicy<Transform>.Candidate(root, distanceSquared, forwardDot));
            lookTargets[root] = target;
        }
        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private void ClearSelection()
        {
            selection.Reset(); candidates.Clear(); lookTargets.Clear();
            if (attention) attention.ClearLookTarget();
        }
        private void OnEnable() { if (attention) attention.enabled = true; }
        private void OnDisable() { ClearSelection(); if (attention) attention.enabled = false; }
    }
}
