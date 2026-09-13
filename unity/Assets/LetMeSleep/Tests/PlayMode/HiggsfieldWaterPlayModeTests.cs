#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Environment.Higgsfield;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public class HiggsfieldWaterPlayModeTests
    {
        [Serializable] class Config { public string prefab,output; }
        GameObject instance;
        [UnityTest] public IEnumerator AuthoredWaterMovesAndRestoresSourceMeshes()
        {
            Time.timeScale=1;Time.captureDeltaTime=0;
            var args=System.Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-higgsfieldReview");
            if(i<0)Assert.Ignore("Requires explicit Higgsfield map review configuration.");
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(args[i+1]));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(config.prefab);
            Assert.That(prefab,Is.Not.Null);
            instance=Object.Instantiate(prefab);
            yield return null;
            var waters=instance.GetComponentsInChildren<HiggsfieldLowPolyWater>();
            Assert.That(waters.Length,Is.GreaterThan(0));
            var filters=waters.Select(w=>w.GetComponent<MeshFilter>()).ToArray();
            var skinned=waters.Select(w=>w.GetComponent<SkinnedMeshRenderer>()).ToArray();
            var vertices=filters.Select(f=>f?f.sharedMesh.vertices:null).ToArray();
            var weights=skinned.Select(s=>s?Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(s.GetBlendShapeWeight).ToArray():null).ToArray();
            yield return new WaitForSeconds(.8f);
            for(int n=0;n<waters.Length;n++)
            {
                Assert.That(waters[n].enabled,Is.True,waters[n].name);
                Assert.That(waters[n].GetComponent<Collider>(),Is.Null,waters[n].name);
                if(filters[n])
                {
                    Assert.That(AssetDatabase.Contains(filters[n].sharedMesh),Is.False,"Must animate a private copy");
                    Assert.That(vertices[n].Zip(filters[n].sharedMesh.vertices,(a,b)=>(a-b).sqrMagnitude).Max(),Is.GreaterThan(1e-8f),waters[n].name+" must move");
                }
                else
                {
                    Assert.That(skinned[n],Is.Not.Null);
                    Assert.That(weights[n].Select((w,k)=>Mathf.Abs(w-skinned[n].GetBlendShapeWeight(k))).Max(),Is.GreaterThan(.01f));
                }
                waters[n].enabled=false;
                if(filters[n])Assert.That(AssetDatabase.Contains(filters[n].sharedMesh),Is.True,"Must restore original mesh on disable");
            }
            Directory.CreateDirectory(config.output);
            File.WriteAllText(Path.Combine(config.output,"water-playmode.txt"),"PASS Unity "+Application.unityVersion+"; observed runtime motion and source restoration for "+string.Join(", ",waters.Select(w=>w.name))+". Not a performance benchmark.");
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(instance)Object.Destroy(instance);yield return null;}
    }
}
#endif
