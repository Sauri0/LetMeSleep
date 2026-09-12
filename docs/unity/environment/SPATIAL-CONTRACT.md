# Entorno Unity 0.9.4/alfa — contrato de trabajo v1

Estado: muestra original en producción; medidas de actores y pasos confirmadas por Director; pendiente integración Unity. No acredita apariencia en Unity ni jugabilidad. Última instrucción de Branko: continuar decisiones razonables sin preguntas ni bloqueo por aprobación artística mientras duerme. Alfa no se amplía por cuenta propia.

## Convenciones

Metros. Unity +Y arriba, +Z frente, +X derecha. Origen de habitación en esquina interior suroeste a nivel de suelo terminado. Blender utiliza +Z arriba y -Y frente; generador convierte explícitamente coordenadas y exporta FBX -Z/Y a escala unitaria. Director debe verificar conversión al importar antes de crear prefabs. No corregir escala con objetos padre escalados.

Contrato confirmado por Director: humano cápsula radio 0.25 m/alto 1.72 m, ojos 1.53 m, agachado alto 1.00 m; mosquito esfera radio 0.055 m. Circulación principal al menos 1.80 m, escalera al menos 1.60 m, puertas 1.10 × 2.20 m libres. La comprobación definitiva depende del controlador, cámara, ataque y tamaño final del personaje.

## Muestra habitable

Una habitación interior de 4.80 × 4.40 m, altura 2.80 m. Muros 0.18 m, piso 0.18 m, techo 0.20 m. Vano estructural sur x=0.50…1.74, jambas dejan x=0.57…1.67 y altura 2.20 m. Sin escalón ni listón atravesando el umbral. Ventana fija al norte, vano x=3.00…4.20, y=1.15…2.25. Vidrio opaco provisional, sin prometer transparencia URP.

Madera miel oscura, yeso crema y ropa de cama azul. Formas limpias con pequeños biseles en muebles; paredes sin triangulación decorativa. Cama doble al noreste, mesita al oeste de cabecera, escritorio y silla al noroeste. El centro y el pie de cama conservan una reserva principal de 1.80 × 1.80 m (x=1.76…3.56, z=0.10…1.90); el acceso desde el vano es la excepción local de 1.10 m acordada. Los espacios laterales de uso junto a muebles son accesos secundarios, no corredores principales. La silla mira al escritorio. La muestra representa un dormitorio con función, sin añadir catálogo de objetos.

| Elemento | Medida aproximada X × Y × Z | Uso y reserva |
|---|---|---|
| Cama | 1.64 × 1.30 × 2.22 m | Acceso por pie y lateral izquierdo; ventana fija sobre cabecera |
| Mesita | 0.59 × 0.65 × 0.56 m | Superficie pickup a 0.65 m; acceso desde sur |
| Escritorio | 1.32 × 0.795 × 0.64 m | Superficie a 0.795 m; interacción desde costado despejado |
| Silla | 0.49 × 0.95 × 0.50 m | Separada del escritorio; no bloquear el corredor central |

El shell se construye como frontera soldada de la unión de volúmenes ortogonales. No emite caras interiores de contacto en esquinas ni encuentros piso/muro/techo. Marco dentro del vano, paneles de puerta con espesor real. Las colisiones son cajas explícitas por pieza; no añadir una caja envolvente que cierre puerta o ventana. Fuente única de medidas para integración: `room_contract.json`, coordenadas Unity locales al nodo indicado.

## Puerta real

Jerarquía: `LMS_RoomSample_01/Door_01/Door_01_Hinge/Door_01_Leaf`. Tiradores, paneles y bisagras pertenecen al pivote. Posición del pivote en habitación: (0.585, 0, 0.045). Hoja local de 1.07 × 2.185 × 0.04, centro (0.535, 1.1025, 0), holgura inferior 0.01 m. Cerrada yaw=0; abierta yaw=-100° sobre +Y Unity. Abre hacia dentro, contra el lado oeste, y la hoja no se traslada al animar.

