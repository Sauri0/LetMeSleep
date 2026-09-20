using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Audio;
using LetMeSleep.Content.Characters;
using LetMeSleep.Content.Environment;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Online;
using LetMeSleep.Presentation;
using LetMeSleep.Presentation.Gameplay;
using LetMeSleep.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication : MonoBehaviour, IMenuActions, IRoomMapActions, IRoomModeActions, ISpectatorActions, IVoiceActions
    {
        public GameObject HousePrefab, LobbyPrefab, HumanPrefab, MosquitoPrefab, GameplayPresentationPrefab, MenuAudioPrefab;
        public Camera MenuCamera, PreviewCamera;
        public Transform PreviewStage;
        public RenderTexture PreviewTexture;
        public TMP_FontAsset HeadingFont, BodyFont;
        public AudioMixer Mixer;
        public LetMeSleep.Presentation.AlfaLightingRig LightingRig;
        public HiggsfieldMapCatalog HiggsfieldMaps;
        private AlfaUiController ui;
        private AlfaAudioDirector menuAudio;
        private EosConnection connection;
        private EosLobbySession lobby;
        private EosPeerTransport transport;
        private OnlineRoomCoordinator room;
        private OnlineGameplaySession gameNetwork;
        private GameplayRuntime game;
        private GameObject presentation;
        private HiggsfieldCameraDistanceOverride cameraDistance;
        private EnvironmentMapDefinition map;
        private GameObject menuCharacters;
        private GameObject customizationBackdrop;
        private bool customizationBackdropWasActive;
        private string playerName = "Jugador", joinCode, lastError = "";
        private bool pendingOnline, createOnline, training, showingResults;
        private string closingError = "";
        private bool intentionalLeave;
        private bool quiescing;
        private int activeRound = -1;
        private RoomPhase lastPhase = RoomPhase.Closed;
        private SpawnActor[] activeRoster;
        private AlfaRole trainingRole;
        private GameplayRoundConfig activeConfig;
        private GameplaySpectatorCamera spectator;
        private double hudAt;
        private string combatFeedback = "";
        private double combatFeedbackUntil;
        private string LocalId => connection?.LocalUserId?.ToString() ?? "practice";
        private string DataPath
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Isolate manual validation without starting the automated build probe.
                // No resolution, input, screen capture or automatic exit is implied.
                string[] args = Environment.GetCommandLineArgs();
                int fixtureOption = Array.IndexOf(args, "--lms-validation-data");
                if (fixtureOption >= 0)
                {
                    if (fixtureOption + 1 >= args.Length || !Path.IsPathRooted(args[fixtureOption + 1]))
                        throw new ArgumentException("--lms-validation-data requires an absolute directory.");
                    return Path.GetFullPath(args[fixtureOption + 1]);
                }
#endif
#if UNITY_EDITOR
                return "N:/LetMeSleep/UserData/Unity";
#else
                string probe=RequestedProbeOutput();
                if(probe!=null) return Path.Combine(probe,"userdata");
                return Application.persistentDataPath;
#endif
            }
        }
        private void Start()
        {
            if (quiescing) return;
            try { Directory.CreateDirectory(DataPath); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { saveError="No se puede guardar en la carpeta de usuario."; }
            LoadPreferences();
            StartPlaytestJournal();
            ui = AlfaUiRuntime.Create(this, new AlfaUiDependencies(HeadingFont, BodyFont, preview:
                new CharacterPreviewSetup(PreviewCamera, PreviewStage, PreviewTexture, HumanPrefab, MosquitoPrefab, ConfigurePreviewAttention)));
            if (HiggsfieldMaps)
            {
                var options = HiggsfieldMaps.Entries.Select(entry => new TrainingMapOption(entry.MapId, entry.DisplayName, HasTaskCatalog(entry.Prefab, entry.MapId))).ToArray();
                ui.SetTrainingMaps(options); ui.SetRoomMaps(options);
            }
            menuAudio = Instantiate(MenuAudioPrefab).GetComponent<AlfaAudioDirector>();
            ui.FeedbackRequested += OnUiFeedback;
            ui.ScreenChanged += OnUiScreenChanged;
            OnUiScreenChanged(ui.CurrentScreen);
            ui.SetRememberedPlayerName(playerName); LoadMap(false); menuAudio.EnterMenu(); PresentPreferences(); ApplySettingsValues();
            StartBuildProbeIfRequested();
        }
        private void Update()
        {
            if (quiescing) return;
            double now = Time.realtimeSinceStartupAsDouble;
            connection?.Tick(now); lobby?.Tick(now); room?.Tick(now); transport?.Poll(); gameNetwork?.Tick(now); voiceRuntime?.Tick(now);
            if (pendingOnline && connection?.State == ConnectionState.Ready) OpenPendingRoom();
            if (pendingOnline && connection?.State == ConnectionState.Failed)
            { pendingOnline = false; ShowOnlineError("No pudimos iniciar la conexión. " + connection.FailureCode); }
            if (lobby?.State == LobbyState.Failed && lobby.ErrorCode != lastError)
            { lastError = lobby.ErrorCode; ShowOnlineError("No pudimos entrar a la sala. " + lobby.ErrorCode); }
            if (room != null && !string.IsNullOrEmpty(room.Error) && lastError != room.Error)
            { lastError = room.Error; if (room.Current == null) { closingError = "No se pudo entrar: " + room.Error; ShowOnlineError(closingError); lobby.Leave(); } else PresentRoom(room.Current, "Los ajustes cambiaron. Volvé a marcar Listo."); }
            if (game != null && !training && gameNetwork != null) game.AutomaticTick = gameNetwork.Ready && game.IsHost;
            if (game?.LatestSnapshot != null && now >= hudAt && !showingResults)
            { hudAt = now + .1; PresentGame(game.LatestSnapshot); }
            ApplyPreviewColors(); TickAppearance(now); SyncMenuAudioContext();
        }
        public void CreateRoom(string name) => BeginOnline(name, null);
        public void JoinRoom(string name, string code) => BeginOnline(name, code);
        private void BeginOnline(string name, string code)
        {
            if (quiescing) return;
            if (pendingOnline || lobby?.State == LobbyState.Connected || lobby?.State == LobbyState.Leaving) return;
            playerName = (name ?? "").Trim(); if (playerName.Length == 0 || playerName.Length > 24) { ShowOnlineError("Escribí un nombre de hasta 24 caracteres."); return; }
            intentionalLeave = false; closingError = ""; createOnline = code == null; joinCode = code; pendingOnline = true; lastError = "";
            SavePreferences(); ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.Connecting, canCancel: true));
            try
            {
                if (connection == null)
                {
                    connection = new EosConnection();
                    string configuration = Path.Combine(Application.streamingAssetsPath, "online.local.json");
#if UNITY_EDITOR
                    configuration = "N:/LetMeSleep/Private/eos.local.json";
#endif
                    connection.Initialize(EosConfiguration.Load(configuration), playerName, Path.Combine(DataPath, "eos-cache"));
                }
                else if (connection.State == ConnectionState.Failed && !connection.RetryAuthentication())
                { pendingOnline = false; ShowOnlineError("Reiniciá el juego para volver a iniciar el servicio online."); }
            }
            catch (Exception exception) { pendingOnline = false; ShowOnlineError("No se pudo preparar el servicio online (" + exception.GetType().Name + ")."); }
        }
        private void OpenPendingRoom()
        {
            if (quiescing) return;
            initializedRoomMap = false;
            pendingOnline = false;
            StopVoiceRoom(); room?.Dispose(); transport?.Dispose(); lobby?.Dispose();
            lobby = new EosLobbySession(connection); transport = new EosPeerTransport(connection, lobby);
            transport.PeerStateChanged += ObservePlaytestPeer;
            peerAppearances.Clear(); peerAppearanceTimes.Clear(); appliedAppearance.Clear(); appearanceAt = 0;
            transport.PacketReceived += ReceiveAppearance;
            room = new OnlineRoomCoordinator(connection, lobby, transport, playerName);
            room.RoomChanged += OnRoomChanged;
            lobby.Changed += OnLobbyChanged;
            StartVoiceRoom();
            ui.PresentOnline(new OnlineUiState(createOnline ? OnlineOperationPhase.Creating : OnlineOperationPhase.Searching, canCancel: true));
            if (createOnline) lobby.Create(); else lobby.Join(joinCode);
        }
        private void OnLobbyChanged()
        {
            if (quiescing) return;
            RecordPlaytest("Lobby",lobby.State.ToString());
            SyncVoiceContext();
            if (lobby.State == LobbyState.Closed && !intentionalLeave)
            { StopVoiceRoom(); StopGame(); StopLobbyMovement(); LoadMap(false); ui.ShowJoinRoom(); if(closingError.Length>0) ShowOnlineError(closingError); else ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.RoomClosed)); }
        }
        public void CancelOnline()
        {
            RecordPlaytest("CancelOnlineRequested");
            pendingOnline = false; intentionalLeave = true;
            if (lobby != null) lobby.Leave();
            StopVoiceRoom();
            ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.Cancelled));
        }
        public void CopyRoomCode(string code) { GUIUtility.systemCopyBuffer = code; }
        public void SetReady(bool ready)
        {
            lastError = "";
            if (ready && room?.Current != null && !IsModeAvailable(room.Current.Rules.MapId, room.Current.Rules.ModeId))
            { PresentRoom(room.Current, "El mapa no tiene el contenido necesario para este modo."); return; }
            var result = room?.SetReady(ready);
            if (result.HasValue && result.Value != RoomError.None) PresentRoom(room.Current, "No se pudo marcar Listo.");
        }
        public void SetHumanCount(int? count)
        {
            if (count.HasValue && (count.Value < 1 || count.Value > 5)) return;
            var previous = room?.Current?.Rules;
            if (previous != null)
            {
                float quota = previous.ModeId == GameModes.Blood && count.HasValue
                    ? GameModes.BloodQuotaForHumans(count.Value) : previous.BloodQuota;
                room.SetRules(new RoomRules(count, previous.RoundSeconds, quota, previous.MapId, previous.ModeId, previous.ModeRuleProfileId));
            }
        }
        public void SetRoomMap(string mapId)
        {
            if (quiescing || lobby?.IsOwner != true || !IsAvailableMap(mapId)) return;
            var current = room?.Current;
            if (current == null || current.Phase != RoomPhase.Waiting) return;
            var previous = current.Rules;
            var error = room.SetRules(new RoomRules(previous.HumanCount, previous.RoundSeconds, previous.BloodQuota, mapId, previous.ModeId, previous.ModeRuleProfileId));
            if (error != RoomError.None) PresentRoom(room.Current, "No se pudo cambiar el mapa.");
        }
        public void SetRoomMode(string modeId)
        {
            var view = room?.Current;
            if (quiescing || lobby?.IsOwner != true || view == null || view.Phase != RoomPhase.Waiting || !GameModes.IsValid(modeId)) return;
            var previous = view.Rules;
            float quota = modeId == GameModes.Blood
                ? previous.HumanCount.HasValue ? GameModes.BloodQuotaForHumans(previous.HumanCount.Value) : 20f
                : 0f;
            var result = room.SetRules(new RoomRules(previous.HumanCount, GameModes.DefaultRoundSeconds(modeId),
                quota, previous.MapId, modeId));
            if (result != RoomError.None) PresentRoom(room.Current, "No se pudo cambiar el modo.");
        }
        public void SetRoomDurationSeconds(int seconds)
        {
            var view = room?.Current;
            if (quiescing || lobby?.IsOwner != true || view == null || view.Phase != RoomPhase.Waiting || seconds < 30 || seconds > 1800) return;
            var previous = view.Rules;
            var result = room.SetRules(new RoomRules(previous.HumanCount, seconds, previous.BloodQuota,
                previous.MapId, previous.ModeId, previous.ModeRuleProfileId));
            if (result != RoomError.None) PresentRoom(room.Current, "No se pudo cambiar la duración.");
        }
        public void StartRound()
        {
            if (room?.Current != null && !IsModeAvailable(room.Current.Rules.MapId, room.Current.Rules.ModeId))
            { PresentRoom(room.Current, "El mapa no tiene el contenido necesario para este modo."); return; }
            var error = room?.StartRound() ?? RoomError.Closed;
            if (error != RoomError.None) PresentRoom(room.Current, "Todavía falta que todos estén listos.");
        }
        private bool initializedRoomMap;
        private void OnRoomChanged(RoomView view)
        {
            if (quiescing || view == null) return;
            if (!initializedRoomMap && view.Phase == RoomPhase.Waiting && lobby.IsOwner)
            {
                initializedRoomMap = true;
                if (HiggsfieldMaps)
                {
                    SetRoomMap(HiggsfieldMaps.Entries[0].MapId);
                    return; // SetRules publishes the authoritative updated view synchronously.
                }
            }
            if (view.Phase == RoomPhase.Playing && !IsModeAvailable(view.Rules.MapId, view.Rules.ModeId))
            {
                LeaveRoom(); ui.ShowJoinRoom(); ShowOnlineError("El mapa no tiene el contenido necesario para este modo."); return;
            }
            ObservePlaytestRoom(view);
            training = false;
            var localMember = view.Members.FirstOrDefault(member => member.Id == LocalId);
            if (view.Phase == RoomPhase.Playing && localMember != null && localMember.Role == PlayerRole.Unassigned)
            {
                StopGame(); StopLobbyMovement(); PresentLateJoinWaiting(view); SyncVoiceContext(view); lastPhase = view.Phase; return;
            }
            if (view.Phase == RoomPhase.Waiting)
            {
                if (lastPhase != RoomPhase.Waiting) { StopGame(); LoadMap(false); menuAudio.gameObject.SetActive(true); menuAudio.EnterMenu(); }
                SyncLobbyMovement(view); SyncVoiceContext(view); PresentRoom(view);
            }
            else if (view.Phase == RoomPhase.Playing && activeRound != view.Round)
            {
                try
                {
                    StopLobbyMovement(); PrepareGame(false, view.Rules.MapId); activeRound = view.Round;
                    gameNetwork = new OnlineGameplaySession(lobby, room, transport, LocalId, map.ContentHash,
                        game.Authority, game, () => game.World.GetDoorDefinitions(), () => game.World.GetToolDefinitions(), () => GetObjectivesForMode(view.Rules.ModeId));
                    gameNetwork.BeginReceived += BeginGame;
                    gameNetwork.Failed += OnGameNetworkFailed;
                    game.InputReady += gameNetwork.SendInput; game.ActionReady += gameNetwork.SendAction;
                    game.SnapshotReady += state => {
                        gameNetwork?.SendSnapshot(state);
                        if (game.IsHost && activeRoster != null)
                            foreach (var actor in activeRoster) { var personal = game.Authority.CapturePrivate(actor.ActorId); if (personal != null) gameNetwork?.SendPrivate(actor.OwnerPuid, personal); }
                    };
                    game.EventReady += item => gameNetwork?.SendEvent(item);
                    if (lobby.IsOwner)
                    {
                        var roster = MakeRoster(view);
                        var config = new GameplayRoundConfig(NewEpoch(), (ulong)view.Round, map.MapId, map.ContentHash,
                            view.Rules.RoundSeconds, view.Rules.BloodQuota, doors: game.World.GetDoorDefinitions(), tools: game.World.GetToolDefinitions(), modeId: view.Rules.ModeId, objectives: GetObjectivesForMode(view.Rules.ModeId));
                        BeginGame(config, roster); gameNetwork.StartHost(config, roster);
                    }
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
                { Debug.LogWarning("LMS_ROUND_START_FAILED " + error.Message); InterruptGame("No se pudo preparar el mapa de la ronda."); }
            }
            else if (view.Phase == RoomPhase.Playing && game != null && game.IsHost && activeRoster != null)
            {
                foreach (var actor in activeRoster)
                {
                    var member = view.Members.FirstOrDefault(item => item.Id == actor.OwnerPuid);
                    if (member == null) game.Authority.RemoveActor(actor.ActorId, ActorRemovalReason.Disconnected);
                    else game.Authority.SetActorConnected(actor.ActorId, member.Connected);
                }
            }
            SyncVoiceContext(view);
            lastPhase = view.Phase;
        }
        private void PresentRoom(RoomView view, string message = "")
        {
            if (view == null || view.Phase != RoomPhase.Waiting) return;
            bool canStart = IsModeAvailable(view.Rules.MapId, view.Rules.ModeId) && view.Members.Count >= 2 && view.Members.All(m => m.Ready)
                && (!view.Rules.HumanCount.HasValue || view.Rules.HumanCount.Value < view.Members.Count);
            string reason = message.Length > 0 ? message : !IsModeAvailable(view.Rules.MapId, view.Rules.ModeId) ? "Este mapa todavía no tiene tareas preparadas para jugar." : view.Members.Count < 2 ? "Invitá a alguien con el código de la sala." : "Todos deben marcar Listo para empezar.";
            var members = view.Members.Select(m => new LobbyMemberUiState(m.Id, m.Name, m.Ready, m.Connected));
            string mapLabel = view.Rules.MapId == RoomRules.AlfaMap ? "Casa con patio" :
                HiggsfieldMaps?.Entries.FirstOrDefault(entry => entry.MapId == view.Rules.MapId)?.DisplayName ?? "Mapa no instalado";
            ui.PresentLobby(new LobbyUiState(lobby.IsOwner, lobby.Code, members, view.Members.First(m => m.Id == LocalId).Ready,
                false, view.Rules.HumanCount, canStart, canStart ? "" : reason, view.Rules.MapId, mapLabel, canExplore: true,
                isWaiting: view.Phase == RoomPhase.Waiting, modeId: view.Rules.ModeId, roundSeconds: view.Rules.RoundSeconds));
        }
        private void PresentLateJoinWaiting(RoomView view)
        {
            var members = view.Members.Select(member => new LobbyMemberUiState(member.Id, member.Name, member.Ready, member.Connected));
            string mapLabel = view.Rules.MapId == RoomRules.AlfaMap ? "Casa con patio" :
                HiggsfieldMaps?.Entries.FirstOrDefault(entry => entry.MapId == view.Rules.MapId)?.DisplayName ?? "Mapa no instalado";
            ui.PresentLobby(new LobbyUiState(false, lobby.Code, members, false, false, view.Rules.HumanCount,
                false, "La ronda está en curso.", view.Rules.MapId, mapLabel, canExplore: false,
                isWaiting: false, modeId: view.Rules.ModeId, roundSeconds: view.Rules.RoundSeconds));
        }
        public void LeaveRoom()
        {
            if (quiescing) return;
            RecordPlaytest("LeaveRequested",lobby?.IsOwner==true ? "Owner" : "Guest");
            pendingOnline = false; intentionalLeave = true; StopVoiceRoom(); StopGame(); StopLobbyMovement(); lobby?.Leave(); room?.Dispose(); room = null;
            transport?.Dispose(); transport = null; lastPhase = RoomPhase.Closed; activeRound = -1;
            LoadMap(false); menuAudio.gameObject.SetActive(true); menuAudio.EnterMenu(); ui.ShowMainMenu();
        }
        public void StartTraining(AlfaRole role, string modeId, string mapId)
        {
            if (quiescing) return;
            if (!GameModes.IsValid(modeId) || !IsModeAvailable(mapId, modeId))
            {
                ui.PresentTraining(new TrainingUiState(role, message: "El mapa no tiene el contenido necesario para este modo.", modeId: GameModes.IsValid(modeId) ? modeId : GameModes.Blood));
                ui.ShowTraining(); return;
            }
            try
            {
                trainingRole = role; training = true; StopLobbyMovement(); PrepareGame(true, mapId);
                var human = role == AlfaRole.Human;
                var roster = new[] {
                    new SpawnActor(1, "practice", human ? PlayerRole.Human : PlayerRole.Mosquito, SpawnPoint(human,0), isBot:false),
                    new SpawnActor(2, "bot-1", human ? PlayerRole.Mosquito : PlayerRole.Human, SpawnPoint(!human,0), isBot:true),
                    new SpawnActor(3, "bot-2", PlayerRole.Mosquito, SpawnPoint(false,1), isBot:true)
                };
                BeginGame(new GameplayRoundConfig(NewEpoch(), 1, map.MapId, map.ContentHash, GameModes.DefaultRoundSeconds(modeId),
                    bloodGoal: modeId == GameModes.Blood ? GameModes.BloodQuotaForHumans(1) : 0,
                    doors: game.World.GetDoorDefinitions(), tools: game.World.GetToolDefinitions(), modeId: modeId, objectives: GetObjectivesForMode(modeId)), roster);
                game.AutomaticTick = true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            {
                Debug.LogWarning("LMS_TRAINING_START_FAILED " + error.Message);
                StopGame(); LoadMap(false); menuAudio.gameObject.SetActive(true); menuAudio.EnterMenu();
                ui.PresentTraining(new TrainingUiState(role, message: "No se pudo preparar el entrenamiento. Volvé a intentarlo o elegí otro mapa.", modeId: modeId)); ui.ShowTraining();
            }
        }
        public void CancelTraining() { if (training) LeaveRoom(); }
        private void PrepareGame(bool practice, string mapId = RoomRules.AlfaMap)
        {
            if (quiescing) return;
            StopGame(); training = practice; showingResults = false; LoadMap(true, mapId); menuAudio.gameObject.SetActive(false);
            var root = new GameObject("Gameplay"); game = root.AddComponent<GameplayRuntime>();
            game.World.MapRoot = map.transform;
            game.NavigationData = map.SpatialData;
            game.IsHost = practice || lobby?.IsOwner == true; game.AutomaticTick = false;
            presentation = Instantiate(GameplayPresentationPrefab); presentation.GetComponent<GameplayPresentationRoot>().Bind(game);
            BindCameraDistance(mapId);
            spectator = presentation.AddComponent<GameplaySpectatorCamera>();
            spectator.Bind(game, presentation.GetComponentInChildren<Camera>(true));
            game.EventReady += ObserveCombatFeedback;
            game.RoundFinished += (_, __) => { if (!quiescing && !training) room?.FinishRound(); };
        }
        private void BeginGame(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster)
        {
            if (quiescing) return;
            activeConfig = config; activeRoster = roster.ToArray(); var local = roster.First(a => a.OwnerPuid == (training ? "practice" : LocalId));
            game.LocalActorId = local.ActorId; game.LocalPrincipal = local.OwnerPuid;
            game.MouseSensitivity = .002f * (local.Role == PlayerRole.Human ? settings.HumanSensitivity : settings.MosquitoSensitivity);
            game.InvertY = settings.InvertY; game.BeginRound(config, roster);
            SyncVoiceContext();
            if (!training) RecordPlaytest("RoundStarted",state:game.LatestSnapshot,role:local.Role.ToString());
            MenuCamera.enabled = false; MenuCamera.GetComponent<AudioListener>().enabled = false;
            presentation.GetComponentInChildren<AlfaAudioDirector>().EnterRound();
            ui.ShowGameplay(training);
            if (game.LatestSnapshot != null) PresentGame(game.LatestSnapshot);
            else ui.PresentHud(new BloodHudUiState(local.Role == PlayerRole.Human ? AlfaRole.Human : AlfaRole.Mosquito,
                config.RoundDurationTicks / 30f, 0, config.BloodGoal, modeId: config.ModeId, networkMessage: "Esperando a los jugadores…"));
        }
        private SpawnActor[] MakeRoster(RoomView view)
        {
            int human = 0, mosquito = 0;
            return view.Members.Select((m, i) => new SpawnActor((uint)i + 1, m.Id, m.Role,
                SpawnPoint(m.Role == PlayerRole.Human, m.Role == PlayerRole.Human ? human++ : mosquito++))).ToArray();
        }
        private Float3 SpawnPoint(bool human, int index)
        { var points = human ? map.HumanSpawnPoints : map.MosquitoSpawnPoints; return points[index % points.Length].position.ToFloat(); }
        private void PresentGame(GameSessionState state)
        {
            ObservePlaytestResult(state);
            if (state.SimulationPhase == SimulationPhase.Ended)
            {
                showingResults = true;
                ui.PresentResults(new ResultsUiState(state.Winner == PlayerRole.Human ? MatchOutcome.Humans : state.Winner == PlayerRole.Mosquito ? MatchOutcome.Mosquitoes : MatchOutcome.Interrupted,
                    training, training || lobby?.IsOwner == true, state.BloodCollected, state.BloodGoal, (float)state.HostTime, ModeHudText.ResultReason(state.Result), trainingRole: trainingRole, modeId: state.ModeId, mapId: state.MapId, tasksCompleted: state.TasksCompleted, tasksGoal: state.TasksGoal, mosquitoesAlive: state.Actors.Count(a => a.Role == PlayerRole.Mosquito && !a.Eliminated))); return;
            }
            var actor = state.Actors.FirstOrDefault(a => a.ActorId == game.LocalActorId); var personal = ModeHudText.LocalPrivate(state, game.LocalActorId, game.LocalPrivate);
            var role = actor?.Role == PlayerRole.Mosquito ? AlfaRole.Mosquito : AlfaRole.Human;
            var status = actor?.Eliminated == true ? HudActorState.Spectating : actor?.LifeState == LifeState.Fainted ? HudActorState.Fainted : actor?.LifeState == LifeState.Biting ? HudActorState.Extracting
                : actor?.LifeState == LifeState.Recovering ? HudActorState.Recovering : actor?.LifeState == LifeState.Stunned ? HudActorState.Stunned : HudActorState.Normal;
            string interaction = personal?.InteractionHint == InteractionHint.Door ? "F · Abrir / cerrar puerta" : personal?.InteractionHint == InteractionHint.Tool ? "F · Recoger matamoscas · G soltar" : personal?.InteractionHint == InteractionHint.ContactRequired ? "Acercate al cuerpo y mantené E" : "";
            if (actor?.Eliminated == true) interaction = "Tab · Cambiar compañero observado";
            else if (personal?.InteractionHint == InteractionHint.Task) interaction = "Mantené R · Trabajar en tu tarea";
            string privateTask = ModeHudText.PrivateTask(state, game.LocalActorId, personal, activeConfig?.Objectives, out float taskProgress);
            ui.PresentHud(new BloodHudUiState(role, state.TimeRemainingTicks / 30f, state.BloodCollected, state.BloodGoal, interaction,
                contextHint: CombatContext(state, actor),
                actorState: status, stateProgress01: personal?.ExtractionProgress ?? 0,
                networkMessage: !training && gameNetwork != null && !gameNetwork.Ready ? "Esperando a los jugadores…" : "",
                modeId: state.ModeId, tasksCompleted: state.TasksCompleted, tasksGoal: state.TasksGoal,
                mosquitoesAlive: state.Actors.Count(a => a.Role == PlayerRole.Mosquito && !a.Eliminated), livesRemaining: actor?.LivesRemaining ?? 0,
                privateTaskText: privateTask, taskProgress01: taskProgress));
        }
        private void ObserveCombatFeedback(GameplayEvent item)
        {
            var state = game?.LatestSnapshot;
            if (state == null || item.SessionEpoch != state.SessionEpoch || item.RoundId != state.RoundId ||
                item.SourceActorId != game.LocalActorId || item.Kind != GameplayEventKind.StrikeImpact) return;
            var target = state.Actors.FirstOrDefault(a => a.ActorId == item.TargetActorId);
            combatFeedback = target?.Role == PlayerRole.Mosquito ? "¡Impacto en el mosquito!" : "El golpe chocó con un obstáculo.";
            combatFeedbackUntil = Time.unscaledTimeAsDouble + 1.1;
        }
        private string CombatContext(GameSessionState state, ActorSnapshot actor)
        {
            if (actor == null) return "";
            if (actor.Eliminated) return "Observás a tu equipo hasta el final de la ronda. Tab · cambiar compañero";
            if (actor.LifeState == LifeState.Falling || actor.LifeState == LifeState.Fainted ||
                actor.LifeState == LifeState.Stunned || actor.LifeState == LifeState.Recovering)
                return state.ModeId == GameModes.Tasks && actor.Role == PlayerRole.Mosquito ? "Esperá ayuda de un aliado. Sin rescate, perdés una vida." : "Estás incapacitado. Esperá la recuperación.";
            if (Time.unscaledTimeAsDouble < combatFeedbackUntil) return combatFeedback;
            if (state.ModeId == GameModes.Survival && actor.Role == PlayerRole.Mosquito) return "Sobreviví hasta el final. Un golpe te elimina. W · volar · F · posarte";
            if (actor.Role == PlayerRole.Human)
            {
                bool beingBitten = state.Actors.Any(a => a.BiteAttachment.HasValue &&
                    a.BiteAttachment.Value.VictimId == actor.ActorId);
                return beingBitten ? "¡Te están picando! Buscá al mosquito y golpeá hacia él." :
                    state.ModeId == GameModes.Tasks ? "R mantenida · trabajar en tu tarea   ·   Clic · defenderte" : "Clic · golpear hacia la mira   ·   Ctrl · agacharte";
            }
            if (actor.LifeState == LifeState.PreparingBite) return "Contacto logrado. Mantené E para picar.";
            if (actor.LifeState == LifeState.Biting) return state.ModeId == GameModes.Blood ? "Extrayendo sangre · Mantené E · Soltá E para despegar" : "Interrumpiendo al humano · Mantené E · Soltá E para despegar";
            if (actor.LifeState == LifeState.Surface || actor.LifeState == LifeState.ApproachingSurface)
                return "W A S D · desplazarte   ·   F · despegar";
            return "W · volar hacia la mira   ·   F · posarte   ·   E mantenida en contacto · picar";
        }
        public void SpectateNext() { if (spectator) spectator.NextTarget(); }
        public void SetGameplayInputBlocked(bool blocked) { game?.SetInputBlocked(blocked); }
        public void ResumeGame() { if (game?.LatestSnapshot != null) { game.SetInputBlocked(false); ui.ShowGameplay(); } }
        public void ReturnToLobby() { if (training) LeaveRoom(); else room?.ReturnToLobby(); }
        private void OnGameNetworkFailed(string reason) => InterruptGame("Se perdió la conexión con la partida. " + reason);
        private void InterruptGame(string reason)
        {
            if (quiescing) return;
            if (lobby?.IsOwner == true && room?.Current?.Phase == RoomPhase.Playing) room.FinishRound();
            showingResults = true; game?.SetInputBlocked(true);
            ui.PresentResults(new ResultsUiState(MatchOutcome.Interrupted, training, training || lobby?.IsOwner == true, 0, activeConfig?.BloodGoal ?? 20, 0, reason, trainingRole, activeConfig?.ModeId ?? GameModes.Blood, activeConfig?.MapId ?? RoomRules.AlfaMap));
        }
        private void BindCameraDistance(string mapId)
        {
            if (cameraDistance) cameraDistance.Unbind();
            if (mapId == RoomRules.AlfaMap) return;
            float far = HiggsfieldMaps.Resolve(mapId).CameraFarPlane;
            if (far == 0) return;
            var cameras = presentation.GetComponentsInChildren<Camera>(true);
            if (cameras.Length != 1) throw new InvalidOperationException("Expected exactly one shared gameplay camera.");
            if (!cameraDistance) cameraDistance = GetComponent<HiggsfieldCameraDistanceOverride>();
            if (!cameraDistance) cameraDistance = gameObject.AddComponent<HiggsfieldCameraDistanceOverride>();
            cameraDistance.Bind(cameras[0], map.transform, far);
        }

        private void StopGame()
        {
            if (cameraDistance) cameraDistance.Unbind();
            combatFeedback = ""; combatFeedbackUntil = 0;
            appliedAppearance.Clear();
            gameNetwork?.Dispose(); gameNetwork = null;
            if (game) { game.EventReady -= ObserveCombatFeedback; game.StopRound(); game.gameObject.SetActive(false); Destroy(game.gameObject); game = null; }
            if (presentation) { presentation.SetActive(false); Destroy(presentation); presentation = null; }
            activeRoster = null; activeConfig = null; spectator = null; showingResults = false;
        }
        private IReadOnlyList<ObjectiveDefinition> GetObjectivesForMode(string modeId) => modeId == GameModes.Tasks ? game.World.GetObjectiveDefinitions() : Array.Empty<ObjectiveDefinition>();
        private static bool HasTaskCatalog(EnvironmentMapDefinition definition, string mapId)
        {
            if (!definition || !definition.TryGetComponent<GameplayObjectiveCatalog>(out var catalog)) return false;
            try { catalog.ValidateAuthoring(mapId); return true; }
            catch (InvalidOperationException) { return false; }
        }
        private bool IsModeAvailable(string mapId, string modeId)
        {
            if (!GameModes.IsValid(modeId) || !IsAvailableMap(mapId)) return false;
            if (modeId != GameModes.Tasks) return true;
            var definition = mapId == RoomRules.AlfaMap ? HousePrefab ? HousePrefab.GetComponent<EnvironmentMapDefinition>() : null : HiggsfieldMaps.Entries.FirstOrDefault(e => e.MapId == mapId)?.Prefab;
            return HasTaskCatalog(definition, mapId);
        }
        private bool IsAvailableMap(string mapId) => mapId == RoomRules.AlfaMap ||
            (HiggsfieldMaps && HiggsfieldMaps.Entries.Any(entry => entry.MapId == mapId));
        private void LoadMap(bool house, string mapId = RoomRules.AlfaMap)
        {
            if (cameraDistance) cameraDistance.Unbind();
            if (quiescing) return;
            var entry = house && mapId != RoomRules.AlfaMap ? HiggsfieldMaps.Resolve(mapId) : null;
            LightingRig.UnbindHiggsfield();
            if (map) { map.gameObject.SetActive(false); Destroy(map.gameObject); }
            map = Instantiate(entry != null ? entry.Prefab.gameObject : house ? HousePrefab : LobbyPrefab).GetComponent<EnvironmentMapDefinition>();
            if (entry != null) LightingRig.BindHiggsfield(map.transform, HiggsfieldMaps.ResolveLighting(mapId, map));
            else LightingRig.BindMap(map.PresentationAnchors,house);
            MenuCamera.enabled = true; MenuCamera.GetComponent<AudioListener>().enabled = true;
            MenuCamera.transform.position = map.PlayBounds.center + new Vector3(5, 4, -6);
            MenuCamera.transform.LookAt(map.PlayBounds.center + Vector3.up);
            menuCharacters = null;
            livingMenu = null;
            customizationBackdrop = null;
            if (!house)
            {
                var cameraAnchor = map.PresentationAnchors.Find("MainMenuCamera");
                if (cameraAnchor) { MenuCamera.transform.SetPositionAndRotation(cameraAnchor.position,cameraAnchor.rotation); MenuCamera.fieldOfView=55; }
                CreateLivingMenu();
            }
            if (ui) OnUiScreenChanged(ui.CurrentScreen);
        }
        private void OnUiScreenChanged(AlfaUiScreen screen)
        {
            if (quiescing) return;
            if (screen == AlfaUiScreen.Customization)
            {
                if (!menuCharacters) return;
                if (customizationBackdrop != menuCharacters)
                {
                    customizationBackdrop = menuCharacters;
                    customizationBackdropWasActive = menuCharacters.activeSelf;
                }
                menuCharacters.SetActive(false);
                return;
            }

            var backdrop = customizationBackdrop;
            customizationBackdrop = null;
            // Restore only this map's decorative root and its prior visibility. Lobby exploration
            // may have hidden it while customization was open; never override that owner.
            if (backdrop && backdrop == menuCharacters)
            {
                backdrop.SetActive(customizationBackdropWasActive && !lobbyMovement);
                ApplyLiveAppearance();
            }
        }
        private static ulong NewEpoch() { ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); return value == 0 ? 1ul : value; }
        private void ShowOnlineError(string text) => ui.PresentOnline(new OnlineUiState(text.Contains("IncompatibleVersion") ? OnlineOperationPhase.IncompatibleVersion : OnlineOperationPhase.RecoverableError, text, canRetry: true));
        public void QuitGame()
        {
            Quiesce();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Also called when only this component is destroyed and the host remains alive.
        // Mark closed before invoking lifecycle hooks: disposal must never rebuild menu/game roots.
        private void Quiesce()
        {
            if (quiescing) return;
            RecordPlaytest("ShutdownRequested",lobby?.IsOwner==true ? "Owner" : "Guest");
            StopPlaytestJournal("ShutdownRequested");
            quiescing = true; pendingOnline = false; intentionalLeave = true; enabled = false;
            StopAllCoroutines();
            if (ui)
            {
                ui.FeedbackRequested -= OnUiFeedback;
                ui.ScreenChanged -= OnUiScreenChanged;
            }
            if (room != null) room.RoomChanged -= OnRoomChanged;
            if (lobby != null) lobby.Changed -= OnLobbyChanged;
            if (transport != null)
            {
                transport.PacketReceived -= ReceiveAppearance;
                transport.PacketReceived -= ReceiveLobbyPacket;
            }
            if (lobbyFrames != null) lobbyFrames.MessageReceived -= ReceiveLobbyMessage;
            if (lobbyMovement)
            {
                lobbyMovement.InputReady -= SendLobbyInput;
                lobbyMovement.SnapshotReady -= SendLobbySnapshot;
            }
            if (gameNetwork != null)
            {
                gameNetwork.BeginReceived -= BeginGame;
                gameNetwork.Failed -= OnGameNetworkFailed;
                if (game)
                {
                    game.InputReady -= gameNetwork.SendInput;
                    game.ActionReady -= gameNetwork.SendAction;
                }
            }

            // Silence is synchronous; Destroy itself is deferred until the end of the frame.
            ShutdownStep(() => DeactivateOwnedRoot(presentation));
            ShutdownStep(() => DeactivateOwnedRoot(menuAudio ? menuAudio.gameObject : null));
            ShutdownStep(() => DeactivateOwnedRoot(game ? game.gameObject : null));
            ShutdownStep(() => DeactivateOwnedRoot(lobbyMovement ? lobbyMovement.gameObject : null));
            DisposeForShutdown(ref gameNetwork);
            ShutdownStep(StopGame);
            ShutdownStep(StopLobbyMovement);
            // A failed StopRound/Unbind must not strand a separate owned root.
            ShutdownStep(() => { if (game) Destroy(game.gameObject); }); game = null;
            ShutdownStep(() => { if (presentation) Destroy(presentation); }); presentation = null;
            ShutdownStep(() => { if (lobbyMovement) Destroy(lobbyMovement.gameObject); }); lobbyMovement = null;
            activeRoster = null; activeConfig = null; spectator = null; showingResults = false;
            ShutdownStep(StopVoiceRoom);
            DisposeForShutdown(ref room);
            DisposeForShutdown(ref transport);
            DisposeForShutdown(ref lobby);
            DisposeForShutdown(ref connection);

            // Release only roots instantiated by this bootstrap, never prefab assets or shared cameras.
            ShutdownStep(() => { if (menuAudio) Destroy(menuAudio.gameObject); }); menuAudio = null;
            ShutdownStep(() => { if (ui) { ui.gameObject.SetActive(false); Destroy(ui.gameObject); } }); ui = null;
            ShutdownStep(() => { if (map) { DeactivateOwnedRoot(map.gameObject); Destroy(map.gameObject); } }); map = null;
            menuCharacters = null;
            customizationBackdrop = null;
        }

        private static void DeactivateOwnedRoot(GameObject root)
        {
            if (!root) return;
            try
            {
                foreach (var director in root.GetComponentsInChildren<AlfaAudioDirector>(true))
                    if (director) ShutdownStep(director.StopAll);
            }
            finally { root.SetActive(false); }
        }

        private static void DisposeForShutdown<T>(ref T resource) where T : class, IDisposable
        {
            var owned = resource; resource = null;
            if (owned != null) ShutdownStep(owned.Dispose);
        }

        private static void ShutdownStep(Action stop)
        {
            try { stop(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void OnDestroy() { Quiesce(); }
    }
}








