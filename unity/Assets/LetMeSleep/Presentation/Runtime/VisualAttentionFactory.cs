using UnityEngine;

namespace LetMeSleep.Presentation
{
    public static class VisualAttentionFactory
    {
        public static bool TryInstall(GameObject visual,bool manualEvaluation,out VisualAttentionRig controller,out string reason)
        {
            controller=null;
            if(!visual) { reason="visual missing"; return false; }
            var contract=visual.GetComponent<VisualAttentionContract>();
            if(!contract) { reason="explicit facial contract absent"; return false; }
            if(contract.Schema!=VisualAttentionContract.SupportedSchema || string.IsNullOrWhiteSpace(contract.RigRevision) ||
                !Digest(contract.SourceSha256) || !contract.UnityAxesVerified || contract.Rig==null)
            { reason="facial contract revision/hash/imported axes not certified"; return false; }
            var source=contract.Rig;
            if(!source.LeftEye || !source.RightEye || (source.ReadLegacyEyeScaleBlink && !contract.LegacyScaleBlinkVerified))
            { reason="pupil bindings missing or legacy scale conversion not explicitly certified"; return false; }
            var writtenBones=new System.Collections.Generic.HashSet<Transform>();
            foreach(var bone in new[]{source.Head,source.Neck,source.LeftEye,source.RightEye})
                if(bone && !writtenBones.Add(bone)) { reason="duplicate facial joint binding"; return false; }
            foreach(var lids in new[]{source.LeftLids,source.RightLids})
                if(lids!=null) foreach(var lid in lids)
                    if(lid==null || !lid.Bone || !writtenBones.Add(lid.Bone) || float.IsNaN(lid.ClosedAngleDegrees) ||
                        float.IsInfinity(lid.ClosedAngleDegrees) || Mathf.Abs(lid.ClosedAngleDegrees)<.001f)
                    { reason="duplicate or invalid eyelid binding"; return false; }
            var existing=visual.GetComponentsInChildren<VisualAttentionRig>(true);
            if(existing.Length>0)
            {
                if(existing.Length==1 && existing[0]==contract.Installed && existing[0].IsConfigured && existing[0].IsManualEvaluation==manualEvaluation)
                { controller=existing[0]; controller.enabled=true; reason=null; return true; }
                reason="another facial writer exists or evaluation mode differs"; return false;
            }
            var bindings=new VisualAttentionRig.Bindings
            {
                Head=source.Head,Neck=source.Neck,LeftEye=source.LeftEye,RightEye=source.RightEye,
                HeadForward=source.HeadForward,HeadUp=source.HeadUp,EyeForward=source.EyeForward,EyeUp=source.EyeUp,
                HeadYawLimit=source.HeadYawLimit,HeadPitchLimit=source.HeadPitchLimit,EyeYawLimit=source.EyeYawLimit,EyePitchLimit=source.EyePitchLimit,
                Eyelids=source.Eyelids,LeftBlinkShapes=source.LeftBlinkShapes,RightBlinkShapes=source.RightBlinkShapes,
                LeftLids=source.LeftLids,RightLids=source.RightLids,ReadLegacyEyeScaleBlink=source.ReadLegacyEyeScaleBlink,
                ManualEvaluation=manualEvaluation
            };
            controller=visual.AddComponent<VisualAttentionRig>();
            if(!controller.Configure(bindings) || !controller.SupportsBlink)
            {
                controller.enabled=false; Object.Destroy(controller); controller=null;
                reason="facial bindings or complete blink samples/lids invalid"; return false;
            }
            contract.Installed=controller; reason=null; return true;
        }
        public static bool TryInstallPreview(GameObject visual,Camera camera,out string reason)
        {
            if(!camera) { reason="preview camera missing"; return false; }
            if(!TryInstall(visual,false,out var rig,out reason)) return false;
            var target=visual.GetComponent<PreviewAttentionTarget>();
            if(!target) target=visual.AddComponent<PreviewAttentionTarget>();
            target.Bind(camera,rig); return true;
        }
        private static bool Digest(string value)
        {
            if(value==null || value.Length!=64) return false;
            foreach(char character in value) if(!System.Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
