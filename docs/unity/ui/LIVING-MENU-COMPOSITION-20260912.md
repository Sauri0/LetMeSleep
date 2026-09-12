# Menú vivo: composición y contrato UI

Requisito: `N:/LetMeSleep/Repository/docs/unity/art-review/MAIN-MENU-LIVING-SCENE.md`. Parte de recuperación alfa. Se revisó menú native4/1080 como antecedente, no como evidencia del montaje nuevo. Candidatos de zonas/FOV aceptados por Director para validar en motor; no aprobados visualmente.

## Composición de pantalla

Coordenadas normalizadas con origen **arriba a izquierda**. Para Camera.WorldToViewportPoint convertir Y con `1-y`. Mismos encuadres relativos en 1920×1080 y 1280×720, referencia Canvas 1920×1080.

| Zona | Rectángulo normalizado x / y | 1080p aproximado | 720p aproximado |
|---|---|---|---|
| Rail UI existente | x .025–.2854 / y .0926–.9074 | x48–548, y100–980 | x32–365, y67–653 |
| Zona calma, incluye margen del rail | x0–.31 / y0–1 | x0–595 | x0–397 |
| Humano sentado y asiento | x.56–.86 / y.25–.91 | x1075–1651, y270–983 | x717–1101, y180–655 |
| Corredor inicial de vuelo | x.36–.89 / y.13–.48 | x691–1709, y140–518 | x461–1139, y94–346 |
| Envolvente total de actuación | x.34–.94 / y.10–.92 | x653–1805, y108–994 | x435–1203, y72–662 |

Puntos candidatos: cara humana (.70,.39), mano/mango (.61,.53), paso cercano mosquito (.61,.46). Las zonas describen siluetas completas: sumar radio de alas, probóscide y punta de herramienta, no comprobar solo pivotes. El pase cercano puede salir del corredor inicial hacia la mano, siempre dentro de la envolvente, sin penetrar el humano ni pasar detrás del panel.

Humano sentado en rincón derecho a tres cuartos, rostro/agarre legibles, pelvis sobre asiento real y dos pantuflas sobre suelo visible. Sillón, mesa/lámpara y fondo doméstico forman planos. El sillón puede exceder la caja humana dentro de la envolvente; no ocultar apoyos para conseguir encuadre. Mosquito subordinado al humano; decidir escala aparente por composición y distancia, no heredar decorativo x4 sin revisión.

Cámara fija, perspectiva tres cuartos, FOV vertical inicial candidato 40–45 grados. Su altura y look-at se resuelven desde los anchors de asiento/pelvis/cabeza de Elementos y Humanos; no inventar un asiento numérico ni mover cámara desde UI. Director integra Bootstrap y acuerda anchors con Elementos. Reducir bandas sobrantes de techo/suelo respecto a native4 conservando apoyos y movimiento completo. Sin balanceo de cámara, zoom automático o seguimiento que desplace el fondo detrás de los botones.

Elementos comunicó como montaje en preparación: sofá derecho existente root(3.3,0,5.35), sin moverlo; humano yaw180 hacia -Z, contacto asiento top.575 cerca worldZ5.005, pies worldZ4.60–4.65 y X3.3±.16. Herramienta producción largo.535m, grip→cabeza.365m. Ruta mosquito worldX2.15–4.5,Y1.4–2,Z4.15–5.2. Root exacto/alcance de piernas aún coordinados con Humanos. Estos datos sirven para ajustar cámara por Director/Presentation; no sustituyen anchors definitivos ni garantizan las zonas de pantalla.

## Cambios propios de UI

