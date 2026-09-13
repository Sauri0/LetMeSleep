using System;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    [Serializable] public sealed class RecoveryPolygonRing
    { public Vector2[] Vertices = Array.Empty<Vector2>(); }
    [Serializable] public sealed class RecoveryPolygonZone
    {
        public string Id;
        public Vector2[] Outer = Array.Empty<Vector2>();
        public RecoveryPolygonRing[] Holes = Array.Empty<RecoveryPolygonRing>();
        public float MinY, MaxY;
        [Range(0,.02f)] public float EdgeTolerance = .001f;
        internal RecoveryFallPrism Compile()
        {
            if(string.IsNullOrWhiteSpace(Id) || Holes==null)throw new ArgumentException("Missing recovery zone identity/holes.");
            var holes=new Float2[Holes.Length][];
            for(int i=0;i<holes.Length;i++)holes[i]=Convert(Holes[i]?.Vertices);
            return new RecoveryFallPrism(Convert(Outer),holes,MinY,MaxY,EdgeTolerance);
        }
        private static Float2[] Convert(Vector2[] source)
        { if(source==null)throw new ArgumentException("Missing recovery vertices.");var values=new Float2[source.Length];for(int i=0;i<values.Length;i++)values[i]=new Float2(source[i].x,source[i].y);return values; }
    }
    /// <summary>Opt-in on MapRoot. Local bounds/zones are authored, never inferred from water materials.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayRecoveryVolume : MonoBehaviour
    {
        public Bounds SafetyBounds = new Bounds(Vector3.zero, new Vector3(100, 30, 100));
        public Bounds[] HumanFallZones = Array.Empty<Bounds>();
        public Bounds[] MosquitoFallZones = Array.Empty<Bounds>();
        public RecoveryPolygonZone[] HumanPolygonFallZones = Array.Empty<RecoveryPolygonZone>();
        public RecoveryPolygonZone[] MosquitoPolygonFallZones = Array.Empty<RecoveryPolygonZone>();
        [Tooltip("Optional local fallback points, in stable authored order. Empty uses this round's role spawns.")]
        public Vector3[] HumanSpawnPoints = Array.Empty<Vector3>();
        public Vector3[] MosquitoSpawnPoints = Array.Empty<Vector3>();
        [Min(1)] public int RetryTicks = 30;
        [Min(.002f)] public float InteriorMargin = .02f;
        [Range(.02f, .2f)] public float SupportProbe = .08f;

        internal bool Valid()
        {
            return ValidBounds(SafetyBounds) && ValidZones(HumanFallZones) && ValidZones(MosquitoFallZones)
                && ValidPoints(HumanSpawnPoints) && ValidPoints(MosquitoSpawnPoints)
                && RetryTicks >= 1 && RetryTicks <= 300 && Finite(InteriorMargin) && InteriorMargin >= .002f
                && InteriorMargin <= .5f && Finite(SupportProbe) && SupportProbe >= .02f && SupportProbe <= .2f
                && Finite(transform.lossyScale) && transform.lossyScale.x > .0001f
                && transform.lossyScale.y > .0001f && transform.lossyScale.z > .0001f;
        }
        // Frozen geometry policy per BeginRound, so repeated guards allocate no polygon copies.
        internal sealed class Settings
        {
            internal readonly Bounds SafetyBounds;
            internal readonly Bounds[] HumanFallZones,MosquitoFallZones;
            internal readonly Vector3[] HumanSpawnPoints,MosquitoSpawnPoints;
            internal readonly RecoveryFallPrism[] HumanPolygons,MosquitoPolygons;
            internal readonly int RetryTicks;
            internal readonly float InteriorMargin,SupportProbe;
            internal Settings(GameplayRecoveryVolume v)
            {
                if(!v.Valid())throw new ArgumentException("Invalid recovery volume settings.");
                SafetyBounds=v.SafetyBounds;HumanFallZones=(Bounds[])v.HumanFallZones.Clone();MosquitoFallZones=(Bounds[])v.MosquitoFallZones.Clone();
                HumanSpawnPoints=(Vector3[])v.HumanSpawnPoints.Clone();MosquitoSpawnPoints=(Vector3[])v.MosquitoSpawnPoints.Clone();
                HumanPolygons=CompileZones(v.HumanPolygonFallZones);MosquitoPolygons=CompileZones(v.MosquitoPolygonFallZones);
                RetryTicks=v.RetryTicks;InteriorMargin=v.InteriorMargin;SupportProbe=v.SupportProbe;
            }
            private static RecoveryFallPrism[] CompileZones(RecoveryPolygonZone[] zones)
            {
                if(zones==null || zones.Length>64)throw new ArgumentException("Invalid recovery polygon zone count.");
                var result=new RecoveryFallPrism[zones.Length];var ids=new System.Collections.Generic.HashSet<string>();
                for(int i=0;i<zones.Length;i++)
                { if(zones[i]==null || !ids.Add(zones[i].Id))throw new ArgumentException("Missing/duplicate recovery polygon zone.");result[i]=zones[i].Compile(); }
                return result;
            }
        }
        private static bool ValidZones(Bounds[] values)
        { if (values == null || values.Length > 64) return false; foreach (var value in values) if (!ValidBounds(value)) return false; return true; }
        private static bool ValidPoints(Vector3[] values)
        { if (values == null || values.Length > 32) return false; foreach (var value in values) if (!Finite(value)) return false; return true; }
        private static bool ValidBounds(Bounds value) => Finite(value.center) && Finite(value.size)
            && value.size.x > 0 && value.size.y > 0 && value.size.z > 0;
        internal static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
