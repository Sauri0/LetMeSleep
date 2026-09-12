# Let me sleep 0.9.4/alfa — contrato de interfaz Unity

Estado: especificación implementable del lote alfa autorizado el 12 de septiembre de 2026. No acredita una implementación ni una captura del juego.

## 1. Objetivo y límites

La interfaz alfa permite completar estos recorridos sin salir del juego:

- Crear una sala online y compartir su código.
- Unirse pegando un código online.
- Esperar en un lobby 3D, marcarse listo y, si se es administrador, configurar e iniciar la ronda.
- Jugar Sangre como humano o mosquito con un HUD breve y sin marcas de picadura.
- Entrenar Sangre con bots eligiendo rol.
- Cambiar la personalización básica y guardarla.
- Modificar ajustes esenciales y volver con `Escape` sin perder contexto.

No aparecen Supervivencia, Tareas, clases, crafting, armas de fuego, recompensas, misiones, minimapa, tienda, inventario de seis espacios ni mapas futuros. Las referencias visuales orientan forma y composición; sus textos y sistemas no forman parte del producto.

## 2. Decisión técnica: uGUI

Usar **uGUI con TextMeshPro** para la UI runtime del alfa.

Motivos:

1. El proyecto Unity todavía no contiene una solución UI previa que deba conservarse.
2. El alfa combina HUD superpuesto, paneles modales, navegación con teclado/mando, una escena de lobby 3D y un visor 3D de personaje. uGUI cubre este conjunto con un flujo conocido y estable.
3. `CanvasScaler`, `Selectable`, `EventSystem`, `LayoutGroup` y `ScrollRect` permiten resolver 1280×720 y 1920×1080 sin posiciones rígidas por pantalla.
4. El visor del personalizador puede usar una cámara y `RenderTexture` dentro de un `RawImage`, sin mezclar la jerarquía del personaje con la del menú.
5. La decisión no requiere instalar paquetes ahora. El Director conserva la propiedad de manifiestos, módulos de entrada y contratos de ensamblado.

No usar IMGUI para runtime. UI Toolkit puede reservarse para herramientas de editor; no aporta una ventaja necesaria para este lote.

### Configuración base prevista

| Elemento | Contrato |
|---|---|
| Canvas | `Screen Space - Overlay` para menú/HUD; un solo Canvas raíz persistente si la arquitectura del Director lo permite. |
| CanvasScaler | `Scale With Screen Size`, referencia `1920 × 1080`, `Match Width Or Height = 0.5`. |
| Resolución mínima | `1280 × 720`. Nada interactivo puede quedar fuera del viewport o depender de scroll horizontal. |
| EventSystem | Exactamente uno. Su módulo de entrada lo decide el Director según los paquetes del proyecto. |
| Texto | TextMeshPro. Bangers sólo para logotipo/títulos breves; Atkinson Hyperlegible para cuerpo, controles y estados. Migrar las fuentes históricas únicamente después de validar licencia y recurso en Unity. |
| Layout | Anclas y `VerticalLayoutGroup`/`HorizontalLayoutGroup`. Evitar offsets absolutos salvo HUD anclado y adornos. |
| Raycasts | Activos sólo en controles. Fondos, marcos, iconos y texto decorativo no interceptan puntero. |

## 3. Identidad visual: «noche en casa»

La referencia muestra buena jerarquía mediante paneles oscuros, contornos claros, botones grandes y acentos por estado. La identidad propia de *Let me sleep* sustituye el tono de aventura por una noche doméstica: azul tinta, crema de sábana, ámbar de velador, coral de pijama y menta para confirmaciones.

### Tokens

| Token | Valor | Uso |
|---|---:|---|
| `Ink900` | `#101727` | Fondo profundo, sombra y texto sobre crema. |
| `Night800` | `#17243A` | Fondo principal. |
| `Night700` | `#223552` | Panel sólido. |
| `Night600` | `#2E4668` | Borde secundario y hover oscuro. |
| `Moon200` | `#CBD9EA` | Texto secundario sobre noche. |
| `Sheet100` | `#F5E9D6` | Texto principal y superficies claras. |
| `Lamp400` | `#F2B84B` | Acción primaria y foco. |
| `Pajama500` | `#E66050` | Peligro de mosquito, error y énfasis cálido. |
| `Mint400` | `#67D19A` | Listo, éxito y confirmación. |
| `Sky400` | `#66A9F5` | Acción online, selección neutral y enlaces de navegación. |
| `Disabled` | `#718198` | Texto deshabilitado; acompañar siempre con forma o etiqueta. |
| `Scrim` | `#08101FCC` | Fondo de modal sobre escena 3D. |

