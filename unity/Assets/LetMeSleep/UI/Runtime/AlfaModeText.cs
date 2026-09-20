using System;
using LetMeSleep.Core;

namespace LetMeSleep.UI
{
    // Pure player-facing mode copy, shared by lobby, training, HUD and results.
    public static class AlfaModeText
    {
        public static readonly string[] ModeIds = { GameModes.Blood, GameModes.Survival, GameModes.Tasks };
        public static string Name(string modeId) => modeId == GameModes.Survival ? "SUPERVIVENCIA" : modeId == GameModes.Tasks ? "TAREAS" : "SANGRE";
        public static string Instructions(string modeId, bool mosquito)
        {
            if (modeId == GameModes.Survival) return mosquito ? "Una vida. Evitá los golpes y sobreviví hasta que termine el tiempo." : "Eliminá a todos los mosquitos antes de que termine el tiempo.";
            if (modeId == GameModes.Tasks) return mosquito ? "Tenés tres vidas. Interrumpí a los humanos; mantené R cerca de un aliado caído para ayudarlo." : "Cumplí tus tareas privadas. Mantené R junto al objeto; el equipo gana si alcanza la meta al terminar el tiempo.";
            return mosquito ? "Picá por contacto y reuní la sangre compartida antes del tiempo." : "Defendé tu descanso hasta que termine el tiempo. Mirá al mosquito y golpeá con clic.";
        }
        public static string Score(string modeId, float blood, float bloodGoal, int completed, int goal, int alive)
        {
            if (modeId == GameModes.Tasks) return $"TAREAS  {completed} / {goal}";
            if (modeId == GameModes.Survival) return $"MOSQUITOS VIVOS  {alive}";
            return $"SANGRE  {blood:0.#} / {bloodGoal:0.#}";
        }
        public static string ResultScore(string modeId, float blood, float bloodGoal, int completed, int goal, int alive)
        {
            if (modeId == GameModes.Tasks) return $"Tareas completadas: {completed} / {goal}";
            if (modeId == GameModes.Survival) return $"Mosquitos sobrevivientes: {alive}";
            return $"Sangre compartida: {blood:0.#} / {bloodGoal:0.#}";
        }
        public static string Clock(float seconds)
        {
            int total = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
