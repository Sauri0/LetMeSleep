# v0.3.0 — Guía de estilo derivada de los bocetos

Autoridad artística: los bocetos del usuario en `C:\Users\brank\Desktop\bocetos`
(inventario y hashes en `Higgsfield/BOCETOS-INVENTARIO.csv`). Referencia principal
de identidad y flujo: **UI-06** (`ui/ChatGPT Image 12 sept 2026, 06_01_41 p.m..png`).
Esta guía traduce los bocetos a valores concretos; ante duda, manda el boceto.

Reglas de producto que se mantienen (AGENTS.md): nombre **Let me sleep** (nunca
"Bite & Build"), sin armas de fuego, sin crafting, sin clases ni recompensas
diarias; humano por defecto en pijama, pantuflas y gorro de dormir; roles aleatorios
por ronda; sin marcadores de picadura.

## 1. Estilo general

- Low-poly **facetado** (flat shading, caras visibles), colores saturados y cálidos,
  formas simples y redondeadas, humor visual (ojos enormes).
- Iluminación: interiores cálidos (faroles/lámparas ámbar, madera), exteriores
  diurnos saturados (verde bosque, lago azul) o nocturnos azul profundo con
  ventanas y faroles cálidos que contrastan.
- Lema del boceto: "simple · colorful · playful". Nada de texturas realistas.

## 2. Interfaz (UI-01, UI-06, PER-08)

### Paleta

| Token | Hex | Uso |
|---|---|---|
| `bg.deep` | `#0E1A30` | fondo de pantallas sin escena |
| `panel` | `#15264A` (alpha 0.92) | paneles principales |
| `panel.header` | `#1C3160` | barra de título de panel |
| `panel.border` | `#3B5E9C` | borde 2 px de paneles |
| `panel.inset` | `#0F1D38` | campos, listas, slots |
| `btn.primary` | `#1F6FE0` → `#3A8DFF` (degradado vertical) | botón principal (Jugar, Crear sala, Iniciar) |
| `btn.primary.border` | `#7CC0FF` | borde/brillo superior |
| `btn.secondary` | `#1E3358` | botones de menú no seleccionados |
| `btn.secondary.hover` | `#274473` | hover/foco |
| `btn.success` | `#2E9E48` → `#46C45F` | Listo, Crear sala (CTA), Aplicar, Jugar de nuevo |
| `btn.danger` | `#C62E36` → `#E5484F` | Salir de la partida, Iniciar como mosquito |
| `accent.yellow` | `#FFC93C` | título "LET ME", resaltados, iconos de objetivo |
| `accent.blue` | `#49B2FF` | título "SLEEP", selección |
| `team.human` | `#2F7BFF` | humanos |
| `team.mosquito` | `#E0393E` | mosquitos |
| `text.primary` | `#F2F6FF` | texto |
| `text.secondary` | `#A8B8D8` | subtítulos, ayudas |
| `status.ok` | `#57D26B` | "Listo", conectado |
| `status.warn` | `#FF6B5E` | "No listo", errores |

### Forma

- Esquinas redondeadas 10–14 px (a 1080p), borde 2 px `panel.border`, sombra
  suave inferior (4 px, negro 35%), línea de brillo superior 1 px en botones.
- Botones de menú: altura 64 px, icono a la izquierda en cuadrado redondeado,
  texto en MAYÚSCULAS, peso bold, tracking ligero. Seleccionado = `btn.primary`.
- Botones CTA: grandes (72–84 px), verde con icono ✓ o ▶.
- Slots de inventario: cuadrados 80 px redondeados, número arriba-izquierda,
  seleccionado con borde `accent.blue` 3 px.
- Listas (salas, jugadores): filas `panel.inset` con barra de señal/estado a la
  derecha; nombres en blanco, subtítulo `text.secondary`.

### Tipografía

- Título: fuente cómica gruesa con contorno oscuro (hoy **Bangers**), "LET ME" en
  `accent.yellow`, "SLEEP" en `accent.blue`, contorno `#0B1426` 6–8%, sombra.
- UI: sans bold en MAYÚSCULAS para botones/encabezados; texto corrido legible
  (hoy **Atkinson Hyperlegible**).

### Pantallas requeridas (UI-06)

1. Menú principal: logo arriba-izquierda, columna de 5 botones (Jugar,
   Entrenamiento, Personalización, Ajustes, Salir), escena 3D del humano durmiendo
   con mosquito a la derecha, lema "LA NOCHE NUNCA ES TAN TRANQUILA".
2. Jugar online: pestañas Crear sala / Unirse a sala; formulario (nombre de sala,
   mapa con miniatura y flechas, modo, jugadores máximos, contraseña opcional) y
   lista de partidas disponibles; botón verde CREAR SALA.
