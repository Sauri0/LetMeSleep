using System;
using System.IO;
using System.Linq;
using LetMeSleep.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Editor
{
    // Captures the real URP camera and optional runtime UI, restoring every temporary setting.
    public static class AlfaReviewCapture
    {
        public static string Capture(string path, int width = 1920, int height = 1080, bool includeUi = true)
        {
            var camera = Camera.main;
            if (!camera) camera = Camera.allCameras.FirstOrDefault(c => c.targetTexture == null);
            if (!camera) throw new InvalidOperationException("No active game camera.");
            var ui = UnityEngine.Object.FindFirstObjectByType<AlfaUiController>();
            var canvas = includeUi && ui ? ui.GetComponent<Canvas>() : null;
            var mode = canvas ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            var uiCamera = canvas ? canvas.worldCamera : null;
            float distance = canvas ? canvas.planeDistance : 0;
            var target = camera.targetTexture; var active = RenderTexture.active;
            var texture = new RenderTexture(width,height,24); texture.Create();
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = texture;
                if(canvas) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=Mathf.Max(1f,camera.nearClipPlane+.5f); }
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=texture });
                RenderTexture.active=texture;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,pixels.EncodeToPNG());
                return path;
            }
            finally
            {
                if(canvas) { canvas.renderMode=mode; canvas.worldCamera=uiCamera; canvas.planeDistance=distance; }
                camera.targetTexture=target; RenderTexture.active=active;
                if(pixels) UnityEngine.Object.DestroyImmediate(pixels);
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
