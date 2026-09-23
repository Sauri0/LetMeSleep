using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Audio
{
    /// <summary>Blocked fraction of the mouth→ear paths and walls crossed by the direct path.</summary>
    public readonly struct VoiceOcclusionSample
    {
        public readonly float Blocked;
        public readonly int Walls;
        public VoiceOcclusionSample(float blocked, int walls) { Blocked = blocked; Walls = walls; }
    }

    /// <summary>
    /// Acoustic character around a point: how enclosed it is (0 open air … 1 closed room), the room size and
    /// the reverb it implies. Same classification is proposed to the SFX front (docs/v030/VOICE-V030.md).
    /// </summary>
    public readonly struct VoiceRoomSample
    {
        public readonly float Enclosure;
        public readonly float Interior;
        public readonly float SizeMeters;
        public readonly bool Ceiling;
        public readonly float DecaySeconds;
        public readonly float Wet;
        public readonly float DampingHz;

        public VoiceRoomSample(float enclosure, float interior, float size, bool ceiling, float decay, float wet, float damping)
        {
            Enclosure = enclosure; Interior = interior; SizeMeters = size; Ceiling = ceiling;
            DecaySeconds = decay; Wet = wet; DampingHz = damping;
        }

        public static VoiceRoomSample Outdoors => FromGeometry(0f, false, 15f);

        /// <summary>
        /// Maps geometry to reverb. Exterior: 0.25 s, send ≈ −29 dB (a trace of space, no room). Interior: decay
        /// grows with size (0.35 s small room … 1.1 s hall), send −12 … −9 dB, darker damping in small rooms.
        /// </summary>
        public static VoiceRoomSample FromGeometry(float enclosure, bool ceiling, float size)
        {
            float interior = VoiceDsp.SmoothStep(.45f, .85f, enclosure) * (ceiling ? 1f : .35f);
            float sizeT = VoiceDsp.Clamp01((size - 2f) / 10f);
            float roomDecay = VoiceDsp.Lerp(.35f, 1.1f, sizeT);
            float decay = VoiceDsp.Lerp(.25f, roomDecay, interior);
            float wet = VoiceDsp.Lerp(.035f, VoiceDsp.Lerp(.25f, .35f, sizeT), interior);
            float damping = VoiceDsp.Lerp(4500f, VoiceDsp.Lerp(3000f, 4200f, sizeT), interior);
            return new VoiceRoomSample(enclosure, interior, size, ceiling, decay, wet, damping);
        }

        public static VoiceRoomSample Blend(in VoiceRoomSample a, in VoiceRoomSample b, float t)
        {
            return new VoiceRoomSample(VoiceDsp.Lerp(a.Enclosure, b.Enclosure, t), VoiceDsp.Lerp(a.Interior, b.Interior, t),
                VoiceDsp.Lerp(a.SizeMeters, b.SizeMeters, t), t < .5f ? a.Ceiling : b.Ceiling,
                VoiceDsp.Lerp(a.DecaySeconds, b.DecaySeconds, t), VoiceDsp.Lerp(a.Wet, b.Wet, t), VoiceDsp.Lerp(a.DampingHz, b.DampingHz, t));
        }
    }

    /// <summary>
    /// Physics probes for voice acoustics. Occlusion casts five mouth→ear rays (centre and four offsets of
    /// ±0.35 m sideways / ±0.3 m vertically, so an open doorway or a low obstacle blocks only part of the sound);
    /// the blocked fraction is weighted 0.4 for the centre and 0.15 for each offset, and the centre ray counts the
    /// walls it crosses. The room probe casts 13 rays (up, 8 around, 4 diagonal) and is cached per 0.5 m cell for
    /// 3 s. <see cref="Filter"/> decides which colliders are acoustic geometry (map, not actors or triggers).
    /// </summary>
    public sealed class VoiceAcousticProbe
    {
        public const float SideOffset = .35f;
        public const float VerticalOffset = .30f;
        private const float CenterWeight = .4f, OffsetWeight = .15f;
        private const float UpRange = 12f, AroundRange = 15f, DiagonalRange = 12f;
        private const float CellSize = .5f, CacheSeconds = 3f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Dictionary<Vector3Int, (VoiceRoomSample sample, float time)> rooms = new Dictionary<Vector3Int, (VoiceRoomSample, float)>();
        private static readonly Vector3[] Around = BuildAround();

        public int LayerMask = Physics.DefaultRaycastLayers;
        public Func<Collider, bool> Filter;
        public int RaycastCount { get; private set; }

        public VoiceOcclusionSample Occlusion(Vector3 mouth, Vector3 ear)
        {
            Vector3 path = ear - mouth;
            float length = path.magnitude;
            if (length < .05f || float.IsNaN(length)) return new VoiceOcclusionSample(0f, 0);
            Vector3 direction = path / length;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right; else side.Normalize();
            Vector3 up = Vector3.Cross(direction, side).normalized;

            int walls = CountBlocking(mouth, ear, true);
            float blocked = walls > 0 ? CenterWeight : 0f;
            blocked += CountBlocking(mouth + side * SideOffset, ear + side * SideOffset, false) > 0 ? OffsetWeight : 0f;
            blocked += CountBlocking(mouth - side * SideOffset, ear - side * SideOffset, false) > 0 ? OffsetWeight : 0f;
            blocked += CountBlocking(mouth + up * VerticalOffset, ear + up * VerticalOffset, false) > 0 ? OffsetWeight : 0f;
            blocked += CountBlocking(mouth - up * VerticalOffset, ear - up * VerticalOffset, false) > 0 ? OffsetWeight : 0f;
            return new VoiceOcclusionSample(Mathf.Clamp01(blocked), walls);
        }

        public VoiceRoomSample Room(Vector3 position)
        {
            if (!IsFinite(position)) return VoiceRoomSample.Outdoors;
            var cell = new Vector3Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize), Mathf.FloorToInt(position.z / CellSize));
            float now = Time.realtimeSinceStartup;
            if (rooms.TryGetValue(cell, out var cached) && now - cached.time < CacheSeconds && now >= cached.time) return cached.sample;
            if (rooms.Count > 256) rooms.Clear();
            VoiceRoomSample sample = MeasureRoom(position);
            rooms[cell] = (sample, now);
            return sample;
        }

        public void ClearCache() => rooms.Clear();

        private VoiceRoomSample MeasureRoom(Vector3 position)
        {
            float up = FirstHit(position, Vector3.up, UpRange);
            bool ceiling = up < UpRange;
            int aroundHits = 0; float aroundSum = 0f;
            for (int i = 0; i < Around.Length; i++)
            {
                float d = FirstHit(position, Around[i], AroundRange);
                if (d < AroundRange) { aroundHits++; aroundSum += d; }
            }
            int diagonalHits = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = (Around[i * 2] + Vector3.up).normalized;
                if (FirstHit(position, dir, DiagonalRange) < DiagonalRange) diagonalHits++;
            }
            float enclosure = .25f * (ceiling ? 1f : 0f) + .5f * aroundHits / Around.Length + .25f * diagonalHits / 4f;
            // Room size from the mean wall distance (misses count as far), bounded to a hall.
            float mean = (aroundSum + (Around.Length - aroundHits) * AroundRange) / Around.Length;
            float size = Mathf.Clamp(mean * 2f, 1.5f, 20f);
            return VoiceRoomSample.FromGeometry(enclosure, ceiling, size);
        }

        private int CountBlocking(Vector3 from, Vector3 to, bool countAll)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < .05f) return 0;
            RaycastCount++;
            int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, LayerMask, QueryTriggerInteraction.Ignore);
            int blocking = 0;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || hits[i].distance < .02f || hits[i].distance > distance - .02f) continue;
                if (Filter != null && !Filter(collider)) continue;
                blocking++;
                if (!countAll) break;
            }
            return Math.Min(blocking, 3);
        }

        private float FirstHit(Vector3 from, Vector3 direction, float range)
        {
            RaycastCount++;
            int count = Physics.RaycastNonAlloc(from, direction, hits, range, LayerMask, QueryTriggerInteraction.Ignore);
            float nearest = range;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || hits[i].distance < .02f) continue;
                if (Filter != null && !Filter(collider)) continue;
                if (hits[i].distance < nearest) nearest = hits[i].distance;
            }
            return nearest;
        }

        private static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        private static Vector3[] BuildAround()
        {
            var directions = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                directions[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }
            return directions;
        }
    }
}
