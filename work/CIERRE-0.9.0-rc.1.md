# Cierre acotado de la candidata 0.9.0 — 11 de septiembre de 2026

Director es el único integrador y publicador. Worker 1 revisó en modo de sólo
lectura los cambios de admisión EOS y el arreglo final del selector de gestos.
No quedan compilaciones paralelas autorizadas ni cambios de otros agentes pendientes.

## Qué se entrega

Windows x86_64, Godot 4.5.2, protocolo 9. Crear sala aloja el servidor en el
mismo juego, configura EOS y genera una invitación LMS1-. La práctica permite
probar humano y mosquito sin amigos. Las mejoras gráficas, casa procedural,
personalización, puertas, emotes y voz quedan incluidas en esta candidata.

Código de runtime exportado: `3c3e8ba007c879f84d9652bc817f98c2fd969568`.
EXE SHA256: `E5928D76F18F14B7573D135A88340B1C9F0237AE7EC468A1AAF29B41F6002D1E`.
Los commits posteriores de documentación no alteran ese runtime.

## Evidencia conservada

- `director09-online-build-sep11.*`: pruebas de lógica completas y pruebas
  nativas hasta encontrar dos fallos en el test del menú. Una fixture de voz
  omitía `can_transmit`; el selector también podía elegir por foco/warp pasivo.
- `release07-sep11-menu-input-fix.*`: corrección, 45/45, exit0, stderr vacío.
- `director09-final-native.*`: nueva batería nativa completa, contactos en
  esquinas y exportación, exit0. Se reutiliza la batería lógica ya aprobada del
  commit 22b1cb1; sólo cambiaron el selector y su test, revalidados nativamente.
- `release07-sep11-rc09-*`: comprobaciones sobre el EXE exportado.
- `build-*.log/.err`: resultados individuales de las pruebas de fuente.

EOS real: dos ciclos de anfitrión 15/15. Sesión/admisión 72/72; red 43/43;
invitaciones 216/216; MTU 4/4; payload de 16 jugadores 18/18. Dos clientes con
RPC real sobre ENet y membresía simulada recorrieron sala y ronda: 24/24.
El EXE abrió la sala 3D desde el menú y cerró EOS: 7/7.

El paquete final pasó nueve recorridos, 468 comprobaciones: anfitrión 7,
dos clientes locales 24, red 43, sesión 72, interfaz 163, puertas 16,
personaje 86, práctica humana 29 y práctica mosquito 28. Cada proceso terminó
con exit0 y stderr vacío. Un intento de interfaz encontró el portapapeles de
Windows ocupado; su repetición sin cambios pasó. Un primer comando de práctica
usó una ruta de informe incorrecta; se corrigió el comando y ambas prácticas
pasaron. Se conservan los intentos fallidos y los resultados finales por separado.

## Pendientes para continuar

1. Dos PCs en redes independientes: pegar LMS1-, entrar, iniciar ronda,
   comprobar movimiento/voz y cerrar. Todavía no hay evidencia de P2P real
   entre dos identidades, relay forzado ni expulsión de un segundo usuario real.
2. Optimización: el objetivo de 1080p/60 FPS sostenidos todavía no se cumple
   en la escena de 16 actores medida. No se certifica GTX 1660 Ti ni 4K/60.
3. Revisión visual exhaustiva de todas las combinaciones de personalización
   y balance con jugadores humanos. Se difieren nuevas mejoras hasta retomar.

Publicar como prerelease `v0.9.0-rc.1`, preservando versiones históricas. No
presentarla como certificación WAN o versión estable. El producto EOS ya está
configurado; sus archivos locales están excluidos de Git, y el cliente Peer2Peer
se incluye en el EXE. No subir configuraciones administrativas ni el SDK fuente.
