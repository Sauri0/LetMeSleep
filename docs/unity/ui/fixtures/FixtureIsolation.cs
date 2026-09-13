using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Validation
{
    // Dedicated batch only. Does not create/save assets. Fail closed if TMP internals change.
    public sealed class PrivateFixtureFonts : IDisposable
    {
        [Serializable] public sealed class SourceFile { public string path,sha256; }
        public readonly List<SourceFile> SourceFiles=new List<SourceFile>();
        private readonly Dictionary<UnityEngine.Object,string> originals=new Dictionary<UnityEngine.Object,string>();
        private readonly Dictionary<TMP_FontAsset,TMP_FontAsset> copies = new Dictionary<TMP_FontAsset,TMP_FontAsset>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly FieldInfo singleton = typeof(TMP_Settings).GetField("s_Instance",BindingFlags.Static|BindingFlags.NonPublic);
        private TMP_Settings originalSettings;
        private TMP_Settings privateSettings;
        public PrivateFixtureFonts()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Font isolation requires dedicated batch.");
            if(singleton==null)throw new InvalidOperationException("Unsupported TMP Settings singleton; abort before UI creation.");
            originalSettings=TMP_Settings.instance;
            if(!originalSettings)throw new InvalidOperationException("TMP Settings missing.");
            Track(originalSettings);
            try
            {
                privateSettings=Own(UnityEngine.Object.Instantiate(originalSettings));
                singleton.SetValue(null,privateSettings);
                // Public setters now affect only our private, unsaved Settings clone.
                TMP_Settings.defaultFontAsset=Copy(TMP_Settings.defaultFontAsset);
                TMP_Settings.fallbackFontAssets=(TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>()).Select(Copy).ToList();
            }
            catch {Dispose();throw;}
        }
        private T Own<T>(T value) where T:UnityEngine.Object
        {
            value.hideFlags=HideFlags.HideAndDontSave;
            owned.Add(value);return value;
        }
        public TMP_FontAsset Copy(TMP_FontAsset source)
        {
            if(!source)return null;
            if(copies.TryGetValue(source,out var existing))return existing;
            Track(source);
            var copy=Own(UnityEngine.Object.Instantiate(source));
            copies.Add(source,copy); // Register before traversing cyclic fallbacks/weights.
            copy.name=source.name+" (private UI fixture)";
            var originals=source.atlasTextures ?? Array.Empty<Texture2D>();
            foreach(var atlas in originals)if(atlas)TrackFile(atlas);
            copy.atlasTextures=originals.Select(atlas=>atlas ? Own(UnityEngine.Object.Instantiate(atlas)) : null).ToArray();
            if(source.material)
            {
                Track(source.material);
                copy.material=Own(UnityEngine.Object.Instantiate(source.material));
                foreach(var property in copy.material.GetTexturePropertyNames())
                    for(int i=0;i<originals.Length;i++)
                        if(originals[i] && copy.material.GetTexture(property)==originals[i])
                            copy.material.SetTexture(property,copy.atlasTextures[i]);
            }
            copy.fallbackFontAssetTable=(source.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Select(Copy).ToList();
            var weights=(TMP_FontWeightPair[])source.fontWeightTable.Clone();
            for(int i=0;i<weights.Length;i++)
            {
                weights[i].regularTypeface=Copy(weights[i].regularTypeface);
                weights[i].italicTypeface=Copy(weights[i].italicTypeface);
            }
            var weightField=typeof(TMP_FontAsset).GetField("m_FontWeightTable",BindingFlags.Instance|BindingFlags.NonPublic);
            if(weightField==null)throw new InvalidOperationException("Unsupported TMP font weights; abort before UI creation.");
            weightField.SetValue(copy,weights);
            return copy;
        }
        private void Track(UnityEngine.Object original)
        {
            if(!originals.ContainsKey(original))originals.Add(original,EditorJsonUtility.ToJson(original));
            TrackFile(original);
        }
        private void TrackFile(UnityEngine.Object original)
        {
            var path=AssetDatabase.GetAssetPath(original);
            if(string.IsNullOrEmpty(path))return;
            path=Path.GetFullPath(path);
            if(!SourceFiles.Any(file=>file.path==path))SourceFiles.Add(new SourceFile{path=path,sha256=Hash(path)});
        }
        public static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        public void AssertSourcesUnchanged()
        {
            foreach(var item in originals)
                if(!item.Key || EditorJsonUtility.ToJson(item.Key)!=item.Value)
                    throw new InvalidOperationException("Source asset mutated in memory: "+(item.Key ? item.Key.name : "destroyed"));
            foreach(var file in SourceFiles)
                if(Hash(file.path)!=file.sha256)throw new InvalidOperationException("Source file changed: "+file.path);
        }
        public void Dispose()
        {
            if(privateSettings && singleton!=null && ReferenceEquals(singleton.GetValue(null),privateSettings))
                singleton.SetValue(null,originalSettings);
            // Include runtime atlas growth, which did not exist when the fonts were copied.
            foreach(var font in copies.Values)
                if(font)foreach(var atlas in font.atlasTextures ?? Array.Empty<Texture2D>())
                    if(atlas && !EditorUtility.IsPersistent(atlas) && !owned.Contains(atlas))owned.Add(atlas);
            for(int i=owned.Count-1;i>=0;i--)
                if(owned[i] && !EditorUtility.IsPersistent(owned[i]))UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();copies.Clear();privateSettings=null;
        }
    }

    public static class FixtureEditorOptions
    {
        private const string Prefix="LetMeSleep.RoomUIFixture.";
        public static void Capture()
        {
            if(SessionState.GetBool(Prefix+"active",false))Restore();
            SessionState.SetBool(Prefix+"enabled",EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Prefix+"options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(Prefix+"active",true);
            AssemblyReloadEvents.beforeAssemblyReload+=Restore;
            EditorApplication.quitting+=Restore;
        }
        public static void Restore()
        {
            if(!SessionState.GetBool(Prefix+"active",false))return;
            // Never fall back to static fields whose defaults change the user's settings after reload.
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Prefix+"options",-1);
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Prefix+"enabled",false);
            if((int)EditorSettings.enterPlayModeOptions!=SessionState.GetInt(Prefix+"options",-1) ||
                EditorSettings.enterPlayModeOptionsEnabled!=SessionState.GetBool(Prefix+"enabled",false))
                throw new InvalidOperationException("Failed to restore original Play Mode options.");
            SessionState.EraseBool(Prefix+"active");
            AssemblyReloadEvents.beforeAssemblyReload-=Restore;
            EditorApplication.quitting-=Restore;
        }
    }
}
