using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Audio;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Online;
using LetMeSleep.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace LetMeSleep.Bootstrap
{
    /// <summary>Owns local capture/playout for one authenticated private room.</summary>
    public sealed class VoiceRuntimeCoordinator : IDisposable
    {
        private const ulong LobbyScope = ulong.MaxValue;
        private const ulong WaitingScopeBit = 1UL << 63;
        /// <summary>Exposed mixer parameters dimmed while someone talks (owned by the mixer/preferences front).</summary>
        public static readonly string[] DuckedParameters = { "MusicVolume", "AmbienceVolume" };
        public const float DuckDb = 4f;
        private const float DuckAttackSeconds = .12f, DuckReleaseSeconds = .5f;
        private const float SpeakingLevel = .004f; // ≈ −48 dBFS decoded RMS
        private readonly GameObject owner;
        private readonly EosLobbySession lobby;
        private readonly AlfaUiController ui;
        private readonly AudioMixer mixer;
        private readonly VoiceMicrophoneCapture capture;
        private readonly VoiceOnlineSession session;
        private readonly VoiceAcousticProbe probe = new VoiceAcousticProbe();
        private readonly Dictionary<uint, VoicePeerPresenter> peers = new Dictionary<uint, VoicePeerPresenter>();
        private readonly Dictionary<uint, double> speakingUntil = new Dictionary<uint, double>();
        private readonly Dictionary<uint, bool> audibleActors = new Dictionary<uint, bool>();
        private readonly Dictionary<uint, VoicePeerRoute> routes = new Dictionary<uint, VoicePeerRoute>();
        private readonly float[] duckBaselineDb = new float[DuckedParameters.Length];
        private readonly float[] duckWrittenDb = new float[DuckedParameters.Length];
        private readonly AudioMixerGroup voiceGroup;
        private InputAction pushToTalk;
        private bool rebinding;
        private RoomView view;
        private GameplayRoundConfig config;
        private SpawnActor[] roster;
        private GameplayRuntime game;
        private LobbyMovementRuntime lobbyMovement;
        private UnityGameplayWorld probeWorld;
        private AudioListener listener;
        private string localMemberId, selectedDevice = string.Empty, contextKey = string.Empty, scopeLabel = string.Empty;
        private string bindingPath = "<Keyboard>/v", bindingLabel = "V", notice = string.Empty;
        private bool disposed, duckActive;
        private float voiceVolume = .8f, duckAmount;
        private double lastAudibleAt, nextUiAt, lastTick, nextListenerLookup;
        private int lastUiHash;

        public bool LocalMuted => session.LocalMuted;
        public bool IsTransmitting => session.IsTransmitting;

        public VoiceRuntimeCoordinator(GameObject owner, EosPeerTransport sharedTransport, EosLobbySession lobby,
            AudioMixer mixer, AlfaUiController ui)
        {
            this.owner = owner ? owner : throw new ArgumentNullException(nameof(owner));
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            this.mixer = mixer;
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            capture = UnityComponents.GetOrAdd<VoiceMicrophoneCapture>(owner);
            session = new VoiceOnlineSession(new VoiceEosChannelTransport(sharedTransport ?? throw new ArgumentNullException(nameof(sharedTransport)), lobby));
            // Routed by name so the mixer owner can rebuild the asset; null falls back to the master output.
            voiceGroup = mixer?.FindMatchingGroups(VoicePlayoutStream.MixerGroupName).FirstOrDefault();
            capture.FrameCaptured += OnCapturedFrame;
            capture.CaptureStopped += OnCaptureStopped;
            capture.CaptureFailed += OnCaptureFailed;
            session.FrameDecoded += OnFrameDecoded;
            session.StreamClosed += OnStreamClosed;
            session.LocalTransmissionStopped += capture.EndPushToTalk;
            Configure(string.Empty, bindingPath, voiceVolume);
        }

        public void Configure(string device, string pttBinding, float volume)
        {
            ThrowIfDisposed();
            string next = device ?? string.Empty;
            if (!string.Equals(next, selectedDevice, StringComparison.Ordinal) && capture.IsCapturing)
            {
                // Switching microphones mid-transmission: close the old device; the next press opens the new one.
                capture.EndPushToTalk();
                if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble);
            }
            selectedDevice = next;
            voiceVolume = Mathf.Clamp01(volume);
            if (!string.IsNullOrWhiteSpace(pttBinding)) bindingPath = pttBinding;
            BuildPushToTalkAction();
            PublishUi(true);
        }

        public void Sync(RoomView roomView, GameplayRoundConfig roundConfig, IReadOnlyList<SpawnActor> activeRoster,
            GameplayRuntime gameplay, LobbyMovementRuntime movement, string localId)
        {
            ThrowIfDisposed();
            view = roomView; config = roundConfig; roster = activeRoster?.ToArray(); game = gameplay;
            lobbyMovement = movement; localMemberId = localId;
            VoiceRoundContext next = BuildContext(out string nextKey, out string nextScope);
            if (!string.Equals(nextKey, contextKey, StringComparison.Ordinal))
            {
                session.UpdateRound(next);
                contextKey = nextKey;
                scopeLabel = nextScope;
                RebuildRoutes(next);
            }
            PublishUi(true);
        }

        public void Tick(double now)
        {
            if (disposed) return;
            float delta = lastTick > 0 ? (float)Math.Min(.25, Math.Max(0, now - lastTick)) : 0f;
            lastTick = now;
            session.Tick(now);
            UpdateSpatial(now, delta);
            UpdateEchoGuard();
            UpdateDucking(now, delta);
            if (now >= nextUiAt) { nextUiAt = now + .1; PublishUi(false); }
        }

        public void SetLocalMuted(bool muted)
        {
            ThrowIfDisposed(); session.SetLocalMuted(muted);
            if (muted) capture.EndPushToTalk();
            notice = muted ? "Micrófono silenciado hasta que lo vuelvas a activar." : string.Empty;
            PublishUi(true);
        }

        public void SetPeerMuted(string memberId, bool muted)
        {
            ThrowIfDisposed(); session.SetMemberMuted(memberId, muted);
            if (muted)
                foreach (var route in routes.Values)
                    if (route.MemberId == memberId && peers.TryGetValue(route.ActorId, out var presenter) && presenter.Stream) presenter.Stream.Clear();
            PublishUi(true);
        }

        public void SetApplicationFocused(bool focused)
        {
            if (disposed) return; session.SetApplicationFocused(focused);
            if (!focused) { capture.EndPushToTalk(); ClearPlayouts(); }
        }

        public void SetApplicationPaused(bool paused)
        {
            if (disposed) return; session.SetApplicationPaused(paused);
            if (paused) { capture.EndPushToTalk(); ClearPlayouts(); }
        }

        // The application rebinds on a temporary action; the live one only pauses and is rebuilt by Configure.
        public void SetRebinding(bool active)
        {
            ThrowIfDisposed();
            if (active == rebinding) return;
            rebinding = active;
            if (active) { capture.EndPushToTalk(); pushToTalk?.Disable(); notice = PushToTalkBindings.WaitingNotice; }
            else { pushToTalk?.Enable(); if (notice == PushToTalkBindings.WaitingNotice) notice = string.Empty; }
            PublishUi(true);
        }

        private VoiceRoundContext BuildContext(out string key, out string label)
        {
            key = string.Empty; label = string.Empty;
            if (view == null || lobby.State != LobbyState.Connected || string.IsNullOrEmpty(localMemberId) ||
                !view.Members.Any(member => member.Id == localMemberId && member.Connected)) return null;

            if (view.Phase == RoomPhase.Waiting)
            {
                label = "SALA · VOZ DE PROXIMIDAD";
                return BuildMemberContext(RoomEpoch(), LobbyScope,
                    view.Members.Where(member => member.Connected).Select(member => member.Id), label, out key);
            }

            if (view.Phase != RoomPhase.Playing || roster == null || config == null) return null;
            SpawnActor local = roster.FirstOrDefault(actor => actor.OwnerPuid == localMemberId);
            if (!string.IsNullOrEmpty(local.OwnerPuid))
            {
                var connected = new HashSet<string>(view.Members.Where(member => member.Connected).Select(member => member.Id), StringComparer.Ordinal);
                // The host view can arrive before this client's EOS lobby notification: route only lobby members.
                var routes = roster.Where(actor => actor.OwnerPuid != localMemberId && connected.Contains(actor.OwnerPuid) && lobby.Contains(actor.OwnerPuid))
                    .Select(actor => new VoicePeerRoute(actor.OwnerPuid, actor.ActorId, false, false, actor.Role == PlayerRole.Mosquito)).ToArray();
                label = "RONDA · VOZ DE PROXIMIDAD";
                key = ContextKey(config.SessionEpoch, config.RoundId, local.ActorId, routes);
                return new VoiceRoundContext(config.SessionEpoch, config.RoundId, local.ActorId, true, true, routes);
            }

            var active = new HashSet<string>(roster.Select(actor => actor.OwnerPuid), StringComparer.Ordinal);
            label = "ESPERA · PRÓXIMA RONDA";
            return BuildMemberContext(RoomEpoch(), WaitingScopeBit | (ulong)(uint)Math.Max(1, view.Round),
                view.Members.Where(member => member.Connected && !active.Contains(member.Id)).Select(member => member.Id), label, out key);
        }

        private VoiceRoundContext BuildMemberContext(ulong epoch, ulong round, IEnumerable<string> memberIds,
            string label, out string key)
        {
            string[] members = memberIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            int localIndex = Array.IndexOf(members, localMemberId);
            if (localIndex < 0) { key = string.Empty; return null; }
            uint localActor = checked((uint)localIndex + 1);
            bool initiallyAudible = label.StartsWith("ESPERA", StringComparison.Ordinal);
            // Actor slots come from the full room view so they agree on every client; peers this client's
            // lobby does not list yet are routed after its lobby notification (the key then changes).
            var peerRoutes = members.Select((member, index) => new { member, actor = checked((uint)index + 1) })
                .Where(item => item.member != localMemberId && lobby.Contains(item.member))
                .Select(item => new VoicePeerRoute(item.member, item.actor, initiallyAudible, initiallyAudible, false)).ToArray();
            key = ContextKey(epoch, round, localActor, peerRoutes) + ":" + label;
            return new VoiceRoundContext(epoch, round, localActor, true, true, peerRoutes);
        }

        private void RebuildRoutes(VoiceRoundContext next)
        {
            routes.Clear();
            audibleActors.Clear();
            probe.ClearCache();
            var keep = new HashSet<uint>();
            if (next != null)
                foreach (VoicePeerRoute route in next.Peers)
                {
                    routes.Add(route.ActorId, route); keep.Add(route.ActorId);
                    if (!peers.TryGetValue(route.ActorId, out VoicePeerPresenter presenter) || !presenter.Stream)
                    {
                        var root = new GameObject("VoicePlayout-" + route.ActorId);
                        root.transform.SetParent(owner.transform, false);
                        root.AddComponent<AudioSource>();
                        var stream = root.AddComponent<VoicePlayoutStream>();
                        stream.Initialize(route.ActorId, voiceGroup);
                        presenter = new VoicePeerPresenter(stream, probe);
                        peers[route.ActorId] = presenter;
                    }
                    else { presenter.Stream.Clear(); presenter.Reset(); }
                    presenter.Stream.SetMosquitoTimbre(route.IsMosquito);
                }
            foreach (uint actor in peers.Keys.Where(actor => !keep.Contains(actor)).ToArray())
            {
                if (peers[actor].Stream) peers[actor].Stream.FadeOutAndDestroy();
                peers.Remove(actor); speakingUntil.Remove(actor);
            }
        }

        private void UpdateSpatial(double now, float delta)
        {
            if (routes.Count == 0) return;
            bool waiting = scopeLabel.StartsWith("ESPERA", StringComparison.Ordinal);
            bool lobbyScope = scopeLabel.StartsWith("SALA", StringComparison.Ordinal);
            if (waiting)
            {
                foreach (var pair in routes)
                    if (peers.TryGetValue(pair.Key, out var presenter)) { presenter.PresentNonSpatial(); Publish(pair.Key, presenter); }
                return;
            }

            Transform ear = ActiveListener(now);
            if (lobbyScope)
            {
                BindProbeWorld(lobbyMovement ? lobbyMovement.GetComponent<UnityGameplayWorld>() : null);
                if (!TryLobbyPosition(localMemberId, out Vector3 local)) return;
                var listenerEndpoint = new VoiceEndpoint(PlayerRole.Human, false, VoiceEndpoint.EarOf(PlayerRole.Human, local, 0f));
                foreach (var pair in routes)
                {
                    if (!peers.TryGetValue(pair.Key, out var presenter)) continue;
                    if (!TryLobbyPosition(pair.Value.MemberId, out Vector3 remote)) { presenter.PresentMissing(); Publish(pair.Key, presenter); continue; }
                    var speaker = new VoiceEndpoint(PlayerRole.Human, false, VoiceEndpoint.MouthOf(PlayerRole.Human, remote, 0f));
                    presenter.PresentSpatial(speaker, listenerEndpoint, ear, now, delta);
                    Publish(pair.Key, presenter);
                }
                return;
            }

            var snapshot = game?.LatestSnapshot;
            ActorSnapshot localActor = snapshot?.Actors.FirstOrDefault(actor => actor.ActorId == game.LocalActorId);
            if (localActor == null) return;
            BindProbeWorld(game.World);
            var listenerPose = new VoiceEndpoint(localActor.Role, localActor.Eliminated,
                VoiceEndpoint.EarOf(localActor.Role, localActor.Position.ToUnity(), localActor.CrouchFraction));
            foreach (var pair in routes)
            {
                if (!peers.TryGetValue(pair.Key, out var presenter)) continue;
                ActorSnapshot remote = snapshot.Actors.FirstOrDefault(actor => actor.ActorId == pair.Key);
                if (remote == null) { presenter.PresentMissing(); Publish(pair.Key, presenter); continue; }
                var speaker = new VoiceEndpoint(remote.Role, remote.Eliminated,
                    VoiceEndpoint.MouthOf(remote.Role, remote.Position.ToUnity(), remote.CrouchFraction));
                presenter.PresentSpatial(speaker, listenerPose, ear, now, delta);
                Publish(pair.Key, presenter);
            }
        }

        private void Publish(uint actorId, VoicePeerPresenter presenter)
        {
            bool wasAudible = audibleActors.TryGetValue(actorId, out bool previous) && previous;
            audibleActors[actorId] = presenter.LocalCanHearPeer;
            session.SetPeerAudibility(actorId, presenter.PeerCanHearLocal, presenter.LocalCanHearPeer);
            if (wasAudible && !presenter.LocalCanHearPeer && presenter.Stream) presenter.Stream.Clear();
        }

        private void BindProbeWorld(UnityGameplayWorld world)
        {
            if (world == probeWorld && probe.Filter != null) return;
            probeWorld = world;
            probe.ClearCache();
            if (world == null) probe.Filter = collider => !collider.isTrigger;
            else
            {
                probe.LayerMask = world.GeometryMask;
                probe.Filter = collider => world && world.IsWorldCollider(collider) && collider.GetComponentInParent<GameplayActorProxy>() == null;
            }
        }

        private Transform ActiveListener(double now)
        {
            if (listener && listener.isActiveAndEnabled && now < nextListenerLookup) return listener.transform;
            nextListenerLookup = now + 1.0;
            listener = null;
            foreach (AudioListener candidate in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled) { listener = candidate; break; }
            return listener ? listener.transform : null;
        }

        private bool TryLobbyPosition(string memberId, out Vector3 position)
        {
            position = default;
            if (!lobbyMovement || !lobbyMovement.TryGetVisual(memberId, out GameObject visual) || !visual) return false;
            position = visual.transform.position; return true;
        }

        private void UpdateEchoGuard()
        {
            float loudest = 0f;
            foreach (var presenter in peers.Values)
                if (presenter.Stream && presenter.Stream.OutputLevel > loudest) loudest = presenter.Stream.OutputLevel;
            capture.SetFarEndLevel(loudest * voiceVolume);
        }

        private void ClearPlayouts()
        {
            foreach (var presenter in peers.Values) if (presenter.Stream) presenter.Stream.Clear();
        }

        private void OnCapturedFrame(float[] samples) => session.SubmitCapturedFrame(samples, Time.realtimeSinceStartupAsDouble);
        private void OnCaptureStopped() { if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble); PublishUi(true); }

        private void OnCaptureFailed(string reason)
        {
            notice = reason ?? string.Empty;
            if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble);
            PublishUi(true);
        }

        private void OnFrameDecoded(VoiceDecodedFrame frame)
        {
            if (!peers.TryGetValue(frame.ActorId, out VoicePeerPresenter presenter) || !presenter.Stream) return;
            presenter.Stream.Submit(frame.Samples, frame.Volume, frame.Concealed);
            if (!frame.Concealed && frame.Level > SpeakingLevel)
            {
                speakingUntil[frame.ActorId] = Time.realtimeSinceStartupAsDouble + .25;
                lastAudibleAt = Time.realtimeSinceStartupAsDouble;
            }
        }

        private void OnStreamClosed(uint actorId)
        {
            if (peers.TryGetValue(actorId, out VoicePeerPresenter presenter) && presenter.Stream) presenter.Stream.EndStream();
        }

        private void BeginPushToTalk(InputAction.CallbackContext _)
        {
            if (session.LocalMuted) { notice = "Activá tu micrófono para hablar."; PublishUi(true); return; }
            if (!VoiceMicrophoneCapture.IsAvailable(selectedDevice))
            {
                notice = string.IsNullOrEmpty(selectedDevice)
                    ? "No hay micrófono disponible: conectá uno o revisá el permiso de micrófono de Windows."
                    : "El micrófono elegido no está disponible: conectalo o elegí otro en Ajustes.";
                PublishUi(true); return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (capture.IsReleasing && session.IsTransmitting && capture.BeginPushToTalk(selectedDevice))
            {
                notice = string.Empty; PublishUi(true); return; // pressed again inside the 40 ms release tail
            }
            if (!session.BeginPushToTalk(now)) return;
            if (!capture.BeginPushToTalk(selectedDevice))
            {
                session.EndPushToTalk(now); notice = "No se pudo iniciar el micrófono.";
            }
            else notice = string.Empty;
            PublishUi(true);
        }

        private void EndPushToTalk(InputAction.CallbackContext _)
        {
            if (capture.IsCapturing) capture.ReleasePushToTalk(); // the release tail ends the stream (OnCaptureStopped)
            else if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble);
            PublishUi(true);
        }

        private void BuildPushToTalkAction()
        {
            pushToTalk?.Disable(); pushToTalk?.Dispose();
            try { pushToTalk = new InputAction("PushToTalk", InputActionType.Button, bindingPath); }
            catch (ArgumentException) { bindingPath = "<Keyboard>/v"; pushToTalk = new InputAction("PushToTalk", InputActionType.Button, bindingPath); }
            bindingLabel = PushToTalkBindings.Label(bindingPath);
            pushToTalk.started += BeginPushToTalk; pushToTalk.canceled += EndPushToTalk;
            if (!rebinding) pushToTalk.Enable();
        }

        /// <summary>
        /// Music and ambience dip 4 dB while a voice plays: 120 ms down, 500 ms back (no steps). The baseline is
        /// whatever the mixer holds when the dip starts; if preferences write a new value meanwhile, it becomes the
        /// baseline (the dip never compounds).
        /// </summary>
        private void UpdateDucking(double now, float delta)
        {
            if (mixer == null) return;
            bool shouldDuck = voiceVolume > .0001f && now - lastAudibleAt < .25;
            float target = shouldDuck ? 1f : 0f;
            if (!duckActive && target == 0f) return;
            for (int i = 0; i < DuckedParameters.Length; i++)
            {
                if (!mixer.GetFloat(DuckedParameters[i], out float current)) { duckBaselineDb[i] = float.NaN; continue; }
                if (!duckActive || float.IsNaN(duckBaselineDb[i]) || Mathf.Abs(current - duckWrittenDb[i]) > .01f) duckBaselineDb[i] = current;
            }
            duckActive = true;
            float tau = target > duckAmount ? DuckAttackSeconds : DuckReleaseSeconds;
            duckAmount += (target - duckAmount) * (1f - Mathf.Exp(-Mathf.Max(delta, .001f) / tau));
            if (Mathf.Abs(target - duckAmount) < .005f) duckAmount = target;
            ApplyDuck();
            if (duckAmount == 0f) duckActive = false;
        }

        private void ApplyDuck()
        {
            for (int i = 0; i < DuckedParameters.Length; i++)
            {
                if (float.IsNaN(duckBaselineDb[i])) continue;
                duckWrittenDb[i] = Mathf.Max(-80f, duckBaselineDb[i] - DuckDb * duckAmount);
                mixer.SetFloat(DuckedParameters[i], duckWrittenDb[i]);
            }
        }

        private void PublishUi(bool force)
        {
            if (disposed || ui == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            bool deviceAvailable = VoiceMicrophoneCapture.IsAvailable(selectedDevice);
            // Change detection first (no allocations at 10 Hz when nothing changed).
            int hash = 17;
            unchecked
            {
                hash = hash * 31 + (session.LocalMuted ? 1 : 0);
                hash = hash * 31 + (session.IsTransmitting ? 1 : 0);
                hash = hash * 31 + (deviceAvailable ? 1 : 0);
                hash = hash * 31 + (rebinding ? 1 : 0);
                hash = hash * 31 + (view != null ? 1 : 0);
                hash = hash * 31 + scopeLabel.GetHashCode();
                hash = hash * 31 + bindingLabel.GetHashCode();
                hash = hash * 31 + notice.GetHashCode();
                foreach (var route in routes.Values)
                {
                    hash = hash * 31 + route.MemberId.GetHashCode();
                    hash = hash * 31 + (audibleActors.TryGetValue(route.ActorId, out bool audible) && audible ? 1 : 0);
                    hash = hash * 31 + (session.IsMemberMuted(route.MemberId) ? 1 : 0);
                    hash = hash * 31 + (speakingUntil.TryGetValue(route.ActorId, out double until) && until > now ? 1 : 0);
                }
                if (view != null) foreach (var member in view.Members) hash = hash * 31 + (member.Name ?? string.Empty).GetHashCode();
            }
            if (!force && hash == lastUiHash) return;
            lastUiHash = hash;
            var names = view?.Members.ToDictionary(member => member.Id, member => member.Name, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var participants = routes.Values.Select(route => new VoiceParticipantUiState(route.MemberId,
                names.TryGetValue(route.MemberId, out string name) ? name : "Jugador",
                audibleActors.TryGetValue(route.ActorId, out bool audible) && audible && !session.IsMemberMuted(route.MemberId),
                session.IsMemberMuted(route.MemberId), speakingUntil.TryGetValue(route.ActorId, out double until) && until > now)).ToArray();
            ui.PresentVoice(new VoiceUiState(view != null, session.LocalMuted, session.IsTransmitting, deviceAvailable,
                scopeLabel, rebinding ? PushToTalkBindings.WaitingLabel : bindingLabel, notice, participants));
        }

        private ulong RoomEpoch()
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(lobby.Code ?? string.Empty));
            ulong value = BitConverter.ToUInt64(hash, 0); return value == 0 ? 1 : value;
        }

        private static string ContextKey(ulong epoch, ulong round, uint localActor, IEnumerable<VoicePeerRoute> peerRoutes)
            => epoch + ":" + round + ":" + localActor + ":" + string.Join("|", peerRoutes.Select(peer => peer.MemberId + "=" + peer.ActorId));

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(VoiceRuntimeCoordinator)); }

        public void Dispose()
        {
            if (disposed) return;
            capture.FrameCaptured -= OnCapturedFrame; capture.CaptureStopped -= OnCaptureStopped; capture.CaptureFailed -= OnCaptureFailed;
            capture.EndPushToTalk();
            pushToTalk?.Disable(); pushToTalk?.Dispose(); pushToTalk = null;
            session.FrameDecoded -= OnFrameDecoded; session.StreamClosed -= OnStreamClosed;
            session.LocalTransmissionStopped -= capture.EndPushToTalk; session.Dispose();
            if (duckActive && mixer != null) { duckAmount = 0f; ApplyDuck(); duckActive = false; }
            // Fade every voice out on the audio thread (5 ms) before its object goes away: no residual audio, no click.
            foreach (VoicePeerPresenter presenter in peers.Values) if (presenter.Stream) presenter.Stream.FadeOutAndDestroy();
            peers.Clear(); routes.Clear(); audibleActors.Clear(); speakingUntil.Clear(); disposed = true;
        }
    }
}
