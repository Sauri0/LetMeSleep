using System;
using System.Collections.Generic;
using System.Globalization;
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
                case "task.camp.barrel": return "LIMPIÁ EL BARRIL DEL FOGÓN";
                case "task.camp.cooler": return "REPARÁ LA CONSERVADORA";
                case "task.camp.cook_table": return "LIMPIÁ LA MESA DE COCINA";
                case "task.camp.crate_one": return "LIMPIÁ LA CAJA DE PROVISIONES 1";
                case "task.camp.crate_two": return "LIMPIÁ LA CAJA DE PROVISIONES 2";
                case "task.camp.shelter_table": return "LIMPIÁ LA MESA DEL REFUGIO";
                case "task.camp.east_field_lantern": return "ACTIVÁ LA LINTERNA DEL CAMPO ESTE";
                case "task.camp.north_bridge_lantern": return "ACTIVÁ LA LINTERNA DEL ACCESO NORTE";
                case "task.camp.washroom_toilet": return "LIMPIÁ EL INODORO DEL BAÑO";
                case "task.camp.washroom_bucket": return "LIMPIÁ EL BALDE DEL BAÑO";
                case "task.yacht.main_deck": return "LIMPIÁ LA CUBIERTA PRINCIPAL";
                case "task.yacht.aft_dining_table": return "LIMPIÁ LA MESA DE POPA";
                case "task.yacht.port_cabin_lamp": return "ACTIVÁ LA LÁMPARA DEL CAMAROTE DE BABOR";
                case "task.yacht.starboard_cabin_lamp": return "ACTIVÁ LA LÁMPARA DEL CAMAROTE DE ESTRIBOR";
                case "task.yacht.helm_console": return "REPARÁ LA CONSOLA DEL TIMÓN";
                case "task.yacht.galley_sink": return "LIMPIÁ LA PILETA DE LA COCINA";
                case "task.yacht.aft_dining_chair": return "LIMPIÁ LA SILLA DE LA MESA DE POPA";
                case "task.yacht.flybridge_drinks_table": return "LIMPIÁ LA MESA DE BEBIDAS DEL FLYBRIDGE";
                case "task.yacht.foredeck_table": return "LIMPIÁ LA MESA BAJA DE PROA";
                case "task.yacht.salon_coffee_table": return "LIMPIÁ LA MESA RATONA DEL SALÓN";
                case "task.yacht.bathroom_toilet": return "LIMPIÁ EL INODORO";
                case "task.port.lighthouse_floor": return "LIMPIÁ EL SUELO DEL FARO";
                case "task.port.plaza_barrel": return "LIMPIÁ EL BARRIL DE LA PLAZA";
                case "task.port.plaza_bench": return "LIMPIÁ EL BANCO DE LA PLAZA";
                case "task.port.cottage_two_writing_desk": return "LIMPIÁ EL ESCRITORIO DE LA CABAÑA 2";
                case "task.port.cottage_three_writing_desk": return "LIMPIÁ EL ESCRITORIO DE LA CABAÑA 3";
                case "task.port.plaza_bench_three": return "LIMPIÁ EL BANCO 3 DE LA PLAZA";
                case "task.port.cottage_one_bedside_table": return "LIMPIÁ LA MESA DE LUZ DE LA CABAÑA 1";
                case "task.port.cottage_two_bedside_table": return "LIMPIÁ LA MESA DE LUZ DE LA CABAÑA 2";
                case "task.port.cottage_three_bedside_table": return "LIMPIÁ LA MESA DE LUZ DE LA CABAÑA 3";
                case "task.port.cottage_writing_desk": return "LIMPIÁ EL ESCRITORIO DE LA CABAÑA 1";
                case "task.port.plaza_bench_two": return "LIMPIÁ EL BANCO 2 DE LA PLAZA";
                default: return "TU TAREA";
            }
        }
        public static string ActionName(string key)
        {
            switch (key)
            {
                case "task.action.hold_clean": return "Mantené E junto al objeto para limpiar";
                case "task.action.hold_switch": return "Mantené E junto al objeto para activar";
                case "task.action.hold_repair": return "Mantené E junto al objeto para reparar";
                default: return "Mantené E junto al objeto";
            }
        }
        public static string EquipmentName(GameSessionState state, uint pickupId)
        {
            if (pickupId == 0) return "VACÍO";
            var tool = state?.ToolPickups.FirstOrDefault(item => item.PickupId == pickupId).ToolId;
            switch (tool)
            {
                case GameplayTools.Flyswatter: return "MATAMOSCAS";
                case GameplayTools.Slipper: return "PANTUFLA";
                case GameplayTools.ElectricRacket: return "RAQUETA ELÉCTRICA";
                case GameplayTools.Aerosol: return "AEROSOL";
                default: return "OBJETO";
            }
        }
        public static EquipmentSlotUiState EquipmentSlot(GameSessionState state, uint pickupId)
        {
            if (pickupId == 0) return new EquipmentSlotUiState("VACÍO", "", AlfaUiIconKind.None);
            var item = state == null ? default : state.ToolPickups.FirstOrDefault(candidate => candidate.PickupId == pickupId);
            string tool = item.PickupId == pickupId ? item.ToolId : null;
            AlfaUiIconKind icon = tool == GameplayTools.Flyswatter ? AlfaUiIconKind.Flyswatter :
                tool == GameplayTools.Slipper ? AlfaUiIconKind.Slipper :
                tool == GameplayTools.ElectricRacket ? AlfaUiIconKind.ElectricRacket :
                tool == GameplayTools.Aerosol ? AlfaUiIconKind.Aerosol : AlfaUiIconKind.None;
            string resource = tool == GameplayTools.ElectricRacket ? item.ResourceUnits + " CARGAS" :
                tool == GameplayTools.Aerosol ? (item.ResourceUnits / (float)HumanEquipmentProfile.TickRate).ToString("0.0", CultureInfo.GetCultureInfo("es-AR")) + " s" :
                tool == GameplayTools.Flyswatter || tool == GameplayTools.Slipper ? "REUTILIZABLE" : "";
            return new EquipmentSlotUiState(EquipmentName(state, pickupId), resource, icon);
        }
        public static EquipmentSlotUiState[] EquipmentSlots(GameSessionState state, ActorPrivateState personal)
        {
            if (personal == null) return new[] { EquipmentSlot(state, 0), EquipmentSlot(state, 0), EquipmentSlot(state, 0) };
            return new[]
            {
                EquipmentSlot(state, personal.Inventory.Slot0),
                EquipmentSlot(state, personal.Inventory.Slot1),
                EquipmentSlot(state, personal.Inventory.Slot2)
            };
        }
        public static bool InventoryFullWithoutSelection(ActorPrivateState personal) => personal != null &&
            personal.Inventory.SelectedSlot < 0 && personal.Inventory.Slot0 != 0 && personal.Inventory.Slot1 != 0 && personal.Inventory.Slot2 != 0;
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
