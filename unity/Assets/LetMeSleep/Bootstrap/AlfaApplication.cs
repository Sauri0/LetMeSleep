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
using LetMeSleep.Presentation.Gameplay;
using LetMeSleep.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication : MonoBehaviour, IMenuActions
    {
        public GameObject HousePrefab, LobbyPrefab, HumanPrefab, MosquitoPrefab, GameplayPresentationPrefab, MenuAudioPrefab;
        public Camera MenuCamera, PreviewCamera;
        public Transform PreviewStage;
        public RenderTexture PreviewTexture;
        public TMP_FontAsset HeadingFont, BodyFont;
        public AudioMixer Mixer;
        public LetMeSleep.Presentation.AlfaLightingRig LightingRig;
        private AlfaUiController ui;
        private AlfaAudioDirector menuAudio;
        private EosConnection connection;
        private EosLobbySession lobby;
        private EosPeerTransport transport;
        private OnlineRoomCoordinator room;
        private OnlineGameplaySession gameNetwork;
        private GameplayRuntime game;
        private GameObject presentation;
        private EnvironmentMapDefinition map;
        private GameObject menuCharacters;
        private string playerName = "Jugador", joinCode, lastError = "";
        private bool pendingOnline, createOnline, training, showingResults;
        private string closingError = "";
        private bool intentionalLeave;
        private int activeRound = -1;
        private RoomPhase lastPhase = RoomPhase.Closed;
        private SpawnActor[] activeRoster;
        private AlfaRole trainingRole;
        private double hudAt;
        private string LocalId => connection?.LocalUserId?.ToString() ?? "practice";
        private string DataPath
        {
            get
            {
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
            try { Directory.CreateDirectory(DataPath); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { saveError="No se puede guardar en la carpeta de usuario."; }
            LoadPreferences();
            ui = AlfaUiRuntime.Create(this, new AlfaUiDependencies(HeadingFont, BodyFont, preview:
                new CharacterPreviewSetup(PreviewCamera, PreviewStage, PreviewTexture, HumanPrefab, MosquitoPrefab)));
            menuAudio = Instantiate(MenuAudioPrefab).GetComponent<AlfaAudioDirector>();
            ui.SetRememberedPlayerName(playerName); LoadMap(false); menuAudio.EnterMenu(); PresentPreferences(); ApplySettingsValues();
            StartBuildProbeIfRequested();
        }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            connection?.Tick(now); lobby?.Tick(now); room?.Tick(now); transport?.Poll(); gameNetwork?.Tick(now);
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
            ApplyPreviewColors(); TickAppearance(now);
        }
        public void CreateRoom(string name) => BeginOnline(name, null);
        public void JoinRoom(string name, string code) => BeginOnline(name, code);
        private void BeginOnline(string name, string code)
        {
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
            pendingOnline = false;
            room?.Dispose(); transport?.Dispose(); lobby?.Dispose();
            lobby = new EosLobbySession(connection); transport = new EosPeerTransport(connection, lobby);
            peerAppearances.Clear(); peerAppearanceTimes.Clear(); appliedAppearance.Clear(); appearanceAt = 0;
            transport.PacketReceived += ReceiveAppearance;
            room = new OnlineRoomCoordinator(connection, lobby, transport, playerName);
            room.RoomChanged += OnRoomChanged;
            lobby.Changed += OnLobbyChanged;
            ui.PresentOnline(new OnlineUiState(createOnline ? OnlineOperationPhase.Creating : OnlineOperationPhase.Searching, canCancel: true));
            if (createOnline) lobby.Create(); else lobby.Join(joinCode);
        }
        private void OnLobbyChanged()
        {
            if (lobby.State == LobbyState.Closed && !intentionalLeave)
            { StopGame(); StopLobbyMovement(); LoadMap(false); ui.ShowJoinRoom(); if(closingError.Length>0) ShowOnlineError(closingError); else ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.RoomClosed)); }
        }
        public void CancelOnline()
        {
            pendingOnline = false; intentionalLeave = true;
            if (lobby != null) lobby.Leave();
            ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.Cancelled));
        }
        public void CopyRoomCode(string code) { GUIUtility.systemCopyBuffer = code; }
        public void SetReady(bool ready) { lastError=""; var result=room?.SetReady(ready); if(result.HasValue && result.Value!=RoomError.None) PresentRoom(room.Current,"No se pudo marcar Listo."); }
        public void SetHumanCount(int? count)
        {
            var previous = room?.Current?.Rules;
            if (previous != null) room.SetRules(new RoomRules(count, previous.RoundSeconds, previous.BloodQuota));
        }
        public void StartRound()
        {
            var error = room?.StartRound() ?? RoomError.Closed;
            if (error != RoomError.None) PresentRoom(room.Current, "Todavía falta que todos estén listos.");
        }
        private void OnRoomChanged(RoomView view)
        {
            if (view == null) return;
            training = false;
            if (view.Phase == RoomPhase.Waiting)
            {
                if (lastPhase != RoomPhase.Waiting) { StopGame(); LoadMap(false); menuAudio.gameObject.SetActive(true); menuAudio.EnterMenu(); }
                SyncLobbyMovement(view); PresentRoom(view);
            }
            else if (view.Phase == RoomPhase.Playing && activeRound != view.Round)
            {
                StopLobbyMovement(); PrepareGame(false); activeRound = view.Round;
                gameNetwork = new OnlineGameplaySession(lobby, room, transport, LocalId, map.ContentHash,
                    game.Authority, game, () => game.World.GetDoorDefinitions(), () => game.World.GetToolDefinitions());
                gameNetwork.BeginReceived += BeginGame;
                gameNetwork.Failed += reason => InterruptGame("Se perdió la conexión con la partida. " + reason);
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
                        view.Rules.RoundSeconds, view.Rules.BloodQuota, doors: game.World.GetDoorDefinitions(), tools: game.World.GetToolDefinitions());
                    BeginGame(config, roster); gameNetwork.StartHost(config, roster);
                }
            }
            else if (view.Phase == RoomPhase.Playing && game != null && game.IsHost && activeRoster != null)
            {
                foreach (var actor in activeRoster) if (!view.Members.Any(m => m.Id == actor.OwnerPuid)) game.Authority.RemoveActor(actor.ActorId, ActorRemovalReason.Disconnected);
            }
            lastPhase = view.Phase;
        }
        private void PresentRoom(RoomView view, string message = "")
        {
            if (view == null || view.Phase != RoomPhase.Waiting) return;
            bool canStart = view.Members.Count >= 2 && view.Members.All(m => m.Ready)
                && (!view.Rules.HumanCount.HasValue || view.Rules.HumanCount.Value < view.Members.Count);
            string reason = message.Length > 0 ? message : view.Members.Count < 2 ? "Invitá a alguien con el código de la sala." : "Todos deben marcar Listo para empezar.";
            var members = view.Members.Select(m => new LobbyMemberUiState(m.Id, m.Name, m.Ready));
            ui.PresentLobby(new LobbyUiState(lobby.IsOwner, lobby.Code, members, view.Members.First(m => m.Id == LocalId).Ready,
                false, view.Rules.HumanCount, canStart, canStart ? "" : reason, canExplore: true));
        }
        public void LeaveRoom()
        {
            pendingOnline = false; intentionalLeave = true; StopGame(); StopLobbyMovement(); lobby?.Leave(); room?.Dispose(); room = null;
            transport?.Dispose(); transport = null; lastPhase = RoomPhase.Closed; activeRound = -1;
            LoadMap(false); menuAudio.gameObject.SetActive(true); menuAudio.EnterMenu(); ui.ShowMainMenu();
        }
        public void StartTraining(AlfaRole role, string modeId, string mapId)
        {
            if (modeId != AlfaUiController.BloodModeId || mapId != RoomRules.AlfaMap) return;
            trainingRole = role; training = true; StopLobbyMovement(); PrepareGame(true);
            var human = role == AlfaRole.Human;
            var roster = new[] {
                new SpawnActor(1, "practice", human ? PlayerRole.Human : PlayerRole.Mosquito, SpawnPoint(human,0), isBot:false),
                new SpawnActor(2, "bot-1", human ? PlayerRole.Mosquito : PlayerRole.Human, SpawnPoint(!human,0), isBot:true),
                new SpawnActor(3, "bot-2", PlayerRole.Mosquito, SpawnPoint(false,1), isBot:true)
            };
            BeginGame(new GameplayRoundConfig(NewEpoch(), 1, map.MapId, map.ContentHash, doors: game.World.GetDoorDefinitions(), tools: game.World.GetToolDefinitions()), roster);
            game.AutomaticTick = true;
        }
        public void CancelTraining() { if (training) LeaveRoom(); }
        private void PrepareGame(bool practice)
        {
            StopGame(); training = practice; showingResults = false; LoadMap(true); menuAudio.gameObject.SetActive(false);
            var root = new GameObject("Gameplay"); game = root.AddComponent<GameplayRuntime>();
            game.World.MapRoot = map.transform;
            game.NavigationData = map.SpatialData;
            game.IsHost = practice || lobby.IsOwner; game.AutomaticTick = false;
            presentation = Instantiate(GameplayPresentationPrefab); presentation.GetComponent<GameplayPresentationRoot>().Bind(game);
            game.RoundFinished += (_, __) => { if (!training) room?.FinishRound(); };
        }
        private void BeginGame(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster)
        {
            activeRoster = roster.ToArray(); var local = roster.First(a => a.OwnerPuid == (training ? "practice" : LocalId));
            game.LocalActorId = local.ActorId; game.LocalPrincipal = local.OwnerPuid;
            game.MouseSensitivity = .002f * (local.Role == PlayerRole.Human ? settings.HumanSensitivity : settings.MosquitoSensitivity);
            game.InvertY = settings.InvertY; game.BeginRound(config, roster);
            MenuCamera.enabled = false; MenuCamera.GetComponent<AudioListener>().enabled = false;
            presentation.GetComponentInChildren<AlfaAudioDirector>().EnterRound();
            PresentGame(game.LatestSnapshot ?? new GameSessionState(config, 0, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>()));
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
            if (state.SimulationPhase == SimulationPhase.Ended)
            {
                showingResults = true;
                ui.PresentResults(new ResultsUiState(state.Winner == PlayerRole.Human ? MatchOutcome.Humans : MatchOutcome.Mosquitoes,
                    training, training || lobby.IsOwner, state.BloodCollected, state.BloodGoal, (float)state.HostTime, trainingRole: trainingRole)); return;
            }
            var actor = state.Actors.FirstOrDefault(a => a.ActorId == game.LocalActorId); var personal = game.LocalPrivate;
            var role = actor?.Role == PlayerRole.Mosquito ? AlfaRole.Mosquito : AlfaRole.Human;
            var status = actor?.LifeState == LifeState.Fainted ? HudActorState.Fainted : actor?.LifeState == LifeState.Biting ? HudActorState.Extracting
                : actor?.LifeState == LifeState.Recovering ? HudActorState.Recovering : actor?.LifeState == LifeState.Stunned ? HudActorState.Stunned : HudActorState.Normal;
            string interaction = personal?.InteractionHint == InteractionHint.Door ? "F · Abrir / cerrar puerta" : personal?.InteractionHint == InteractionHint.Tool ? "F · Recoger matamoscas · G soltar" : personal?.InteractionHint == InteractionHint.ContactRequired ? "Acercate al cuerpo y mantené E" : "";
            ui.PresentHud(new BloodHudUiState(role, state.TimeRemainingTicks / 30f, state.BloodCollected, state.BloodGoal, interaction,
                actorState: status, stateProgress01: personal?.ExtractionProgress ?? 0,
                networkMessage: !training && gameNetwork != null && !gameNetwork.Ready ? "Esperando a los jugadores…" : ""));
        }
        public void SetGameplayInputBlocked(bool blocked) { game?.SetInputBlocked(blocked); }
        public void ResumeGame() { if (game?.LatestSnapshot != null) { game.SetInputBlocked(false); ui.ShowGameplay(); } }
        public void ReturnToLobby() { if (training) LeaveRoom(); else room?.ReturnToLobby(); }
        private void InterruptGame(string reason)
        {
            if (lobby?.IsOwner == true && room?.Current?.Phase == RoomPhase.Playing) room.FinishRound();
            showingResults = true; game?.SetInputBlocked(true);
            ui.PresentResults(new ResultsUiState(MatchOutcome.Interrupted, training, training || lobby.IsOwner, 0, 20, 0, reason));
        }
        private void StopGame()
        {
            appliedAppearance.Clear();
            gameNetwork?.Dispose(); gameNetwork = null;
            if (game) { game.StopRound(); game.gameObject.SetActive(false); Destroy(game.gameObject); game = null; }
            if (presentation) { presentation.SetActive(false); Destroy(presentation); presentation = null; }
            activeRoster = null; showingResults = false;
        }
        private void LoadMap(bool house)
        {
            if (map) { map.gameObject.SetActive(false); Destroy(map.gameObject); }
            map = Instantiate(house ? HousePrefab : LobbyPrefab).GetComponent<EnvironmentMapDefinition>();
            LightingRig.BindMap(map.PresentationAnchors,house);
            MenuCamera.enabled = true; MenuCamera.GetComponent<AudioListener>().enabled = true;
            MenuCamera.transform.position = map.PlayBounds.center + new Vector3(5, 4, -6);
            MenuCamera.transform.LookAt(map.PlayBounds.center + Vector3.up);
            menuCharacters = null;
            if (!house)
            {
                var cameraAnchor = map.PresentationAnchors.Find("MainMenuCamera");
                if (cameraAnchor) { MenuCamera.transform.SetPositionAndRotation(cameraAnchor.position,cameraAnchor.rotation); MenuCamera.fieldOfView=55; }
                menuCharacters = new GameObject("MenuCharacterDisplay"); menuCharacters.transform.SetParent(map.transform,false);
                CreateMenuCharacter(HumanPrefab,"HumanMenuStage",0);
                CreateMenuCharacter(MosquitoPrefab,"MosquitoMenuStage",1);
            }
        }
        private void CreateMenuCharacter(GameObject prefab,string anchorName,int motion)
        {
            var anchor=map.PresentationAnchors.Find(anchorName); if(!anchor) return;
            var instance=Instantiate(prefab,anchor.position,anchor.rotation,menuCharacters.transform);
            if(prefab==MosquitoPrefab) instance.transform.localScale*=2.5f;
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            var view=instance.GetComponent<CharacterView>(); ApplyAppearance(view,appearance); view.PlayMotion(motion);
        }
        private static ulong NewEpoch() { ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); return value == 0 ? 1ul : value; }
        private void ShowOnlineError(string text) => ui.PresentOnline(new OnlineUiState(text.Contains("IncompatibleVersion") ? OnlineOperationPhase.IncompatibleVersion : OnlineOperationPhase.RecoverableError, text, canRetry: true));
        public void QuitGame() { Application.Quit(); }
        private void OnDestroy() { gameNetwork?.Dispose(); room?.Dispose(); transport?.Dispose(); lobby?.Dispose(); connection?.Dispose(); if (ui) Destroy(ui.gameObject); }
    }
}








