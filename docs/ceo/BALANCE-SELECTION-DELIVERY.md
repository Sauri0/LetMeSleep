# Bases de modos y selección de tareas

J09 define bases Sangre180s, Supervivencia150s y Tareas240s, con variantes
del host dentro de30..1800s. GameModes expone esas bases; Bootstrap/UI tiene
su integración a cargo de modos_red. J10 calcula cuota12+6×humanos a partir
del sorteo final en RoomSession.StartRound; no cambia la duración elegida.

La selección de tareas admite IGameplayTaskSelectionWorld.CanAssignObjective
para filtrar por distancia/presupuesto antes de asignar. El reloj activo
consulta disponibilidad ambiental por separado. Esto evita que alejarse
fuera del presupuesto de selección conceda una pausa o ampliación de plazo.
El mundo Unity y sus rutas son propiedad de continuidad_tecnica.

Validación CPU:41 casos de sala,48 casos de modos/picadura/puertas, sin fallos.
Unity6000.3.24f1: `N:/LetMeSleep/Validation/V020/balance-selection-native-01.xml`,
77 PASS,0 FAIL,0 omitidos. La compilación incluye cambios actuales de UI;
esto no sustituye inspección visual720/1080 ni navegación humana real por mapa.
