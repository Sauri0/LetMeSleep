using System;
using LetMeSleep.Audio;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    /// <summary>One side of a voice link: role, cohort and where the mouth (speaker) or ear (listener) is.</summary>
    public readonly struct VoiceEndpoint
    {
        public readonly PlayerRole Role;
        public readonly bool Eliminated;
        public readonly Vector3 Position;

        public VoiceEndpoint(PlayerRole role, bool eliminated, Vector3 position)
        {
            Role = role; Eliminated = eliminated; Position = position;
        }

        public bool Valid => (Role == PlayerRole.Human || Role == PlayerRole.Mosquito) &&
            !(float.IsNaN(Position.x) || float.IsNaN(Position.y) || float.IsNaN(Position.z) ||
              float.IsInfinity(Position.x) || float.IsInfinity(Position.y) || float.IsInfinity(Position.z));

        /// <summary>Human mouth: eye height (1.53 m standing, −0.64 m crouched) minus 8 cm. Mosquito: its body.</summary>
        public static Vector3 MouthOf(PlayerRole role, Vector3 position, float crouch)
            => role == PlayerRole.Human ? position + Vector3.up * (1.45f - .64f * Mathf.Clamp01(crouch)) : position;

        /// <summary>Human ear: eye height. Mosquito: its body (the follow camera is not where it hears from).</summary>
        public static Vector3 EarOf(PlayerRole role, Vector3 position, float crouch)
            => role == PlayerRole.Human ? position + Vector3.up * (1.53f - .64f * Mathf.Clamp01(crouch)) : position;
    }

    /// <summary>
    /// Local presentation of one remote speaker: distance law and cohorts (<see cref="VoiceSpatialPolicy"/>),
    /// graded occlusion and room acoustics (<see cref="VoiceAcousticProbe"/>), direction cues relative to the
    /// active AudioListener, all smoothed in time before they reach the <see cref="VoicePlayoutStream"/>:
    /// occlusion attacks in 150 ms and releases in 300 ms (walking past a pillar does not pump), the room glides
    /// over 0.5 s (crossing a door changes the reverb without a jump). Routing uses 1 m of hysteresis past the cut.
    /// Shared by <see cref="VoiceRuntimeCoordinator"/> and the voice evidence scenarios.
    /// </summary>
    public sealed class VoicePeerPresenter
    {
        public const float OcclusionIntervalSeconds = .1f;
        public const float RoomIntervalSeconds = .25f;
        public const float OcclusionAttackSeconds = .15f;
        public const float OcclusionReleaseSeconds = .30f;
        public const float RoomGlideSeconds = .5f;

        private readonly VoiceAcousticProbe probe;
        private float occlusionTarget;
        private VoiceRoomSample roomTarget = VoiceRoomSample.Outdoors;
        private double nextOcclusionAt, nextRoomAt;
        private bool first = true;

        public VoicePlayoutStream Stream { get; }
        public bool LocalCanHearPeer { get; private set; }
        public bool PeerCanHearLocal { get; private set; }
        public float Occlusion { get; private set; }
        public int Walls { get; private set; }
        public VoiceRoomSample Room { get; private set; } = VoiceRoomSample.Outdoors;
        public float Distance { get; private set; }
        public VoicePlayoutParameters Parameters { get; private set; } = VoicePlayoutParameters.Open();

        public VoicePeerPresenter(VoicePlayoutStream stream, VoiceAcousticProbe probe)
        {
            Stream = stream ? stream : throw new ArgumentNullException(nameof(stream));
            this.probe = probe;
        }

        /// <summary>Forgets smoothing so the next update snaps (new round, teleport, new context).</summary>
        public void Reset()
        {
            first = true; nextOcclusionAt = nextRoomAt = 0;
            Occlusion = occlusionTarget = 0f; Walls = 0;
            Room = roomTarget = VoiceRoomSample.Outdoors;
        }

        /// <param name="speaker">Remote peer (mouth position).</param>
        /// <param name="listener">Local player (ear position).</param>
        /// <param name="listenerTransform">Active AudioListener (for behind/above cues); may be null.</param>
        public void PresentSpatial(in VoiceEndpoint speaker, in VoiceEndpoint listener, Transform listenerTransform, double now, float deltaSeconds)
        {
            bool cohort = speaker.Valid && listener.Valid && speaker.Eliminated == listener.Eliminated;
            Vector3 mouth = speaker.Position, ear = listener.Position;
            float distance = cohort ? Vector3.Distance(mouth, ear) : float.PositiveInfinity;
            Distance = distance;
            float cutoffIn = VoiceSpatialPolicy.CutoffDistance(speaker.Role, listener.Role);
            float cutoffOut = VoiceSpatialPolicy.CutoffDistance(listener.Role, speaker.Role);
            LocalCanHearPeer = cohort && VoiceSpatialPolicy.RouteAudible(distance, cutoffIn, LocalCanHearPeer);
            PeerCanHearLocal = cohort && VoiceSpatialPolicy.RouteAudible(distance, cutoffOut, PeerCanHearLocal);

            if (LocalCanHearPeer && probe != null)
            {
                if (first || now >= nextOcclusionAt)
                {
                    VoiceOcclusionSample sample = probe.Occlusion(mouth, ear);
                    occlusionTarget = sample.Blocked;
                    Walls = sample.Blocked > 0f ? Math.Max(1, sample.Walls) : 0;
                    nextOcclusionAt = now + OcclusionIntervalSeconds;
                }
                if (first || now >= nextRoomAt)
                {
                    roomTarget = VoiceRoomSample.Blend(probe.Room(mouth), probe.Room(ear), .5f);
                    nextRoomAt = now + RoomIntervalSeconds;
                }
            }

            float dt = Mathf.Clamp(deltaSeconds, 0f, .25f);
            if (first) { Occlusion = occlusionTarget; Room = roomTarget; }
            else
            {
                float tau = occlusionTarget > Occlusion ? OcclusionAttackSeconds : OcclusionReleaseSeconds;
                Occlusion += (occlusionTarget - Occlusion) * (1f - Mathf.Exp(-dt / tau));
                Room = VoiceRoomSample.Blend(Room, roomTarget, 1f - Mathf.Exp(-dt / RoomGlideSeconds));
            }

            VoiceSpatialResult result = cohort
                ? VoiceSpatialPolicy.Evaluate(speaker.Role, speaker.Eliminated, ToFloat(mouth), listener.Role, listener.Eliminated, ToFloat(ear), Occlusion, Walls)
                : default;
            float distanceGain = cohort ? VoiceSpatialPolicy.DistanceGain(distance, cutoffIn) : 0f;
            float occlusionGain = VoiceSpatialPolicy.OcclusionGain(Occlusion, Walls);
            float nearDry = VoiceDsp.SmoothStep(.3f, 1.2f, distance); // right at the ear: all direct sound
            float lowPass = Mathf.Min(result.LowPassHertz >= VoicePlayoutEngine.OpenLowPassHz ? VoicePlayoutEngine.OpenLowPassHz : result.LowPassHertz,
                VoiceSpatialPolicy.AirLowPassHertz(distance, cutoffIn));

            float behind = 0f, elevation = 0f;
            if (listenerTransform != null && distance > .05f && distance < 1e4f)
            {
                Vector3 local = listenerTransform.InverseTransformDirection((mouth - listenerTransform.position).normalized);
                float angle = Vector3.Angle(Vector3.forward, new Vector3(local.x, 0f, local.z));
                float horizontal = new Vector2(local.x, local.z).magnitude;
                behind = VoiceDsp.SmoothStep(100f, 155f, angle) * VoiceDsp.SmoothStep(.3f, .8f, horizontal);
                elevation = Mathf.Clamp(local.y * 1.4f, -1f, 1f) * VoiceDsp.SmoothStep(.4f, 1.5f, distance);
            }

            var parameters = new VoicePlayoutParameters
            {
                DirectGain = result.Gain,
                ReverbSend = LocalCanHearPeer ? Room.Wet * Mathf.Sqrt(distanceGain * occlusionGain) * nearDry : 0f,
                LowPassHz = lowPass,
                Behind = behind,
                Elevation = elevation,
                ReverbDecaySeconds = Room.DecaySeconds,
                ReverbDampingHz = Room.DampingHz,
                Crossfeed = Mathf.Lerp(.06f, .2f, VoiceDsp.SmoothStep(.4f, 3f, distance))
            };
            Parameters = parameters;
            Stream.transform.position = mouth;
            Stream.SetSpatial(true);
            Stream.ApplyEnvironment(parameters);
            first = false;
        }

        /// <summary>Late joiners waiting for the next round hear each other flat (no position to render).</summary>
        public void PresentNonSpatial()
        {
            LocalCanHearPeer = PeerCanHearLocal = true;
            Occlusion = 0f; Walls = 0; Distance = 0f;
            var parameters = VoicePlayoutParameters.Open(1f);
            parameters.Crossfeed = 0f;
            Parameters = parameters;
            Stream.SetSpatial(false);
            Stream.ApplyEnvironment(parameters);
            first = true;
        }

        /// <summary>Peer without a position this frame (not spawned yet, despawned): silent and unrouted.</summary>
        public void PresentMissing()
        {
            LocalCanHearPeer = PeerCanHearLocal = false;
            var parameters = VoicePlayoutParameters.Open(0f);
            Parameters = parameters;
            Stream.ApplyEnvironment(parameters);
        }

        private static Float3 ToFloat(Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}
