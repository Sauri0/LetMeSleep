using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// External Director-slot diagnostic. All commands, plans, sweeps and snapshots are
// produced by production Authority + UnityGameplayWorld; no private actor mutation.
public static class AuthorityStrikeProbe
{
    const string HumanPath = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Human.prefab";
    const string ToolPath = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Flyswatter.prefab";
    [Serializable] public sealed class Stamp { public string path, sha256; }
    [Serializable] public sealed class Sample
    {
        public uint tick; public int hand; public string phase, clip;
        public float elapsed, progress, crouch, radius;
        public Vector3 origin, target, sweepFrom, sweepTo, previousVisualPoint, visualPoint, proxyWrist, handPoint;
        public Vector3 hitPoint, shoulder, unclampedWrist, toolVector;
        public bool swept, hit, previouslyBlocked, hasPreviousVisual, proxyTargetClamped, actualClipObserved;
        public float endResidual, pointToSweep, visualSegmentToSweep, previousEndResidual;
        public float upperLength, lowerLength, handToProxy, reachExcess, gripDistance, gripAngle;
        public float upperLengthDelta, lowerLengthDelta, pelvisIKDelta, legsIKDelta;
        public float handToImpactLength, contactReachExcess, avoidableEndResidual;
    }
    [Serializable] public sealed class Case
    {
        public string posture, tool, targetMode, error; public float pitch, actionYaw;
        public int expectedHand, sweeps, activeTicks; public bool equipmentVerified, authorityPlanVerified;
        public List<Sample> samples = new List<Sample>();
    }
    [Serializable] public sealed class Report
    {
        public string utc, project, unityVersion, status;
        public bool nativeExecuted = true, authoritySimulation = true, visualApproval = false;
        public int expectedCases = 54, casesCompleted, failedCases;
        public string scope = "Real Authority commands, real UnityGameplayWorld plans/motor/PhysX sweep results; real imported human/tool, each 30 Hz strike tick. Animator deterministic real-state seeks at elapsed seconds, manual binding LateUpdate. No live player-loop, rendering, WAN or visual quality approval.";
        public List<string> assemblies = new List<string>(), limits = new List<string>();
        public List<Stamp> files = new List<Stamp>(); public List<Case> cases = new List<Case>();
    }
    public static string Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var report = new Report { utc=DateTime.UtcNow.ToString("o"), project=Directory.GetParent(Application.dataPath).FullName, unityVersion=Application.unityVersion };
        foreach(var type in new[]{typeof(GameplayAuthority),typeof(GameplayActorProxy),typeof(ActorVisualBinding),typeof(CharacterView),typeof(AuthorityStrikeProbe)})
        { report.assemblies.Add(type.Assembly.FullName+" MVID="+type.Module.ModuleVersionId); AddStamp(report,type.Assembly.Location); }
        foreach(string source in new[]{"Gameplay/GameplayAuthority.cs","Gameplay.Unity/UnityGameplayWorld.cs","Gameplay.Unity/GameplayActorProxy.cs","Presentation/Gameplay/ActorVisualBinding.cs"})
            AddStamp(report,Path.Combine(Application.dataPath,"LetMeSleep",source));
        foreach(string asset in AssetDatabase.GetDependencies(new[]{HumanPath,ToolPath},true))
        {
            string path=Path.Combine(report.project,asset);
            if(!File.Exists(path) && asset.StartsWith("Packages/"))
            { var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset); if(package!=null) path=Path.Combine(package.resolvedPath,asset.Substring(package.assetPath.Length).TrimStart('/')); }
            if(File.Exists(path)) AddStamp(report,path);
        }
        foreach(bool crouch in new[]{false,true}) foreach(int configuration in new[]{0,1,2})
        foreach(float pitch in new[]{-45f,0f,45f}) foreach(string target in new[]{"free","near","reach_limit"})
        {
            var c=new Case { posture=crouch?"crouch":"standing",tool=configuration==2?GameplayTools.Flyswatter:GameplayTools.Hands,
                pitch=pitch,actionYaw=configuration==1?-.12f:.12f,expectedHand=configuration==0?-1:1,targetMode=target };
            report.cases.Add(c);
            try { using(var fixture=new Fixture(c,crouch)) fixture.Run(); }
            catch(Exception e) { c.error=e.ToString(); }
        }
        report.casesCompleted=report.cases.Count;report.failedCases=report.cases.Count(c=>c.error!=null);
        report.status=report.failedCases==0?"MEASURED_NOT_ALIGNMENT_APPROVAL":"HARNESS_INCOMPLETE";
        report.limits.Add("Hands use Hand bone origin as the visual point; no palm contact anchor is authored. Tool uses real Impact anchor.");
        report.limits.Add("End residual compares current visual point with actual sweep To; point/segment distances compare the whole effective collision segment. Target-only residual is not a verdict.");
        report.limits.Add("The final sweep may occur in Recovery at 8/30 s. All issued sweeps are captured, including blockers; later blocked ticks have no effective sweep.");
        report.limits.Add("Commands and collision plans are production, but map/aim scenarios are controlled. Tool attachment reproduces the production Grip/socket mounting convention.");
        report.limits.Add("Fresh owned instances per case; face/attention behaviours disabled. No animator crossfade timing or autonomous LateUpdate-order certification.");
        string pathOut=Path.Combine(directory,"authority-strike-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
        File.WriteAllText(pathOut,AuthorityStrikeJson.Write(report));
        return report.status+" cases="+report.casesCompleted+" failed="+report.failedCases+" JSON="+pathOut;
    }
    sealed class Fixture : IDisposable
    {
        readonly GameObject root; readonly UnityGameplayWorld world; readonly RecordingWorld recorder; readonly GameplayAuthority host;
        readonly Case result; readonly bool crouch; readonly Float3 aim; readonly Float3 commandAim;
        readonly CharacterView view; readonly ActorVisualBinding binding; readonly GameplayActorProxy proxy; readonly ToolView tool;
        readonly Transform upper,lower,hand; readonly Transform[] legs;
        uint inputSequence,actionSequence; Vector3 previousVisual; bool hasPrevious,blocked;
        public Fixture(Case c,bool crouched)
        {
            result=c;crouch=crouched;
            root=new GameObject("AuthorityStrikeProbe-owned");root.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                world=root.AddComponent<UnityGameplayWorld>();
                var map=new GameObject("Map-owned");map.transform.SetParent(root.transform,false);world.MapRoot=map.transform;
                // Offset from ordinary game geometry; world filters to owned map/actors.
                Vector3 spawn=new Vector3(100,0,100);
                Box("Floor",spawn+new Vector3(0,-.05f,0),new Vector3(8,.1f,8));
                var pickup=new GameObject("Pickup-owned");pickup.transform.SetParent(map.transform,false);pickup.transform.position=spawn+new Vector3(0,1.53f,.4f);
                var pc=pickup.AddComponent<BoxCollider>();pc.size=Vector3.one*.08f;pc.isTrigger=true;
                var p=pickup.AddComponent<GameplayToolPickup>();p.PickupId=1;p.InteractionCollider=pc;
                recorder=new RecordingWorld(world);host=new GameplayAuthority(recorder);
                host.BeginRound(new GameplayRoundConfig(7,1,"strike-probe","owned",tools:world.GetToolDefinitions()),new[]{
                    new SpawnActor(1,"human",PlayerRole.Human,spawn.ToFloat()),new SpawnActor(2,"mosquito",PlayerRole.Mosquito,(spawn+new Vector3(3,2,3)).ToFloat())});
                if(c.tool==GameplayTools.Flyswatter)
                {
                    SendInput(Float3.Forward,0,false); Action(ActionKind.Use,Float3.Forward);host.Advance(new HostTick(host.CurrentTick+1));
                    Require(State.EquippedToolId==GameplayTools.Flyswatter,"Production Use did not equip tool");
                }
                result.equipmentVerified=State.EquippedToolId==c.tool;
                // Unused pickup is a trigger, not a strike obstacle.
                aim=MathEx.Aim(0,c.pitch*Mathf.Deg2Rad);commandAim=MathEx.Aim(c.actionYaw,c.pitch*Mathf.Deg2Rad);
                for(int i=0;i<12;i++) { SendInput(aim,c.pitch*Mathf.Deg2Rad,crouch);host.Advance(new HostTick(host.CurrentTick+1)); }
                Require(Mathf.Abs(State.CrouchFraction-(crouch?1:0))<.001f,"Authority did not settle posture");
                if(c.targetMode!="free")
                {
                    Vector3 eye=State.Position.ToUnity()+Vector3.up*(1.53f-.64f*State.CrouchFraction);
                    var b=Box("AimTarget",eye+commandAim.ToUnity()*(c.targetMode=="near"?.42f:1.20f),new Vector3(.18f,.18f,.035f));
                    b.transform.rotation=Quaternion.LookRotation(commandAim.ToUnity());
                }
                Physics.SyncTransforms();proxy=world.Actors[1];
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HumanPath);Require(prefab,"Human prefab missing");
                var model=Object.Instantiate(prefab,root.transform);
                foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
                foreach(var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                view=model.GetComponentInChildren<CharacterView>(true);Require(view&&view.Animator,"CharacterView/Animator missing");
                view.Animator.enabled=true;view.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;view.Animator.applyRootMotion=false;view.Animator.Rebind();view.Animator.Update(0);
                binding=view.GetComponent<ActorVisualBinding>()??view.gameObject.AddComponent<ActorVisualBinding>();binding.enabled=false;binding.Initialize(proxy,world,view,true);view.SetFirstPersonVisibility(false);
                upper=Bone("UpperArm."+(c.expectedHand<0?"L":"R"));lower=Bone("LowerArm."+(c.expectedHand<0?"L":"R"));hand=Bone("Hand."+(c.expectedHand<0?"L":"R"));
                legs=new[]{"Hips","UpperLeg.L","LowerLeg.L","Foot.L","UpperLeg.R","LowerLeg.R","Foot.R"}.Select(Bone).ToArray();
                binding.ApplySnapshot(State,host.CurrentTick);Seek(0,null);view.RefreshAnchors();
                if(c.tool==GameplayTools.Flyswatter)
                {
                    var socket=view.GetAnchor("ToolSocket_R");var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ToolPath),socket);
                    foreach(var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                    tool=instance.GetComponent<ToolView>();Require(tool&&tool.Grip&&tool.Impact,"Tool anchors missing");
                    instance.transform.rotation=(socket.rotation*Quaternion.Inverse(tool.Grip.rotation))*instance.transform.rotation;
                    instance.transform.position+=socket.position-tool.Grip.position;binding.BindFlyswatter(instance);
                }
                host.DrainEvents();
            }
            catch { Object.DestroyImmediate(root);throw; }
        }
        ActorSnapshot State=>host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1);
        CommandHeader Header(uint sequence)=>new CommandHeader(7,1,1,sequence,host.CurrentTick,State.ViewRevision);
        void SendInput(Float3 direction,float pitch,bool duck)
        { Require(host.SubmitInput("human",new PlayerInputCommand(Header(++inputSequence),default,0,0,pitch,direction,crouch:duck))==CommandReject.None,"Input rejected"); }
        void Action(ActionKind kind,Float3 direction)
        { Require(host.SubmitAction("human",new PlayerActionCommand(Header(++actionSequence),kind,direction))==CommandReject.None,"Action rejected"); }
        public void Run()
        {
            Action(ActionKind.Primary,commandAim);
            for(int i=0;i<20;i++)
            {
                recorder.sweeps.Clear();SendInput(aim,result.pitch*Mathf.Deg2Rad,crouch);host.Advance(new HostTick(host.CurrentTick+1));
                var state=State;var strike=state.StrikeState;
                if(i==0) { Require(strike.Phase==StrikePhase.Windup&&strike.Hand==result.expectedHand,"Authority strike/hand missing");result.authorityPlanVerified=true; }
                if(strike.Phase==StrikePhase.None) break;
                binding.ApplySnapshot(state,host.CurrentTick);foreach(var e in host.DrainEvents())binding.ApplyEvent(e);
                var sample=new Sample { tick=host.CurrentTick,hand=strike.Hand,phase=strike.Phase.ToString(),elapsed=(host.CurrentTick-strike.StartTick)/30f,
                    progress=strike.Progress,crouch=state.CrouchFraction,origin=strike.Origin.ToUnity(),target=strike.Target.ToUnity(),previouslyBlocked=blocked };
                Seek(sample.elapsed,sample);
                var control=legs.Select(t=>t.position).ToArray();float beforeUpper=Vector3.Distance(upper.position,lower.position),beforeLower=Vector3.Distance(lower.position,hand.position);
                var forearm=proxy.BodySurfaces[strike.Hand<0?105u:106u];
                sample.proxyWrist=forearm.transform.TransformPoint(Vector3.up*Mathf.Max(0,forearm.Collider.height*.5f-forearm.Collider.radius));
                sample.shoulder=upper.position;sample.reachExcess=Mathf.Max(0,Vector3.Distance(sample.proxyWrist,upper.position)-(beforeUpper+beforeLower-.0001f));
                sample.proxyTargetClamped=sample.reachExcess>.0001f;
                sample.handToImpactLength=tool?Vector3.Distance(tool.Impact.position,hand.position):0;
                typeof(ActorVisualBinding).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(binding,null);
                sample.handPoint=hand.position;sample.visualPoint=tool?tool.Impact.position:hand.position;
                sample.hasPreviousVisual=hasPrevious;sample.previousVisualPoint=previousVisual;
                sample.upperLength=Vector3.Distance(upper.position,lower.position);sample.lowerLength=Vector3.Distance(lower.position,hand.position);
                sample.upperLengthDelta=Mathf.Abs(sample.upperLength-beforeUpper);sample.lowerLengthDelta=Mathf.Abs(sample.lowerLength-beforeLower);
                sample.handToProxy=Vector3.Distance(hand.position,sample.proxyWrist);
                sample.pelvisIKDelta=Vector3.Distance(legs[0].position,control[0]);sample.legsIKDelta=legs.Select((t,j)=>Vector3.Distance(t.position,control[j])).Max();
                if(tool) { var socket=view.GetAnchor("ToolSocket_R");sample.gripDistance=Vector3.Distance(tool.Grip.position,socket.position);sample.gripAngle=Quaternion.Angle(tool.Grip.rotation,socket.rotation);sample.toolVector=tool.Impact.position-tool.Grip.position; }
                Require(recorder.sweeps.Count<=1,"Multiple sweeps for one strike tick");
                if(recorder.sweeps.Count==1)
                {
                    var capture=recorder.sweeps[0];var q=capture.query;
                    sample.swept=true;sample.sweepFrom=q.From.ToUnity();sample.sweepTo=q.To.ToUnity();sample.radius=q.Radius;sample.hit=capture.hit.Hit;sample.hitPoint=capture.hit.Point.ToUnity();
                    sample.endResidual=Vector3.Distance(sample.visualPoint,sample.sweepTo);sample.pointToSweep=PointSegment(sample.visualPoint,sample.sweepFrom,sample.sweepTo);
                    sample.contactReachExcess=Mathf.Max(0,Vector3.Distance(sample.shoulder,sample.sweepTo)-(beforeUpper+beforeLower-.0001f+sample.handToImpactLength));
                    sample.avoidableEndResidual=Mathf.Max(0,sample.endResidual-sample.contactReachExcess);
                    sample.previousEndResidual=hasPrevious?Vector3.Distance(previousVisual,sample.sweepFrom):0;
                    sample.visualSegmentToSweep=hasPrevious?SegmentDistance(previousVisual,sample.visualPoint,sample.sweepFrom,sample.sweepTo):sample.pointToSweep;
                    if(capture.hit.Hit && capture.hit.ActorId==0)blocked=true;
                    result.sweeps++;
                }
                if(strike.Phase==StrikePhase.Active)result.activeTicks++;
                result.samples.Add(sample);previousVisual=sample.visualPoint;hasPrevious=true;
            }
            Require(result.activeTicks==5,"Missing active ticks");Require(result.sweeps>0,"No Authority sweep recorded");
        }
        void Seek(float elapsed,Sample s)
        {
            int id=crouch?3:12;var motion=view.Motions.Single(m=>m.Id==id);var clip=view.Animator.runtimeAnimatorController.animationClips.First(c=>c.name==motion.ClipName);
            float normalized=crouch?.999f:elapsed/clip.length;
            view.PlayMotion(id,0);view.Animator.Play(motion.StateName,0,normalized);view.Animator.Update(0);
            bool observed=view.Animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip==clip&&c.weight>.99f);
            Require(observed&&view.Animator.GetCurrentAnimatorStateInfo(0).IsName(motion.StateName),"Expected real animator clip not evaluated");
            if(s!=null){s.clip=AssetDatabase.GetAssetPath(clip);s.actualClipObserved=observed;}
            view.RefreshAnchors();
        }
        Transform Bone(string name)=>view.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        BoxCollider Box(string name,Vector3 center,Vector3 size)
        { var go=new GameObject(name);go.transform.SetParent(world.MapRoot,false);go.transform.position=center;var box=go.AddComponent<BoxCollider>();box.size=size;return box; }
        public void Dispose(){if(root)Object.DestroyImmediate(root);}
    }
    sealed class RecordingWorld : IGameplayWorld, IGameplayToolWorld
    {
        public sealed class Capture { public StrikeSweep query; public StrikeHit hit; }
        readonly UnityGameplayWorld w; public readonly List<Capture> sweeps=new List<Capture>();
        public RecordingWorld(UnityGameplayWorld world){w=world;}
        public void BeginRound(IReadOnlyList<SpawnActor> a,IReadOnlyList<DoorDefinition> d)=>w.BeginRound(a,d);
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a)=>w.SynchronizeActors(a);
        public MotorResult MoveHuman(in MotorQuery q)=>w.MoveHuman(q);
        public MotorResult MoveMosquito(in MotorQuery q)=>w.MoveMosquito(q);
        public bool TrySurface(in SurfaceQuery q,out SurfaceContact c)=>w.TrySurface(q,out c);
        public bool ResolveSurface(in SurfaceAttachment a,out SurfaceContact c)=>w.ResolveSurface(a,out c);
        public bool TryBiteContact(in BiteQuery q,out BiteContact c)=>w.TryBiteContact(q,out c);
        public bool ResolveBite(uint id,in BiteAttachment a,int n,out BiteContact c)=>w.ResolveBite(id,a,n,out c);
        public bool TryPlanStrike(uint id,Float3 aim,string tool,out StrikePlan p)=>w.TryPlanStrike(id,aim,tool,out p);
        public StrikeHit SweepStrike(in StrikeSweep q){var hit=w.SweepStrike(q);sweeps.Add(new Capture{query=q,hit=hit});return hit;}
        public bool TryFreeRecoveryPoint(uint id,Float3 p,out Float3 r)=>w.TryFreeRecoveryPoint(id,p,out r);
        public bool HasLineOfSight(uint id,Float3 f,uint target,Float3 t)=>w.HasLineOfSight(id,f,target,t);
        public bool TryDoorInteraction(in DoorInteractionQuery q,out DoorInteractionCandidate c)=>w.TryDoorInteraction(q,out c);
        public DoorSweepResult SweepDoor(in DoorMotionQuery q)=>w.SweepDoor(q);
        public void ApplyDoorPose(in DoorPose p)=>w.ApplyDoorPose(p);
        public void BeginTools(IReadOnlyList<ToolPickupDefinition> d)=>w.BeginTools(d);
        public bool TryToolInteraction(in ToolInteractionQuery q,out ToolInteractionCandidate c)=>w.TryToolInteraction(q,out c);
        public bool TryDropTool(uint id,out Float3 p,out Rotation r)=>w.TryDropTool(id,out p,out r);
        public void ApplyToolState(in ToolPickupSnapshot s)=>w.ApplyToolState(s);
    }
    static float PointSegment(Vector3 p,Vector3 a,Vector3 b)
    { var d=b-a;return Vector3.Distance(p,a+d*(d.sqrMagnitude<1e-12f?0:Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude))); }
    static float SegmentDistance(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        // Convex quadratic distance on two bounded segments; endpoints plus interior stationary pair.
        float result=Mathf.Min(PointSegment(a,c,d),PointSegment(b,c,d),PointSegment(c,a,b),PointSegment(d,a,b));
        var u=b-a;var v=d-c;var w=a-c;float uu=Vector3.Dot(u,u),vv=Vector3.Dot(v,v),uv=Vector3.Dot(u,v),uw=Vector3.Dot(u,w),vw=Vector3.Dot(v,w),den=uu*vv-uv*uv;
        if(den>1e-12f){float s=(uv*vw-vv*uw)/den,t=(uu*vw-uv*uw)/den;if(s>=0&&s<=1&&t>=0&&t<=1)result=Mathf.Min(result,Vector3.Distance(a+u*s,c+v*t));}return result;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void AddStamp(Report r,string path)
    { if(r.files.Any(f=>f.path==path))return;using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))r.files.Add(new Stamp{path=path,sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")}); }
}
