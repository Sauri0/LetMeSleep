using System;
using System.Collections.Generic;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxPresenter : MonoBehaviour
    {
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
        private ulong playedEpoch;
        private ulong playedRound;
        private bool hasPlayedScope;
        private bool subscribed;
        private int cursor;

        private void Awake() => EnsurePool();
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].gameObject.activeSelf && !pool[i].IsAlive(true))
                    pool[i].gameObject.SetActive(false);
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
            if (!hasPlayedScope || item.SessionEpoch != playedEpoch || item.RoundId != playedRound)
            {
                playedEvents.Clear();
                playedEpoch = item.SessionEpoch;
                playedRound = item.RoundId;
                hasPlayedScope = true;
            }
            if (!playedEvents.Add(new EventKey(item.SessionEpoch, item.RoundId, item.EventId)))
                return;
            Vector3 normal = item.Normal.ToUnity();
            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal.normalized)
                : Quaternion.identity;
            Spawn(item.Position.ToUnity(), rotation);
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

        private void Subscribe()
        {
            if (subscribed || gameplay == null)
                return;
            gameplay.EventReady += HandleEvent;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || gameplay == null)
                return;
            gameplay.EventReady -= HandleEvent;
            subscribed = false;
        }
    }
}
