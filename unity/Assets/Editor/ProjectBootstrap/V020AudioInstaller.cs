using System;
using LetMeSleep.Audio;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    public static class V020AudioInstaller
    {
        public static void InstallResultStings()
        {
            string[] names = { "HumansWin", "MosquitoesWin" };
            var clips = new AudioClip[names.Length];
            var cues = new AudioCue[names.Length];
            // Validate every input before changing either existing cue.
            for (int i = 0; i < names.Length; i++)
            {
                string path = "Assets/LetMeSleep/Audio/Clips/STG_V020_" + names[i] + ".wav";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                cues[i] = AssetDatabase.LoadAssetAtPath<AudioCue>(
                    "Assets/LetMeSleep/Audio/Generated/Cues/" + names[i] + ".asset");
                if (!clips[i] || !cues[i] || clips[i].channels != 2 || Math.Abs(clips[i].length - 3f) > .001f)
                    throw new InvalidOperationException("Missing or invalid three-second result cue: " + names[i]);
            }
            for (int i = 0; i < names.Length; i++)
            {
                var serialized = new SerializedObject(cues[i]);
                var variants = serialized.FindProperty("clips");
                variants.arraySize = 1;
                variants.GetArrayElementAtIndex(0).objectReferenceValue = clips[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cues[i]);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LMS_V020_RESULT_STINGS installed=2 seconds=3 listeningReview=pending");
        }
    }
}
