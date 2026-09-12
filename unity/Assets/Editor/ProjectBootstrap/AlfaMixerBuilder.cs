using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Editor
{
    // Unity 6000.3 exposes mixer authoring through its editor controller API only.
    // Keep the reflection isolated here; runtime uses ordinary AudioMixer.SetFloat.
    public static class AlfaMixerBuilder
    {
        public const string Path = "Assets/LetMeSleep/Audio/Generated/LMS_AlfaMixer.mixer";
        [MenuItem("Let Me Sleep/Audio/Build Mixer")]
        public static void Build()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var controllerType = assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            var groupType = assembly.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            var exposedType = assembly.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            if (!mixer)
                mixer = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { Path });
            var master = controllerType.GetProperty("masterGroup").GetValue(mixer);
            var childrenProperty = groupType.GetProperty("children");
            var children = ((Array)childrenProperty.GetValue(master)).Cast<object>().ToList();
            var groups = new Dictionary<string, object> { ["Master"] = master };
            foreach (string name in new[] { "Music", "Ambience", "Character", "Critical", "Mosquito", "World", "UI", "Voice" })
            {
                var group = children.FirstOrDefault(g => ((UnityEngine.Object)g).name == name);
                if (group == null)
                {
                    group = controllerType.GetMethod("CreateNewGroup", flags).Invoke(mixer, new object[] { name, true });
                    children.Add(group);
                }
                groups[name] = group;
            }
            var childArray = Array.CreateInstance(groupType, children.Count);
            for (int i = 0; i < children.Count; i++) childArray.SetValue(children[i], i);
            childrenProperty.SetValue(master, childArray);
            var parameters = Array.CreateInstance(exposedType, groups.Count);
            int index = 0;
            foreach (var pair in groups)
            {
                object item = Activator.CreateInstance(exposedType);
                exposedType.GetField("guid").SetValue(item, groupType.GetMethod("GetGUIDForVolume").Invoke(pair.Value, null));
                exposedType.GetField("name").SetValue(item, pair.Key + "Volume");
                parameters.SetValue(item, index++);
                EditorUtility.SetDirty((UnityEngine.Object)pair.Value);
            }
            controllerType.GetProperty("exposedParameters").SetValue(mixer, parameters);
            EditorUtility.SetDirty(mixer); AssetDatabase.SaveAssets();
            if (mixer.FindMatchingGroups("").Length != 9) throw new InvalidOperationException("Mixer routing incomplete.");
            foreach (var name in groups.Keys)
                if (!mixer.GetFloat(name + "Volume", out _)) throw new InvalidOperationException("Mixer parameter missing: " + name);
            Debug.Log("LMS_ALFA_MIXER_PASSED groups=9 exposed=9");
        }
    }
}
