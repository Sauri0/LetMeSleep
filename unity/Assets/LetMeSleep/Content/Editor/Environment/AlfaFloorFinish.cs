using System;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        const float FloorRepeatWidth=1.28f,FloorRepeatLength=2.56f;

        static void BuildFloorFinish()
        {
            const int width=512,height=1024;var pixels=new Color[width*height];
            var oak=new Color(.34f,.215f,.12f,1); // warm oak, not a noisy photographic texture
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){
                int row=x/128;float px=(x+.5f)*FloorRepeatWidth/width,pz=(y+.5f)*FloorRepeatLength/height;
                float across=px% .32f;float along=(pz+(row%2)*.64f)%1.28f;
                int board=(int)((pz+(row%2)*.64f)/1.28f)%2;
                float variation=1f+(((row*7+board*3)%5)-2)*.035f;
                Color color=oak*variation;color.a=1;
                if(across<.0015f||across>.3185f||along<.0015f||along>1.2785f)color=new Color(.20f,.12f,.065f,1);
                else if(across<.006f||along<.006f){color*=1.04f;color.a=1;}
                // Two broad, low-contrast longitudinal fibers per board; no stochastic speckle.
                else if(Mathf.Abs(across-.105f)<.0018f||Mathf.Abs(across-.235f)<.0012f){color*=.975f;color.a=1;}
                pixels[y*width+x]=color;
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,false){name="Floor_Oak_WidePlanks",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            texture.SetPixels(pixels);texture.Apply(true,false);
            string path=Output+"/Materials/Floor_Oak_WidePlanks.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(saved==null){AssetDatabase.CreateAsset(texture,path);saved=texture;}
            else{EditorUtility.CopySerialized(texture,saved);Object.DestroyImmediate(texture);EditorUtility.SetDirty(saved);}
            var floor=materials["Floor_Oak"];floor.SetTexture("_BaseMap",saved);floor.SetColor("_BaseColor",Color.white);floor.SetFloat("_Smoothness",.16f);
            floor.SetTextureScale("_BaseMap",Vector2.one);floor.SetTextureOffset("_BaseMap",Vector2.zero);EditorUtility.SetDirty(floor);
            // Initial agreed bases; W2 owns subsequent lighting/material review and tuning.
            materials["Plaster_Warm"].SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(materials["Plaster_Warm"]);
            foreach(var pair in materials)if(pair.Key.StartsWith("Textile_",StringComparison.Ordinal)||pair.Key=="Linen"){
                pair.Value.SetFloat("_Smoothness",.10f);EditorUtility.SetDirty(pair.Value);}
        }

        static void ApplyFloorCoordinates(GameObject model,Vector3 finalScale)
        {
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;
                if(uv.Length!=vertices.Length)uv=new Vector2[vertices.Length];
                bool changed=false;int uv2Count=mesh.uv2.Length;
                for(int sub=0;sub<mesh.subMeshCount;sub++){
                    if(renderer.sharedMaterials[sub].name!="Floor_Oak")continue;
                    foreach(int index in mesh.GetIndices(sub)){
                        var p=Vector3.Scale(filter.transform.TransformPoint(vertices[index]),finalScale);
                        uv[index]=new Vector2(p.x/FloorRepeatWidth,p.z/FloorRepeatLength);changed=true;
                    }
                }
                if(changed){mesh.uv=uv;EditorUtility.SetDirty(mesh);Need(mesh.uv2.Length==uv2Count,"Floor finish must preserve lightmap coordinates");}
            }
        }
    }
}
