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
        public string Hash => string.Join(":", Id, MosquitoLives, TaskCadenceTicks, TaskDeadlineTicks, TaskMinimumDeadlineTicks, TaskFailurePenaltyTicks);
        public ModeRuleProfile(string modeId, uint taskCadenceTicks = 1200, uint taskDeadlineTicks = 900, uint taskMinimumDeadlineTicks = 450, uint taskFailurePenaltyTicks = 90)
        {
            if (!GameModes.IsValid(modeId) || taskCadenceTicks < 30 || taskCadenceTicks > 54000 || taskDeadlineTicks < 1 || taskDeadlineTicks > taskCadenceTicks || taskMinimumDeadlineTicks < 1 || taskMinimumDeadlineTicks > taskDeadlineTicks || taskFailurePenaltyTicks < 1 || taskFailurePenaltyTicks > taskDeadlineTicks) throw new ArgumentException("Invalid mode profile.");
            ModeId = modeId; TaskCadenceTicks = taskCadenceTicks; TaskDeadlineTicks = taskDeadlineTicks;
            TaskMinimumDeadlineTicks = taskMinimumDeadlineTicks; TaskFailurePenaltyTicks = taskFailurePenaltyTicks;
        }
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
    internal sealed class TaskRules
    {
        private sealed class Personal
        {
            internal uint Slot, Issued, Deadline, Progress;
            internal int Failures;
            internal ObjectiveDefinition Objective;
            internal TaskAssignmentStatus Status;
            internal bool Finished;
        }
        private readonly GameplayRoundConfig config;
        private readonly IGameplayModeWorld world;
        private readonly Dictionary<uint, Personal> people = new Dictionary<uint, Personal>();
        public int Completed { get; private set; }
        public int Opportunities { get; }
        public int Goal { get; }
        public TaskRules(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster, IGameplayModeWorld world)
        {
            this.config = config; this.world = world;
            int slots = 1 + (int)((config.RoundDurationTicks - config.ModeRules.TaskDeadlineTicks) / config.ModeRules.TaskCadenceTicks);
            foreach (var human in roster.Where(a => a.Role == PlayerRole.Human).OrderBy(a => a.ActorId)) people.Add(human.ActorId, new Personal());
            Opportunities = slots * people.Count;
            Goal = config.ConfiguredTasksGoal == 0 ? (2 * Opportunities + 2) / 3 : Math.Min(config.ConfiguredTasksGoal, Opportunities);
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
                if (!p.Finished && p.Objective != null && p.Status == TaskAssignmentStatus.Active && tick >= p.Deadline) p.Failures++;
                p.Slot = slot; p.Objective = null; p.Progress = 0; p.Finished = false;
            }
            uint start = slot * cadence;
            if (start + config.ModeRules.TaskDeadlineTicks > config.RoundDurationTicks || p.Finished) return false;
            uint end = Math.Min(start + cadence, config.RoundDurationTicks);
            if (p.Objective == null)
            {
                uint duration = config.ModeRules.DeadlineFor(p.Failures);
                if (tick + duration > end) return false;
                int offset = (int)(((ulong)actorId + config.RoundId + slot) % (ulong)config.Objectives.Count);
                for (int i = 0; i < config.Objectives.Count; i++)
                {
                    var objective = config.Objectives[(offset + i) % config.Objectives.Count];
                    if (!world.IsObjectiveAvailable(actorId, objective)) continue;
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
            if (tick >= p.Deadline) { p.Failures++; p.Status = TaskAssignmentStatus.Missed; p.Finished = true; return false; }
            if (tick == p.Issued || !canWork || (position - p.Objective.Position).Length > p.Objective.UseRadius || !world.CanWorkObjective(actorId, p.Objective, position, aim)) return false;
            p.Progress++;
            if (p.Progress < p.Objective.WorkTicks) return false;
            p.Status = TaskAssignmentStatus.Completed; p.Finished = true; Completed++; return true;
        }
    }
}
