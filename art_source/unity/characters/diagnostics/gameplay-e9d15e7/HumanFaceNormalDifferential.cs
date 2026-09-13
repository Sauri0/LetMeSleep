// External CLI body. Read prefab asset only; no instances, imports or rendering.
var watch=System.Diagnostics.Stopwatch.StartNew();
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Human.prefab");
var c=prefab.GetComponent<LetMeSleep.Presentation.VisualAttentionContract>();
var renderer=c.Rig.Eyelids;var mesh=renderer.sharedMesh;
var vertices=mesh.vertices;var normals=mesh.normals;var skin=mesh.boneWeights;var bones=renderer.bones;
var headBind=mesh.bindposes[System.Array.IndexOf(bones,c.Rig.Head)];
var up=c.Rig.HeadUp.normalized;var forward=c.Rig.HeadForward.normalized;
var lateral=UnityEngine.Vector3.Cross(up,forward).normalized;
float[] V(UnityEngine.Vector3 p)=>new[]{p.x,p.y,p.z};
float Height(int i)=>UnityEngine.Vector3.Dot(headBind.MultiplyPoint3x4(vertices[i]),up)+1.34f;
bool EyeRegion(int i)
{
    var p=headBind.MultiplyPoint3x4(vertices[i]);float h=Height(i);
    float x=UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(p,lateral)),d=UnityEngine.Vector3.Dot(p,forward);
    return h>=1.49f&&h<=1.625f&&x>=.023f&&x<=.139f&&d>=.105f&&d<=.170f;
}
var shapeNames=new[]{"Blink25.","Blink50.","Blink75.","Blink."};
var rows=new System.Collections.Generic.List<object>();
for(int step=0;step<4;step++)
{
    var leftP=new UnityEngine.Vector3[vertices.Length];var rightP=new UnityEngine.Vector3[vertices.Length];
    var leftN=new UnityEngine.Vector3[vertices.Length];var rightN=new UnityEngine.Vector3[vertices.Length];
    mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex(shapeNames[step]+"L"),0,leftP,leftN,null);
    mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex(shapeNames[step]+"R"),0,rightP,rightN,null);
    var changed=new System.Collections.Generic.List<object>();
    float maxOutsideAngle=0,maxChinAngle=0,maxCheekAngle=0;int overOneDegree=0;
    for(int i=0;i<vertices.Length;i++)
    {
        if(EyeRegion(i))continue;
        float angle=UnityEngine.Vector3.Angle(normals[i],normals[i]+leftN[i]+rightN[i]);
        maxOutsideAngle=UnityEngine.Mathf.Max(maxOutsideAngle,angle);
        if(Height(i)<1.46f)maxChinAngle=UnityEngine.Mathf.Max(maxChinAngle,angle);
        if(Height(i)>=1.46f&&Height(i)<1.50f)maxCheekAngle=UnityEngine.Mathf.Max(maxCheekAngle,angle);
        if(angle>1)overOneDegree++;
        if(leftN[i].sqrMagnitude+rightN[i].sqrMagnitude<1e-12f)continue;
        var w=skin[i];var weights=new System.Collections.Generic.List<object>();
        var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var ws=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
        for(int k=0;k<4;k++)if(ws[k]>0)weights.Add(new{bone=bones[ids[k]].name,weight=ws[k]});
        changed.Add(new{index=i,basis=V(vertices[i]),sourceHeightMeters=Height(i),baseNormal=V(normals[i]),
            deltaLeft=V(leftN[i]),deltaRight=V(rightN[i]),angleDegrees=angle,
            positionDeltaMeters=(leftP[i]+rightP[i]).magnitude,skin=weights});
    }
    rows.Add(new{closure=(step+1)*.25f,outsideEyeRecords=changed.Count,overOneDegree,maxOutsideAngle,maxChinAngle,maxCheekAngle,vertices=changed});
}
var receipt=new{utc=System.DateTime.UtcNow.ToString("O"),unity=UnityEngine.Application.unityVersion,
    sourceSha=c.SourceSha256,mesh=mesh.name,vertexCount=vertices.Length,
    scope="Imported normal deltas outside authored eye ROI; paired left+right samples, no renderer/visual acceptance.",
    elapsedSeconds=watch.Elapsed.TotalSeconds,rows};
var path="N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/diagnostics/gameplay-e9d15e7/unity-receipts/face-normals-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json";
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
System.IO.File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));
return new{path,receipt.elapsedSeconds,rows=rows.Count};
