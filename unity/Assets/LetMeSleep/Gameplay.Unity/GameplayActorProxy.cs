using System.Collections.Generic;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed class GameplayActorProxy : MonoBehaviour
    {
        public uint ActorId { get; private set; }
        public PlayerRole Role { get; private set; }
        public Collider MotorCollider { get; private set; }
        public ActorSnapshot State { get; private set; }
        public readonly Dictionary<uint, GameplayBodySurface> BodySurfaces = new Dictionary<uint, GameplayBodySurface>();
        public void Initialize(SpawnActor actor)
        {
            ActorId = actor.ActorId; Role = actor.Role; transform.position = actor.Position.ToUnity();
            if (Role == PlayerRole.Human)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>(); capsule.radius = .25f; capsule.height = 1.72f; capsule.center = Vector3.up * .86f; MotorCollider = capsule;
                CreateBody(1, "Torso", new Vector3(0, 1.19f, 0), .18f, .56f);
                CreateBody(2, "Head", new Vector3(0, 1.57f, 0), .13f, .26f);
                CreateBody(3, "UpperArm.L", new Vector3(-.25f, 1.26f, 0), .065f, .28f);
                CreateBody(4, "UpperArm.R", new Vector3(.25f, 1.26f, 0), .065f, .28f);
                CreateBody(5, "Forearm.L", new Vector3(-.26f, 1.0f, .04f), .055f, .28f);
                CreateBody(6, "Forearm.R", new Vector3(.26f, 1.0f, .04f), .055f, .28f);
                CreateBody(7, "Thigh.L", new Vector3(-.11f, .69f, 0), .10f, .4f);
                CreateBody(8, "Thigh.R", new Vector3(.11f, .69f, 0), .10f, .4f);
                CreateBody(9, "Shin.L", new Vector3(-.11f, .28f, 0), .073f, .42f);
                CreateBody(10, "Shin.R", new Vector3(.11f, .28f, 0), .073f, .42f);
            }
            else { var sphere = gameObject.AddComponent<SphereCollider>(); sphere.radius = .055f; MotorCollider = sphere; }
        }
        private void CreateBody(uint localId, string part, Vector3 position, float radius, float height)
        {
            var child = new GameObject(part); child.transform.SetParent(transform, false); child.transform.localPosition = position;
            var collider = child.AddComponent<CapsuleCollider>(); collider.radius = radius; collider.height = height; collider.isTrigger = true;
            var surface = child.AddComponent<GameplayBodySurface>(); surface.Actor = this; surface.SurfaceId = ActorId * 100 + localId; surface.Collider = collider; surface.RestPosition = position; surface.PartId = localId;
            BodySurfaces.Add(surface.SurfaceId, surface);
        }
        public void Apply(ActorSnapshot state)
        {
            State = state; transform.SetPositionAndRotation(state.Position.ToUnity(), state.BodyRotation.ToUnity());
            if (MotorCollider is CapsuleCollider capsule)
            {
                capsule.height = 1.72f - .72f * state.CrouchFraction; capsule.center = Vector3.up * (capsule.height * .5f);
                foreach (var surface in BodySurfaces.Values)
                {
                    if (surface.PartId == 1)
                    {
                        surface.transform.localPosition = new Vector3(0, 1.19f - .50f * state.CrouchFraction, 0);
                        surface.Collider.height = .56f - .20f * state.CrouchFraction;
                    }
                    else if (surface.PartId == 2) surface.transform.localPosition = new Vector3(0, 1.57f - .68f * state.CrouchFraction, .03f * state.CrouchFraction);
                }
                PoseLimbs(state);
            }
        }
        private void PoseLimbs(ActorSnapshot state)
        {
            float cycle = state.MotionPhase * Mathf.PI * 2, speed = Mathf.Min(1, state.Velocity.Length / 3.1f), crouch = state.CrouchFraction;
            for (int side = -1; side <= 1; side += 2)
            {
                float stride = Mathf.Sin(cycle + (side == 1 ? Mathf.PI : 0)) * speed;
                var hip = new Vector3(side * .11f, .90f - .47f * crouch, -.05f * crouch);
                var ankle = new Vector3(side * .11f, .08f + Mathf.Max(0, stride) * .045f, stride * .16f);
                var knee = BendJoint(hip, ankle, .41f, .41f, Vector3.forward);
                SetSegment(side < 0 ? 7u : 8u, hip, knee);
                SetSegment(side < 0 ? 9u : 10u, knee, ankle);
                var shoulder = new Vector3(side * .23f, 1.39f - .57f * crouch, 0);
                var wrist = shoulder + new Vector3(side * .025f, -.49f, .055f - stride * .13f);
                if (state.StrikeState.Phase != StrikePhase.None && state.StrikeState.Hand == side)
                {
                    var target = transform.InverseTransformPoint(state.StrikeState.Target.ToUnity());
                    float p = state.StrikeState.Progress;
                    float extension = p < .42f ? Mathf.SmoothStep(0, 1, p / .42f) : 1 - Mathf.SmoothStep(0, 1, (p - .42f) / .58f);
                    wrist = Vector3.Lerp(wrist, target, extension);
                }
                wrist = shoulder + Vector3.ClampMagnitude(wrist - shoulder, .55f);
                var elbow = BendJoint(shoulder, wrist, .28f, .28f, new Vector3(side, 0, -.3f));
                SetSegment(side < 0 ? 3u : 4u, shoulder, elbow);
                SetSegment(side < 0 ? 5u : 6u, elbow, wrist);
            }
        }
        private void SetSegment(uint part, Vector3 from, Vector3 to)
        {
            var surface = BodySurfaces[ActorId * 100 + part];
            surface.transform.localPosition = (from + to) * .5f;
            surface.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            surface.Collider.height = (to - from).magnitude + surface.Collider.radius * 2;
        }
        private static Vector3 BendJoint(Vector3 root, Vector3 end, float upper, float lower, Vector3 pole)
        {
            var delta = end - root; float length = Mathf.Clamp(delta.magnitude, .001f, upper + lower - .0001f); var axis = delta.normalized;
            var bend = Vector3.ProjectOnPlane(pole, axis).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.Cross(axis, Vector3.right).normalized;
            float along = (upper * upper - lower * lower + length * length) / (2 * length);
            return root + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        }
    }
}