Los estados nunca dependen sólo del color. `LISTO` y `NO LISTO` incluyen texto e icono; los errores incluyen frase y símbolo; el foco tiene contorno y desplazamiento visual.

### Tipografía y escala a 1080p

| Estilo | Tamaño | Fuente | Uso |
|---|---:|---|---|
| Logo | 88 px | Bangers | `LET ME SLEEP`, máximo dos líneas. |
| H1 | 48 px | Bangers | Título de pantalla. |
| H2 | 32 px | Atkinson Bold | Título de panel. |
| Botón | 24 px | Atkinson Bold | Verbos breves. |
| Cuerpo | 21 px | Atkinson Regular | Explicación y estado. |
| Etiqueta | 18 px | Atkinson Bold | Campo y HUD. |
| Nota | 16 px | Atkinson Regular | Atajo o ayuda secundaria. No bajar de 16 px a 1080p. |

Texto normal: contraste mínimo 4.5:1. Texto grande: mínimo 3:1. No usar Bangers en párrafos, códigos de sala o ajustes.

### Forma, espaciado y movimiento

- Retícula de 8 px. Espacios habituales: 8, 16, 24, 32 y 48 px.
- Paneles con esquinas recortadas o radio visual de 10 px, borde de 2 px y sombra corta de 6 px. No reproducir el marco ni el logotipo de la referencia.
- Botón principal: alto 68 px a 1080p; secundario 56 px; área interactiva nunca menor de 44 px a 720p.
- Foco: contorno `Lamp400` de 3 px, más un desplazamiento de 2 px. Hover sin foco no puede ocultar el foco de teclado.
- Transición de panel: 140 ms de opacidad y desplazamiento máximo de 12 px. Modal: 180 ms. Ninguna información depende de la animación.
- Confirmación y error pueden usar una pulsación de escala entre 0.98 y 1.02; evitar rebotes largos, flashes y vibración continua.

## 4. Arquitectura de vistas

Jerarquía recomendada; los nombres finales pueden adaptarse al ensamblado del Director, conservando responsabilidades y orden:

```text
UIRoot
├── ScreenCanvas                         sorting 100
│   ├── MainMenuView
│   ├── OnlineRoomView
│   ├── TrainingView
│   ├── CustomizationView
│   └── SettingsView
├── WorldOverlayCanvas                   sorting 120
│   ├── LobbyOverlayView
│   ├── GameplayHudView
│   └── ResultsView
├── ModalCanvas                          sorting 200
│   ├── PauseView
│   ├── ConfirmDialog
│   └── BlockingConnectionView
└── FeedbackCanvas                       sorting 300
    ├── ToastRegion
    └── ScreenReaderStatus
```

Sólo una vista de pantalla ocupa el foco a la vez. Los overlays de lobby y ronda pertenecen a su escena 3D y no deben activar controles ocultos. Un modal establece un foco acotado y devuelve el foco al control que lo abrió.

### Contratos de presentación

- Las vistas reciben estados inmutables y emiten intenciones. No validan reglas de partida, composición de equipos ni códigos EOS por su cuenta.
- El estado autoritativo decide `CanStart`, `StartBlockReason`, lista de jugadores, rol sorteado y resultado.
- Todo estado asincrónico muestra una fase legible, permite cancelar cuando corresponda e impide doble envío.
- Un control deshabilitado conserva una explicación accesible mediante texto cercano; no depender sólo de tooltip.
- La UI no guarda secretos ni convierte un código de sala en dirección IP/puerto.

## 5. Flujo y pantallas

El diagrama complementario está en [ALFA-UI-FLOWS.svg](./ALFA-UI-FLOWS.svg).

