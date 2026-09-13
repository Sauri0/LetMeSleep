using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    // Stable order for callers/serialization. This module deliberately has no network dependency.
    public enum MosquitoBodyId
    {
        Thorax, Head, Abdomen01, Abdomen02, WingLeft, WingRight,
        Leg1UpperLeft, Leg1LowerLeft, Leg2UpperLeft, Leg2LowerLeft, Leg3UpperLeft, Leg3LowerLeft,
        Leg1UpperRight, Leg1LowerRight, Leg2UpperRight, Leg2LowerRight, Leg3UpperRight, Leg3LowerRight
    }

    public enum MosquitoRagdollMode { Prepared, LocalSimulation, RemotePose, Held, Disposed }

    public readonly struct MosquitoBodyPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        /// <summary>World velocity at the bone origin, in m/s (not at the center of mass).</summary>
        public readonly Vector3 OriginVelocity;
        /// <summary>World angular velocity, in radians/s.</summary>
        public readonly Vector3 AngularVelocity;

        public MosquitoBodyPose(Vector3 position, Quaternion rotation, Vector3 originVelocity, Vector3 angularVelocity)
        {
            RagdollValue.RequireFinite(position, nameof(position));
            RagdollValue.RequireFinite(originVelocity, nameof(originVelocity));
            RagdollValue.RequireFinite(angularVelocity, nameof(angularVelocity));
            Position = position;
            Rotation = RagdollValue.Normalized(rotation);
            OriginVelocity = originVelocity;
            AngularVelocity = angularVelocity;
        }
    }

    public readonly struct MosquitoLocalPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public MosquitoLocalPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            RagdollValue.RequireFinite(position, nameof(position));
            RagdollValue.RequireFinite(scale, nameof(scale));
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), "Positive scale required.");
            Position = position;
            Rotation = RagdollValue.Normalized(rotation);
            Scale = scale;
        }

        internal static MosquitoLocalPose Read(Transform bone) => new MosquitoLocalPose(bone.localPosition, bone.localRotation, bone.localScale);
        internal void Write(Transform bone)
        {
            bone.localPosition = Position;
            bone.localRotation = Rotation;
            bone.localScale = Scale;
        }
    }

    /// <summary>Immutable R4 body world poses plus nonphysical, nonfacial local poses.</summary>
    public sealed class MosquitoRagdollPose
    {
        public const string Contract = "Mosquito-R4-18-v1";
        public const int BodyCount = 18;
        public const int AuxiliaryCount = 15;
        private readonly MosquitoBodyPose[] bodies;
        private readonly MosquitoLocalPose[] auxiliary;

        public MosquitoRagdollPose(MosquitoBodyPose[] bodies, MosquitoLocalPose[] auxiliary)
        {
            if (bodies == null || bodies.Length != BodyCount) throw new ArgumentException("Expected 18 R4 bodies.", nameof(bodies));
            if (auxiliary == null || auxiliary.Length != AuxiliaryCount) throw new ArgumentException("Expected 15 R4 auxiliary bones.", nameof(auxiliary));
            this.bodies = new MosquitoBodyPose[BodyCount];
            this.auxiliary = new MosquitoLocalPose[AuxiliaryCount];
            // Reconstruct to reject default structs, nonfinite data, or malformed remote payloads.
            for (int i = 0; i < BodyCount; i++)
                this.bodies[i] = new MosquitoBodyPose(bodies[i].Position, bodies[i].Rotation, bodies[i].OriginVelocity, bodies[i].AngularVelocity);
            for (int i = 0; i < AuxiliaryCount; i++)
                this.auxiliary[i] = new MosquitoLocalPose(auxiliary[i].Position, auxiliary[i].Rotation, auxiliary[i].Scale);
        }

        public MosquitoBodyPose GetBody(MosquitoBodyId body) => bodies[RagdollValue.Index(body)];
        public MosquitoLocalPose GetAuxiliary(int index) => auxiliary[index];
        public static string GetBoneName(MosquitoBodyId body) => MosquitoRagdollBuilder.BodyNames[RagdollValue.Index(body)];
        public static string GetAuxiliaryName(int index) => MosquitoRagdollBuilder.AuxiliaryNames[index];
    }

    /// <summary>Per-ragdoll settings. Gravity and collision layer are deliberate caller choices.</summary>
    public sealed class MosquitoRagdollSettings
    {
        public Vector3 Gravity { get; }
        public int CollisionLayer { get; }
        public float MassScale { get; }
        public float ContactOffset { get; }
        public float SettleLinearSpeed { get; }
        public float SettleAngularSpeed { get; }
        public float SettleSeconds { get; }
        public int SolverIterations { get; }
        public int SolverVelocityIterations { get; }

        public MosquitoRagdollSettings(Vector3 gravity, int collisionLayer, float massScale = 1f,
            float contactOffset = .0005f, float settleLinearSpeed = .02f, float settleAngularSpeed = .3f,
            float settleSeconds = .35f, int solverIterations = 12, int solverVelocityIterations = 4)
        {
            RagdollValue.RequireFinite(gravity, nameof(gravity));
            if (collisionLayer < 0 || collisionLayer > 31) throw new ArgumentOutOfRangeException(nameof(collisionLayer));
            RagdollValue.RequirePositive(massScale, nameof(massScale));
            RagdollValue.RequirePositive(contactOffset, nameof(contactOffset));
            RagdollValue.RequirePositive(settleLinearSpeed, nameof(settleLinearSpeed));
            RagdollValue.RequirePositive(settleAngularSpeed, nameof(settleAngularSpeed));
            RagdollValue.RequirePositive(settleSeconds, nameof(settleSeconds));
            if (solverIterations < 1 || solverIterations > 255 || solverVelocityIterations < 1 || solverVelocityIterations > 255)
                throw new ArgumentOutOfRangeException(nameof(solverIterations));
            Gravity = gravity; CollisionLayer = collisionLayer; MassScale = massScale; ContactOffset = contactOffset;
            SettleLinearSpeed = settleLinearSpeed; SettleAngularSpeed = settleAngularSpeed; SettleSeconds = settleSeconds;
            SolverIterations = solverIterations; SolverVelocityIterations = solverVelocityIterations;
        }
    }

    internal static class RagdollValue
    {
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static void RequireFinite(Vector3 v, string name)
        {
            if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) throw new ArgumentException("Finite vector required.", name);
        }
        internal static void RequirePositive(float v, string name)
        {
            if (!Finite(v) || v <= 0) throw new ArgumentOutOfRangeException(name);
        }
        internal static Quaternion Normalized(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (!Finite(n) || n < 1e-10f) throw new ArgumentException("Finite nonzero rotation required.");
            float s = 1f / Mathf.Sqrt(n);
            return new Quaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }
        internal static int Index(MosquitoBodyId id)
        {
            int i = (int)id;
            if (i < 0 || i >= MosquitoRagdollPose.BodyCount) throw new ArgumentOutOfRangeException(nameof(id));
            return i;
        }
    }
}
