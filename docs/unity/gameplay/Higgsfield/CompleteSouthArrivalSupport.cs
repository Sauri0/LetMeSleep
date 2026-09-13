using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LetMeSleep.Content.Environment;
using Object=UnityEngine.Object;

// Complete support of this one source path only. Disposable fixture clone, no persistence API.
public static class CompleteSouthArrivalSupport
{
    public static SouthArrivalColliderCandidate.Receipt Apply(GameObject map,List<Object> owned,int maxRows=3)
    {
        if(map.GetComponent<EnvironmentMapDefinition>()?.MapId!="hf-isla-del-laguito-v2")throw new Exception("Isla v2 required.");
        var candidates=map.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name=="Path_South_Arrival_COLLIDABLE").ToArray();
        if(candidates.Length!=1 || candidates[0].convex)throw new Exception("Expected one original concave south path.");
        var source=candidates[0];var mesh=source.sharedMesh;
        if(mesh==null || mesh.triangles.Length!=960 || source.GetComponent<MeshFilter>()?.sharedMesh!=mesh)throw new Exception("Source binding/topology changed; re-audit.");
        var vertices=mesh.vertices;var world=vertices.Select(v=>source.transform.TransformPoint(v)).ToArray();var tris=mesh.triangles;
        // Rounded keys absorb imported transform micrometres, never alter mesh vertex positions.
        var rows=world.Select(v=>Mathf.RoundToInt(v.z*10000)).Distinct().OrderBy(v=>v).ToArray();
        if(rows.Length!=41 || Math.Abs(rows[0]/10000f+37)>.0001f || Math.Abs(rows[40]/10000f+16.8f)>.0001f)throw new Exception("Audited41rows /20.2m bounds changed.");
        if(maxRows!=1 && maxRows!=3)throw new Exception("Only audited1or3row variants.");
        var receipt=new SouthArrivalColliderCandidate.Receipt{variant=maxRows==1?"one-row-convex-support":"complete-convex-support",sourceTriangles=320,minZ=world.Min(v=>v.z),maxZ=world.Max(v=>v.z),segments=new List<SouthArrivalColliderCandidate.Receipt>(),contactOffset=source.contactOffset,cookingOptions=source.cookingOptions.ToString(),cookingFlags=(int)source.cookingOptions,maxRowsPerHull=maxRows};
        var covered=new HashSet<int>();
        for(int first=0;first<40;)
        {
            int last=first+1;
            while(last<40 && last-first<maxRows && Bound(UniqueWorld(first,last+1))<=.0099f)last++;
            var cut=new List<int>();
            for(int t=0;t<tris.Length;t+=3)
            {
                float z=(world[tris[t]].z+world[tris[t+1]].z+world[tris[t+2]].z)/3;
                if(z<=rows[first]/10000f || z>=rows[last]/10000f)continue;
                if(!covered.Add(t/3))throw new Exception("Overlapping triangle assignment.");
                cut.AddRange(new[]{tris[t],tris[t+1],tris[t+2]});
            }
            if(cut.Count!=(last-first)*24)throw new Exception("Each full-width row must contain8triangles.");
            var indices=new Dictionary<Vector3Int,int>();var top=new List<Vector3>();var topWorld=new List<Vector3>();var faces=new List<int>();
            foreach(int i in cut)
            {
                var key=Vector3Int.RoundToInt(world[i]*10000);
                if(!indices.TryGetValue(key,out int index)){index=top.Count;indices.Add(key,index);top.Add(vertices[i]);topWorld.Add(world[i]);}faces.Add(index);
            }
            if(top.Count!=5*(last-first+1))throw new Exception("Unexpected grid width.");
            float bound=Bound(topWorld);if(bound>.01f)throw new Exception("Pre-cooking support raise bound exceeds1cm.");
            var edges=new Dictionary<(int,int),(int count,int a,int b)>();int topFaceCount=faces.Count;
            for(int i=0;i<topFaceCount;i+=3)for(int j=0;j<3;j++)
            {int a=faces[i+j],b=faces[i+(j+1)%3];var key=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(key,out var old);edges[key]=(old.count+1,a,b);}
            for(int i=0;i<topFaceCount;i+=3)faces.AddRange(new[]{faces[i]+top.Count,faces[i+2]+top.Count,faces[i+1]+top.Count});
            foreach(var e in edges.Values.Where(e=>e.count==1))faces.AddRange(new[]{e.b,e.a,e.a+top.Count,e.b,e.a+top.Count,e.b+top.Count});
            var hull=new Mesh{name="SouthPathSupport_"+first+"_"+last+"_TEMP"};owned.Add(hull);
            var down=source.transform.InverseTransformVector(Vector3.down*.04f);hull.vertices=top.Concat(top.Select(v=>v+down)).ToArray();hull.triangles=faces.ToArray();hull.RecalculateBounds();
            var child=new GameObject(hull.name){layer=source.gameObject.layer};child.transform.SetParent(source.transform,false);
            var collider=child.AddComponent<MeshCollider>();collider.sharedMaterial=source.sharedMaterial;collider.contactOffset=source.contactOffset;collider.cookingOptions=source.cookingOptions;collider.convex=true;collider.sharedMesh=hull;
            receipt.segments.Add(new SouthArrivalColliderCandidate.Receipt{variant="rows-"+first+"-"+last,sourceTriangles=cut.Count/3,removedTriangles=cut.Count/3,hullInputVertices=top.Count*2,minZ=topWorld.Min(v=>v.z),maxZ=topWorld.Max(v=>v.z),upperBoundTopRaise=bound});
            receipt.hullInputVertices+=top.Count*2;receipt.upperBoundTopRaise=Math.Max(receipt.upperBoundTopRaise,bound);first=last;
        }
        if(covered.Count!=320)throw new Exception("Complete support must cover all320originaltriangles.");
        receipt.removedTriangles=covered.Count;source.enabled=false; // visual MeshFilter, original mesh and every other collider stay intact.
        Physics.SyncTransforms();return receipt;

        List<Vector3> UniqueWorld(int a,int b)=>world.Where(v=>Mathf.RoundToInt(v.z*10000)>=rows[a] && Mathf.RoundToInt(v.z*10000)<=rows[b]).GroupBy(v=>Vector3Int.RoundToInt(v*10000)).Select(g=>g.First()).ToList();
    }
    static float Bound(List<Vector3> points)
    {
        // Least-squares plane with full2x2 covariance (native transform can slightly tilt axes).
        double mx=points.Average(v=>(double)v.x),my=points.Average(v=>(double)v.y),mz=points.Average(v=>(double)v.z);
        double xx=0,zz=0,xz=0,xy=0,zy=0;
        foreach(var p in points){double x=p.x-mx,y=p.y-my,z=p.z-mz;xx+=x*x;zz+=z*z;xz+=x*z;xy+=x*y;zy+=z*y;}
        double det=xx*zz-xz*xz;if(det<1e-10)throw new Exception("Degenerate support plane.");
        double a=(xy*zz-zy*xz)/det,b=(zy*xx-xy*xz)/det;
        var residual=points.Select(p=>p.y-(my+a*(p.x-mx)+b*(p.z-mz))).ToArray();
        return (float)(residual.Max()-residual.Min()+.00001); //10micrometre arithmetic allowance; not a cooking guarantee.
    }
}
