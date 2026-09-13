// External Unity CLI evaluation body. Director invokes; never add to Assets.
// Read imported prefab/mesh only; temporary instance is destroyed in finally.
var timer = System.Diagnostics.Stopwatch.StartNew();
var outputDir = "N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/diagnostics/gameplay-e9d15e7/unity-receipts";
var prefabPath = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Human.prefab";
var modelPath = "Assets/LetMeSleep/Content/Characters/Models/LMS_Human_alpha.fbx";
var expectedSha = "2473a8e6420dcb7c15d58c6dcb0e9fc64769bc8af6636844c71851a42bd84b66";
var shapeRows = new System.Collections.Generic.List<object>();
var samples = new System.Collections.Generic.List<object>();
var support = new System.Collections.Generic.HashSet<int>();
var outsideRoi = new System.Collections.Generic.HashSet<int>();
UnityEngine.GameObject instance = null;
UnityEngine.Mesh baked = null;
string actualSha = null, error = null;
bool queued = false;
float[] V(UnityEngine.Vector3 v) => new[] {v.x,v.y,v.z};
object BoundsOf(UnityEngine.Vector3[] vertices)
{
    if(vertices.Length==0) return null;
    var b = new UnityEngine.Bounds(vertices[0],UnityEngine.Vector3.zero);
    foreach(var v in vertices) b.Encapsulate(v);
    return new {min=V(b.min),max=V(b.max)};
}
try
{
    using(var hash=System.Security.Cryptography.SHA256.Create())
        actualSha=System.BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath,modelPath.Substring(7))))).Replace("-","").ToLowerInvariant();
    if(actualSha!=expectedSha) throw new System.Exception("Source FBX changed: re-pin diagnostic before evaluation.");
    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    if(!prefab) throw new System.Exception("Human prefab missing.");
    instance=UnityEngine.Object.Instantiate(prefab);
    instance.name="HumanFaceImportDifferential_Temporary";
    instance.hideFlags=UnityEngine.HideFlags.HideAndDontSave;
    instance.transform.SetPositionAndRotation(UnityEngine.Vector3.zero,UnityEngine.Quaternion.identity);
    var animator=instance.GetComponentInChildren<UnityEngine.Animator>(true);
    if(!animator) throw new System.Exception("Animator missing.");
    animator.enabled=false;
    foreach(var renderer in instance.GetComponentsInChildren<UnityEngine.Renderer>(true)) renderer.enabled=false;
    var contract=instance.GetComponent<LetMeSleep.Presentation.VisualAttentionContract>();
    if(!contract || contract.SourceSha256!=actualSha) throw new System.Exception("Facial contract missing or stale.");
    if(!LetMeSleep.Presentation.VisualAttentionFactory.TryInstall(instance,true,out var attention,out var reason))
        throw new System.Exception(reason);
    var rendererHead=contract.Rig.Eyelids;
    if(!rendererHead || rendererHead.name!="HumanHead") throw new System.Exception("Expected HumanHead renderer binding.");
    var mesh=rendererHead.sharedMesh;
    var vertices=mesh.vertices;
    var bones=rendererHead.bones;
    var skin=mesh.boneWeights;
    var bindposes=mesh.bindposes;
    int headIndex=System.Array.IndexOf(bones,contract.Rig.Head);
    if(headIndex<0) throw new System.Exception("Head absent from renderer skin bones.");
    var up=contract.Rig.HeadUp.normalized;
    var forward=contract.Rig.HeadForward.normalized;
    var lateral=UnityEngine.Vector3.Cross(up,forward).normalized;
    float Height(int i) => UnityEngine.Vector3.Dot(bindposes[headIndex].MultiplyPoint3x4(vertices[i]),up)+1.34f;
    bool EyeRegion(int i)
    {
        var p=bindposes[headIndex].MultiplyPoint3x4(vertices[i]);
        float h=UnityEngine.Vector3.Dot(p,up)+1.34f;
        float x=UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(p,lateral));
        float depth=UnityEngine.Vector3.Dot(p,forward);
        return h>=1.49f && h<=1.625f && x>=.023f && x<=.139f && depth>=.105f && depth<=.170f;
    }
    object SkinOf(int i)
    {
        var w=skin[i]; var rows=new System.Collections.Generic.List<object>();
        var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
        var weights=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
        for(int k=0;k<4;k++) if(weights[k]>0) rows.Add(new {bone=bones[ids[k]].name,weight=weights[k]});
        return rows;
    }
    var names=new[]{"Blink25.L","Blink50.L","Blink75.L","Blink.L","Blink25.R","Blink50.R","Blink75.R","Blink.R"};
    var indexes=new int[8];
    for(int s=0;s<8;s++)
    {
        if(timer.Elapsed.TotalSeconds>4.2) {queued=true;break;}
        int index=mesh.GetBlendShapeIndex(names[s]); indexes[s]=index;
        if(index<0) throw new System.Exception("Missing shape "+names[s]);
        for(int f=0;f<mesh.GetBlendShapeFrameCount(index);f++)
        {
            var dv=new UnityEngine.Vector3[vertices.Length];
            var dn=new UnityEngine.Vector3[vertices.Length];
            var dt=new UnityEngine.Vector3[vertices.Length];
            mesh.GetBlendShapeFrameVertices(index,f,dv,dn,dt);
            var affected=new System.Collections.Generic.List<object>();
            int normalOnly=0,normalOutside=0; float maxDelta=0;
            for(int i=0;i<vertices.Length;i++)
            {
                bool moved=dv[i].sqrMagnitude>1e-12f;
                if(dn[i].sqrMagnitude>1e-12f) {if(!moved) normalOnly++;if(!EyeRegion(i)) normalOutside++;}
                if(!moved) continue;
                support.Add(i);if(!EyeRegion(i)) outsideRoi.Add(i);
                maxDelta=UnityEngine.Mathf.Max(maxDelta,dv[i].magnitude);
                affected.Add(new {index=i,basis=V(vertices[i]),delta=V(dv[i]),normalDelta=V(dn[i]),
                    headBindLocal=V(bindposes[headIndex].MultiplyPoint3x4(vertices[i])),
                    sourceHeightMeters=Height(i),outsideAuthoredEyeRegion=!EyeRegion(i),skin=SkinOf(i)});
            }
            shapeRows.Add(new {name=names[s],index,frame=f,frameWeight=mesh.GetBlendShapeFrameWeight(index,f),
                movingCount=affected.Count,maxDelta,normalOnly,normalOutsideEyeRegion=normalOutside,vertices=affected});
        }
    }
    if(!queued)
    {
        baked=new UnityEngine.Mesh();
        var allClips=animator.runtimeAnimatorController.animationClips;
        var clips=new[]{"Human_Idle","Human_Crouch","Human_Swat"};
        var phases=new[]{0f,.8f,.48f};
        for(int p=0;p<clips.Length && !queued;p++)
        {
            UnityEngine.AnimationClip clip=null;
            foreach(var c in allClips) if(c.name==clips[p]) {clip=c;break;}
            if(!clip) throw new System.Exception("Missing diagnostic clip "+clips[p]);
            for(int looking=0;looking<2 && !queued;looking++)
            {
                attention.SetReducedMotion(true);attention.PrepareForAnimation();
                clip.SampleAnimation(animator.gameObject,clip.length*phases[p]);
                for(int s=0;s<8;s++) rendererHead.SetBlendShapeWeight(indexes[s],0);
                if(looking==1)
                {
                    attention.SetReducedMotion(false);
                    attention.SetLookPoint(contract.Rig.Head.position+instance.transform.right*2f+instance.transform.forward*2f+UnityEngine.Vector3.up*.8f);
                    for(int frame=0;frame<20;frame++)
                    {
                        attention.PrepareForAnimation();
                        attention.EvaluateAfterAnimation(1f/60f);
                    }
                }
                // Freeze this evaluated head/neck pose while varying only eyelid weights.
                UnityEngine.Vector3[] baseline=null;
                for(int step=0;step<=4;step++)
                {
                    if(timer.Elapsed.TotalSeconds>4.2) {queued=true;break;}
                    for(int s=0;s<8;s++) rendererHead.SetBlendShapeWeight(indexes[s],0);
                    if(step>0) {rendererHead.SetBlendShapeWeight(indexes[step-1],100);rendererHead.SetBlendShapeWeight(indexes[step+3],100);}
                    rendererHead.BakeMesh(baked,true);
                    var posed=baked.vertices;
                    var actor=new UnityEngine.Vector3[posed.Length];
                    for(int i=0;i<posed.Length;i++) actor[i]=instance.transform.InverseTransformPoint(rendererHead.transform.TransformPoint(posed[i]));
                    if(baseline==null) baseline=actor;
                    int changedOutside=0;float maxBlinkDelta=0,maxOutside=0,maxChin=0,maxLowerCheek=0;
                    var unexpected=new System.Collections.Generic.List<object>();
                    for(int i=0;i<actor.Length;i++)
                    {
                        float d=UnityEngine.Vector3.Distance(actor[i],baseline[i]);
                        maxBlinkDelta=UnityEngine.Mathf.Max(maxBlinkDelta,d);
                        if(Height(i)<1.46f) maxChin=UnityEngine.Mathf.Max(maxChin,d);
                        if(Height(i)>=1.46f && Height(i)<1.49f) maxLowerCheek=UnityEngine.Mathf.Max(maxLowerCheek,d);
                        if(!support.Contains(i) && d>1e-6f)
                        {
                            changedOutside++;maxOutside=UnityEngine.Mathf.Max(maxOutside,d);
                            unexpected.Add(new {index=i,basis=V(vertices[i]),before=V(baseline[i]),after=V(actor[i]),skin=SkinOf(i)});
                        }
                    }
                    samples.Add(new {clip=clips[p],phase=phases[p],attentionActive=looking==1,closure=step*.25f,
                        headPosition=V(contract.Rig.Head.position),headEuler=V(contract.Rig.Head.localEulerAngles),neckEuler=V(contract.Rig.Neck.localEulerAngles),
                        actorBounds=BoundsOf(actor),maxBlinkDeltaMeters=maxBlinkDelta,maxChinBlinkDeltaMeters=maxChin,
                        maxLowerCheekBlinkDeltaMeters=maxLowerCheek,changedOutsideMorphSupport=changedOutside,
                        maxOutsideDeltaMeters=maxOutside,unexpectedVertices=unexpected});
                }
            }
        }
    }
}
catch(System.Exception exception) {error=exception.ToString();}
finally
{
    if(baked) UnityEngine.Object.DestroyImmediate(baked);
    if(instance) UnityEngine.Object.DestroyImmediate(instance);
}
var receipt=new {utc=System.DateTime.UtcNow.ToString("O"),unity=UnityEngine.Application.unityVersion,
    scope="Imported FBX sparse deltas and CPU BakeMesh; no rendering, visual approval, live actor or runtime mutation.",
    prefabPath,modelPath,expectedSha,actualSha,elapsedSeconds=timer.Elapsed.TotalSeconds,
    status=error!=null?"ERROR":queued?"PARTIAL_QUEUE_REMAINDER":"MEASURED_REVIEW_REQUIRED",error,
    uniqueMorphSupport=support.Count,uniqueOutsideEyeRegion=outsideRoi.Count,shapes=shapeRows,samples,
    limits="Eye ROI uses certified Head bind axes and source Head origin z1.34. Mesh indices may split/reorder on import. Each pose compares closure against its own zero-closure baseline; head motion itself is not a blink delta. Normals/material artifacts and side-view clipping require graphic review."};
System.IO.Directory.CreateDirectory(outputDir);
var output=System.IO.Path.Combine(outputDir,"face-import-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
System.IO.File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));
return new {output,receipt.status,receipt.error,receipt.elapsedSeconds,shapeCount=shapeRows.Count,sampleCount=samples.Count};