### 5.1 Menú principal

Composición a 16:9:

- Mitad izquierda: logotipo, subtítulo `HUMANOS CONTRA MOSQUITOS`, personaje o escena nocturna no interactiva y versión alfa discreta.
- Derecha: panel de acciones de 520–600 px de ancho. En 1280×720 puede ocupar 46 % del ancho; no superponer el logotipo.

Orden y copy exacto:

1. `JUGAR ONLINE`
2. `ENTRENAMIENTO`
3. `PERSONALIZAR`
4. `AJUSTES`
5. `SALIR DEL JUEGO`

Foco inicial: `JUGAR ONLINE`. `Tab`, `Shift+Tab` y flechas recorren las acciones en el mismo orden. `Enter` activa una sola vez. `Escape` no sale inmediatamente: abre `¿Salir del juego?` con `VOLVER` enfocado y `SALIR` como acción destructiva secundaria.

### 5.2 Jugar online

Primera vista: dos tarjetas grandes y un botón de volver.

- `CREAR SALA`
  - Ayuda: `Abrí una sala y compartí el código con tus amigos.`
- `UNIRME CON CÓDIGO`
  - Ayuda: `Pegá el código que te mandó el anfitrión.`
- `← VOLVER`

No mostrar LAN, IP, puerto, servidor local ni opciones avanzadas.

#### Crear sala

Campos y acciones:

- Etiqueta `TU NOMBRE`; input de una línea, máximo fijado por Core.
- Primaria `CREAR SALA`.
- Secundaria `CANCELAR` durante una operación o `← VOLVER` en reposo.

Foco inicial: nombre. `Enter` en el nombre ejecuta Crear sólo si el estado permite enviar. Al enviar, nombre y botón quedan bloqueados; no puede haber dos solicitudes simultáneas.

#### Unirse

- `TU NOMBRE`.
- `CÓDIGO DE SALA` con placeholder `Pegá el código completo`.
- Botón `PEGAR` junto al campo.
- Primaria `UNIRME`.
- Secundaria `CANCELAR` o `← VOLVER`.

El campo acepta `Ctrl+V`; `PEGAR` produce la misma intención. La UI conserva el código completo visible con desplazamiento horizontal, nunca lo recorta para convertirlo en otro código. El código es texto, no seis casillas separadas.

#### Estados online canónicos

| Estado | Texto visible | Controles |
|---|---|---|
| Reposo | Sin indicador. | Campos editables; primaria activa según validación básica. |
| Conectando | `Conectando con el servicio…` | Campos bloqueados; `CANCELAR` visible. |
| Creando | `Creando la sala…` | Igual. |
| Buscando | `Buscando la sala…` | Igual. |
| Entrando | `Sala encontrada. Entrando…` | Igual, sin doble envío. |
| Cancelado | `Conexión cancelada.` | Campos restaurados; foco vuelve a la primaria. |
| Error recuperable | Mensaje del servicio traducido y `INTENTAR OTRA VEZ`. | Campos restaurados. |
| Incompatible | `La sala usa otra versión del juego.` | Sólo `VOLVER`. |
| Sala cerrada | `La sala ya no está disponible.` | `INTENTAR OTRA VEZ` y `VOLVER`. |

Validaciones breves:

- Nombre vacío: `Escribí tu nombre.`
- Código vacío o inválido: `Ese código no es válido. Copialo completo.`
- Tiempo agotado: `No pudimos conectar. Revisá Internet e intentá otra vez.`
- Anfitrión desconectado: `El anfitrión cerró la sala.`

`Escape` durante una operación emite Cancelar una vez y permanece en la vista hasta recibir cancelado/error. En reposo vuelve a la selección online. Otro `Escape` vuelve al menú.

### 5.3 Lobby 3D

La escena ocupa todo el fondo. La UI usa dos zonas y deja libre el centro para personajes:

- Superior: nombre de sala, código online y `COPIAR CÓDIGO`.
- Izquierda: lista de participantes, 360–430 px.
- Derecha inferior: reglas y acciones, 420–520 px.

