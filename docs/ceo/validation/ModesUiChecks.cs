using System;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Bootstrap;
using LetMeSleep.UI;

static class ModesUiChecks
{
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static ActorSnapshot Actor(uint id, PlayerRole role, bool eliminated = false) => new ActorSnapshot(id, role, eliminated ? LifeState.Eliminated : role == PlayerRole.Human ? LifeState.Active : LifeState.Flying, 1, Float3.Zero, Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0, livesRemaining: role == PlayerRole.Mosquito && !eliminated ? 3 : 0);
    static ActorPrivateState Private(uint actor = 1, ulong epoch = 1, ulong round = 1, TaskAssignmentStatus status = TaskAssignmentStatus.Active) => new ActorPrivateState(actor, 1, 1, CommandReject.None, InteractionHint.Task, 0, 0, 0, 0, true, DoorUseResult.Accepted, epoch, round, 60, new TaskAssignment("cabin", 0, 900, 90, status == TaskAssignmentStatus.Completed ? 90u : 30u, status, 1));
    static int Main()
    {
        var objective = new ObjectiveDefinition("cabin", ObjectiveKind.Clean, "task.isla.cabin_access", "task.action.hold_clean", Float3.Zero, Float3.Zero, 1, 90, "room");
        var config = new GameplayRoundConfig(1, 1, RoomRules.IslaDelLaguitoMap, "hash", 120, modeId: GameModes.Tasks, objectives: new[] { objective });
        var actors = new[] { Actor(1, PlayerRole.Human), Actor(2, PlayerRole.Mosquito, true), Actor(3, PlayerRole.Human), Actor(4, PlayerRole.Mosquito), Actor(5, PlayerRole.Mosquito) };
        var state = new GameSessionState(config, 60, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, actors, Array.Empty<DoorSnapshot>(), tasksGoal: 4, viableTaskOpportunities: 6);
        float progress;
        string text = ModeHudText.PrivateTask(state, 1, Private(), config.Objectives, out progress);
        Check(text.Contains("CABAÑA") && text.Contains("Mantené R") && text.Contains("00:28"), "own objective/action/deadline displayed");
        Check(Math.Abs(progress - 1f / 3) < .0001f, "own progress preserved");
        foreach (var wrong in new[] { Private(3), Private(epoch: 2), Private(round: 2) })
        {
            text = ModeHudText.PrivateTask(state, 1, wrong, config.Objectives, out progress);
            Check(!text.Contains("CABAÑA") && progress == 0, "foreign or stale private assignment hidden");
        }
        text = ModeHudText.PrivateTask(state, 2, Private(2), config.Objectives, out progress);
        Check(text == "" && progress == 0, "eliminated mosquito never receives task copy");
        text = ModeHudText.PrivateTask(state, 4, Private(4), config.Objectives, out progress);
        Check(text == "", "active mosquito never receives task copy");
        Check(ModeHudText.PrivateTask(state, 1, Private(status: TaskAssignmentStatus.Completed), config.Objectives, out progress).Contains("COMPLETADA"), "completion awaits next assignment");
        Check(ModeHudText.PrivateTask(state, 1, Private(status: TaskAssignmentStatus.Missed), config.Objectives, out progress).Contains("PLAZO VENCIDO"), "missed deadline explained");
        Check(ModeHudText.PrivateTask(state, 1, Private(status: TaskAssignmentStatus.WaitingForRoute), config.Objectives, out progress).Contains("no suma un fallo"), "blocked route has distinct non-penalty message");
        Check(ModeHudText.PrivateTask(state, 1, Private(), Array.Empty<ObjectiveDefinition>(), out progress).Contains("Esperando información"), "unknown local catalog never renders raw network id");
        Check(ModeHudText.SpectatorTarget(state, 2, 0, false) == 4, "spectator chooses live teammate only");
        Check(ModeHudText.SpectatorTarget(state, 2, 4, true) == 5, "spectator next teammate");
        Check(ModeHudText.SpectatorTarget(state, 2, 5, true) == 4, "spectator cycles teammates");
        Check(ModeHudText.SpectatorTarget(state, 1, 4, true) == 0, "active actor cannot spectate");
        Check(ModeHudText.SpectatorTarget(state, 2, 3, false) == 4, "foreign team target rejected");
        Check(ModeHudText.SpectatorTarget(null, 2, 4, true) == 0, "round cleanup clears spectator selection");
        var noTeam = new GameSessionState(config, 60, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, actors.Take(3).ToArray(), Array.Empty<DoorSnapshot>(), tasksGoal: 4, viableTaskOpportunities: 6);
        Check(ModeHudText.SpectatorTarget(noTeam, 2, 4, true) == 0, "no live teammates never falls back to opponents");
        Check(AlfaModeText.ModeIds.SequenceEqual(new[] { GameModes.Blood, GameModes.Survival, GameModes.Tasks }), "three public modes exposed");
        Check(AlfaModeText.Score(GameModes.Survival, 99, 20, 8, 9, 2) == "MOSQUITOS VIVOS  2", "survival HUD suppresses blood and task counters");
        Check(AlfaModeText.Score(GameModes.Tasks, 99, 20, 8, 9, 2) == "TAREAS  8 / 9", "tasks HUD uses collective score");
        Check(AlfaModeText.ResultScore(GameModes.Tasks, 99, 20, 8, 9, 2).Contains("8 / 9"), "tasks results preserve score");
        Check(AlfaModeText.Instructions(GameModes.Tasks, false).Contains("Mantené R"), "task training copy matches real held-use key");
        Check(AlfaModeText.Instructions(GameModes.Survival, true).Contains("Una vida"), "survival training states elimination risk");
        Check(ModeHudText.ResultReason(RoundEndReason.TasksMet).Contains("alcanzaron"), "tasks met result reason");
        Check(ModeHudText.ResultReason(RoundEndReason.TasksMissed).Contains("sin alcanzar"), "tasks missed result reason");
        Check(ModeHudText.ResultReason(RoundEndReason.AllOpponentsEliminated).Contains("ningún mosquito"), "elimination result reason");
        foreach (string key in new[] { "task.isla.cabin_access", "task.casa.bathroom_tile", "task.camp.washroom", "task.yacht.main_deck", "task.port.lighthouse_floor" }) Check(ModeHudText.ObjectiveName(key) != "TU TAREA", "authored objective text " + key);
        Check(!ModeHudText.ObjectiveName("malicious.raw.id").Contains("malicious"), "unknown key never renders raw id");
        Console.WriteLine("RESULT " + checks + " PASS 0 FAIL"); return 0;
    }
}
