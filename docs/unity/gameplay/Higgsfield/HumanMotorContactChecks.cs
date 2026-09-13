using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// External native regression fixture. Calls integrated motor directly with its
// ordinary dimensions/speeds; never substitutes the solver or mutates map assets.
public static class HumanMotorContactChecks
{
    public sealed class Report
    {
        public string status,utc,unityVersion,motorAssembly,motorMvid;
        public string scope="Native direct MotorQuery regression, synthetic geometry; no authority/input/visual/WAN certification.";
        public List<Case> cases=new List<Case>();
        public bool cleanup;
    }
    public sealed class Case
    {
        public string id,status="INCOMPLETE",reason,peakBlocker;
        public Vector3 start,end,peakPosition;
        public float maxPenetration,traveled,minExpectedNormalGap,minMeasuredNormalGap;
        public int ticks;
    }
    public static string Run(string configPath,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)throw new Exception("Idle native slot required.");
        Directory.CreateDirectory(output);
        var report=new Report{utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,motorAssembly=typeof(UnityGameplayWorld).Assembly.Location,motorMvid=typeof(UnityGameplayWorld).Module.ModuleVersionId.ToString()};
        RunCase(report,"flat-triangle-tangent",f=>{
            f.Plane(0);f.Start(Vector3.zero);f.Walk(new Vector3(0,0,3.1f),30);
            Check(f.Position.z>3,"Tangent flat mesh stopped forward travel.");Check(f.Grounded,"Lost flat ground.");
        });
        foreach(int sign in new[]{-1,1})RunCase(report,"slope8deg-"+(sign<0?"down":"up"),f=>{
            float slope=sign*Mathf.Tan(8*Mathf.Deg2Rad);f.Plane(slope);f.Start(new Vector3(0,.25f*(Mathf.Sqrt(1+slope*slope)-1),0));
            f.Walk(new Vector3(0,0,3.1f),45);Check(f.Position.z>4,"Tangent slope failed to advance.");Check(f.Grounded,"Lost slope support.");
            Check(Mathf.Abs(f.Position.y-slope*f.Position.z)<.02f,"Departed slope support.");
        });
        RunCase(report,"wall-frontal-clearance",f=>{
            f.Floor();f.Box("Wall",new Vector3(0,2,2.1f),new Vector3(80,4,.2f));f.Start(new Vector3(0,.001f,0));
            f.Walk(new Vector3(0,0,3.1f),60);float gap=2-f.Position.z-.25f;
            f.Row.minExpectedNormalGap=.0009f;f.Row.minMeasuredNormalGap=gap;
            Check(f.Position.z>1.7f && gap>=.0009f && gap<.005f,"Frontal wall clearance changed.");
        });
        foreach(float approach in new[]{.1f,1f})RunCase(report,"wall-oblique-"+approach,f=>{
            f.Floor();f.Box("Wall",new Vector3(0,2,2.1f),new Vector3(80,4,.2f));f.Start(new Vector3(0,.001f,1.748f));
            var velocity=new Vector3(3.1f,0,approach).normalized*3.1f;
            // Existing solver's Skin is along the sweep, so its normal gap is
            // Skin * incidence, not a full1mm at every oblique angle.
            float required=.001f*velocity.z/velocity.magnitude-.00001f;f.Row.minExpectedNormalGap=required;f.Row.minMeasuredNormalGap=float.MaxValue;
            for(int t=0;t<90;t++){f.Walk(velocity,1);float gap=2-f.Position.z-.25f;f.Row.minMeasuredNormalGap=Math.Min(f.Row.minMeasuredNormalGap,gap);Check(gap>=required,"Oblique sweep reduced the previous effective normal clearance.");}
            Check(f.Position.x>8,"Wall slide stalled.");
        });
        RunCase(report,"ceiling-frontal-clearance",f=>{
            f.Box("Ceiling",new Vector3(0,2.1f,0),new Vector3(10,.2f,10));f.Start(Vector3.zero);
            for(int t=0;t<12;t++)f.Move(new Vector3(0,4,0),false);
            float gap=2-f.Position.y-1.72f;f.Row.minMeasuredNormalGap=gap;f.Row.minExpectedNormalGap=.0009f;
            Check(f.Position.y>.25f && gap>=.0009f && gap<.005f,"Ceiling clearance changed.");
        });
        foreach(float height in new[]{.2f,.24f})RunCase(report,"step-"+height,f=>{
            f.Floor();f.Box("Step",new Vector3(0,height*.5f,2),new Vector3(2,height,2));f.Start(new Vector3(0,.001f,0));
            for(int t=0;t<45 && f.Position.z<2;t++)f.Walk(new Vector3(0,0,3.1f),1);
            if(height<.22f)Check(f.Position.z>=2 && f.Position.y>=height && f.Position.y<height+.01f,"Legal20cm step failed.");
            else Check(f.Position.z<.76f && f.Position.y<.01f,"24cm step bypassed22cm limit.");
        });
        RunCase(report,"mosquito-wall-unchanged",f=>{
            f.Box("Wall",new Vector3(0,2,2.1f),new Vector3(10,4,.2f));f.Start(new Vector3(0,1,0),PlayerRole.Mosquito);
            for(int t=0;t<30;t++)f.Move(new Vector3(0,0,3.8f),false);
            Check(Mathf.Abs(f.Position.z-1.944f)<.0002f,"Mosquito sphere clearance changed.");
        });
        report.status=report.cases.All(c=>c.status=="PASS")?"PASS_SCOPED":"FAIL";report.cleanup=true;
        string path=Path.Combine(output,"human-motor-contacts-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(path,HiggsfieldMapJson.Write(report));return path;
    }
    static void RunCase(Report report,string id,Action<Fixture> test)
    {
        var row=new Case{id=id};report.cases.Add(row);
        try{using(var fixture=new Fixture(row)){test(fixture);Check(row.maxPenetration<=.002f,"Motor penetration exceeded2mm.");row.status="PASS";}}
        catch(Exception e){row.status="FAIL";row.reason=e.Message;}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    sealed class Fixture:IDisposable
    {
        readonly GameObject owner;readonly Transform map;readonly UnityGameplayWorld world;readonly List<Mesh> meshes=new List<Mesh>();readonly Stopwatch watch=Stopwatch.StartNew();
        GameplayActorProxy actor;PlayerRole role;public readonly Case Row;public Vector3 Position;public bool Grounded;
        public Fixture(Case row){Row=row;owner=new GameObject("MotorContact_TEMP"){hideFlags=HideFlags.HideAndDontSave};map=new GameObject("Map").transform;map.SetParent(owner.transform,false);world=owner.AddComponent<UnityGameplayWorld>();world.MapRoot=map;}
        public void Box(string name,Vector3 center,Vector3 size){var go=new GameObject(name);go.transform.SetParent(map,false);var box=go.AddComponent<BoxCollider>();box.center=center;box.size=size;}
        public void Floor()=>Box("Floor",new Vector3(0,-.5f,0),new Vector3(80,1,80));
        public void Plane(float slope)
        {
            var go=new GameObject("TriangulatedSlope");go.transform.SetParent(map,false);var mesh=new Mesh{name="MotorSlope_TEMP"};meshes.Add(mesh);
            var vertices=new List<Vector3>();var tris=new List<int>();
            for(int row=0;row<=40;row++){float z=-10+row*.5f;vertices.Add(new Vector3(-20,slope*z,z));vertices.Add(new Vector3(0,slope*z,z));vertices.Add(new Vector3(20,slope*z,z));}
            for(int row=0;row<40;row++)for(int col=0;col<2;col++){int a=row*3+col,b=a+1,c=a+3,d=c+1;tris.AddRange(new[]{a,c,b,b,c,d});}
            mesh.vertices=vertices.ToArray();mesh.triangles=tris.ToArray();mesh.RecalculateBounds();go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        public void Start(Vector3 position,PlayerRole actorRole=PlayerRole.Human)
        {
            role=actorRole;Position=position;Row.start=position;Row.end=position;Grounded=role==PlayerRole.Human;
            world.BeginRound(new[]{new SpawnActor(1,"motor-contact",role,position.ToFloat())},Array.Empty<DoorDefinition>());actor=world.Actors[1];Physics.SyncTransforms();
        }
        public void Walk(Vector3 velocity,int ticks){for(int t=0;t<ticks;t++)Move(velocity+Vector3.down*.5f,true);}
        public void Move(Vector3 velocity,bool allowStep)
        {
            Check(watch.Elapsed.TotalSeconds<15,"Per-case15s budget exhausted.");var before=Position;
            var query=new MotorQuery(1,Position.ToFloat(),velocity.ToFloat(),1f/30,1.72f,role==PlayerRole.Human?.25f:.055f,0,Grounded,allowStep);
            var result=role==PlayerRole.Human?world.MoveHuman(query):world.MoveMosquito(query);Position=result.Position.ToUnity();Grounded=result.Grounded;
            Check(result.Position.IsFinite && result.Velocity.IsFinite,"Nonfinite motor state.");Row.end=Position;Row.ticks++;Row.traveled+=Vector3.Distance(before,Position);
            foreach(var collider in map.GetComponentsInChildren<Collider>())if(Physics.ComputePenetration(actor.MotorCollider,actor.transform.position,actor.transform.rotation,collider,collider.transform.position,collider.transform.rotation,out _,out float depth) && depth>Row.maxPenetration){Row.maxPenetration=depth;Row.peakBlocker=collider.name;Row.peakPosition=Position;}
        }
        public void Dispose(){Object.DestroyImmediate(owner);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Physics.SyncTransforms();watch.Stop();}
    }
}