3. Sala de espera: cabecera "ESPERANDO JUGADORES" + contador + `4/6` + mapa/modo;
   escena 3D con nombres flotantes; chat abajo-izquierda; INVITAR AMIGOS y LISTO.
4. Entrenamiento: dos tarjetas grandes (humano azul / mosquito rojo) con
   ilustración, descripción y botón INICIAR.
5–6. Personalización humano/mosquito: categorías a la izquierda, visor 3D central
   con pedestal, opciones/colores a la derecha, vistas Frente/Espalda/Lado.
7. HUD humano: objetivo arriba-izquierda con barra, temporizador central, conteo
   de mosquitos arriba-derecha, inventario 4 slots abajo-centro.
7b. HUD mosquito: barra "molestá al humano", conteo de humanos + temporizador,
   ayudas de control abajo-derecha (Volar, Shift Acelerar, E Interactuar).
8. Pausa: Continuar (azul), Ajustes, Volver a la sala, Salir de la partida.
9. Resultados: "¡HUMANOS GANAN!" / "¡MOSQUITOS GANAN!" con personajes celebrando,
   marcador por equipo, VOLVER A LA SALA y JUGAR DE NUEVO (verde).
10. Ajustes: pestañas General/Audio/Video/Controles/Accesibilidad; sliders con
   valor; RESTAURAR y APLICAR (verde).
11. Conexión y errores: tarjeta "CONECTANDO…" con spinner; tarjeta de error con
   icono de advertencia rojo y REINTENTAR.

## 3. Personajes (PER-01..PER-07)

### Humano

- Proporción caricaturesca: cabeza ~1/5 de la altura, cuerpo delgado, manos
  grandes con dedos simples, pies con calzado grueso.
- Cara facetada color piel (`#C98B5A` por defecto; 6 tonos del boceto PER-04),
  **ojos enormes**: esferas blancas que sobresalen, pupila negra pequeña, cejas
  oscuras gruesas que cambian la expresión (normal, enojado, sorprendido, sueño).
- Defecto del juego: camiseta clara `#E8DCC5` o pijama, pantalón de pijama azul
  `#2D4F9A` (lunares claros en UI-06), pantuflas, gorro de dormir; variantes:
  gorra roja `#C8322E`, capucha, overol, mochila (PER-01/PER-04/PER-06).
- Objetos en mano permitidos: linterna, matamoscas, aerosol, farol, botiquín,
  taza, llave. **No escopeta** aunque aparezca en el boceto.

### Mosquito

- Tamaño "jugable": en pantalla debe leerse como personaje, no como insecto real.
- Cuerpo rojo `#B8262B` facetado con segmentos oscuros `#6E1418`; tórax redondo;
  abdomen alargado puntiagudo; cabeza pequeña con **ojos blancos enormes**
  (más grandes que la cabeza), pupila negra; probóscide larga y fina.
- 6 patas largas y finas oscuras `#2A1A1A` con articulaciones; 2 alas facetadas
  translúcidas lavanda `#DCDDF5` alpha ~0.45 con venas sutiles.
- Variantes (PER-03/PER-07): paletas Natural/Bosque/Desierto/Urbano/Fantasía/
  Tóxico/Sangre/Hielo; formas de alas, ojos (normal, grandes, pequeños, entornados,
  enojados, dormidos), probóscide (estándar, corta, larga, curva), marcas (anillos,
  lunares, rayas).

## 4. Mapas y ambientación (ENV-01..ENV-05, PRP-01/02)

- Interiores: madera cálida `#8B5A2B`/`#A86F3A`, paredes crema, alfombras rojas y
  azules, faroles y lámparas ámbar `#FFB347` con halo, chimenea con fuego, cuadros,
  plantas, estantes con objetos, cofres/cajas. Luz de ventana diagonal.
- Exteriores: pinos low-poly en capas, rocas grises facetadas, flores blancas y
  amarillas, senderos de tierra, cercas de madera, carteles tallados, muelle con
  postes y farol, carpa verde, fogón con rocas y fuego, buzón rojo, bicicleta.
- Color grading: saturación +10–20%, contraste suave, sombras azuladas y luces
  cálidas; bloom leve sólo en fuentes de luz; niebla de distancia suave.
- Agua: azul saturado, movimiento suave, espuma estilizada (sin realismo).

## 5. Animación

- Exagerada y legible: squash & stretch leve en saltos/golpes, brazos que
  balancean al caminar, cabeza que acompaña la mirada, ojos que parpadean.
- Mosquito: aleteo rápido constante (blur/alpha), cuerpo que se inclina con la
  velocidad, patas colgando en vuelo y apoyadas al posarse, probóscide que se
  extiende al picar, rebote cómico al recibir golpe.
