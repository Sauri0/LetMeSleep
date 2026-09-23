using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// One batch entry point for the v0.3.0 scenes/decoration front, so several reproducible steps share one editor launch:
    ///   -executeMethod LetMeSleep.Editor.V030SceneTools.Run -v030Steps props,decor,menu,plans -v030Out &lt;dir&gt;
    /// Steps (in the given order):
    ///   props  V030PropLibraryImporter (FBX + flat URP materials + prefabs)
    ///   characters CharacterContentPipeline.RebuildAll (after art_source character/menu FBX changes)
    ///   decor  HiggsfieldDecorBuilder (per-map Decor prefabs + catalog Entry.Decor, validated) -v030DecorConfig &lt;json&gt;
    ///   menu   V030MenuSceneBuilder (bedroom menu set, lobby decor, AlfaApplication bindings)
    ///   plans  HiggsfieldDecorPlanner (JSON dumps and plan images, with decor when present)
    ///   spawns HiggsfieldDecorBuilder.SpawnClearanceReport (free first view of every human spawn, decor included)
    ///   atmosphere HiggsfieldAtmosphereCorrection (docs/v030/maps/atmosphere-v030.json or -higgsfieldAtmosphereConfig)
    /// </summary>
    public static class V030SceneTools
    {
        public static void Run()
        {
            try
            {
                string steps = V030PropLibraryImporter.Argument("-v030Steps") ?? throw new ArgumentException("-v030Steps required");
                string output = V030PropLibraryImporter.Argument("-v030Out") ?? "N:/LetMeSleep/Validation/V030/Scenes/receipts";
                Directory.CreateDirectory(output);
                foreach (string step in steps.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                {
                    Debug.Log("LMS_V030_STEP_BEGIN " + step);
                    switch (step)
                    {
                        case "props": V030PropLibraryImporter.Import(Path.Combine(output, "props-import.json")); break;
                        case "characters": CharacterContentPipeline.RebuildAll(); break;
                        case "decor": HiggsfieldDecorBuilder.BuildFromConfig(output); break;
                        case "menu": V030MenuSceneBuilder.Build(output); break;
                        case "plans": HiggsfieldDecorPlanner.Dump(Path.Combine(output, "plans"), true); break;
                        case "spawns": HiggsfieldDecorBuilder.SpawnClearanceReport(output); break;
                        case "atmosphere":
                        {
                            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                            string config = V030PropLibraryImporter.Argument("-higgsfieldAtmosphereConfig") ??
                                Path.Combine(repository, "docs/v030/maps/atmosphere-v030.json");
                            HiggsfieldAtmosphereCorrection.Apply(Path.GetFullPath(config));
                            break;
                        }
                        default: throw new ArgumentException("Unknown step " + step);
                    }
                    Debug.Log("LMS_V030_STEP_DONE " + step);
                }
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // Later steps live in their own files; resolving by name keeps this dispatcher compiling while they are added.
        static void RunByName(string type, string method, string output)
        {
            var target = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(type, false)).FirstOrDefault(t => t != null)
                ?? throw new InvalidOperationException("Missing tool " + type);
            var info = target.GetMethod(method, new[] { typeof(string) }) ?? throw new InvalidOperationException("Missing " + type + "." + method + "(string)");
            try { info.Invoke(null, new object[] { output }); }
            catch (System.Reflection.TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }
    }
}
