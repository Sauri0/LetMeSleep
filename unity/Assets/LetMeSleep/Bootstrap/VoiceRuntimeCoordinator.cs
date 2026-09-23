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
        private readonly GameObject owner;
        private readonly EosLobbySession lobby;
        private readonly AlfaUiController ui;
        private readonly AudioMixer mixer;
        private readonly VoiceMicrophoneCapture capture;
        private readonly VoiceOnlineSession session;
        private readonly Dictionary<uint, VoicePlayoutStream> playouts = new Dictionary<uint, VoicePlayoutStream>();
        private readonly Dictionary<uint, double> speakingUntil = new Dictionary<uint, double>();
        private readonly Dictionary<uint, bool> audibleActors = new Dictionary<uint, bool>();
        private readonly Dictionary<uint, VoicePeerRoute> routes = new Dictionary<uint, VoicePeerRoute>();
        private readonly AudioMixerGroup voiceGroup;
        private InputAction pushToTalk;
        private bool rebinding;
        private RoomView view;
        private GameplayRoundConfig config;
        private SpawnActor[] roster;
        private GameplayRuntime game;
        private LobbyMovementRuntime lobbyMovement;
        private string localMemberId, selectedDevice = string.Empty, contextKey = string.Empty, scopeLabel = string.Empty;
        private string bindingPath = "<Keyboard>/v", bindingLabel = "V", notice = string.Empty;
        private bool disposed, ducked;
        private float voiceVolume = .8f, savedMusicDb, savedAmbienceDb;
        private double lastAudibleAt, nextUiAt;

        public bool LocalMuted => session.LocalMuted;
        public bool IsTransmitting => session.IsTransmitting;

        public VoiceRuntimeCoordinator(GameObject owner, EosPeerTransport sharedTransport, EosLobbySession lobby,
            AudioMixer mixer, AlfaUiController ui)
        {
            this.owner = owner ? owner : throw new ArgumentNullException(nameof(owner));
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            this.mixer = mixer;
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            capture = owner.GetComponent<VoiceMicrophoneCapture>() ?? owner.AddComponent<VoiceMicrophoneCapture>();
            session = new VoiceOnlineSession(new VoiceEosChannelTransport(sharedTransport ?? throw new ArgumentNullException(nameof(sharedTransport)), lobby));
            voiceGroup = mixer?.FindMatchingGroups("Voice").FirstOrDefault();
            capture.FrameCaptured += OnCapturedFrame;
            capture.CaptureStopped += OnCaptureStopped;
            session.FrameDecoded += OnFrameDecoded;
            session.LocalTransmissionStopped += capture.EndPushToTalk;
            Configure(string.Empty, bindingPath, voiceVolume);
        }

        public void Configure(string device, string pttBinding, float volume)
        {
            ThrowIfDisposed();
            selectedDevice = device ?? string.Empty;
            voiceVolume = Mathf.Clamp01(volume);
            if (ducked) ducked = false; // preferences already wrote the new mixer baseline
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
            session.Tick(now);
            UpdateSpatial(now);
            UpdateDucking(now);
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
            ThrowIfDisposed(); session.SetMemberMuted(memberId, muted); PublishUi(true);
        }

        public void SetApplicationFocused(bool focused)
        {
            if (disposed) return; session.SetApplicationFocused(focused);
            if (!focused) capture.EndPushToTalk();
        }

        public void SetApplicationPaused(bool paused)
        {
            if (disposed) return; session.SetApplicationPaused(paused);
            if (paused) capture.EndPushToTalk();
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
            var peers = members.Select((member, index) => new { member, actor = checked((uint)index + 1) })
                .Where(item => item.member != localMemberId && lobby.Contains(item.member))
                .Select(item => new VoicePeerRoute(item.member, item.actor, initiallyAudible, initiallyAudible, false)).ToArray();
            key = ContextKey(epoch, round, localActor, peers) + ":" + label;
            return new VoiceRoundContext(epoch, round, localActor, true, true, peers);
        }

        private void RebuildRoutes(VoiceRoundContext next)
        {
            routes.Clear();
            audibleActors.Clear();
            var keep = new HashSet<uint>();
            if (next != null)
                foreach (VoicePeerRoute route in next.Peers)
                {
                    routes.Add(route.ActorId, route); keep.Add(route.ActorId);
                    if (!playouts.ContainsKey(route.ActorId))
                    {
                        var root = new GameObject("VoicePlayout-" + route.ActorId);
                        root.transform.SetParent(owner.transform, false);
                        var source = root.AddComponent<AudioSource>();
                        var stream = root.AddComponent<VoicePlayoutStream>();
                        stream.Initialize(route.ActorId, voiceGroup); stream.SetMosquitoTimbre(route.IsMosquito);
                        playouts.Add(route.ActorId, stream);
                    }
                    else playouts[route.ActorId].SetMosquitoTimbre(route.IsMosquito);
                }
            foreach (uint actor in playouts.Keys.Where(actor => !keep.Contains(actor)).ToArray())
            {
                if (playouts[actor]) UnityEngine.Object.Destroy(playouts[actor].gameObject);
                playouts.Remove(actor); speakingUntil.Remove(actor);
            }
        }

        private void UpdateSpatial(double now)
        {
            if (routes.Count == 0) return;
            bool waiting = scopeLabel.StartsWith("ESPERA", StringComparison.Ordinal);
            bool lobbyScope = scopeLabel.StartsWith("SALA", StringComparison.Ordinal);
            if (waiting)
            {
                foreach (var pair in routes) Apply(pair.Key, VoiceSpatialPolicy.NonSpatialWaitingRoom(), false);
                return;
            }

            if (lobbyScope)
            {
                if (!TryLobbyPosition(localMemberId, out Vector3 local)) return;
                foreach (var pair in routes)
                {
                    if (!TryLobbyPosition(pair.Value.MemberId, out Vector3 remote)) { Apply(pair.Key, default, true); continue; }
                    bool occluded = LobbyOccluded(local + Vector3.up * 1.4f, remote + Vector3.up * 1.4f, pair.Value.MemberId);
                    var result = VoiceSpatialPolicy.Evaluate(PlayerRole.Human, false, remote.ToFloat(), PlayerRole.Human, false, local.ToFloat(), occluded);
                    if (playouts.TryGetValue(pair.Key, out var stream) && stream) stream.transform.position = remote + Vector3.up * 1.4f;
                    Apply(pair.Key, result, true);
                }
                return;
            }

            var snapshot = game?.LatestSnapshot;
            ActorSnapshot localActor = snapshot?.Actors.FirstOrDefault(actor => actor.ActorId == game.LocalActorId);
            if (localActor == null) return;
            foreach (var pair in routes)
            {
                ActorSnapshot remote = snapshot.Actors.FirstOrDefault(actor => actor.ActorId == pair.Key);
                if (remote == null) { Apply(pair.Key, default, true); continue; }
                bool occluded = game.World != null && !game.World.HasLineOfSight(localActor.ActorId, localActor.Position,
                    remote.ActorId, remote.Position);
                var result = VoiceSpatialPolicy.Evaluate(remote.Role, remote.Eliminated, remote.Position,
                    localActor.Role, localActor.Eliminated, localActor.Position, occluded);
                if (playouts.TryGetValue(pair.Key, out var stream) && stream) stream.transform.position = remote.Position.ToUnity() + Vector3.up * .25f;
                Apply(pair.Key, result, true);
            }
        }

        private void Apply(uint actorId, VoiceSpatialResult result, bool spatial)
        {
            audibleActors[actorId] = result.Audible;
            session.SetPeerAudibility(actorId, result.Audible, result.Audible);
            if (!playouts.TryGetValue(actorId, out VoicePlayoutStream stream) || !stream) return;
            stream.SetSpatial(spatial); stream.ApplyAcoustics(result.Gain, result.LowPassHertz <= 0 ? VoiceSpatialPolicy.OpenLowPassHertz : result.LowPassHertz);
            if (!result.Audible) stream.Clear();
        }

        private bool TryLobbyPosition(string memberId, out Vector3 position)
        {
            position = default;
            if (!lobbyMovement || !lobbyMovement.TryGetVisual(memberId, out GameObject visual) || !visual) return false;
            position = visual.transform.position; return true;
        }

        private bool LobbyOccluded(Vector3 from, Vector3 to, string remoteMember)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(value => value.distance))
            {
                if (lobbyMovement != null && lobbyMovement.TryGetVisual(remoteMember, out GameObject remote) &&
                    hit.collider.transform.IsChildOf(remote.transform)) return false;
                return true;
            }
            return false;
        }

        private void OnCapturedFrame(float[] samples) => session.SubmitCapturedFrame(samples, Time.realtimeSinceStartupAsDouble);
        private void OnCaptureStopped() { if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble); }
        private void OnFrameDecoded(VoiceDecodedFrame frame)
        {
            if (!playouts.TryGetValue(frame.ActorId, out VoicePlayoutStream stream) || !stream) return;
            stream.Submit(frame.Samples, frame.Volume); speakingUntil[frame.ActorId] = Time.realtimeSinceStartupAsDouble + .18;
            lastAudibleAt = Time.realtimeSinceStartupAsDouble;
        }

        private void BeginPushToTalk(InputAction.CallbackContext _)
        {
            if (session.LocalMuted) { notice = "Activá tu micrófono para hablar."; PublishUi(true); return; }
            if (!DeviceAvailable()) { notice = string.IsNullOrEmpty(selectedDevice) ? "Elegí un micrófono en Ajustes." : "El micrófono elegido no está disponible."; PublishUi(true); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (!session.BeginPushToTalk(now)) return;
            if (!capture.BeginPushToTalk(selectedDevice))
            {
                session.EndPushToTalk(now); notice = "No se pudo iniciar el micrófono elegido.";
            }
            else notice = string.Empty;
            PublishUi(true);
        }

        private void EndPushToTalk(InputAction.CallbackContext _)
        {
            capture.EndPushToTalk();
            if (session.IsTransmitting) session.EndPushToTalk(Time.realtimeSinceStartupAsDouble);
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

        private void UpdateDucking(double now)
        {
            if (mixer == null) return;
            bool shouldDuck = voiceVolume > .0001f && now - lastAudibleAt < .25;
            if (shouldDuck == ducked) return;
            if (shouldDuck)
            {
                mixer.GetFloat("MusicVolume", out savedMusicDb); mixer.GetFloat("AmbienceVolume", out savedAmbienceDb);
                mixer.SetFloat("MusicVolume", Mathf.Max(-80, savedMusicDb - 4));
                mixer.SetFloat("AmbienceVolume", Mathf.Max(-80, savedAmbienceDb - 4));
            }
            else { mixer.SetFloat("MusicVolume", savedMusicDb); mixer.SetFloat("AmbienceVolume", savedAmbienceDb); }
            ducked = shouldDuck;
        }

        private void PublishUi(bool force)
        {
            if (disposed || (!force && ui == null)) return;
            var names = view?.Members.ToDictionary(member => member.Id, member => member.Name, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            double now = Time.realtimeSinceStartupAsDouble;
            var peers = routes.Values.Select(route => new VoiceParticipantUiState(route.MemberId,
                names.TryGetValue(route.MemberId, out string name) ? name : "Jugador",
                audibleActors.TryGetValue(route.ActorId, out bool audible) && audible && !session.IsMemberMuted(route.MemberId),
                session.IsMemberMuted(route.MemberId), speakingUntil.TryGetValue(route.ActorId, out double until) && until > now)).ToArray();
            ui.PresentVoice(new VoiceUiState(view != null, session.LocalMuted, session.IsTransmitting, DeviceAvailable(),
                scopeLabel, rebinding ? PushToTalkBindings.WaitingLabel : bindingLabel, notice, peers));
        }

        private bool DeviceAvailable() => !string.IsNullOrEmpty(selectedDevice) &&
            VoiceMicrophoneCapture.Devices.Any(device => string.Equals(device, selectedDevice, StringComparison.Ordinal));

        private ulong RoomEpoch()
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(lobby.Code ?? string.Empty));
            ulong value = BitConverter.ToUInt64(hash, 0); return value == 0 ? 1 : value;
        }

        private static string ContextKey(ulong epoch, ulong round, uint localActor, IEnumerable<VoicePeerRoute> peers)
            => epoch + ":" + round + ":" + localActor + ":" + string.Join("|", peers.Select(peer => peer.MemberId + "=" + peer.ActorId));

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(VoiceRuntimeCoordinator)); }

        public void Dispose()
        {
            if (disposed) return;
            capture.FrameCaptured -= OnCapturedFrame; capture.CaptureStopped -= OnCaptureStopped;
            capture.EndPushToTalk();
            pushToTalk?.Disable(); pushToTalk?.Dispose(); pushToTalk = null;
            session.FrameDecoded -= OnFrameDecoded; session.LocalTransmissionStopped -= capture.EndPushToTalk; session.Dispose();
            if (ducked && mixer != null) { mixer.SetFloat("MusicVolume", savedMusicDb); mixer.SetFloat("AmbienceVolume", savedAmbienceDb); }
            foreach (VoicePlayoutStream stream in playouts.Values) if (stream) UnityEngine.Object.Destroy(stream.gameObject);
            playouts.Clear(); routes.Clear(); audibleActors.Clear(); disposed = true;
        }
    }
}
