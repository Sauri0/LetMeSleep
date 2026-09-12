using System.Collections.Generic;

namespace LetMeSleep.Gameplay
{
    public readonly struct MotorQuery
    {
        public readonly uint ActorId;
        public readonly Float3 Position, Velocity;
        public readonly float DeltaSeconds, Height, Radius, CrouchFraction;
        public readonly bool WasGrounded, AllowStep;
        public MotorQuery(uint actor, Float3 position, Float3 velocity, float dt, float height, float radius, float crouch, bool grounded, bool step = true)
        { ActorId = actor; Position = position; Velocity = velocity; DeltaSeconds = dt; Height = height; Radius = radius; CrouchFraction = crouch; WasGrounded = grounded; AllowStep = step; }
    }
    public readonly struct MotorResult
    {
        public readonly Float3 Position, Velocity, GroundNormal;
        public readonly bool Grounded;
        public readonly float CrouchFraction;
        public MotorResult(Float3 position, Float3 velocity, bool grounded, Float3 normal, float crouch = 0)
        { Position = position; Velocity = velocity; Grounded = grounded; GroundNormal = normal; CrouchFraction = crouch; }
    }
    public readonly struct SurfaceQuery
    {
        public readonly uint ActorId;
        public readonly Float3 Position, Direction;
        public readonly float Reach;
        public SurfaceQuery(uint actor, Float3 position, Float3 direction, float reach) { ActorId = actor; Position = position; Direction = direction; Reach = reach; }
    }
    public readonly struct SurfaceContact
    {
        public readonly SurfaceAttachment Attachment;
        public readonly Float3 WorldPoint, WorldNormal;
        public SurfaceContact(SurfaceAttachment attachment, Float3 point, Float3 normal) { Attachment = attachment; WorldPoint = point; WorldNormal = normal; }
    }
    public readonly struct BiteQuery
    {
        public readonly uint ActorId;
        public readonly Float3 Position, AimForward;
        public readonly float Reach;
        public readonly int ActiveHumanCount;
        public BiteQuery(uint actor, Float3 position, Float3 aim, float reach, int humanCount) { ActorId = actor; Position = position; AimForward = aim; Reach = reach; ActiveHumanCount = humanCount; }
    }
    public readonly struct BiteContact
    {
        public readonly BiteAttachment Attachment;
        public readonly Float3 MosquitoPosition, WorldNormal;
        public BiteContact(BiteAttachment attachment, Float3 position, Float3 normal) { Attachment = attachment; MosquitoPosition = position; WorldNormal = normal; }
    }
    public readonly struct StrikePlan
    {
        public readonly Float3 Origin, Target, Normal;
        public readonly float Radius;
        public readonly int Hand;
        public readonly string ToolId;
        public StrikePlan(Float3 origin, Float3 target, Float3 normal, float radius, int hand, string tool) { Origin = origin; Target = target; Normal = normal; Radius = radius; Hand = hand; ToolId = tool; }
    }
    public readonly struct StrikeSweep
    {
        public readonly uint ActorId;
        public readonly ulong StrikeId;
        public readonly Float3 From, To;
        public readonly float Radius;
        public StrikeSweep(uint actor, ulong id, Float3 from, Float3 to, float radius) { ActorId = actor; StrikeId = id; From = from; To = to; Radius = radius; }
    }
    public readonly struct StrikeHit
    {
        public readonly bool Hit;
        public readonly uint ActorId;
        public readonly Float3 Point, Normal;
        public StrikeHit(bool hit, uint actor, Float3 point, Float3 normal) { Hit = hit; ActorId = actor; Point = point; Normal = normal; }
    }
    public readonly struct DoorInteractionQuery
    {
        public readonly uint ActorId, HostTick;
        public readonly Float3 EyeOrigin, AimForward;
        public readonly float Reach;
        public DoorInteractionQuery(uint actor, uint tick, Float3 origin, Float3 aim, float reach) { ActorId = actor; HostTick = tick; EyeOrigin = origin; AimForward = aim; Reach = reach; }
    }
    public readonly struct DoorInteractionCandidate
    {
        public readonly uint DoorId, DoorRevision;
        public readonly Float3 HitPoint;
        public readonly float Distance;
        public DoorInteractionCandidate(uint id, uint revision, Float3 point, float distance) { DoorId = id; DoorRevision = revision; HitPoint = point; Distance = distance; }
    }
    public readonly struct DoorMotionQuery
    {
        public readonly uint DoorId, DoorRevision, HostTick;
        public readonly float FromAngleRadians, ToAngleRadians;
        public DoorMotionQuery(uint id, uint revision, uint tick, float from, float to) { DoorId = id; DoorRevision = revision; HostTick = tick; FromAngleRadians = from; ToAngleRadians = to; }
    }
    public readonly struct DoorSweepResult
    {
        public readonly float SafeAngleRadians;
        public readonly bool Blocked;
        public readonly uint BlockingActorId;
        public DoorSweepResult(float angle, bool blocked, uint actor = 0) { SafeAngleRadians = angle; Blocked = blocked; BlockingActorId = actor; }
    }
    public readonly struct DoorPose
    {
        public readonly uint DoorId, Revision, HostTick;
        public readonly float AngleRadians;
        public DoorPose(uint id, uint revision, uint tick, float angle) { DoorId = id; Revision = revision; HostTick = tick; AngleRadians = angle; }
    }
    // World owns only collision/kinematic proxies. It never applies damage or life transitions.
    public interface IGameplayWorld
    {
        void BeginRound(IReadOnlyList<SpawnActor> actors, IReadOnlyList<DoorDefinition> doors);
        void SynchronizeActors(IReadOnlyList<ActorSnapshot> actors);
        MotorResult MoveHuman(in MotorQuery query);
        MotorResult MoveMosquito(in MotorQuery query);
        bool TrySurface(in SurfaceQuery query, out SurfaceContact contact);
        bool ResolveSurface(in SurfaceAttachment attachment, out SurfaceContact contact);
        bool TryBiteContact(in BiteQuery query, out BiteContact contact);
        bool ResolveBite(uint mosquitoId, in BiteAttachment attachment, int humanCount, out BiteContact contact);
        bool TryPlanStrike(uint actorId, Float3 aim, string toolId, out StrikePlan plan);
        StrikeHit SweepStrike(in StrikeSweep query);
        bool TryFreeRecoveryPoint(uint actorId, Float3 position, out Float3 point);
        bool HasLineOfSight(uint actorId, Float3 from, uint targetActorId, Float3 to);
        bool TryDoorInteraction(in DoorInteractionQuery query, out DoorInteractionCandidate candidate);
        DoorSweepResult SweepDoor(in DoorMotionQuery query);
        void ApplyDoorPose(in DoorPose pose);
    }
}
