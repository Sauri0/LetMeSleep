# Dejame dormir — prototipo 0.1.0

Juego nativo Windows de humanos contra mosquitos. Godot **4.5.2 stable**, GDScript, servidor ENet/UDP autoritativo. Proyecto en `game/`; compilación y pruebas reproducibles en `work/`. No requiere Steam, navegador ni cuentas.

## Jugar

Descomprimir el paquete Windows completo. Abrir `Dejame-dormir.exe`. Una persona inicia el servidor en su computadora, crea sala y comparte dirección, puerto **UDP 27840** y código. Los demás se unen a esa dirección con ese código. El servidor y el juego del anfitrión son procesos separados. No contrataron servicios ni se modificaron router/firewall automáticamente.

Al comenzar: de 1 a **5 humanos**, al menos dos mosquitos por humano. Límite técnico inicial: 12 mosquitos y 16 participantes en total. Se valida la proporción al inicio, sin rebalancear por cada baja.

## Reglas y controles

Humanos en primera persona: WASD y ratón. Clic para palmada/golpe; Q defensa propia por banda corporal según dónde mirás (arriba/cabeza y hombros, medio/torso, abajo/piernas). Las zonas traseras requieren un compañero. R recoge o cambia herramienta cercana; G la suelta. Empezás con manos. Matamoscas, raqueta eléctrica, diario y escoba tienen diferente alcance, área y recuperación; todos son objetos únicos gobernados por servidor. E mantenida hace tu tarea si estás cerca y no te están picando.

Mosquitos en tercera persona: WASD, Espacio sube, Ctrl baja. E pulsada pica cerca de tu marca; otra pulsación desprende. Soltar E no te libera. F posa cerca de una superficie; moverte vuelve a volar. Solo vos recibís tu marca, y no se ve a través de cuerpos/muebles. Se reasigna a intervalos individuales fijos sin mostrar cronómetro. Picando viajás con el humano y conservás la zona; desprenderte cambia inmediatamente a otra sin reiniciar calendario.

Esc abre ajustes/menú (la ronda online sigue). Teclas, sensibilidad por rol, inversión, volumen y pulso de marca se guardan localmente.

| Modo | Condición |
|---|---|
| Recolección de sangre | Mosquitos ganan al alcanzar cuota compartida; humanos si vence reloj o eliminan a todos. Sangre acumulada permanece tras desprenderse/morir. Sin reapariciones. |
| Supervivencia | Al menos un mosquito vivo al finalizar gana su equipo. Humanos ganan si eliminan a todos. Sin hambre ni picaduras obligatorias. Sin reapariciones. |
| Dejanos dormir | Humanos cumplen meta colectiva al final, o ganan antes si todos los mosquitos agotan sus vidas. Cada mosquito tiene 3 vidas totales propias por defecto y reaparece mientras conserve alguna. Fallar tarea reduce solo el plazo de futuras tareas de ese humano. |

Si se pierde un participante durante la ronda se interrumpe sin ganador y se vuelve a sala. La reconexión entra a sala, no reanuda una ronda vieja. En resultados el anfitrión vuelve a sala y todos vuelven a prepararse. Eliminados ven espera neutra, sin cámara libre de espionaje.

## Compilar y verificar

Se descargó editor y plantillas oficiales a `work/tools/`, sin instalación global. Paquetes verificados contra SHA512-SUMS oficial. El ZIP de fuentes no incluye esas dependencias de 1.4GB: `work/setup-tools.ps1` las restaura de Godot oficial.

En PowerShell: `./work/build.ps1` importa, ejecuta reglas y exporta Windows. `./work/test-network.ps1 -Mode blood -Humans 1 -Mosquitoes 2` lanza servidor y clientes automatizados reales en loopback. Modos: blood/survival/sleep; opciones `-DisconnectTest`, `-RematchTest`, `-Incompatible`, `-Executable <exe exportado>`. Logs e informes por ejecución bajo `work/network-*`.

Las pruebas de reglas son deterministas y los bots son diagnósticos, no rivales IA incluidos en la sala. Las capturas provienen del cliente real renderizado en OpenGL. Las pruebas locales no sustituyen pruebas desde varias computadoras/conexiones ni validación de balance.

## Estructura

- `simulation.gd`: autoridad, vidas, zonas, tareas, herramientas y finales; sin dependencias gráficas.
- `arena.gd`: geometría y colisiones compartidas.
- `network.gd`: una sala registrada por código, compatibilidad, permisos, canales y snapshots privados dirigidos.
- `client.gd`: controles, cámara con colisión e interpolación de presentación.
- `world.gd`, `actor_view.gd`, `audio_fx.gd`: arte y audio procedural original.
- `ui.gd`, `preferences.gd`: flujo y ajustes en español.

Los planes originales del 6 de septiembre están en la carpeta de la conversación de planificación; sus decisiones posteriores están incorporadas en esta versión. Ver el informe de pruebas y LEEME del paquete para estado y límites concretos.
