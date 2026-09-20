using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.UI;

namespace LetMeSleep.Bootstrap
{
    public static class ModeHudText
    {
        public static ActorPrivateState LocalPrivate(GameSessionState state, uint localId, ActorPrivateState personal)
            => state != null && personal != null && personal.ActorId == localId && personal.SessionEpoch == state.SessionEpoch && personal.RoundId == state.RoundId ? personal : null;

        public static string PrivateTask(GameSessionState state, uint localId, ActorPrivateState personal, IReadOnlyList<ObjectiveDefinition> objectives, out float progress)
        {
            progress = 0;
            if (state == null || state.ModeId != GameModes.Tasks || !state.Actors.Any(a => a.ActorId == localId && a.Role == PlayerRole.Human && !a.Eliminated)) return "";
            var task = LocalPrivate(state, localId, personal)?.TaskAssignment;
            if (task == null) return "TU TAREA\nEsperando una asignación disponible.";
            progress = task.WorkTicks == 0 ? 0 : (float)task.ProgressTicks / task.WorkTicks;
            if (task.Status == TaskAssignmentStatus.Completed) return "TAREA COMPLETADA\nEsperá la próxima asignación.\nFallos personales: " + task.PersonalFailures;
            if (task.Status == TaskAssignmentStatus.Missed) return "PLAZO VENCIDO\nLa próxima tarea tendrá menos tiempo.\nFallos personales: " + task.PersonalFailures;
            var objective = objectives?.FirstOrDefault(o => o.ObjectiveId == task.ObjectiveId);
            if (objective == null) return "TU TAREA\nEsperando información del objetivo.";
            string title = ObjectiveName(objective.DisplayKey);
            if (task.Status == TaskAssignmentStatus.WaitingForRoute) return title + "\nRuta bloqueada: tarea en espera.\nEl bloqueo no suma un fallo.";
            float remaining = Math.Max(0, (long)task.DeadlineTick - state.HostTick) / 30f;
            return title + "\n" + ActionName(objective.ActionKey) + "\n" + AlfaModeText.Clock(remaining) + " · " + Math.Round(progress * 100) + "% · Fallos: " + task.PersonalFailures;
        }
        public static string ObjectiveName(string key)
        {
            switch (key)
            {
                case "task.isla.cabin_access": return "LIMPIÁ EL ACCESO DE LA CABAÑA";
                case "task.casa.bathroom_tile": return "LIMPIÁ LOS AZULEJOS DEL BAÑO";
                case "task.casa.ground_basin": return "LIMPIÁ EL LAVAMANOS DE ABAJO";
                case "task.casa.ground_toilet": return "ACTIVÁ EL BOTÓN DEL INODORO DE ABAJO";
                case "task.casa.kitchen_sink": return "LIMPIÁ LA BACHA DE LA COCINA";
                case "task.casa.oven": return "REPARÁ LA MANIJA DEL HORNO";
                case "task.casa.fridge": return "REPARÁ LA MANIJA DE LA HELADERA";
                case "task.casa.coffee_table": return "LIMPIÁ LA MESA RATONA";
                case "task.casa.bedroom_one_lamp": return "ACTIVÁ LA LÁMPARA DEL DORMITORIO 1";
                case "task.casa.bedroom_two_lamp": return "ACTIVÁ LA LÁMPARA DEL DORMITORIO 2";
                case "task.casa.upper_basin": return "LIMPIÁ EL LAVAMANOS DE ARRIBA";
                case "task.casa.upper_toilet": return "ACTIVÁ EL BOTÓN DEL INODORO DE ARRIBA";
                case "task.camp.washroom": return "LIMPIÁ EL LAVADERO";
                case "task.yacht.main_deck": return "LIMPIÁ LA CUBIERTA PRINCIPAL";
                case "task.port.lighthouse_floor": return "LIMPIÁ EL SUELO DEL FARO";
                default: return "TU TAREA";
            }
        }
        public static string ActionName(string key)
        {
            switch (key)
            {
                case "task.action.hold_clean": return "Mantené R junto al objeto para limpiar";
                case "task.action.hold_switch": return "Mantené R junto al objeto para activar";
                case "task.action.hold_repair": return "Mantené R junto al objeto para reparar";
                default: return "Mantené R junto al objeto";
            }
        }
        public static string ResultReason(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.AllOpponentsEliminated: return "No queda ningún mosquito en juego.";
                case RoundEndReason.TasksMet: return "Los humanos alcanzaron la meta de tareas.";
                case RoundEndReason.TasksMissed: return "Se terminó el tiempo sin alcanzar la meta de tareas.";
                case RoundEndReason.BloodGoal: return "Los mosquitos reunieron la sangre necesaria.";
                case RoundEndReason.TimeExpired: return "Se terminó el tiempo de la ronda.";
                case RoundEndReason.OpponentLeft: return "El equipo contrario salió de la partida.";
                case RoundEndReason.Aborted: return "La ronda fue interrumpida.";
                default: return "";
            }
        }
        public static uint SpectatorTarget(GameSessionState state, uint localId, uint current, bool next)
        {
            var self = state?.Actors.FirstOrDefault(a => a.ActorId == localId);
            if (self == null || !self.Eliminated) return 0;
            var teammates = state.Actors.Where(a => a.ActorId != localId && a.Role == self.Role && !a.Eliminated).OrderBy(a => a.ActorId).ToArray();
            if (teammates.Length == 0) return 0;
            int index = Array.FindIndex(teammates, a => a.ActorId == current);
            return teammates[index < 0 ? 0 : next ? (index + 1) % teammates.Length : index].ActorId;
        }
    }
}
