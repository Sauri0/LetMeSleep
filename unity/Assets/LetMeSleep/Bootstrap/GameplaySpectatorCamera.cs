using System.Linq;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Presentation;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    // Local presentation only. Never changes LocalActorId, controls, ownership or private state.
    [DefaultExecutionOrder(1500)]
    public sealed class GameplaySpectatorCamera : MonoBehaviour
    {
        private GameplayRuntime runtime;
        private Camera cameraView;
        private HumanViewCamera humanCamera;
        private MosquitoFollowCamera mosquitoCamera;
        private bool observing, humanWasEnabled, mosquitoWasEnabled;
        private uint target;
        public void Bind(GameplayRuntime game, Camera view)
        {
            Restore(); runtime = game; cameraView = view;
            humanCamera = view ? view.GetComponent<HumanViewCamera>() : null;
            mosquitoCamera = view ? view.GetComponent<MosquitoFollowCamera>() : null;
        }
        public void NextTarget()
        {
            if (runtime) target = ModeHudText.SpectatorTarget(runtime.LatestSnapshot, runtime.LocalActorId, target, true);
        }
        private void LateUpdate()
        {
            if (!runtime || !cameraView) { Restore(); return; }
            var snapshot = runtime.LatestSnapshot;
            var self = snapshot?.Actors.FirstOrDefault(a => a.ActorId == runtime.LocalActorId);
            if (self == null || !self.Eliminated) { Restore(); return; }
            if (!observing)
            {
                observing = true; humanWasEnabled = humanCamera && humanCamera.enabled; mosquitoWasEnabled = mosquitoCamera && mosquitoCamera.enabled;
            }
            if (humanCamera) humanCamera.enabled = false;
            if (mosquitoCamera) mosquitoCamera.enabled = false;
            target = ModeHudText.SpectatorTarget(snapshot, runtime.LocalActorId, target, false);
            var actor = snapshot.Actors.FirstOrDefault(a => a.ActorId == target);
            if (actor == null) return;
            var look = actor.Position.ToUnity() + Vector3.up * .15f;
            var forward = actor.ViewForward.ToUnity().normalized;
            var offset = -forward * 1.8f + Vector3.up * .65f;
            float distance = offset.magnitude;
            // SphereCastAll avoids choosing own/actor proxies as world obstruction.
            var hits = Physics.SphereCastAll(look, .12f, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => runtime.World.IsWorldCollider(h.collider)).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) distance = Mathf.Max(.05f, hits[0].distance - .06f);
            cameraView.transform.position = look + offset.normalized * distance;
            cameraView.transform.rotation = Quaternion.LookRotation(look - cameraView.transform.position, Vector3.up);
        }
        private void OnDisable() => Restore();
        private void Restore()
        {
            if (observing)
            {
                if (humanCamera) humanCamera.enabled = humanWasEnabled;
                if (mosquitoCamera) mosquitoCamera.enabled = mosquitoWasEnabled;
            }
            observing = false; target = 0;
        }
    }
}
