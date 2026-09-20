using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxPresenter : MonoBehaviour
    {
        private sealed class ToolEffectVisual
        {
            internal GameplayModel.ToolEffectKind Kind;
            internal GameObject Root;
            internal LineRenderer Pulse;
            internal ParticleSystem Cloud;
            internal uint StartHalfTick, EndHalfTick;
        }
        private readonly struct EventKey : IEquatable<EventKey>
        {
            private readonly ulong epoch;
            private readonly ulong round;
            private readonly ulong eventId;
            public EventKey(ulong epoch, ulong round, ulong eventId)
            {
                this.epoch = epoch;
                this.round = round;
                this.eventId = eventId;
            }
            public bool Equals(EventKey other) => epoch == other.epoch && round == other.round && eventId == other.eventId;
            public override bool Equals(object obj) => obj is EventKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = epoch.GetHashCode();
                    hash = (hash * 397) ^ round.GetHashCode();
                    return (hash * 397) ^ eventId.GetHashCode();
                }
            }
        }

        [SerializeField] private GameplayRuntime gameplay = null;
        [SerializeField] private ParticleSystem impactPrefab = null;
        [SerializeField, Range(1, 16)] private int poolCapacity = 8;
        private readonly List<ParticleSystem> pool = new List<ParticleSystem>(8);
        private readonly HashSet<EventKey> playedEvents = new HashSet<EventKey>();
        private readonly Dictionary<uint, ToolEffectVisual> toolEffects = new Dictionary<uint, ToolEffectVisual>();
        private ulong playedEpoch;
        private ulong playedRound;
        private bool hasPlayedScope;
        private bool subscribed;
        private int cursor;
        private uint snapshotHalfTick;
        private double snapshotTime;
        private Material electricMaterial, aerosolMaterial;

        private void Awake() => EnsurePool();
        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); ClearToolEffects(); }
        private void OnDestroy()
        {
            ClearToolEffects();
            if (electricMaterial != null) Destroy(electricMaterial);
            if (aerosolMaterial != null) Destroy(aerosolMaterial);
        }

        private void Update()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].gameObject.activeSelf && !pool[i].IsAlive(true))
                    pool[i].gameObject.SetActive(false);
            double halfTick = snapshotHalfTick + Math.Max(0, Time.unscaledTimeAsDouble - snapshotTime) * 60.0;
            foreach (var pair in toolEffects.ToArray())
            {
                if (halfTick >= pair.Value.EndHalfTick) { RemoveToolEffect(pair.Key); continue; }
                if (pair.Value.Pulse != null)
                {
                    double duration = Math.Max(1, pair.Value.EndHalfTick - pair.Value.StartHalfTick);
                    float progress = Mathf.Clamp01((float)((halfTick - pair.Value.StartHalfTick) / duration));
                    float pulse = 1f + .28f * Mathf.Sin(progress * Mathf.PI * 6f);
                    pair.Value.Pulse.startWidth = .045f * pulse;
                    pair.Value.Pulse.endWidth = .018f * pulse;
                }
            }
        }

        public void Bind(GameplayRuntime runtime)
        {
            Unsubscribe();
            gameplay = runtime;
            Subscribe();
        }

        public void ApplyEvent(in GameplayModel.GameplayEvent item)
        {
            if (item.Kind != GameplayModel.GameplayEventKind.StrikeImpact &&
                item.Kind != GameplayModel.GameplayEventKind.MosquitoKnockedDown)
                return;
            EnsureScope(item.SessionEpoch, item.RoundId);
            if (!playedEvents.Add(new EventKey(item.SessionEpoch, item.RoundId, item.EventId)))
                return;
            Vector3 normal = item.Normal.ToUnity();
            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal.normalized)
                : Quaternion.identity;
            Spawn(item.Position.ToUnity(), rotation);
        }

        public void ApplySnapshot(GameplayModel.GameSessionState snapshot)
        {
            if (snapshot == null) return;
            EnsureScope(snapshot.SessionEpoch, snapshot.RoundId);
            snapshotHalfTick = snapshot.HostTick * 2;
            snapshotTime = Time.unscaledTimeAsDouble;
            var active = new HashSet<uint>();
            foreach (var effect in snapshot.ToolEffects)
            {
                if (effect.EffectId == 0 || effect.EndHalfTick <= snapshotHalfTick || !effect.Origin.IsFinite ||
                    !effect.Forward.IsFinite || Mathf.Abs(effect.Forward.LengthSquared - 1) > .02f) continue;
                active.Add(effect.EffectId);
                if (!toolEffects.TryGetValue(effect.EffectId, out var visual) || visual.Kind != effect.Kind)
                {
                    if (visual != null) RemoveToolEffect(effect.EffectId);
                    visual = CreateToolEffect(effect);
                    if (visual == null) { active.Remove(effect.EffectId); continue; }
                    toolEffects.Add(effect.EffectId, visual);
                }
                visual.StartHalfTick = effect.StartTick * 2;
                visual.EndHalfTick = effect.EndHalfTick;
                var forward = effect.Forward.ToUnity();
                var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .98f ? Vector3.right : Vector3.up;
                visual.Root.transform.SetPositionAndRotation(effect.Origin.ToUnity(), Quaternion.LookRotation(forward, up));
            }
            foreach (var id in toolEffects.Keys.Where(id => !active.Contains(id)).ToArray()) RemoveToolEffect(id);
        }

        private ToolEffectVisual CreateToolEffect(in GameplayModel.ToolEffectSnapshot effect)
        {
            var root = new GameObject("ToolEffect-" + effect.Kind + "-" + effect.EffectId);
            root.transform.SetParent(transform, false);
            var visual = new ToolEffectVisual { Kind = effect.Kind, Root = root };
            if (effect.Kind == GameplayModel.ToolEffectKind.RacketPulse)
            {
                var line = root.AddComponent<LineRenderer>(); visual.Pulse = line;
                line.useWorldSpace = false; line.positionCount = 5; line.numCapVertices = 4; line.numCornerVertices = 3;
                line.SetPositions(new[]
                {
                    Vector3.zero, new Vector3(.08f, .035f, .24f), new Vector3(-.07f, -.025f, .5f),
                    new Vector3(.06f, .02f, .78f), new Vector3(0, 0, GameplayModel.HumanEquipmentProfile.RacketRange)
                });
                line.startColor = new Color(.35f, .95f, 1f, .95f); line.endColor = new Color(.65f, .85f, 1f, .15f);
                line.material = ElectricMaterial(); line.alignment = LineAlignment.View;
            }
            else if (effect.Kind == GameplayModel.ToolEffectKind.AerosolCloud)
            {
                var cloud = root.AddComponent<ParticleSystem>(); visual.Cloud = cloud;
                cloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = cloud.main; main.playOnAwake = false; main.loop = true; main.duration = 1f; main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.45f); main.startSize = new ParticleSystem.MinMaxCurve(.09f, .2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .86f, .72f, .24f), new Color(.85f, .9f, .82f, .08f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 48;
                var emission = cloud.emission; emission.rateOverTime = 28;
                var shape = cloud.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = GameplayModel.HumanEquipmentProfile.AerosolHalfAngleDegrees; shape.radius = .035f;
                var renderer = cloud.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = AerosolMaterial();
                cloud.Play(true);
            }
            else { Destroy(root); return null; }
            return visual;
        }

        private Material ElectricMaterial() => electricMaterial != null ? electricMaterial : electricMaterial = CreateMaterial("LMS Electric Pulse", new Color(.35f, .95f, 1f, .95f));
        private Material AerosolMaterial() => aerosolMaterial != null ? aerosolMaterial : aerosolMaterial = CreateMaterial("LMS Aerosol Cloud", new Color(.72f, .86f, .72f, .22f));
        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return null;
            var material = new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
            return material;
        }

        private void RemoveToolEffect(uint id)
        {
            if (!toolEffects.TryGetValue(id, out var visual)) return;
            if (visual.Cloud != null) visual.Cloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visual.Root != null) Destroy(visual.Root);
            toolEffects.Remove(id);
        }

        private void ClearToolEffects()
        {
            foreach (var id in toolEffects.Keys.ToArray()) RemoveToolEffect(id);
        }

        private void EnsureScope(ulong epoch, ulong round)
        {
            if (hasPlayedScope && epoch == playedEpoch && round == playedRound) return;
            playedEvents.Clear(); ClearToolEffects();
            playedEpoch = epoch; playedRound = round; hasPlayedScope = true;
        }

        private void Spawn(Vector3 position, Quaternion rotation)
        {
            EnsurePool();
            if (pool.Count == 0)
                return;
            ParticleSystem effect = pool[cursor++ % pool.Count];
            effect.gameObject.SetActive(true);
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.Clear(true);
            effect.Play(true);
        }

        private void EnsurePool()
        {
            if (impactPrefab == null)
                return;
            poolCapacity = Mathf.Clamp(poolCapacity, 1, 16);
            while (pool.Count < poolCapacity)
            {
                ParticleSystem effect = Instantiate(impactPrefab, transform);
                effect.name = $"Impact_{pool.Count:00}";
                effect.gameObject.SetActive(false);
                pool.Add(effect);
            }
        }

        private void HandleEvent(GameplayModel.GameplayEvent item) => ApplyEvent(in item);
        private void HandleSnapshot(GameplayModel.GameSessionState snapshot) => ApplySnapshot(snapshot);

        private void Subscribe()
        {
            if (subscribed || gameplay == null)
                return;
            gameplay.EventReady += HandleEvent;
            gameplay.SnapshotApplied += HandleSnapshot;
            subscribed = true;
            if (gameplay.LatestSnapshot != null) ApplySnapshot(gameplay.LatestSnapshot);
        }

        private void Unsubscribe()
        {
            if (!subscribed || gameplay == null)
                return;
            gameplay.EventReady -= HandleEvent;
            gameplay.SnapshotApplied -= HandleSnapshot;
            subscribed = false;
        }
    }
}
