Let me sleep es un prototipo nativo para Windows de humanos contra mosquitos, con práctica local y salas privadas entre amigos.

Para jugar, descargá **Let-me-sleep-0.4.0-Windows.zip**, descomprimí toda la carpeta y abrí **Let-me-sleep.exe**. La guía **LEEME.html** explica los controles y cómo alojar una partida. El archivo **-fuentes.zip** contiene el proyecto Godot, no es necesario para jugar.

Esta entrega incorpora una casa de dos pisos con 16 ambientes y dos escaleras, vuelo hacia la mira con frenado al soltar, concentración mantenida para picar y defensa por cabeza/torso/piernas. Incluye Recolección de sangre, Supervivencia y Tareas; práctica contra bots, cosméticos, controles reasignables e invitación única.

Godot 4.5.2 · Windows x86_64 · UDP 27840 · protocolo 4. Los clientes 0.3 no pueden entrar a una sala 0.4. La invitación DD3 contiene dirección, puerto y sala; no abre puertos ni resuelve CGNAT.

El ZIP fue probado en Windows: práctica nativa 73/73, demo 8/8, plazos 59/59, encuentros 5/5, privacidad 24/24 y sesiones ENet locales, incluida una de 16 clientes. **No hay validación de Internet entre casas ni de diversión, balance o rendimiento en distintas PCs.** BUILD.txt y PRUEBAS.md detallan la evidencia.

Se publica la entrega 0.4 existente. Detectamos problemas en la confirmación de arranque del servidor, la dirección/puerto al crear y los mensajes de conexión; la versión 0.5 en desarrollo los corregirá. Esta publicación no soluciona todavía el timeout entre casas.

SHA256 de los paquetes:

```text
AA33AF5C7A4A540BF8D6586F4C7045FAEDF4546A5749CBA3AF7E44D6C06469FD  Let-me-sleep-0.4.0-Windows.zip
26434036162B8D3586EB3D0513147B2B9C6AF143FCA7BEA06D620393B3E03B3E  Let-me-sleep-0.4.0-fuentes.zip
```

El manifiesto adjunto relaciona paquetes, ejecutable y commits. Etiqueta de fuentes y documentación: `267219376b61b77e5fe47e371f3ff583e6273157`.
