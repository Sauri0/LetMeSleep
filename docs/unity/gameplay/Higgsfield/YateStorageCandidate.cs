using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicitly authorized rigid relocation of one storage chest/lid on an owned clone.
public static class YateStorageCandidate
{
    public sealed class Receipt
    {
        public string revision="yate-swim-stair-storage-relocation-01";
        public Vector3 worldDelta=new Vector3(-1.4f,0,2);
        public List<Part> parts=new List<Part>();
        public List<Support> supports=new List<Support>();
        public float maximumConservativeBoxPenetration;
        public string blocker;
        public Vector3 destinationMin,destinationMax;
    }
    public sealed class Part{public string name,meshGuid;public long meshLocalId;public Vector3 before,after;public Quaternion rotation;public Vector3 scale;public string[] materialGuids;}
    public sealed class Support{public Vector3 footprint,physicalFloor,visibleTeak;public float physicalGap,visibleGap;}
    public static Receipt Apply(GameObject clone)
    {
        Require(clone.GetComponent<EnvironmentMapDefinition>().MapId=="hf-yate-a-la-deriva-v3","Yate v3 only.");
        var receipt=new Receipt();var transforms=clone.GetComponentsInChildren<Transform>(true);
        foreach(string name in new[]{"YATE_DeckStorage_01","YATE_DeckStorage_Lid_01"})
        {
            var transform=transforms.Single(t=>t.name==name);var filter=transform.GetComponent<MeshFilter>();var renderer=transform.GetComponent<Renderer>();
            Require(filter && renderer && transform.GetComponent<MeshCollider>(),"Expected original storage mesh/renderer/collider.");
            var part=new Part{name=name,before=transform.position,rotation=transform.rotation,scale=transform.localScale,materialGuids=renderer.sharedMaterials.Select(m=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).ToArray()};
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh,out part.meshGuid,out part.meshLocalId),"Original mesh reference required.");
            Require(part.before.sqrMagnitude<.000001f,"Expected baked source geometry with unchanged transform origin.");
            transform.position+=receipt.worldDelta;part.after=transform.position;receipt.parts.Add(part);
        }
        Physics.SyncTransforms();var parts=receipt.parts.Select(p=>transforms.Single(t=>t.name==p.name).GetComponent<Collider>()).ToArray();
        Bounds bounds=parts[0].bounds;bounds.Encapsulate(parts[1].bounds);receipt.destinationMin=bounds.min;receipt.destinationMax=bounds.max;
        GameObject probe=null;MeshCollider teakProbe=null;
        try
        {
            probe=new GameObject("YateStorageClearance_TEMP"){hideFlags=HideFlags.HideAndDontSave};var box=probe.AddComponent<BoxCollider>();box.size=bounds.size;probe.transform.position=bounds.center;
            foreach(var other in clone.GetComponentsInChildren<Collider>(false).Where(c=>c.enabled && !c.isTrigger && !parts.Contains(c)))
                if(Physics.ComputePenetration(box,bounds.center,Quaternion.identity,other,other.transform.position,other.transform.rotation,out _,out float depth) && depth>receipt.maximumConservativeBoxPenetration){receipt.maximumConservativeBoxPenetration=depth;receipt.blocker=other.name;}
            Require(receipt.maximumConservativeBoxPenetration<=.002f,"Destination conservative box overlaps "+receipt.blocker);
            var physical=transforms.Single(t=>t.name=="YATE_MainDeck_Continuous").GetComponent<Collider>();
            var teak=transforms.Single(t=>t.name=="YATE_Teak_MainDeck");Require(!teak.GetComponent<Collider>(),"Visible teak should remain non-solid.");
            teakProbe=teak.gameObject.AddComponent<MeshCollider>();teakProbe.sharedMesh=teak.GetComponent<MeshFilter>().sharedMesh;Physics.SyncTransforms();
            foreach(var offset in new[]{Vector3.zero,new Vector3(-.35f,0,-.25f),new Vector3(.35f,0,-.25f),new Vector3(-.35f,0,.25f),new Vector3(.35f,0,.25f)})
            {
                var foot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)+offset;var ray=new Ray(foot+Vector3.up*.2f,Vector3.down);
                Require(physical.Raycast(ray,out var ground,.5f),"Destination footprint has no physical deck support.");
                Require(teakProbe.Raycast(ray,out var visual,.5f),"Destination footprint has no visible teak support.");
                var row=new Support{footprint=foot,physicalFloor=ground.point,visibleTeak=visual.point,physicalGap=foot.y-ground.point.y,visibleGap=foot.y-visual.point.y};receipt.supports.Add(row);
                Require(Mathf.Abs(row.visibleGap)<.002f && row.physicalGap>=0 && row.physicalGap<.04f,"Chest must retain authored support on visible teak over physical deck.");
            }
        }
        finally{if(teakProbe)Object.DestroyImmediate(teakProbe);if(probe)Object.DestroyImmediate(probe);Physics.SyncTransforms();}
        return receipt;
    }
    static void Require(bool value,string text){if(!value)throw new Exception(text);}
}
