using LetMeSleep.Core;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Local, presentation-only facial mood for one actor, derived from its replicated state and events
    /// (no network or authority changes). Mosquito: alert near a human, focused while preparing, happy
    /// while feeding, excited after a bite, surprised when knocked down, dizzy when stunned. Human: sleepy
    /// with a yawn after a long calm idle, angry while striking, happy after a hit, surprised and then
    /// angry when bitten, eyes shut while fainted.
    /// </summary>
    public sealed class GameplayMoodPolicy
    {
        public const double IdleSleepySeconds = 6, YawnIntervalSeconds = 10, YawnSeconds = 3;

        public readonly struct Frame
        {
            public readonly PlayerRole Role;
            public readonly GameplayModel.LifeState Life;
            public readonly float PlanarSpeed, Crouch;
            public readonly bool Grounded, Striking, OpponentNear;
            public Frame(PlayerRole role, GameplayModel.LifeState life, float planarSpeed, bool grounded,
                float crouch, bool striking, bool opponentNear)
            {
                Role = role; Life = life; PlanarSpeed = planarSpeed; Grounded = grounded;
                Crouch = crouch; Striking = striking; OpponentNear = opponentNear;
            }
        }

        public readonly struct Result
        {
            public readonly FacialMood Mood;
            public readonly float Weight;
            /// <summary>True on the single evaluation that starts a yawn (the body clip is the caller's).</summary>
            public readonly bool StartYawn;
            public Result(FacialMood mood, float weight, bool startYawn) { Mood = mood; Weight = weight; StartYawn = startYawn; }
        }

        private double angryUntil = double.MinValue, happyUntil = double.MinValue, surprisedUntil = double.MinValue;
        private double excitedUntil = double.MinValue, sleepyUntil = double.MinValue, yawnUntil = double.MinValue;
        private double calmSince = double.NaN, nextYawn = double.NaN, bitingSince = double.NaN;

        public void Notify(GameplayModel.GameplayEventKind kind, bool asSource, bool asTarget, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            switch (kind)
            {
                case GameplayModel.GameplayEventKind.StrikeStarted when asSource:
                    angryUntil = now + .7; break;
                case GameplayModel.GameplayEventKind.StrikeImpact when asSource:
                    happyUntil = now + 1.1; angryUntil = double.MinValue; break;
                case GameplayModel.GameplayEventKind.StrikeImpact when asTarget:
                    surprisedUntil = now + .6; break;
                case GameplayModel.GameplayEventKind.BiteStarted when asTarget:
                    surprisedUntil = now + .6; angryUntil = now + 2.0; break;
                case GameplayModel.GameplayEventKind.BiteEnded when asSource:
                    excitedUntil = now + 1.2; break;
                case GameplayModel.GameplayEventKind.MosquitoKnockedDown when asSource:
                    surprisedUntil = now + .5; break;
                case GameplayModel.GameplayEventKind.Recovered when asSource:
                    sleepyUntil = now + 1.2; break;
            }
        }

        public Result Evaluate(in Frame frame, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return new Result(FacialMood.Neutral, 0, false);
            return frame.Role == PlayerRole.Mosquito ? Mosquito(frame, now) : Human(frame, now);
        }

        private Result Human(in Frame frame, double now)
        {
            bool calm = frame.Life == GameplayModel.LifeState.Active && frame.Grounded && frame.PlanarSpeed < .1f &&
                !frame.Striking && frame.Crouch < .25f && now >= angryUntil && now >= surprisedUntil;
            bool startYawn = false;
            if (!calm) { calmSince = nextYawn = double.NaN; yawnUntil = double.MinValue; }
            else
            {
                if (double.IsNaN(calmSince)) { calmSince = now; nextYawn = now + IdleSleepySeconds + .5; }
                if (now >= nextYawn) { startYawn = true; yawnUntil = now + YawnSeconds; nextYawn = now + YawnIntervalSeconds; }
            }
            switch (frame.Life)
            {
                case GameplayModel.LifeState.Fainted:
                case GameplayModel.LifeState.Falling:
                    return new Result(FacialMood.Unconscious, 1, false);
                case GameplayModel.LifeState.Recovering:
                    return new Result(FacialMood.Sleepy, 1, false);
            }
            if (now < surprisedUntil) return new Result(FacialMood.Surprised, 1, false);
            if (now < happyUntil) return new Result(FacialMood.Happy, 1, false);
            if (frame.Striking || now < angryUntil) return new Result(FacialMood.Angry, 1, false);
            if (now < sleepyUntil) return new Result(FacialMood.Sleepy, .8f, false);
            if (calm)
            {
                if (now < yawnUntil) return new Result(FacialMood.Yawning, 1, startYawn);
                double idle = now - calmSince;
                if (idle >= IdleSleepySeconds)
                    return new Result(FacialMood.Sleepy, (float)System.Math.Min(1, (idle - IdleSleepySeconds) / 2 + .35), startYawn);
            }
            return new Result(FacialMood.Neutral, 0, false);
        }

        private Result Mosquito(in Frame frame, double now)
        {
            if (frame.Life == GameplayModel.LifeState.Biting)
            {
                if (double.IsNaN(bitingSince)) bitingSince = now;
            }
            else bitingSince = double.NaN;
            switch (frame.Life)
            {
                case GameplayModel.LifeState.Falling:
                    return new Result(FacialMood.Surprised, 1, false);
                case GameplayModel.LifeState.Stunned:
                    return new Result(FacialMood.Dizzy, 1, false);
                case GameplayModel.LifeState.Recovering:
                    return new Result(FacialMood.Sleepy, .8f, false);
                case GameplayModel.LifeState.PreparingBite:
                    return new Result(FacialMood.Focused, 1, false);
                case GameplayModel.LifeState.Biting:
                    return new Result(FacialMood.Happy, (float)System.Math.Min(1, .55 + (now - bitingSince) * .3), false);
            }
            if (now < surprisedUntil) return new Result(FacialMood.Surprised, 1, false);
            if (now < excitedUntil) return new Result(FacialMood.Excited, 1, false);
            if (frame.OpponentNear) return new Result(FacialMood.Alert, 1, false);
            return new Result(FacialMood.Neutral, 0, false);
        }
    }
}