Cada fila de participante muestra avatar, nombre y estado `LISTO`/`NO LISTO`. Antes de empezar, el rol muestra `SE SORTEA AL EMPEZAR`; no hay botones Humano/Mosquito ni reparto visible que permita inferir o elegir el rol futuro.

#### Código de sala

- El código es readonly, seleccionable y visible completo.
- `COPIAR CÓDIGO` copia exactamente el token recibido y confirma `Código copiado.`
- Mientras no exista: mostrar `Preparando el código…` y deshabilitar Copiar.
- Al cerrar la sala, limpiar texto y portapapeles lógico de la vista; un código anterior no vuelve a copiarse.

#### Reglas de alfa

| Control | Administrador | Invitado |
|---|---|---|
| Modo | Fila visible `SANGRE`; bloqueada porque es el único modo alfa. | Mismo valor, readonly. |
| Mapa | Fila visible `CASA CON PATIO`; bloqueada mientras sea el único mapa alfa. No listar mapas futuros. | Mismo valor, readonly. |
| Humanos | Selector segmentado `AUTO · 1 · 2 · 3 · 4 · 5`. | Valor visible, readonly. |
| Iniciar | `INICIAR RONDA`, sólo administrador. | No aparece como acción activable. |

`AUTO` significa que la autoridad elige una cantidad válida al iniciar. La UI no implementa una proporción 2:1 ni calcula equipos. Si `CanStart` es falso, `INICIAR RONDA` queda deshabilitado y debajo se muestra `StartBlockReason` recibido, por ejemplo `Hace falta al menos un jugador para cada bando.`

Acción común: botón grande `LISTO`/`CANCELAR LISTO`. Su estado se confirma con respuesta autoritativa; mientras espera, queda bloqueado con `Guardando…`.

#### Explorar el lobby

Si la escena habilita el movimiento de tercera persona:

- `RECORRER SALA` cierra el foco UI y captura el ratón.
- `Escape` restaura el panel, muestra el cursor y enfoca `LISTO` o la última acción usada.
- `Escape` desde el panel abre Pausa de lobby con `VOLVER A LA SALA` y `SALIR DE LA SALA`; nunca abandona directamente.

### 5.4 Entrenamiento

Composición: personaje/escena a la izquierda, configuración breve a la derecha.

- Título `ENTRENAMIENTO`.
- Paso `1. ELEGÍ TU ROL`: tarjetas `HUMANO` y `MOSQUITO`.
- Fila readonly `MODO · SANGRE`.
- Fila readonly `MAPA · CASA CON PATIO`.
- Texto: `Practicá con bots antes de entrar a una sala.`
- Primaria `EMPEZAR ENTRENAMIENTO`.
- `← VOLVER`.

Foco inicial: `HUMANO`. Seleccionar un rol actualiza el visor y la explicación; no inicia. `Enter` sobre la primaria envía rol, modo y mapa. Durante carga: `Preparando entrenamiento…`. `Escape` vuelve al menú en reposo o solicita cancelar durante carga.

### 5.5 Personalización básica

Alfa contiene únicamente:

- Humano: `TONO DE PIEL` y `COLOR DE PIJAMA`.
- Mosquito: `COLOR`.
- Predeterminado humano visible: pijama, pantuflas y gorro de noche. En alfa estas prendas no forman un catálogo seleccionable.

Composición:

- Centro/izquierda: `RawImage` 3D de al menos 720×720 a 1080p, con fondo `Night800` y suelo suave.
- Derecha: pestañas `HUMANO` y `MOSQUITO`, categorías alfa y muestras de color con nombre accesible.
- Pie: `GUARDAR`, `RESTABLECER` y `← VOLVER`.

Controles del visor:

- Arrastre horizontal: giro orbital del modelo.
- Rueda: zoom limitado; nunca atraviesa el modelo.
- Botones `FRENTE`, `PERFIL`, `ESPALDA` y `RESTABLECER VISTA` para teclado/mando.
- Teclas de navegación recorren primero pestañas, luego colores, vistas y acciones.

El color seleccionado usa borde, check y nombre; no sólo tono. `GUARDAR` confirma `Personalización guardada.` y sólo se habilita si existe un cambio. `Escape` con cambios abre `¿Salir sin guardar?`, con `SEGUIR EDITANDO` enfocado. No hay aleatorio, presets, pelo, rostro, accesorios, ropa adicional ni emotes en alfa.

