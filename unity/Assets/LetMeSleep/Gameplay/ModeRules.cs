using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public sealed class ModeRuleProfile
    {
        public string ModeId { get; }
        public string Id => GameModes.ProfileId(ModeId);
        public int MosquitoLives => ModeId == GameModes.Tasks ? 3 : ModeId == GameModes.Survival ? 1 : 0;
        public uint TaskCadenceTicks { get; }
        public uint TaskDeadlineTicks { get; }
        public uint TaskMinimumDeadlineTicks { get; }
        public uint TaskFailurePenaltyTicks { get; }
        public uint TaskSuccessRecoveryTicks { get; }
        public uint TaskInterruptionGraceTicks { get; }
        public uint TaskDecayBasisPointsPerSecond { get; }
        public string Hash => string.Join(":", Id, MosquitoLives, TaskCadenceTicks, TaskDeadlineTicks, TaskMinimumDeadlineTicks, TaskFailurePenaltyTicks,
            TaskSuccessRecoveryTicks, TaskInterruptionGraceTicks, TaskDecayBasisPointsPerSecond);
        public ModeRuleProfile(string modeId, uint taskCadenceTicks = 1200, uint taskDeadlineTicks = 900, uint taskMinimumDeadlineTicks = 450, uint taskFailurePenaltyTicks = 150,
            uint taskSuccessRecoveryTicks = 90, uint taskInterruptionGraceTicks = 30, uint taskDecayBasisPointsPerSecond = 1000)
        {
            if (!GameModes.IsValid(modeId) || taskCadenceTicks < 30 || taskCadenceTicks > 54000 || taskDeadlineTicks < 1 || taskDeadlineTicks > taskCadenceTicks || taskMinimumDeadlineTicks < 1 || taskMinimumDeadlineTicks > taskDeadlineTicks || taskFailurePenaltyTicks < 1 || taskFailurePenaltyTicks > taskDeadlineTicks) throw new ArgumentException("Invalid mode profile.");
            if (taskSuccessRecoveryTicks > 54000 || taskInterruptionGraceTicks > 54000 || taskDecayBasisPointsPerSecond > 10000) throw new ArgumentException("Invalid task recovery or decay profile.");
            ModeId = modeId; TaskCadenceTicks = taskCadenceTicks; TaskDeadlineTicks = taskDeadlineTicks;
            TaskMinimumDeadlineTicks = taskMinimumDeadlineTicks; TaskFailurePenaltyTicks = taskFailurePenaltyTicks;
            TaskSuccessRecoveryTicks = taskSuccessRecoveryTicks; TaskInterruptionGraceTicks = taskInterruptionGraceTicks; TaskDecayBasisPointsPerSecond = taskDecayBasisPointsPerSecond;
        }
        // Failure-only projection retained for callers; live windows also recover on success.
        public uint DeadlineFor(int failures) => (uint)Math.Max(TaskMinimumDeadlineTicks, (long)TaskDeadlineTicks - (long)Math.Max(0, failures) * TaskFailurePenaltyTicks);
    }
    public enum ObjectiveKind : byte { Repair, Clean, Switch }
    public enum TaskAssignmentStatus : byte { Active, Completed, Missed, WaitingForRoute }
    public sealed class ObjectiveDefinition
    {
        public string ObjectiveId { get; }
        public ObjectiveKind Kind { get; }
        public string DisplayKey { get; }
        public string ActionKey { get; }
        public Float3 Position { get; }
        public Float3 ApproachPoint { get; }
        public float UseRadius { get; }
        public uint WorkTicks { get; }
        public string RouteRegionId { get; }
        public uint RouteBudgetTicks { get; }
        public ObjectiveDefinition(string objectiveId, ObjectiveKind kind, string displayKey, string actionKey, Float3 position, Float3 approachPoint, float useRadius, uint workTicks, string routeRegionId, uint routeBudgetTicks = 150)
        {
            if (!ValidId(objectiveId) || !Enum.IsDefined(typeof(ObjectiveKind), kind) || !ValidId(displayKey) || !ValidId(actionKey) || !ValidId(routeRegionId) || !position.IsFinite || position.Length > 10000 || !approachPoint.IsFinite || approachPoint.Length > 10000 || !MathEx.Finite(useRadius) || useRadius < .1f || useRadius > 3 || workTicks < 1 || workTicks > 54000 || routeBudgetTicks > 54000 || (position - approachPoint).Length > useRadius) throw new ArgumentException("Invalid authored objective.");
            ObjectiveId = objectiveId; Kind = kind; DisplayKey = displayKey; ActionKey = actionKey; Position = position; ApproachPoint = approachPoint; UseRadius = useRadius; WorkTicks = workTicks; RouteRegionId = routeRegionId; RouteBudgetTicks = routeBudgetTicks;
        }
        public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 96 && id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
        internal static IReadOnlyList<ObjectiveDefinition> ValidateCatalog(IReadOnlyList<ObjectiveDefinition> definitions, ModeRuleProfile rules, bool required)
        {
            var copy = GameplayRoundConfig.Copy(definitions);
            if (copy.Length > 128 || (required && copy.Length == 0)) throw new ArgumentException("Authored task catalog required (maximum 128).");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in copy)
                if (d == null || !ids.Add(d.ObjectiveId) || d.WorkTicks + d.RouteBudgetTicks > rules.TaskMinimumDeadlineTicks) throw new ArgumentException("Duplicate or unviable objective.");
            return Array.AsReadOnly(copy.OrderBy(d => d.ObjectiveId, StringComparer.Ordinal).ToArray());
        }
        public static string CatalogHash(IReadOnlyList<ObjectiveDefinition> definitions)
        {
            var text = new StringBuilder("objectives-v1");
            foreach (var d in definitions.OrderBy(d => d.ObjectiveId, StringComparer.Ordinal))
            {
                text.Append('|').Append(d.ObjectiveId).Append('|').Append((int)d.Kind).Append('|').Append(d.DisplayKey).Append('|').Append(d.ActionKey).Append('|').Append(d.RouteRegionId).Append('|').Append(d.WorkTicks).Append('|').Append(d.RouteBudgetTicks);
                foreach (float n in new[] { d.Position.X, d.Position.Y, d.Position.Z, d.ApproachPoint.X, d.ApproachPoint.Y, d.ApproachPoint.Z, d.UseRadius }) text.Append('|').Append(n.ToString("R", CultureInfo.InvariantCulture));
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
    }
    public sealed class TaskAssignment
    {
        public string ObjectiveId { get; }
        public uint IssuedTick { get; }
        public uint DeadlineTick { get; }
        public uint WorkTicks { get; }
        public uint ProgressTicks { get; }
        public TaskAssignmentStatus Status { get; }
        public int PersonalFailures { get; }
        public TaskAssignment(string objectiveId, uint issuedTick, uint deadlineTick, uint workTicks, uint progressTicks, TaskAssignmentStatus status, int personalFailures)
        {
            if (!ObjectiveDefinition.ValidId(objectiveId) || deadlineTick <= issuedTick || deadlineTick > 54000 || workTicks == 0 || workTicks > 54000 || progressTicks > workTicks || !Enum.IsDefined(typeof(TaskAssignmentStatus), status) || personalFailures < 0 || personalFailures > 1800) throw new ArgumentException("Invalid private assignment.");
            ObjectiveId = objectiveId; IssuedTick = issuedTick; DeadlineTick = deadlineTick; WorkTicks = workTicks; ProgressTicks = progressTicks; Status = status; PersonalFailures = personalFailures;
        }
    }
    // Optional capability. Tasks refuses to start without this authored-map validation.
    // ValidateObjective must check known region, static geometry, spawn/inter-objective routes and RouteBudgetTicks.
    // Dynamic closure is reported separately and cannot lower the collective goal.
    public interface IGameplayModeWorld
    {
        bool ValidateObjective(in SpawnActor human, ObjectiveDefinition objective);
        bool IsObjectiveAvailable(uint actorId, ObjectiveDefinition objective);
        bool CanWorkObjective(uint actorId, ObjectiveDefinition objective, Float3 position, Float3 aim);
        bool TryMosquitoRespawn(uint actorId, out Float3 position);
    }
    // Optional selection-only budget. Moving away after assignment must not turn
    // distance into an environmental obstruction that extends the task deadline.
    public interface IGameplayTaskSelectionWorld
    {
        bool CanAssignObjective(uint actorId, ObjectiveDefinition objective);
    }
    internal sealed class TaskRules
    {
        private sealed class Personal
        {
            internal uint Slot, Issued, Deadline, Progress, PersonalDeadline, GraceUsed;
            internal ulong DecayRemainder;
            internal int Failures;
            internal ObjectiveDefinition Objective;
            internal TaskAssignmentStatus Status;
            internal bool Finished;
        }
        private readonly GameplayRoundConfig config;
        private readonly IGameplayModeWorld world;
        private readonly Dictionary<uint, Personal> people = new Dictionary<uint, Personal>();
        private readonly int slotsPerPerson;
        public int Completed { get; private set; }
        public int Opportunities { get; private set; }
        public int Goal { get; private set; }
        public TaskRules(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster, IGameplayModeWorld world)
        {
            this.config = config; this.world = world;
            slotsPerPerson = 1 + (int)((config.RoundDurationTicks - config.ModeRules.TaskDeadlineTicks) / config.ModeRules.TaskCadenceTicks);
            foreach (var human in roster.Where(a => a.Role == PlayerRole.Human).OrderBy(a => a.ActorId)) people.Add(human.ActorId, new Personal { PersonalDeadline = config.ModeRules.TaskDeadlineTicks });
            Opportunities = slotsPerPerson * people.Count;
            Goal = GoalFor(Opportunities);
        }
        private int GoalFor(int opportunities) => config.ConfiguredTasksGoal == 0 ? (2 * opportunities + 2) / 3 : Math.Min(config.ConfiguredTasksGoal, opportunities);
        // A departed human's unfinished and future opportunities stop counting toward the collective goal.
        // Completed work and slots it already finished (completed or missed) remain counted.
        public void RemovePerson(uint actorId, uint tick)
        {
            if (!people.TryGetValue(actorId, out var p)) return;
            people.Remove(actorId);
            // The last human leaving ends the round (OpponentLeft/Aborted). Keep the last reachable goal:
            // a Tasks snapshot with no opportunities or a zero goal is invalid and would stop the end from publishing.
            if (people.Count == 0) return;
            int current = (int)Math.Min(int.MaxValue, tick / config.ModeRules.TaskCadenceTicks);
            int finished = Math.Min(slotsPerPerson, current);
            if (current < slotsPerPerson && p.Slot == current && p.Finished) finished++;
            // Each remaining human keeps at least one slot, so both stay >= 1 and >= Completed.
            Opportunities = Math.Max(Math.Max(1, Completed), Opportunities - (slotsPerPerson - finished));
            Goal = Math.Max(1, GoalFor(Opportunities));
        }
        public TaskAssignment Capture(uint actorId)
        {
            if (!people.TryGetValue(actorId, out var p) || p.Objective == null) return null;
            return new TaskAssignment(p.Objective.ObjectiveId, p.Issued, p.Deadline, p.Objective.WorkTicks, p.Progress, p.Status, p.Failures);
        }
        public bool Update(uint actorId, uint tick, bool canWork, Float3 position, Float3 aim)
        {
            if (!people.TryGetValue(actorId, out var p)) return false;
            uint cadence = config.ModeRules.TaskCadenceTicks;
            uint slot = tick / cadence;
            // Cadence is anchored to the round, never to completion or another human's failure.
            if (slot != p.Slot)
            {
                if (!p.Finished && p.Objective != null && p.Status == TaskAssignmentStatus.Active && tick >= p.Deadline) Miss(p);
                p.Slot = slot; p.Objective = null; p.Progress = 0; p.Finished = false; p.GraceUsed = 0; p.DecayRemainder = 0;
            }
            uint start = slot * cadence;
            if (start + config.ModeRules.TaskDeadlineTicks > config.RoundDurationTicks || p.Finished) return false;
            uint end = Math.Min(start + cadence, config.RoundDurationTicks);
            if (p.Objective == null)
            {
                uint duration = p.PersonalDeadline;
                if (tick + duration > end) return false;
                int offset = (int)(((ulong)actorId + config.RoundId + slot) % (ulong)config.Objectives.Count);
                for (int i = 0; i < config.Objectives.Count; i++)
                {
                    var objective = config.Objectives[(offset + i) % config.Objectives.Count];
                    if (!world.IsObjectiveAvailable(actorId, objective)) continue;
                    if (world is IGameplayTaskSelectionWorld selection && !selection.CanAssignObjective(actorId, objective)) continue;
                    p.Objective = objective; p.Issued = tick; p.Deadline = tick + duration; p.Status = TaskAssignmentStatus.Active; break;
                }
                if (p.Objective == null) return false;
            }
            if (!world.IsObjectiveAvailable(actorId, p.Objective))
            {
                p.Status = TaskAssignmentStatus.WaitingForRoute;
                if (p.Deadline < end) p.Deadline++;
                return false;
            }
            // A route reopened too late remains non-penalizing for this opportunity.
            if (p.Status == TaskAssignmentStatus.WaitingForRoute && tick >= p.Deadline) { p.Finished = true; return false; }
            p.Status = TaskAssignmentStatus.Active;
            if (tick >= p.Deadline) { Miss(p); return false; }
            if (tick == p.Issued) return false;
            if (!canWork || (position - p.Objective.Position).Length > p.Objective.UseRadius || !world.CanWorkObjective(actorId, p.Objective, position, aim))
            { Interrupt(p); return false; }
            p.Progress++;
            if (p.Progress < p.Objective.WorkTicks) return false;
            p.Status = TaskAssignmentStatus.Completed; p.Finished = true;
            p.PersonalDeadline = (uint)Math.Min(config.ModeRules.TaskDeadlineTicks, (ulong)p.PersonalDeadline + config.ModeRules.TaskSuccessRecoveryTicks);
            Completed++; return true;
        }
        private void Miss(Personal p)
        {
            p.Failures++;
            p.PersonalDeadline = (uint)Math.Max(config.ModeRules.TaskMinimumDeadlineTicks,
                (long)p.PersonalDeadline - config.ModeRules.TaskFailurePenaltyTicks);
            p.Status = TaskAssignmentStatus.Missed; p.Finished = true;
        }
        private void Interrupt(Personal p)
        {
            if (p.Progress == 0) return;
            // One cumulative grace budget per assignment. Briefly resuming work
            // neither replenishes grace nor discards fractional decay already owed.
            if (p.GraceUsed < config.ModeRules.TaskInterruptionGraceTicks) { p.GraceUsed++; return; }
            const uint denominator = 30 * 10000; // fixed 30Hz; basis points of total work per second
            p.DecayRemainder += (ulong)p.Objective.WorkTicks * config.ModeRules.TaskDecayBasisPointsPerSecond;
            ulong lost = p.DecayRemainder / denominator;
            p.DecayRemainder %= denominator;
            if (lost >= p.Progress) { p.Progress = 0; p.DecayRemainder = 0; }
            else p.Progress -= (uint)lost;
        }
    }
}
