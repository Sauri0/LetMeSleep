using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

// External, synchronous real-PhysX checks for Director's slot. No rendering/WAN claim.
public static class SurfaceTraversalNativeChecks
{
    [Serializable] public sealed class Sample { public string label; public uint oldId, newId; public Vector3 normal; public float distance; }
    [Serializable] public sealed class Result { public int cases, failures; public List<string> outcomes = new List<string>(); public List<Sample> trace = new List<Sample>(); }
    public static string RunAll()
    {
        var r = new Result();
        Run(r, "concave_floor_wall_and_rotated_corner", f => {
            foreach (float angle in new[] { 0f, 27f })
            {
                f.Map.rotation = Quaternion.Euler(0, 35, angle); Physics.SyncTransforms();
                var origin = f.Map.TransformPoint(new Vector3(0, .057f, .39f));
                var old = f.Acquire(origin, -f.Map.up);
                Check(f.World.TryFollowSurface(2, old.Attachment, origin.ToFloat(), f.Map.forward.ToFloat(), out var next), "No adjacent front support");
                Check(next.Attachment.SurfaceId == 2, "Did not select wall");
                Check(Vector3.Dot(next.WorldNormal.ToUnity(), -f.Map.forward) > .999f, "Wrong physical normal");
                Trace(r, "front", old, next, origin);
            }
        });
        Run(r, "convex_box_edge_wrap", f => {
            f.Wall.gameObject.SetActive(false); f.Ceiling.gameObject.SetActive(false);
            f.Floor.center = new Vector3(-.5f, -.5f, 0); f.Floor.size = Vector3.one;
            Physics.SyncTransforms();
            var old = f.Acquire(new Vector3(-.01f, .057f, 0), Vector3.down);
            var origin = new Vector3(.02f, .057f, 0);
            Check(f.World.TryFollowSurface(2, old.Attachment, origin.ToFloat(), Vector3.right.ToFloat(), out var next), "No convex side support");
            Check(Vector3.Dot(next.WorldNormal.ToUnity(), Vector3.right) > .999f, "Convex readback normal wrong");
            Trace(r, "wrap", old, next, origin);
        });
        Run(r, "gap_and_nonperch_blocker_do_not_become_neighbors", f => {
            f.Floor.center = new Vector3(0, -.05f, 0); f.Floor.size = new Vector3(2, .1f, .8f); // ends z=.4, wall starts .45
            Physics.SyncTransforms();
            var origin = new Vector3(0, .057f, .39f); var old = f.Acquire(origin, Vector3.down);
            Check(f.World.TryFollowSurface(2, old.Attachment, origin.ToFloat(), Vector3.forward.ToFloat(), out var next) && next.Attachment.SurfaceId == 1,
                "A 5 cm gap was treated as a join");
            f.Floor.center = new Vector3(0, -.05f, 0); f.Floor.size = new Vector3(2, .1f, 2);
            f.Wall.GetComponent<GameplaySurface>().CanPerch = false; Physics.SyncTransforms();
            Check(f.World.TryFollowSurface(2, old.Attachment, origin.ToFloat(), Vector3.forward.ToFloat(), out next) && next.Attachment.SurfaceId == 1,
                "Non-perch wall became support");
        });
        Run(r, "exposed_edge_does_not_snap_to_distant_object", f => {
            f.Wall.gameObject.SetActive(false); f.Ceiling.gameObject.SetActive(false);
            f.Floor.center = new Vector3(-.5f, -.05f, 0); f.Floor.size = new Vector3(1, .1f, 2);
            var other = f.Box("FarBox", 4, new Vector3(.7f, -.5f, 0), Vector3.one * .3f);
            f.World.RegisterGeometry(); Physics.SyncTransforms();
            var old = f.Acquire(new Vector3(-.01f, .057f, 0), Vector3.down);
            Check(!f.World.TryFollowSurface(2, old.Attachment, new Float3(.18f, .057f, 0), Float3.Forward, out _), "Distant object/edge attracted the actor");
        });
        Run(r, "host_motor_floor_wall_ceiling_and_reverse", f => {
            f.HostPath(r, false, false);
            f.HostPath(r, true, false);
        });
        Run(r, "host_motor_rounds_convex_box_edge", f => f.HostConvex(r));
        Run(r, "F_cancels_real_host_edge_approach", f => f.HostPath(r, false, true));
        Run(r, "translated_rotated_disabled_support", f => {
            var origin = new Vector3(0, .057f, 0); var old = f.Acquire(origin, Vector3.down);
            f.Floor.transform.SetPositionAndRotation(new Vector3(.02f, 0, 0), Quaternion.Euler(0, 0, 15));
            Physics.SyncTransforms();
            Check(f.World.ResolveSurface(old.Attachment, out var moved), "Moving support lost identity");
            Vector3 expected = f.Floor.transform.TransformPoint(old.Attachment.LocalPoint.ToUnity());
            Check(Vector3.Distance(expected, moved.WorldPoint.ToUnity()) < .0001f, "Local point did not follow object");
            Vector3 center = moved.WorldPoint.ToUnity() + moved.WorldNormal.ToUnity() * .057f;
            Check(f.World.TryFollowSurface(2, moved.Attachment, center.ToFloat(), Float3.Zero, out var next), "Moving support not followed");
            Trace(r, "moving", old, next, center);
            f.Floor.enabled = false;
            Check(!f.World.ResolveSurface(next.Attachment, out _), "Disabled collider retained attachment");
        });
        return JsonUtility.ToJson(r, true);
    }
    private static void Run(Result r, string name, Action<Fixture> test)
    {
        r.cases++;
        try { using (var f = new Fixture()) test(f); r.outcomes.Add("PASS " + name); }
        catch (Exception e) { r.failures++; r.outcomes.Add("FAIL " + name + ": " + e); }
    }
    private static void Trace(Result r, string label, SurfaceContact old, SurfaceContact next, Vector3 origin)
    { r.trace.Add(new Sample { label = label, oldId = old.Attachment.SurfaceId, newId = next.Attachment.SurfaceId, normal = next.WorldNormal.ToUnity(), distance = Vector3.Distance(origin, next.WorldPoint.ToUnity() + next.WorldNormal.ToUnity() * .057f) }); }
    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("SurfaceTraversalChecks-owned");
        public readonly UnityGameplayWorld World;
        public readonly Transform Map;
        public readonly BoxCollider Floor, Wall, Ceiling;
        public Fixture()
        {
            World = root.AddComponent<UnityGameplayWorld>();
            Map = new GameObject("Map").transform; Map.SetParent(root.transform, false); World.MapRoot = Map;
            Floor = Box("Floor", 1, new Vector3(0, -.05f, 0), new Vector3(2, .1f, 2));
            Wall = Box("Wall", 2, new Vector3(0, .5f, .5f), new Vector3(2, 1, .1f));
            Ceiling = Box("Ceiling", 3, new Vector3(0, 1.05f, 0), new Vector3(2, .1f, 2));
            World.BeginRound(Roster(new Vector3(0, .057f, 0)), Array.Empty<DoorDefinition>());
            Physics.SyncTransforms();
        }
        public BoxCollider Box(string name, uint id, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(Map, false);
            var box = go.AddComponent<BoxCollider>(); box.center = center; box.size = size;
            go.AddComponent<GameplaySurface>().SurfaceId = id; return box;
        }
        public SurfaceContact Acquire(Vector3 origin, Vector3 normalRay)
        {
            Check(World.TrySurface(new SurfaceQuery(2, origin.ToFloat(), normalRay.ToFloat(), .12f), out var contact), "Cannot acquire initial support");
            return contact;
        }
        public void HostPath(Result result, bool reverse, bool cancel)
        {
            // Retire fixture/previous path actors explicitly: avoid deferred edit-mode destruction.
            foreach (var actor in World.Actors.Values.ToArray()) Object.DestroyImmediate(actor.gameObject);
            ((IDictionary<uint, GameplayActorProxy>)World.Actors).Clear(); // Only this fixture's private world.
            var a = new GameplayAuthority(World);
            a.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "native-traversal", 300, 100),
                Roster(new Vector3(0, reverse ? .943f : .057f, 0)));
            uint inputSequence = 0;
            Send(a, ++inputSequence, 0, reverse ? 89 * Mathf.Deg2Rad : -90 * Mathf.Deg2Rad, false);
            var self = Self(a);
            a.SubmitAction("m", new PlayerActionCommand(new CommandHeader(1, 1, 2, 1, a.CurrentTick, self.ViewRevision), ActionKind.PerchToggle, self.ViewForward));
            a.Advance(new HostTick(a.CurrentTick + 1));
            uint wanted = reverse ? 1u : 3u;
            for (int i = 0; i < 150; i++)
            {
                self = Self(a);
                Check(self.SurfaceAttachment.HasValue, "Unexpected detach at step " + i + " position " + self.Position.ToUnity());
                uint oldId = self.SurfaceAttachment.Value.SurfaceId;
                if (cancel && oldId == 2 && self.LifeState == LifeState.ApproachingSurface)
                {
                    a.SubmitAction("m", new PlayerActionCommand(new CommandHeader(1, 1, 2, 2, a.CurrentTick, self.ViewRevision), ActionKind.PerchToggle, self.ViewForward));
                    a.Advance(new HostTick(a.CurrentTick + 1));
                    Check(!Self(a).SurfaceAttachment.HasValue, "F did not cancel edge approach"); return;
                }
                if (!cancel && oldId == wanted && self.LifeState == LifeState.Surface) return;
                float yaw = oldId == wanted ? Mathf.PI : 0;
                float pitch = oldId == 2 ? (reverse ? -89 : 89) * Mathf.Deg2Rad : 0;
                Send(a, ++inputSequence, yaw, pitch, true);
                var before = self.Position; a.Advance(new HostTick(a.CurrentTick + 1));
                var after = Self(a);
                Check((after.Position - before).Length < .04f, "Motor step exceeded bounded edge travel");
                if (after.SurfaceAttachment.HasValue && after.SurfaceAttachment.Value.SurfaceId != oldId)
                {
                    World.ResolveSurface(after.SurfaceAttachment.Value, out var next);
                    result.trace.Add(new Sample { label = reverse ? "host-reverse" : "host-forward", oldId = oldId, newId = next.Attachment.SurfaceId, normal = next.WorldNormal.ToUnity(), distance = (after.Position - before).Length });
                }
            }
            throw new InvalidOperationException("Host path did not finish");
        }
        public void HostConvex(Result result)
        {
            Wall.gameObject.SetActive(false); Ceiling.gameObject.SetActive(false);
            Floor.center = new Vector3(-.5f, -.5f, 0); Floor.size = Vector3.one;
            foreach (var actor in World.Actors.Values.ToArray()) Object.DestroyImmediate(actor.gameObject);
            ((IDictionary<uint, GameplayActorProxy>)World.Actors).Clear();
            var a = new GameplayAuthority(World);
            a.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "native-convex", 300, 100), Roster(new Vector3(-.12f, .057f, 0)));
            uint sequence = 1;
            Send(a, sequence, 0, -90 * Mathf.Deg2Rad, false);
            a.SubmitAction("m", new PlayerActionCommand(new CommandHeader(1, 1, 2, 1, 0, Self(a).ViewRevision), ActionKind.PerchToggle, Self(a).ViewForward));
            a.Advance(new HostTick(1));
            for (int i = 0; i < 60; i++)
            {
                var self = Self(a);
                Check(self.SurfaceAttachment.HasValue, "Convex path detached at " + self.Position.ToUnity());
                World.ResolveSurface(self.SurfaceAttachment.Value, out var support);
                bool side = support.WorldNormal.X > .9f;
                if (side && self.LifeState == LifeState.Surface)
                {
                    Check(self.Position.Y < -.01f && Mathf.Abs(self.Position.X - .057f) < .009f, "Convex arrival is not on side face");
                    result.trace.Add(new Sample { label = "host-convex", oldId = 1, newId = 1, normal = support.WorldNormal.ToUnity(), distance = (self.Position - support.WorldPoint).Length }); return;
                }
                Send(a, ++sequence, side ? 0 : Mathf.PI / 2, side ? -89 * Mathf.Deg2Rad : 0, true);
                a.Advance(new HostTick(a.CurrentTick + 1));
                Check((Self(a).Position - self.Position).Length < .04f, "Convex path exceeded motor step bound");
            }
            throw new InvalidOperationException("Convex path did not finish");
        }
        private static SpawnActor[] Roster(Vector3 mosquito) => new[] { new SpawnActor(1, "h", PlayerRole.Human, new Float3(10, 0, 10)), new SpawnActor(2, "m", PlayerRole.Mosquito, mosquito.ToFloat()) };
        private static ActorSnapshot Self(GameplayAuthority a) => a.CaptureSnapshot().Actors.Single(x => x.ActorId == 2);
        private static void Send(GameplayAuthority a, uint sequence, float yaw, float pitch, bool move)
        {
            var s = Self(a);
            Check(a.SubmitInput("m", new PlayerInputCommand(new CommandHeader(1, 1, 2, sequence, a.CurrentTick, s.ViewRevision), move ? new Float2(0, 1) : default, 0, yaw, pitch, MathEx.Aim(yaw, pitch))) == CommandReject.None, "Input rejected");
        }
        public void Dispose() { Object.DestroyImmediate(root); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