### 5.6 Ajustes

Mostrar sólo capacidades alfa realmente conectadas. Una fila sin implementación se oculta; no se publica deshabilitada como promesa.

Categorías previstas:

- `AUDIO`: volumen general, música y efectos.
- `VIDEO`: modo de pantalla, resolución y calidad si Presentation expone valores aplicables.
- `CONTROLES`: sensibilidad humana, sensibilidad mosquito, invertir eje vertical y lista de controles. Reasignación sólo si el contrato de entrada del Director la incluye en alfa.

No incluir voz, mezcla por jugador, perfiles 1440p/4K, límite de FPS avanzado ni opciones de etapas futuras.

Foco inicial: primera pestaña. `APLICAR` sólo se habilita con cambios; `RESTABLECER` pide confirmación. `Escape` descarta cambios no aplicados mediante diálogo y vuelve exactamente a la pantalla que abrió Ajustes. Abrir Ajustes durante la ronda mantiene la pausa y no reinicia música/escena.

### 5.7 Pausa y resultados

Pausa durante ronda:

1. `CONTINUAR`
2. `AJUSTES`
3. `CONTROLES`
4. `VOLVER AL LOBBY` si el flujo autoritativo lo permite, o `SALIR DE LA SALA`.

Foco inicial: `CONTINUAR`. `Escape` cierra Pausa y captura el ratón. Si Ajustes está abierto, el primer `Escape` vuelve a Pausa; el segundo reanuda.

Resultado Sangre:

- Título: `GANARON LOS HUMANOS`, `GANARON LOS MOSQUITOS` o `RONDA INTERRUMPIDA`.
- Mostrar cuota final, tiempo y motivo autoritativo en un máximo de cuatro líneas.
- Administrador: `VOLVER AL LOBBY` como primaria.
- Invitado: `ESPERANDO AL ANFITRIÓN…` no interactivo y `SALIR DE LA SALA` secundaria.
- Entrenamiento: `REPETIR ENTRENAMIENTO` y `VOLVER AL MENÚ`.

## 6. HUD de Sangre sin marcas de picadura

El mundo permanece dominante. El HUD no dibuja partes del cuerpo, zonas candidatas, siluetas, flechas a puntos de mordida ni marcadores sobre personajes.

### Elementos comunes

- Superior centro: reloj y cuota compartida `SANGRE 18 / 40` con icono propio de gota. Ancho máximo 420 px.
- Centro: retícula neutral pequeña, sin cambio de forma por parte corporal.
- Centro inferior: una sola acción contextual, por ejemplo `[E] ABRIR` o `[E] RECOGER` cuando el runtime confirme disponibilidad.
- Inferior izquierda: ayuda contextual de una o dos líneas; desaparece al aprenderse o al perder contexto.
- Superior derecha: estado de red sólo durante degradación, reconexión o error. No mostrar métricas técnicas al jugador.

### Humano

- No mostrar barra de salud inventada.
- Cuando recibe una picadura confirmada: viñeta coral breve y texto `TE ESTÁN PICANDO · MIRÁ Y GOLPEÁ`. No hay indicador direccional ni dibujo de zona corporal.
- La mira manual y el cuerpo visible son la fuente para defenderse. El HUD no afirma un golpe hasta la confirmación autoritativa.
- Estado de golpe: `RECUPERANDO…` con una barra corta sólo durante el tiempo real de recuperación.
- Desmayo confirmado: `DESMAYADO` y tiempo/condición recibidos. No fijar valores de balance en copy.

### Mosquito

- Ayuda inicial contextual: `W · VOLAR HACIA LA MIRA` y `SOLTÁ W · FRENAR`.
- Cerca de contacto válido: texto central `MANTENÉ [E] PARA PICAR`. No aparece una marca sobre el cuerpo.
- Extracción confirmada: barra `EXTRAYENDO` asociada al estado autoritativo. Soltar/cancelar muestra `PICADURA CANCELADA`; completar muestra `EXTRACCIÓN COMPLETA` brevemente.
- Si está adherido y puede desprenderse: `[E] DESPRENDERTE` según el binding real.
- Caída: `ATURDIDO` y recuperación recibida. No mostrar vidas en Sangre.

