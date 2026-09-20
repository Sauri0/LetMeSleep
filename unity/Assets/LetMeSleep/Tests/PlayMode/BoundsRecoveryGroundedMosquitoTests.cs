using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    // Engine-only fixture: actual Physics colliders/authority. KnockDown is invoked as a
    // deterministic fault trigger; this is NOT a hitbox/aim or mouse-input acceptance test.
    public sealed class BoundsRecoveryGroundedMosquitoTests
    {
        private GameObject owner,map,dryPlatform,secondPlatform;
        private UnityGameplayWorld world;
        private GameplayAuthority authority;
        private readonly Vector3 origin=new Vector3(1000,100,1000);
        private readonly List<BoundsRecoveryStatus> insectResults=new List<BoundsRecoveryStatus>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private ActorSnapshot Insect=>authority.CaptureSnapshot().Actors.Single(a=>a.ActorId==2);
        private void Start(bool occupyFirst=false,bool occupySecond=false)
        {
            owner=new GameObject("Bounds recovery fixture v3");world=owner.AddComponent<UnityGameplayWorld>();
            map=new GameObject("Authored map");map.transform.SetParent(owner.transform);map.transform.position=origin;
            world.MapRoot=map.transform;
            dryPlatform=Box("Dry deck A",new Vector3(-4,-.25f,0),new Vector3(1,.5f,2));
            secondPlatform=Box("Dry deck B",new Vector3(-2,-.25f,0),new Vector3(1,.5f,2));
            Box("Human deck",new Vector3(-7,-.25f,0),new Vector3(2,.5f,2));
            Box("Submerged solid floor",new Vector3(4,-2.25f,0),new Vector3(4,.5f,4));
            var settings=map.AddComponent<GameplayRecoveryVolume>();
            settings.SafetyBounds=new Bounds(new Vector3(0,2,0),new Vector3(40,6,40));
            settings.MosquitoFallZones=new[]{new Bounds(new Vector3(4,-3,0),new Vector3(6,8,6))};
            settings.HumanFallZones=(Bounds[])settings.MosquitoFallZones.Clone();
            settings.MosquitoSpawnPoints=new[]{new Vector3(-4,3,0),new Vector3(-2,3,0)};
            authority=new GameplayAuthority(world);
            var actors=new List<SpawnActor>{new SpawnActor(1,"human",PlayerRole.Human,(origin+new Vector3(-7,.002f,0)).ToFloat()),
                new SpawnActor(2,"insect",PlayerRole.Mosquito,(origin+new Vector3(4,3,0)).ToFloat())};
            if(occupyFirst||occupySecond) actors.Add(new SpawnActor(3,"other",PlayerRole.Mosquito,
                (origin+new Vector3(occupyFirst?-4:-2,.057f,0)).ToFloat()));
            authority.BeginRound(new GameplayRoundConfig(1,1,"native recovery fixture","v3"),actors);
            world.BoundsRecoveryReported+=(id,status)=>{if(id==2)insectResults.Add(status);};
            Physics.SyncTransforms();Tick(); // Observe controlled flight as last-safe, over the water.
        }
        private GameObject Box(string name,Vector3 position,Vector3 size)
        { var go=new GameObject(name);go.transform.SetParent(map.transform,false);go.transform.localPosition=position;go.AddComponent<BoxCollider>().size=size;return go; }
        private object InternalActor(uint actor=2)=>((System.Collections.IDictionary)typeof(GameplayAuthority)
            .GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(authority))[actor];
        private void SetActor(string field,object value,uint actor=2)
        { var state=InternalActor(actor);state.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(state,value); }
        private void KnockDown()=>typeof(GameplayAuthority).GetMethod("KnockDown",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(authority,new[]{InternalActor(),(object)Float3.Zero});
        private void Tick()=>authority.Advance(new HostTick(authority.CurrentTick+1));
        [TearDown] public void Cleanup()
        { if(authority!=null && authority.IsRunning)authority.EndRound(RoundEndReason.Aborted);if(owner)Object.DestroyImmediate(owner);
          foreach(var mesh in meshes)if(mesh)Object.DestroyImmediate(mesh);meshes.Clear();insectResults.Clear();authority=null; }

        [TestCase(false)] [TestCase(true)]
        public void FallingOverWaterFindsDryGroundWithoutAirborneTeleportLoop(bool occupyFirst)
        {
            Start(occupyFirst);KnockDown();
            for(int i=0;i<90 && !insectResults.Contains(BoundsRecoveryStatus.Recovered);i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(1),"Must recover once from natural gravity descent.");
            var rescued=Insect;var local=rescued.Position.ToUnity()-origin;
            Assert.That(local.x,Is.EqualTo(occupyFirst?-2:-4).Within(.003f));
            Assert.That(local.y,Is.EqualTo(.057f).Within(.003f),"Place sphere above deck, not at the airborne spawn.");
            Assert.That(rescued.Grounded,Is.True);
            Assert.That(rescued.LifeState,Is.EqualTo(LifeState.Falling),"Technical rescue must not wake the insect.");
            Assert.That(rescued.Velocity.LengthSquared,Is.EqualTo(0));
            Tick();Assert.That(Insect.LifeState,Is.EqualTo(LifeState.Stunned),"Next real ground contact starts existing recovery.");
            for(int i=0;i<120;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(1));
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.UnstableRecovery),Is.False);
            Assert.That(Insect.Position.IsFinite,Is.True);
            Assert.That((world.Actors[2].transform.position-Insect.Position.ToUnity()).magnitude,Is.LessThan(.0001f));
            Assert.That(Insect.LifeState,Is.EqualTo(LifeState.Stunned));
        }

        [TestCase(LifeState.Stunned)] [TestCase(LifeState.Recovering)]
        public void AlreadyIncapacitatedInWaterKeepsItsTimer(LifeState state)
        {
            Start();KnockDown();SetActor("State",state);SetActor("Recovery",9f);
            // Explicit fault injection tests a branch that never enters its movement motor.
            SetActor("Position",(origin+new Vector3(4,.5f,0)).ToFloat());Tick();
            Assert.That(Insect.Grounded,Is.True);Assert.That(Insect.LifeState,Is.EqualTo(state));
            Assert.That((Insect.Position.ToUnity()-origin).y,Is.EqualTo(.057f).Within(.003f));
            Assert.That(authority.CapturePrivate(2).RecoverySeconds,Is.EqualTo(9f-1f/30).Within(.001f));
        }

        [Test] public void NoDryPlatformReportsFailureInsteadOfUsingSubmergedFloorOrAir()
        {
            Start();Object.DestroyImmediate(dryPlatform);Object.DestroyImmediate(secondPlatform);Physics.SyncTransforms();KnockDown();
            for(int i=0;i<150;i++)Tick();
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.NoSafeDestination),Is.True);
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.Recovered),Is.False);
            Assert.That(Insect.Position.IsFinite,Is.True);Assert.That(Insect.Velocity.LengthSquared,Is.EqualTo(0));
            Assert.That(authority.CapturePrivate(2).CanAct,Is.False);
        }

        [Test] public void FailedDestinationIsQuarantinedUntilASecondDestinationBecomesFree()
        {
            Start(occupySecond:true);KnockDown();
            for(int i=0;i<90 && insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered)<1;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(1));
            Assert.That((Insect.Position.ToUnity()-origin).x,Is.EqualTo(-4).Within(.003f));

            // Remove the support immediately after rescue. This is a real second fall before
            // thirty stable observations, while B remains occupied by actor 3.
            Object.DestroyImmediate(dryPlatform);Physics.SyncTransforms();
            for(int i=0;i<29;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(1));
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.UnstableRecovery),Is.False);
            for(int i=0;i<60 && !insectResults.Contains(BoundsRecoveryStatus.NoSafeDestination);i++)Tick();
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.NoSafeDestination),Is.True,
                "A temporarily occupied alternative must report no safe destination after cooldown.");

            SetActor("Position",(origin+new Vector3(8,3,0)).ToFloat(),3);Tick();
            for(int i=0;i<90 && insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered)<2;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(2));
            Assert.That((Insect.Position.ToUnity()-origin).x,Is.EqualTo(-2).Within(.003f));

            // A and B both fail. Their quarantine blocks immediate ping-pong, but it expires so
            // a repaired destination can be revalidated instead of freezing this round forever.
            Object.DestroyImmediate(secondPlatform);Physics.SyncTransforms();
            int noSafeBefore=insectResults.Count(s=>s==BoundsRecoveryStatus.NoSafeDestination);
            for(int i=0;i<90 && insectResults.Count(s=>s==BoundsRecoveryStatus.NoSafeDestination)==noSafeBefore;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(2));
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.NoSafeDestination),Is.GreaterThan(noSafeBefore));

            dryPlatform=Box("Repaired dry deck A",new Vector3(-4,-.25f,0),new Vector3(1,.5f,2));
            Physics.SyncTransforms();
            for(int i=0;i<29;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(2),
                "A repaired destination must still respect the cooldown.");
            for(int i=0;i<150 && insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered)<3;i++)Tick();
            Assert.That(insectResults.Count(s=>s==BoundsRecoveryStatus.Recovered),Is.EqualTo(3));
            Assert.That((Insect.Position.ToUnity()-origin).x,Is.EqualTo(-4).Within(.003f));
            Assert.That(insectResults.Contains(BoundsRecoveryStatus.UnstableRecovery),Is.False);
        }

        [Test] public void PeripheralProbeRejectsFirstSteepSolidInsteadOfAcceptingFloorBelow()
        {
            StartHumanProbe();
            Box("Lower floor",new Vector3(0,-.05f,0),new Vector3(2,.1f,2));
            SteepPeripheralPatch();Physics.SyncTransforms();
            var result=world.CheckBounds(new BoundsRecoveryQuery(1,1,PlayerRole.Human,
                (origin+new Vector3(10,0,0)).ToFloat(),1.8f,.25f,false));
            Assert.That(result.Status,Is.EqualTo(BoundsRecoveryStatus.NoSafeDestination),
                "The first peripheral solid is too steep; a lower floor must not make it valid.");
        }

        [Test] public void PeripheralProbeRejectsDisconnectedSupportsBelowCentralContact()
        {
            StartHumanProbe();
            Box("Central pedestal",new Vector3(0,-.1f,0),new Vector3(.1f,.2f,.1f));
            Box("Disconnected lower supports",new Vector3(0,-.17f,0),new Vector3(2,.2f,2));
            Physics.SyncTransforms();
            var result=world.CheckBounds(new BoundsRecoveryQuery(1,1,PlayerRole.Human,
                (origin+new Vector3(10,0,0)).ToFloat(),1.8f,.25f,false));
            Assert.That(result.Status,Is.EqualTo(BoundsRecoveryStatus.NoSafeDestination),
                "Peripheral support seven centimetres below the centre is not a continuous footprint.");
        }

        private void StartHumanProbe()
        {
            owner=new GameObject("Bounds recovery footprint fixture v3");world=owner.AddComponent<UnityGameplayWorld>();
            map=new GameObject("Authored footprint map");map.transform.SetParent(owner.transform);map.transform.position=origin;
            world.MapRoot=map.transform;
            var settings=map.AddComponent<GameplayRecoveryVolume>();
            settings.SafetyBounds=new Bounds(new Vector3(0,1,0),new Vector3(6,4,6));
            settings.HumanSpawnPoints=new[]{new Vector3(0,.05f,0)};
            world.BeginBoundsRecovery(new[]{new SpawnActor(1,"human",PlayerRole.Human,
                (origin+new Vector3(10,0,0)).ToFloat())});
        }

        private void SteepPeripheralPatch()
        {
            var go=new GameObject("First steep peripheral solid");go.transform.SetParent(map.transform,false);
            var mesh=new Mesh{name="Steep peripheral support fixture"};meshes.Add(mesh);
            mesh.vertices=new[]{new Vector3(.1575f,.02f,-.01f),new Vector3(.1675f,.04f,-.01f),
                new Vector3(.1675f,.04f,.01f),new Vector3(.1575f,.02f,.01f)};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();mesh.RecalculateNormals();
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