Reservar barrido radio 1.09 m y su caja x=0.36…1.69, z=0.02…1.15 libre de mobiliario. El humano entra cuando la puerta ha abierto; no intentar atravesar hoja durante movimiento. `Socket_Door_Use` acompaña el tirador. Director/Gameplay deciden duración, autoridad, bloqueo, chequeo de cuerpos y reproducción: estado autoritativo del anfitrión, interpolación visual; cerrar no debe empujar ni atravesar actores. Mesh exportado sin animación horneada ni física autónoma. `door_01.fbx` permite prefab separado; su root está en origen de habitación y el pivote conserva el desplazamiento documentado. `room_furnished_without_door.fbx` lo complementa sin duplicación. `room_sample.fbx` es el conjunto completo solo para revisión; no instanciarlo junto con los otros dos. La importación debe comprobar signo de giro y escala antes de añadir collider móvil/rigidbody según contrato del Director.

## Sockets

Nodos Empty con ID estable, posición, normal hacia usuario, posición sugerida de pie y reserva radial 0.40 m en JSON. La posición de pie es suelo, no el centro de cápsula. Cada normal +Z local mira hacia el acceso. `Socket_Pickup_Desk` y `Socket_Pickup_Nightstand` son superficies reservadas para la única herramienta alfa cuando se integre; no crean inventario adicional. `Socket_Task_Bed` prepara interfaz futura, desactivado en alfa. La ventana permanece fija y no tiene tarea: la cama ocupa su frente. Interacción requiere alcance y visibilidad validados por Gameplay; un socket no autoriza atravesar muebles.

## Casa fija de dos pisos y patio — propuesta espacial

No se genera todavía la casa completa. Envolvente propuesta 12.0 × 10.0 m exteriores; pisos terminados y=0 y y=3.0. Planta baja: acceso/vestíbulo central conectado a sala, comedor/cocina y salida al patio; lavado/baño con puerta desde distribución. Planta alta: distribuidor conectado a dos dormitorios (uno basado en la muestra), baño y rellano. No pasar por un dormitorio para llegar al otro ni hacer circular a través de un mueble.

Escalera en U con ancho libre 1.60 m por tramo, 18 contrahuellas de 0.166667 m en total, 9 por tramo y 8 huellas de 0.28 m por tramo. Rellano intermedio 1.60 m de fondo; caja interior reservada 3.50 × 3.84 m, antes de cerramientos. Altura libre mínima propuesta 2.20 m sobre nariz de peldaño y rellanos, comprobada con hueco real de forjado en integración. Escalera y barandas requieren muestra geométrica posterior y prueba con cápsula; no usar una rampa visual como prueba de peldaños correctos.

Patio fijo 12 × 8 m conectado por puerta de 1.20 m; sendero principal 1.80 m, área de giro 1.80 m frente a salida. Vegetación y cerca perimetral no invaden puertas, ruta o cámara. Suelo sin desnivel en el acceso; cualquier borde futuro llevará colisión coherente. Límites de vuelo del mosquito se definen en integración, nunca mediante huecos accidentales del modelo.

## Lobby separado

Espacio propio 10 × 8 m útiles, altura 3.20 m; sin continuidad física con el mapa de partida. Zona central libre 6 × 4 m, circulación perimetral 1.80 m y reserva de aparición distribuida fuera de muebles y puertas. Puntos de spawn propuestos se validarán para capacidad y cámara en Unity; 16 participantes es objetivo condicionado, no certificación. Ajustes/listo/código son responsabilidad de UI/Core, no textos incrustados en el mesh. Cambios de mapa y regreso cargan escenas según contrato Director.

## Alcance y evidencia

Referencias: `work/references094/expanded/environment-modules.png`, `expanded2/first-person-interiors.png` y `expanded2/furniture-and-nature.png`, aportadas por Branko. Solo dirección de formas, paleta y composición. Geometría propia escrita desde cero; no se copia la casa Godot ni marcas o mecánicas dibujadas. Sin assets remotos ni texturas externas.

Catálogo del ciclo decidido por Director: casa/patio, isla marítima, pantano nocturno, cabaña montaña y granja/granero. Este lote solo muestra y contrato alfa. Archivo .blend editable + FBX + JSON se mantienen fuera de `unity/Assets` como fuentes. Director crea y versiona .meta al importar assets a Unity; no se entregan escenas/prefabs serializados manualmente.

Revisión requerida: reimport FBX con escala/pivote conservados, validez de shell y barrido de puerta, luego import Unity 6000.3.24f1, materiales URP, vista desde cámara humana/mosquito, posado en techo/muro, acceso real a sockets y movimiento a través del vano. Sin render GPU ni editor hasta slot coordinado. Evidencia Blender nunca sustituye esa revisión Unity.
