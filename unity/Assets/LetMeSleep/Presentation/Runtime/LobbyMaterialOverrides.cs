using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 warm "sala" (scenes.md #5): visual-only restyle of the parent private-lobby instance from its decor child.
    /// On enable it swaps named materials of renderers found by exact relative path under the parent (floor, plaster) and
    /// hides placeholder renderers (the flat blue inlay, the framed print replaced by a real window prop); on disable it
    /// restores exactly what it changed. Never touches colliders, spawns or the lobby prefab asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMaterialOverrides : MonoBehaviour
    {
        [Serializable]
        public sealed class Swap
        {
            [Tooltip("Relative path from the parent lobby root ('/' separated, exact names).")]
            public string Path;
            [Tooltip("Name of the material to replace in that renderer (every slot using it).")]
            public string FromMaterial;
            public Material To;
        }

        public Swap[] Swaps = Array.Empty<Swap>();
        [Tooltip("Relative paths (from the parent lobby root) whose renderers, including children, are hidden.")]
        public string[] Hide = Array.Empty<string>();

        private readonly List<KeyValuePair<Renderer, Material[]>> swapped = new List<KeyValuePair<Renderer, Material[]>>();
        private readonly List<Renderer> hidden = new List<Renderer>();

        public int AppliedSwaps => swapped.Count;
        public int HiddenRenderers => hidden.Count;

        private void OnEnable()
        {
            Transform lobby = transform.parent;
            if (!lobby) return;
            foreach (var swap in Swaps ?? Array.Empty<Swap>())
            {
                if (swap == null || !swap.To || string.IsNullOrEmpty(swap.Path)) continue;
                var target = lobby.Find(swap.Path);
                var renderer = target ? target.GetComponent<Renderer>() : null;
                if (!renderer) { Debug.LogWarning("LMS_LOBBY_DECOR_SWAP_MISSING " + swap.Path, this); continue; }
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && materials[i].name == swap.FromMaterial) { materials[i] = swap.To; changed = true; }
                if (!changed) continue;
                swapped.Add(new KeyValuePair<Renderer, Material[]>(renderer, renderer.sharedMaterials));
                renderer.sharedMaterials = materials;
            }
            foreach (string path in Hide ?? Array.Empty<string>())
            {
                var target = string.IsNullOrEmpty(path) ? null : lobby.Find(path);
                if (!target) { Debug.LogWarning("LMS_LOBBY_DECOR_HIDE_MISSING " + path, this); continue; }
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
            }
        }

        private void OnDisable()
        {
            // Restore in reverse so a renderer swapped twice returns to its original materials.
            for (int i = swapped.Count - 1; i >= 0; i--) if (swapped[i].Key) swapped[i].Key.sharedMaterials = swapped[i].Value;
            swapped.Clear();
            foreach (var renderer in hidden) if (renderer) renderer.enabled = true;
            hidden.Clear();
        }
    }
}