- Menú principal conserva rail 500×880, botones y callbacks: Jugar online, Entrenamiento, Personalizar, Ajustes, Salir. Salir mantiene altura72; primario88. No se compacta el texto para agrandar escena.
- Se retira el velo global18% únicamente del menú principal. Contraste se concentra en rail Ink900 alpha.92; escena recibe su iluminación íntegra. No se retocan scrims de otras pantallas.
- Se retira la etiqueta «CASA CON PATIO · NOCHE»: el escenario de menú es la sala independiente. Libera además el margen inferior derecho.
- Personalizador y su cámara/bounds permanecen sin cambios; cierre native7 se conserva.

## Luz coordinada con Presentation

Fuente cálida localizada junto al asiento y relleno frío suave opuesto. Orientaciones deben mostrar cara, dedos, mango y volumen del pijama sin lavar el material; nada de aumentar exposición global para recuperar un único actor. Separar patas del mosquito con fondo/luz durante todo el corredor. Lámpara conserva forma sin blanco quemado, sombras de contacto visibles bajo pelvis/pantuflas/mesa. La zona izquierda queda sin flashes ni luces móviles aunque el rail sea opaco. UI no modifica luces ni materiales del mundo.

## Movimiento reducido: contrato aprobado por Director

`AlfaSettingsDraft.ReduceMenuMotion` bool, default false. MemberwiseClone lo conserva; SameValues lo incluye en dirty-state. `SettingsUiState` agrega propiedad `SupportsReducedMenuMotion` y parámetro **opcional al final** `supportsReducedMenuMotion=false`.

En Ajustes/Video aparece «REDUCIR MOVIMIENTO DEL MENÚ» únicamente si backend anuncia soporte. Sin soporte se oculta la fila completa y no ocupa navegación ni muestra un control vacío. Deshacer reconstruye SettingsUiState conservando capability. El toggle modifica solo draft; Aplicar envía el snapshot mediante el callback existente; Deshacer y salir con cambios siguen el flujo actual. No reutilizar VSync/FPS/volúmenes como sustituto de accesibilidad de movimiento.

Director conserva persistencia/lectura, manejo de fallos y aplicación real desde Bootstrap. Activar capability solo cuando el controlador de escena pueda recibir el valor guardado, también al recrear escena y volver del personalizador. Al guardar, informar estado Saved confirmado; si falla, conservar Saved anterior. Archivos antiguos sin campo conservan default false.

Presentation debe resolver modo reducido en pose sentada estable: quitar recorrido amplio, pasadas rápidas e intentos de espantar; mosquito en posición segura legible con mínimo aleteo, sin dejar rig congelado en mitad del golpe. Cambios sin saltos, cámara siempre fija y navegación activa. No convertirlo en ajuste de locomoción/combate. Propuesta enviada: API SetReducedMotion(bool), nombre final propiedad de Presentation/Director.

MainMenu activa actuación. Al abrir submenús, coordinar reposo seguro sin audio residual; al salir al personalizador/lobby explorable/partida, el dueño de ciclo de vida suspende/oculta decorativos y no restaura actores de otra escena. No cambiar desde UI los roots de modelos: se conserva ScreenChanged y ApplyLiveAppearance del integrador.

## Verificación y pendientes

Compilación externa del conjunto UI contra assemblies Unity6000.3.24f1: cero errores/advertencias. Log `N:/LetMeSleep/Validation/UI-PreviewBounds-20260912/compile-living-menu.log`. El contenido de Video añade una fila44 más espacio10 dentro del panel558, sin cambiar tamaños de otras categorías; verificar layout real720/1080 tras integrar.

Falta evidencia nativa de dos ciclos completos por resolución, apoyos/agarre, recorrido sin recortes/penetraciones, texto estable y Salir entero; controles de movimiento reducido con Aplicar/Deshacer/salida con cambios, fallo de guardado, reinicio y retorno. Guardar colores de ambos y regresar debe actualizar actores reales sin duplicados. Revisar salida/retorno de entrenamiento/lobby/personalizador y ausencia de audio residual. Ningún render propio ni proceso Unity/Blender ejecutado por UI. Compilación y estas zonas propuestas no cierran menú vivo ni calidad global.
