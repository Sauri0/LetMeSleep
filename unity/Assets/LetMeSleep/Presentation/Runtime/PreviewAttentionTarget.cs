using UnityEngine;

namespace LetMeSleep.Presentation
{
    [DefaultExecutionOrder(1150)]
    [DisallowMultipleComponent]
    public sealed class PreviewAttentionTarget : MonoBehaviour
    {
        private Camera cameraTarget;
        private VisualAttentionRig attention;
        public void Bind(Camera camera,VisualAttentionRig rig) { cameraTarget=camera; attention=rig; }
        private void LateUpdate()
        {
            if(!attention) return;
            Vector3 direction=cameraTarget ? cameraTarget.transform.position-attention.LookOrigin.position : Vector3.zero;
            // Keep profile/back inspection neutral instead of twisting the face to chase an orbiting camera.
            if(cameraTarget && Vector3.Dot(transform.forward,direction.normalized)>.25f) attention.SetLookTarget(cameraTarget.transform);
            else attention.ClearLookTarget();
        }
        private void OnDisable() { if(attention) attention.ClearLookTarget(); }
    }
}
