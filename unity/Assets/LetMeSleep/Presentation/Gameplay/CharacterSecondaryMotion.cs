using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Cartoon secondary motion written on already evaluated bones (ActorVisualBinding calls Apply after the
    /// base pose and before hand IK/anchors/bite contact, so every later writer sees the final skeleton).
    /// Squash and stretch keeps volume (sy = 1 + s, sx = sz = 1 / sqrt(1 + s)) about the character origin;
    /// the mosquito also gets a roll wobble when hit and a filling abdomen while feeding; the human gait gets
    /// pelvis roll/sway with planted feet, shoulder counter-rotation and elbow flexion synced to the gait phase.
    /// Presentation only: never moves the visual root, hit volumes or anything the authority reads.
    /// </summary>
    public sealed class CharacterSecondaryMotion
    {
        public struct Input
        {
            public float DeltaSeconds;
            public bool GaitActive;
            public double GaitPhase;
            public float PlanarSpeed;
            public bool Biting;
            public float BitingSeconds;
            /// <summary>Local first-person human: the eye rides on the head, so nothing may move the torso.</summary>
            public bool CameraOnBody;
        }

        private const float SquashHz = 3.5f, SquashDamping = .45f, WobbleHz = 4.5f, WobbleDamping = .25f;
        private const float MaximumSquash = .32f;
        private readonly PlayerRole role;
        private readonly Transform actor, root, hips, spine, chest, abdomen;
        private readonly Transform[] upperLeg = new Transform[2], lowerLeg = new Transform[2], foot = new Transform[2];
        private readonly Transform[] upperArm = new Transform[2], lowerArm = new Transform[2], hand = new Transform[2];
        private readonly Vector3 rootRestScale, abdomenRestScale;
        private readonly Quaternion rootRestRotation;
        private readonly int upAxis = -1;
        private float squash, squashVelocity, wobble, wobbleVelocity, gaitWeight, abdomenFill;

        public float Squash => squash;
        public float Wobble => wobble;
        public float GaitWeight => gaitWeight;
        public float AbdomenFill => abdomenFill;
        public bool SupportsSquash => root && upAxis >= 0;
        public bool SupportsGait => hips && spine && chest && upperLeg[0] && upperLeg[1] && lowerLeg[0] && lowerLeg[1] && foot[0] && foot[1];

        public CharacterSecondaryMotion(PlayerRole actorRole, Transform actorTransform, Transform rigRoot)
        {
            role = actorRole; actor = actorTransform;
            if (!actor || !rigRoot) return;
            root = Find(rigRoot, "Root");
            if (root)
            {
                rootRestScale = root.localScale; rootRestRotation = root.localRotation;
                Vector3 up = root.InverseTransformDirection(actor.up);
                for (int i = 0; i < 3; i++) if (Mathf.Abs(up[i]) > .9f) upAxis = i;
            }
            if (role == PlayerRole.Human)
            {
                hips = Find(rigRoot, "Hips"); spine = Find(rigRoot, "Spine"); chest = Find(rigRoot, "Chest");
                for (int i = 0; i < 2; i++)
                {
                    string side = i == 0 ? "L" : "R";
                    upperLeg[i] = Find(rigRoot, "UpperLeg." + side); lowerLeg[i] = Find(rigRoot, "LowerLeg." + side);
                    foot[i] = Find(rigRoot, "Foot." + side);
                    upperArm[i] = Find(rigRoot, "UpperArm." + side); lowerArm[i] = Find(rigRoot, "LowerArm." + side);
                    hand[i] = Find(rigRoot, "Hand." + side);
                }
            }
            else
            {
                abdomen = Find(rigRoot, "Abdomen01");
                if (abdomen) abdomenRestScale = abdomen.localScale;
            }
        }

        /// <summary>Positive stretches, negative squashes. A kick against the current deformation replaces it
        /// (a landing always reads as a squash, even right after a take-off stretch); same-sign kicks add up.</summary>
        public void Kick(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount == 0) return;
            if (Mathf.Sign(amount) != Mathf.Sign(squash)) { squash = 0; squashVelocity = 0; }
            squash = Mathf.Clamp(squash + amount, -MaximumSquash, MaximumSquash);
        }

        /// <summary>Cartoon roll shake (degrees) about the actor forward axis.</summary>
        public void KickWobble(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return;
            wobbleVelocity += degrees * 2 * Mathf.PI * WobbleHz;
        }

        public void Reset()
        {
            squash = squashVelocity = wobble = wobbleVelocity = gaitWeight = abdomenFill = 0;
            if (root) { root.localScale = rootRestScale; root.localRotation = rootRestRotation; }
            if (abdomen) abdomen.localScale = abdomenRestScale;
        }

        public void Apply(in Input input)
        {
            if (!actor) return;
            float dt = input.DeltaSeconds;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) dt = 0;
            dt = Mathf.Min(dt, .1f);
            // Semi-implicit Euler in <=1/60 s steps stays stable for both springs.
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 60f)));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Integrate(ref squash, ref squashVelocity, SquashHz, SquashDamping, step);
                Integrate(ref wobble, ref wobbleVelocity, WobbleHz, WobbleDamping, step);
            }
            squash = Mathf.Clamp(squash, -MaximumSquash, MaximumSquash);
            wobble = Mathf.Clamp(wobble, -45f, 45f);
            ApplyRoot(input.CameraOnBody);
            if (role == PlayerRole.Mosquito) ApplyAbdomen(input, dt);
            else ApplyGait(input, dt);
        }

        private static void Integrate(ref float value, ref float velocity, float hz, float damping, float dt)
        {
            float omega = 2 * Mathf.PI * hz;
            velocity += (-omega * omega * value - 2 * damping * omega * velocity) * dt;
            value += velocity * dt;
            if (float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(velocity) || float.IsInfinity(velocity))
                value = velocity = 0;
        }

        private void ApplyRoot(bool cameraOnBody)
        {
            if (!root) return;
            if (cameraOnBody) { squash = squashVelocity = wobble = wobbleVelocity = 0; }
            var scale = Vector3.one;
            if (upAxis >= 0)
            {
                float side = 1 / Mathf.Sqrt(1 + squash);
                scale = new Vector3(side, side, side); scale[upAxis] = 1 + squash;
            }
            root.localScale = Vector3.Scale(rootRestScale, scale);
            root.localRotation = rootRestRotation;
            if (Mathf.Abs(wobble) > .01f)
                root.rotation = Quaternion.AngleAxis(wobble, actor.forward) * root.rotation;
        }

        private void ApplyAbdomen(in Input input, float dt)
        {
            if (!abdomen) return;
            // Blood fills the abdomen while feeding and drains slowly afterwards.
            float target = input.Biting ? Mathf.Clamp01(input.BitingSeconds / 3f) : 0;
            abdomenFill = Mathf.MoveTowards(abdomenFill, target, dt * (input.Biting ? 1f : .4f));
            float pulse = input.Biting ? .035f * Mathf.Sin(input.BitingSeconds * 2 * Mathf.PI * 2.2f) : 0;
            abdomen.localScale = abdomenRestScale * (1 + .22f * abdomenFill + pulse);
        }

        private void ApplyGait(in Input input, float dt)
        {
            gaitWeight = Mathf.MoveTowards(gaitWeight, input.GaitActive ? Mathf.Clamp01(input.PlanarSpeed / 1.2f) : 0, dt * 6);
            if (gaitWeight <= .001f || !SupportsGait) return;
            float phase = (float)(input.GaitPhase % 1.0) * 2 * Mathf.PI;
            if (float.IsNaN(phase) || float.IsInfinity(phase)) return;
            Vector3 forward = actor.forward, up = actor.up;
            // Arms first: their swing (from the clip) drives the shoulder counter-rotation.
            float swing = 0;
            if (hand[0] && hand[1]) swing = Mathf.Clamp(Vector3.Dot(hand[1].position - hand[0].position, forward) / .45f, -1, 1);
            for (int i = 0; i < 2; i++)
            {
                if (!upperArm[i] || !lowerArm[i] || !hand[i]) continue;
                // 18-30 deg total elbow flexion over the cycle: the arm swinging forward bends more.
                float forwardness = Mathf.Clamp(Vector3.Dot(hand[i].position - upperArm[i].position, forward) / .25f, -1, 1);
                Vector3 direction = hand[i].position - lowerArm[i].position;
                Vector3 axis = Vector3.Cross(direction, forward);
                if (axis.sqrMagnitude < 1e-8f) continue;
                lowerArm[i].rotation = Quaternion.AngleAxis(gaitWeight * (8f + 6f * forwardness), axis.normalized) * lowerArm[i].rotation;
            }
            if (input.CameraOnBody) return;
            // Pelvis rolls toward the stance leg (L stance around phase .25) and sways over it; the feet stay put.
            Vector3 left = upperLeg[0].position - upperLeg[1].position;
            left = Vector3.ProjectOnPlane(left, up);
            if (left.sqrMagnitude < 1e-6f) return;
            left.Normalize();
            float wave = Mathf.Sin(phase);
            float roll = -4f * gaitWeight * wave;
            Vector3 sway = left * (.025f * gaitWeight * wave);
            Vector3 footPosition0 = foot[0].position, footPosition1 = foot[1].position;
            Quaternion footRotation0 = foot[0].rotation, footRotation1 = foot[1].rotation;
            hips.position += sway;
            hips.rotation = Quaternion.AngleAxis(roll, forward) * hips.rotation;
            // Torso stays upright over the rolled pelvis; shoulders counter-rotate against the arm swing.
            spine.rotation = Quaternion.AngleAxis(-roll * .8f, forward) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(-6f * gaitWeight * swing, up) * chest.rotation;
            TwoBoneSolver.Solve(upperLeg[0], lowerLeg[0], foot[0], footPosition0, forward);
            TwoBoneSolver.Solve(upperLeg[1], lowerLeg[1], foot[1], footPosition1, forward);
            foot[0].rotation = footRotation0; foot[1].rotation = footRotation1;
        }

        private static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found) return found;
            }
            return null;
        }
    }
}
