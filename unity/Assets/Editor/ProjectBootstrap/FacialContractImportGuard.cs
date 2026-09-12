using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    [InitializeOnLoad]
    public sealed class FacialContractImportGuard : AssetPostprocessor
    {
        private const string PendingKey="LetMeSleep.FacialContractImportGuard.Pending";
        private static string PendingFile=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/LetMeSleepFacialCertification.pending"));
        private static bool flushing,scheduled;
        static FacialContractImportGuard() { Schedule(); }
        private static bool Tracked(string path)=>FacialContentBuilder.Models.Any(model=>string.Equals(path,model,StringComparison.OrdinalIgnoreCase));
        private void OnPreprocessModel()
        {
            if(Tracked(assetPath)) MarkPending(); // Do not load/save prefabs inside model preprocessing.
        }
        private static void MarkPending()
        {
            SessionState.SetBool(PendingKey,true);
            File.WriteAllText(PendingFile,"Facial model changed; invalidate certification before rebuilding.");
        }
        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(Tracked)) return;
            MarkPending();
            try { FlushPending(); }
            catch(Exception error) { Debug.LogException(error); Schedule(); }
        }
        internal static void FlushPending()
        {
            if(flushing) throw new InvalidOperationException("Facial certification invalidation is already running");
            if(!SessionState.GetBool(PendingKey,false) && !File.Exists(PendingFile)) return;
            flushing=true;
            try
            {
                FacialContentBuilder.InvalidateAll("tracked facial model imported, moved or deleted");
                if(File.Exists(PendingFile)) File.Delete(PendingFile);
                SessionState.SetBool(PendingKey,false);
            }
            finally { flushing=false; }
        }
        private static void Schedule()
        {
            if(scheduled) return;
            scheduled=true;
            EditorApplication.delayCall+=RetryOnce;
        }
        private static void RetryOnce()
        {
            scheduled=false;
            try { FlushPending(); }
            catch(Exception error)
            {
                // Keep the persistent dirty marker; don't busy-loop while a prefab is locked/read-only.
                Debug.LogException(error);
            }
        }
    }
}