### Prioridades

Sólo una instrucción ocupa el centro inferior. Orden: bloqueo/error inmediato → acción defensiva o desprendimiento → interacción → guía inicial. Estados antiguos se limpian al cambiar de rol, ronda, lobby o conexión.

## 7. Foco, ratón y Escape

| Contexto | Ratón | Foco inicial | `Escape` |
|---|---|---|---|
| Menú | Visible | Jugar online | Confirmar salida. |
| Crear/Unirse | Visible | Primer campo | Cancela operación o vuelve un nivel. |
| Lobby panel | Visible | Listo / última acción | Abre Pausa de lobby. |
| Lobby recorrido | Capturado | Ninguno | Vuelve al panel. |
| Entrenamiento | Visible | Humano | Cancela carga o vuelve al menú. |
| Personalizador | Visible | Pestaña Humano | Confirma descarte si hay cambios. |
| Ajustes | Visible | Primera pestaña | Vuelve al invocador; si hay cambios, confirma. |
| Ronda | Capturado | Ninguno | Abre Pausa. |
| Pausa | Visible | Continuar | Reanuda. |
| Modal | Visible | Acción segura | Cierra sólo el modal. |
| Resultados | Visible | Acción primaria válida | No abandona la sala directamente. |

Reglas adicionales:

- Un modal encierra `Tab` y flechas dentro de su jerarquía.
- Al cerrar una vista, el foco vuelve al control que la abrió, si sigue activo; de lo contrario, a la primaria de la pantalla.
- Eventos repetidos o `key repeat` no activan Crear, Unirse, Listo, Iniciar ni Guardar más de una vez.
- Cambiar de escena limpia foco oculto antes de asignar el nuevo.
- Los campos de texto consumen letras y atajos de edición sin mover al personaje.
- Pausa, error y conexión bloqueante liberan el puntero. Reanudar una ronda lo captura sólo después de cerrar todos los modales.

## 8. Contratos de estado e intención

Los nombres son semánticos; el Director puede adaptarlos al namespace final sin cambiar contenido ni responsabilidad.

### Estado mínimo por vista

| Estado | Campos mínimos |
|---|---|
| `OnlineRoomUiState` | fase, mensaje, nombre, código pegado, puede cancelar, puede reintentar, campos bloqueados. |
| `LobbyUiState` | es administrador, código online, participantes, estado listo local, configuración humana `auto/1…5`, modo Sangre, mapa Casa con patio, puede iniciar, motivo de bloqueo. |
| `ParticipantUiState` | id estable, nombre, avatar disponible, listo, conexión. El rol previo a ronda es desconocido. |
| `TrainingUiState` | rol elegido, modo Sangre, mapa Casa con patio, cargando, error. |
| `CustomizationUiState` | rol editado, valores guardados, borrador, vista y zoom. |
| `BloodHudUiState` | rol local, reloj, sangre actual/meta, interacción, estado de picadura/golpe/desmayo, conexión. |

### Intenciones emitidas

- `CreateOnlineRoom(playerName)`
- `JoinOnlineRoom(playerName, roomCode)`
- `CancelOnlineOperation()`
- `CopyRoomCode()` y `PasteRoomCode()`
- `SetReady(bool)`
- `SetHumanCount(Auto | 1..5)` sólo administrador
- `StartRound()` sólo administrador
- `LeaveRoom()` después de confirmación
- `StartTraining(Human | Mosquito, Blood, HousePatio)`
- `SaveBasicCustomization(draft)`
- `ApplySettings(draft)`

La UI puede bloquear una intención por vacío básico o estado busy. Toda validación de autoridad, compatibilidad, cupo, equipos, mapa y transporte permanece fuera de la vista.

## 9. Accesibilidad y localización

