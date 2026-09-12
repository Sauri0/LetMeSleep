// Read-only: several snapshots at different actual frames; no animation/render/readback writes.
var rows=new System.Collections.Generic.List<object>();
foreach(var scene in UnityEngine.Object.FindObjectsByType<LetMeSleep.Presentation.MainMenuLivingScene>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
{
    var field=scene.GetType().GetField("bindings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    var binding=field?.GetValue(scene) as LetMeSleep.Presentation.MainMenuLivingScene.Bindings;
    var wings=new System.Collections.Generic.List<object>();
    if(binding?.MosquitoRoot)
        foreach(var bone in binding.MosquitoRoot.GetComponentsInChildren<UnityEngine.Transform>(true))
            if(bone.name=="Wing.L" || bone.name=="Wing.R") wings.Add(new{bone=bone.name,localRotation=bone.localRotation,worldRotation=bone.rotation,localPosition=bone.localPosition});
    var clip=binding?.Flight;
    string path=clip ? UnityEditor.AssetDatabase.GetAssetPath(clip) : null;
    rows.Add(new{scene=scene.name,scene.IsConfigured,scene.IsRunning,scene.ReducedMotion,scene.CurrentBeat,scene.FlightClipTime,
        active=scene.gameObject.activeInHierarchy,clip=clip?clip.name:null,path,guid=path==null?null:UnityEditor.AssetDatabase.AssetPathToGUID(path),
        length=clip?clip.length:0,frameRate=clip?clip.frameRate:0,
        animatorEnabled=binding?.MosquitoAnimator ? binding.MosquitoAnimator.enabled : false,
        animatorSpeed=binding?.MosquitoAnimator ? binding.MosquitoAnimator.speed : 0,
        animatorCulling=binding?.MosquitoAnimator ? binding.MosquitoAnimator.cullingMode.ToString() : null,wings});
}
return new{utc=System.DateTime.UtcNow.ToString("o"),frame=UnityEngine.Time.frameCount,renderedFrame=UnityEngine.Time.renderedFrameCount,
    time=UnityEngine.Time.unscaledTimeAsDouble,focused=UnityEngine.Application.isFocused,rows};
