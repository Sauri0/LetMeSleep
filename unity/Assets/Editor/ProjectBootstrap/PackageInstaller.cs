using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace LetMeSleep.Editor
{
    public static class PackageInstaller
    {
        private static AddAndRemoveRequest request;
        private static double deadline;
        public static void Install()
        {
            // Use UPM to resolve and record the exact dependency graph.
            request = Client.AddAndRemove(new[] {
                "com.unity.pipeline@0.7.0-exp.1",
                "https://github.com/EOS-Contrib/eos_plugin_for_unity_upm.git#0b8f679193c5b6c74df5bddbe3248c9d7aadaee8"
            }, new[] { "com.unity.collab-proxy", "com.unity.multiplayer.center", "com.unity.visualscripting", "com.unity.ide.rider" });
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                Debug.LogError("LMS_PACKAGE_TIMEOUT");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                Debug.LogError("LMS_PACKAGE_FAILED: " + request.Error.message);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("LMS_PACKAGES_RESOLVED");
            EditorApplication.Exit(0);
        }
    }
}