- Copy alfa en español rioplatense consistente: `Elegí`, `Pegá`, `Copiá`, `Volver`. No mezclar inglés en botones visibles.
- Código de sala y nombres usan Atkinson, selección de texto y caret visible.
- Cada icono tiene etiqueta de texto o descripción accesible; no usar iconos solos en acciones críticas.
- El foco se reconoce sin color y conserva contraste sobre todos los estados.
- Soportar teclado y mando desde el primer prefab. El puntero no es requisito para completar Crear, Unirse, Listo, Entrenamiento, Personalizar o Ajustes.
- Los mensajes asincrónicos se anuncian en una región de estado y permanecen hasta ser reemplazados; no dependen de un toast fugaz.
- No cortar copy con puntos suspensivos salvo estados de espera deliberados. Los textos admiten al menos 30 % de expansión futura sin invadir acciones.
- No usar vibración, sonido o color como única confirmación.

## 10. Criterios de aceptación de implementación

### Estructura y resolución

- Un único `EventSystem` y un `GraphicRaycaster` por Canvas interactivo.
- 1920×1080 y 1280×720 sin desborde, solapamiento ni controles menores a 44 px.
- 21:9 conserva paneles dentro de una anchura máxima y expande el mundo, no estira la UI.
- Todo texto usa TMP; los fondos decorativos tienen `Raycast Target` desactivado.

### Flujos

- Menú → Crear → Cancelar/volver; Menú → Unirse → pegar → error/reintento; y `Escape` por niveles.
- Sala online no expone LAN, IP ni puerto.
- Código readonly, seleccionable, pegable, copiable sin alteraciones y limpiado al cerrar.
- Administrador modifica Auto/1…5 e inicia sólo con permiso autoritativo; invitado ve reglas readonly.
- Roles no se eligen en lobby y se muestran como sorteados al empezar.
- Sólo Sangre y Casa con patio aparecen en alfa; no hay opciones futuras vacías.
- Entrenamiento permite ambos roles y conserva modo/mapa fijos.
- Personalización sólo incluye piel/pijama humano y color mosquito; default humano mantiene pijama, pantuflas y gorro.
- Ajustes sólo muestran capacidades conectadas.

### Interacción

- Foco inicial, orden, restauración y encierro modal funcionan con teclado/mando.
- `Escape` nunca salta dos niveles, abandona una sala sin confirmación ni deja el ratón en modo incorrecto.
- Inputs repetidos no duplican solicitudes.
- Campos de texto no envían movimiento o acciones de juego.
- HUD humano y mosquito no contiene marcas de picadura, minimapa, clases, crafting o sistemas de otras etapas.
- Un cambio de estado elimina prompts antiguos en el mismo frame de UI.

## 11. Dependencias y supuestos registrados

- Core/Online entrega estados y mensajes autoritativos; la UI no interpreta tokens EOS ni direcciones.
- Gameplay entrega cuota, reloj, rol, contacto válido, extracción, golpe, desmayo e interacciones confirmadas.
- Presentation entrega cámaras para lobby y visor de personaje. UI consume el resultado y no modifica rigs ni escenas ajenas.
- Content entrega nombres visibles `Sangre` y `Casa con patio`, además de los colores alfa disponibles.
- El alfa tiene un único modo y mapa; sus filas permanecen visibles y readonly para explicar la partida sin prometer opciones futuras.
- El número exacto de participantes, balance de desmayo y condiciones de equipo quedan en autoridad y pruebas reales; la UI muestra razones recibidas.
- Los cinco mapas confirmados del ciclo pertenecen a etapas posteriores. No se listan ni se diseñan en este lote.
- No se requiere aprobación artística intermedia mientras Branko duerme; cualquier desviación se registra y se revisa al cierre técnico del alfa.

## 12. Entrega visual posterior

Cuando el Director asigne turno de Unity, revisar en movimiento y con capturas nativas:

1. Menú y online a 1280×720 y 1920×1080.
2. Lobby con administrador e invitado, panel y recorrido.
3. HUD humano bajo picadura y mosquito en extracción, comprobando ausencia de marcas.
4. Personalizador frente/perfil/espalda, giro y extremos de zoom.
5. Ajustes abiertos desde menú y pausa.
6. Estados de error, espera, código largo y desconexión.

Estas capturas serán evidencia sólo de la implementación Unity y del commit exacto que se ejecute.
