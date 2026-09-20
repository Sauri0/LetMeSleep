# Plan actualizado desde respuestas — v0.2.0

Fuente: ORIGINAL.json más aclaraciones directas del usuario en esta conversación.
DECISIONES.json y DECISIONES.md conservan las150 entradas con procedencia y
distinción entre elección del usuario, delegación CEO e interpretación pendiente.
Este plan especifica el alcance vigente; no declara implementadas las elecciones.

## Reglas cerradas con mayor impacto

- Tres modos con180/150/240s de base; Sangre usa12+6×humanos y tasa de picadura
  compartida por víctima. Composiciones sugeridas editables, roles sorteados.
- Tareas: al menos10 puntos por mapa (mínimo50), cadencia40s/plazo30s/mínimo15s;
  fallar reduce5s y completar recupera3s. Meta ceil(2/3), rescate2s, protección1,5s.
  Progreso conserva una gracia y luego decae; cuantificar ese balance con pruebas.
- Estamina100, carrera16/s, regeneración12/s caminando y28/s quieto/agachado;
  salto10 y lanzamiento8–15. Defensa básica gratuita.
- Mosquito:3,8m/s, aceleración13 y freno28m/s². Posado explícito por tecla;
  cámara sigue su horizonte, permite mirar libre estando quieto y vuelve al
  horizonte del mosquito al moverse. Comparar transiciones reales suelo/pared/techo.
- Tres slots más manos; rueda y números, E para interactuar. Sólo pantufla se
  lanza con carga; no convertir diario o matamoscas en arrojables. Carga0,9s,
  matamoscas35% más alcance/25% más lento, raqueta5cargas y aerosol4s consumible.
- Bots sólo en entrenamiento. Memoria3s sin datos ocultos. Ninguna IA controla
  jugadores desconectados online: reserva30s y después aplicar regla por modo.
- Tres bases humanas compatibles; catálogo cuantificado en DECISIONES.md.
  Apariencia fiel a originales PER-06/07/08; no sustituirlos por bocetos A/B/C.
- Voz: PTT, base clara4m/corte12m; voz mosquito corta8m ante humano y16m ante
  mosquito. Oclusión−6dB y filtro; música/ambiente bajan4dB con voz audible.
  Eliminados separados de activos; mute de persona dura hasta salir de sala.
- Música de caja musical y jazz ligero con entradas puntuales; ambiente por
  capas selectivas y resultado con motivo3s. Español completo, objetivo1080p60.

## Orden de ejecución y aceptación

1. **Cerrar correcciones físicas ya en curso.** Importar nuevos scripts mediante
   Unity, comprobar destino corporal libre al adquirir/aproximar, repetir pruebas
   dirigidas y matriz histórica sin borrar21fallos originales. Distinguir fallos
   de runtime, pruebas mal planteadas y cobertura faltante. Preservar el WIP.
2. **Aplicar reglas y catálogos jugables.** Comparar valores elegidos con dominio,
   UI y protocolo; actualizar juntos. Los candidatos de un objetivo por mapa son
   diagnósticos, nunca catálogo final. Crear≥10 objetivos verificables por mapa,
   rutas desde todos los spawns y presupuesto compatible con plazo/trabajo.
3. **Inventario, estamina y bots de entrenamiento.** Verificar recoger lleno,
   depósito bloqueado, cancelación sin consumo, recursos finitos y comportamiento
   sin información oculta. Eliminar sustitución online por IA si existe.
4. **Personajes y personalización.** Comparar fuentes existentes con las láminas
   originales. Matriz de cada pieza/base/animación; visor, autoguardado privado,
   deshacer y publicación explícita. Conservar cantidades elegidas y registrar
   por separado cantidad mosquito/categorías aún abiertas. Fuentes nuevas en N:.
5. **Presentación, cámara y audio.** Implementar horizonte del mosquito conforme
   a la respuesta literal, probar movimiento/quietud/cambio de soporte y paredes.
   Iluminación Casa/Camp cálida interior y fría exterior; Puerto lavanda/cálida;
   Isla diurna. Evidencia de clip y comparación real, no sólo capturas aisladas.
6. **Integración voz/sesiones.** Conectar núcleo y UI, identidad de sala, roster,
   PTT, dispositivos, mute, foco, pausa y transiciones. Reserva30s sin IA; salas
   nuevas al perder host. Completar reglas de espera/eliminados y vencimiento
   por modo antes de aceptar sus ciclos.
7. **Validación de candidato privado.** Cinco mapas/tres modos, UI720/1080,
   rendimiento en equipo identificado, comparación Godot, pruebas auditivas
   USB+integrado en dos PCs. Dos identidades, dos redes y al menos dos rondas
   completas antes de cualquier release pública, conforme O24.
8. **Publicar y verificar descarga.** BuildWindows, BUILD.json, checksum,
   launcher, tag/release y guía. Descargar artefacto publicado y verificarlo.
   Capacidad se certifica sólo hasta el último escalón real aprobado2→4→8→16.

## Decisiones delegadas al CEO

Quedaron resueltas16: las13 del archivo y J04/J27/D05 delegadas después.
Ver tabla con elección y razón en DECISIONES.md. No se pide una aprobación nueva
para cada una. Sí se distinguen de las elecciones explícitas del usuario.

## Detalles todavía abiertos

Las150 preguntas tienen respuesta, pero el cuestionario no cierra cada parámetro:
cantidad de cuerpos mosquito, diseños concretos de accesorios, alcance exacto de
categorías adicionales de la lámina, intensidad facial por acción, gracia/decaimiento
de tareas, canal de espera y modalidad del canal eliminado, efecto por modo tras
30s sin reconexión, excepción PTT con mando y hardware de referencia. Conservarlos
visibles; no presentar esos detalles como aprobados ni frenar frentes independientes.

## Coordinación y evidencia

CEO+máximo3subagentes, propiedad explícita, modelos/esfuerzos según TEAM.md,
una sola ventana Unity/Blender/render y Assets estables durante importación.
Cambios críticos revisados por otro agente. Registrar resultados reales, retrabajo
y evidencia; costo/tokens desconocidos siguen null. Los chats históricos y el
contenido de imágenes no activan trabajadores ni autorizan funciones nuevas.
