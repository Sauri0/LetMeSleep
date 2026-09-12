using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    // Derive axes from the imported bind frame and actual pupil/lid geometry, not Blender axis guesses.
    public static class FacialContentBuilder
    {
        private const string Root="Assets/LetMeSleep/Content/Characters/";
        internal static readonly string[] Models={Root+"Models/LMS_Human_alpha.fbx",Root+"Models/LMS_HumanMenu.fbx",Root+"Models/LMS_Mosquito_alpha.fbx"};
        internal static readonly string[] Prefabs={Root+"Prefabs/LMS_Human.prefab",Root+"Prefabs/LMS_Human_FirstPerson.prefab",Root+"Prefabs/LMS_HumanMenu.prefab",Root+"Prefabs/LMS_Mosquito.prefab"};
        public static void BuildAll(AlfaApplication app)
        {
            // Persist rejection on EVERY old marker before any imported geometry gate can throw.
            InvalidateAll("build started");
            try
            {
                Require(app,"missing bootstrap");
                // Make the measured imported meshes correspond to the exact on-disk FBX and current import settings.
                var hashes=new Dictionary<string,string>(); var imports=new Dictionary<string,string>();
                foreach(var model in Models)
                {
                    string before=Hash(model);
                    AssetDatabase.ImportAsset(model,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                    Require(Hash(model)==before,"FBX changed while importing: "+model);
                    hashes.Add(model,before); imports.Add(model,AssetDatabase.GetAssetDependencyHash(model).ToString());
                }
                FacialContractImportGuard.FlushPending();
                Bind(Prefabs[0],Models[0],true,hashes[Models[0]],imports[Models[0]]);
                Bind(Prefabs[1],Models[0],true,hashes[Models[0]],imports[Models[0]]);
                Bind(Prefabs[2],Models[1],true,hashes[Models[1]],imports[Models[1]]);
                Bind(Prefabs[3],Models[2],false,hashes[Models[2]],imports[Models[2]]);
                foreach(var model in Models) Require(Hash(model)==hashes[model] && AssetDatabase.GetAssetDependencyHash(model).ToString()==imports[model],"model changed before build commit: "+model);
                app.HumanPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[0]);
                app.MosquitoPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[3]);
                app.MenuHumanPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[2]);
            }
            catch(Exception failure)
            {
                try { InvalidateAll("build failed"); }
                catch(Exception invalidationFailure) { throw new AggregateException(failure,invalidationFailure); }
                throw;
            }
        }
        internal static void InvalidateAll(string reason)
        {
            var errors=new List<Exception>();
            foreach(var prefab in Prefabs)
            {
                if(!File.Exists(prefab)) continue; // No prefab means no persisted marker to invalidate.
                GameObject instance=null;
                try
                {
                    instance=PrefabUtility.LoadPrefabContents(prefab);
                    bool changed=false;
                    foreach(var marker in instance.GetComponentsInChildren<VisualAttentionContract>(true))
                    {
                        if(!marker.UnityAxesVerified && !marker.LegacyScaleBlinkVerified && string.IsNullOrEmpty(marker.SourceSha256) && string.IsNullOrEmpty(marker.RigRevision) && marker.Rig==null) continue;
                        marker.UnityAxesVerified=false; marker.LegacyScaleBlinkVerified=false;
                        marker.SourceSha256=""; marker.RigRevision=""; marker.Rig=null; changed=true;
                    }
                    if(changed) Require(PrefabUtility.SaveAsPrefabAsset(instance,prefab),"could not invalidate "+prefab);
                }
                catch(Exception error) { errors.Add(new InvalidDataException("Cannot invalidate "+prefab+": "+reason,error)); }
                finally { if(instance) PrefabUtility.UnloadPrefabContents(instance); }
            }
            if(errors.Count>0) throw new AggregateException("Facial markers could not all be invalidated",errors);
        }
        private static string Hash(string path)
        {
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        private static Transform Bone(CharacterView view,string name) =>
            view.Animator.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        private static void Require(bool test,string message) { if(!test) throw new InvalidDataException("Facial import: "+message); }
        private static void Bind(string prefab,string model,bool human,string modelHash,string importHash)
        {
            Require(Hash(model)==modelHash && AssetDatabase.GetAssetDependencyHash(model).ToString()==importHash,"model changed before validation: "+model);
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var view=root.GetComponent<CharacterView>();
                Require(view && view.Animator,"missing visual/animator");
                // Imported Generic rigs can start in the first take rather than the bind pose.
                bool animatorEnabled=view.Animator.enabled;
                view.Animator.enabled=false;
                RestoreAndValidateBindPose(view.transform);
                view.Animator.enabled=animatorEnabled;
                var head=Bone(view,"Head");
                var left=Bone(view,human ? "Eye.L" : "Pupil.L");
                var right=Bone(view,human ? "Eye.R" : "Pupil.R");
                Vector3 forward=root.transform.forward, up=root.transform.up;
                foreach(var eye in new[]{left,right})
                {
                    Vector3 pupil=WeightedCenter(view,eye)-eye.position;
                    Require(pupil.sqrMagnitude>1e-8f && Vector3.Dot(pupil.normalized,forward)>.96f,"pupil does not face imported character front: "+eye.name);
                }
                var bindings=new VisualAttentionRig.Bindings
                {
                    Head=head, Neck=human ? Bone(view,"Neck") : null, LeftEye=left, RightEye=right,
                    HeadForward=head.InverseTransformDirection(forward).normalized,
                    HeadUp=head.InverseTransformDirection(up).normalized,
                    EyeForward=left.InverseTransformDirection(forward).normalized,
                    EyeUp=left.InverseTransformDirection(up).normalized,
                    HeadYawLimit=human ? 55 : 25, HeadPitchLimit=human ? 25 : 15,
                    EyeYawLimit=human ? 22 : 12, EyePitchLimit=human ? 15 : 10,
                    ReadLegacyEyeScaleBlink=false
                };
                Require(Vector3.Dot(right.TransformDirection(bindings.EyeForward).normalized,forward)>.999f &&
                    Vector3.Dot(right.TransformDirection(bindings.EyeUp).normalized,up)>.999f,"asymmetric imported eye bases");
                if(human)
                {
                    Require(Vector3.Dot(bindings.Neck.TransformDirection(bindings.HeadForward).normalized,forward)>.999f &&
                        Vector3.Dot(bindings.Neck.TransformDirection(bindings.HeadUp).normalized,up)>.999f,"neck/head bases differ");
                    bindings.Eyelids=view.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="HumanHead");
                    foreach(var shape in bindings.LeftBlinkShapes.Concat(bindings.RightBlinkShapes))
                        Require(bindings.Eyelids.sharedMesh.GetBlendShapeIndex(shape)>=0,"missing imported morph "+shape);
                }
                else
                {
                    bindings.LeftLids=new[]{Lid(view,"LidUpper.L",true),Lid(view,"LidLower.L",false)};
                    bindings.RightLids=new[]{Lid(view,"LidUpper.R",true),Lid(view,"LidLower.R",false)};
                }
                var marker=root.GetComponent<VisualAttentionContract>() ?? root.AddComponent<VisualAttentionContract>();
                marker.Schema=VisualAttentionContract.SupportedSchema;
                marker.RigRevision=human ? "human-joints2-facial" : "mosquito-r4-facial";
                Require(Hash(model)==modelHash && AssetDatabase.GetAssetDependencyHash(model).ToString()==importHash,"model/import changed during validation");
                marker.SourceSha256=modelHash;
                marker.Rig=bindings; marker.LegacyScaleBlinkVerified=false;
                marker.UnityAxesVerified=true; // Geometric bind-frame checks above ran on this exact imported model.
                Require(PrefabUtility.SaveAsPrefabAsset(root,prefab),"could not save "+prefab);
                Debug.Log("LMS_FACIAL_IMPORT_BOUND "+prefab+" "+marker.SourceSha256+"; axes/morph availability checked, animated visual acceptance pending.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void RestoreAndValidateBindPose(Transform model)
        {
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length>0,"no skinned mesh for bind validation");
            var matrices=new Dictionary<Transform,Matrix4x4>();
            foreach(var renderer in renderers)
            {
                Require(renderer.sharedMesh,"missing skin mesh");
                var poses=renderer.sharedMesh.bindposes;
                Require(poses.Length==renderer.bones.Length,"bind pose count mismatch: "+renderer.name);
                for(int i=0;i<poses.Length;i++)
                {
                    var bone=renderer.bones[i]; Require(bone && bone.IsChildOf(model),"missing or foreign bind bone");
                    var world=renderer.localToWorldMatrix*poses[i].inverse;
                    if(matrices.TryGetValue(bone,out var previous)) RequireMatrix(previous,world,"inconsistent skin bind for "+bone.name);
                    else matrices.Add(bone,world);
                }
            }
            // Explicit parent-first order. Recomposition check rejects unsupported shear/reflection decomposition.
            foreach(var bone in matrices.Keys.OrderBy(Depth))
            {
                var world=matrices[bone];
                var local=bone.parent ? bone.parent.worldToLocalMatrix*world : world;
                bone.localPosition=local.GetColumn(3); bone.localRotation=local.rotation; bone.localScale=local.lossyScale;
                RequireMatrix(bone.localToWorldMatrix,world,"cannot reconstruct bind transform: "+bone.name);
            }
            foreach(var renderer in renderers)
            {
                var poses=renderer.sharedMesh.bindposes;
                for(int i=0;i<poses.Length;i++)
                    RequireMatrix(renderer.worldToLocalMatrix*renderer.bones[i].localToWorldMatrix*poses[i],Matrix4x4.identity,
                        "skin not in bind pose: "+renderer.name+"/"+renderer.bones[i].name);
            }
        }
        private static int Depth(Transform value) { int result=0; for(var parent=value.parent;parent;parent=parent.parent) result++; return result; }
        private static void RequireMatrix(Matrix4x4 actual,Matrix4x4 expected,string message)
        {
            for(int i=0;i<16;i++)
                Require(!float.IsNaN(actual[i]) && !float.IsInfinity(actual[i]) && !float.IsNaN(expected[i]) && !float.IsInfinity(expected[i]) &&
                    Mathf.Abs(actual[i]-expected[i])<=.0001f*Mathf.Max(1,Mathf.Abs(expected[i])),message);
        }
        private static VisualAttentionRig.BlinkBone Lid(CharacterView view,string name,bool upper)
        {
            var bone=Bone(view,name);
            Vector3 center=WeightedCenter(view,bone)-bone.position;
            Vector3 axis=view.transform.right, forward=view.transform.forward, up=view.transform.up;
            Vector3 positive=Quaternion.AngleAxis(90,axis)*center, negative=Quaternion.AngleAxis(-90,axis)*center;
            float sign=Vector3.Dot(positive,forward)>Vector3.Dot(negative,forward) ? 1 : -1;
            Vector3 closed=sign>0 ? positive : negative;
            Require(Vector3.Dot(closed,forward)>0 && Vector3.Dot(closed,up)*(upper ? 1 : -1)>0,
                "cannot derive closed-lid orientation "+name);
            return new VisualAttentionRig.BlinkBone { Bone=bone, LocalAxis=bone.InverseTransformDirection(axis).normalized, ClosedAngleDegrees=90*sign };
        }
        private static Vector3 WeightedCenter(CharacterView view,Transform bone)
        {
            Vector3 sum=Vector3.zero; int count=0;
            foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int index=Array.IndexOf(renderer.bones,bone); if(index<0) continue;
                var mesh=renderer.sharedMesh; var vertices=mesh.vertices; var weights=mesh.boneWeights;
                Require(vertices.Length==weights.Length,"missing skin weights");
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];
                    float influence=(w.boneIndex0==index ? w.weight0 : 0)+(w.boneIndex1==index ? w.weight1 : 0)+
                        (w.boneIndex2==index ? w.weight2 : 0)+(w.boneIndex3==index ? w.weight3 : 0);
                    if(influence<.999f) continue;
                    sum+=renderer.transform.TransformPoint(vertices[i]); count++;
                }
            }
            Require(count>0,"no rigid pupil/lid vertices for "+bone.name);
            return sum/count;
        }
    }
}
