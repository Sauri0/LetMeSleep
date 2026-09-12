# Presentación y audio 0.9.4/alfa

Este directorio define la receta que debe convertir la primera muestra Unity de
Let me sleep en una escena nocturna legible y medible. Es un contrato de
integración para Unity 6000.3.24f1; todavía no constituye un preset importado,
un bake, una captura ni una medición en GTX 1660 Ti.

Documentos:

- [URP-ALFA.md](URP-ALFA.md): iluminación, sombras, reflejos, materiales,
  transparencias, cámaras y perfiles visuales.
- [PRESENTATION-CONTRACTS.md](PRESENTATION-CONTRACTS.md): entregas exactas de
  Modelador 1, Modelador 2, Worker 1 y Director.
- [AUDIO-ALFA.md](AUDIO-ALFA.md): buses, snapshots, eventos, mezcla, música y
  presets de importación.
- [PERFORMANCE-BUDGET.md](PERFORMANCE-BUDGET.md): presupuesto y protocolo de
  medición para el hardware objetivo.
- [IMPLEMENTATION.md](IMPLEMENTATION.md): contenido del primer lote ejecutable,
  comando del builder y punto de integración del AudioMixer.
- [alfa-presentation-presets.json](alfa-presentation-presets.json): copia
  legible por herramientas de los valores de partida. No sustituye assets
  `.asset`, `.renderer`, `.mixer` o `.prefab` serializados por Unity.

## Orden de integración

1. Director crea el proyecto y fija URP, paquetes, ensamblados, capas y presets.
2. Modeladores entregan una habitación, puerta, humano, mosquito y props con
   los contratos de esta carpeta.
3. Worker 1 publica el estado de presentación y los eventos de audio sin dar a
   Presentation autoridad sobre reglas o movimiento.
4. Worker 2 conecta Animator, cámara, materiales, luces y mezcla en una escena
   de muestra alfa.
5. Se hace bake de iluminación y reflejos; luego se ejecutan capturas de los
   ángulos definidos en `PERFORMANCE-BUDGET.md`.
6. Branko revisa frente, perfil, espalda y movimiento. Sus observaciones se
   corrigen antes de producir variantes.
7. El perfil de GTX 1660 Ti se ejecuta en un build Development sin límite de
   FPS. El preset predeterminado sólo se acepta cuando cumple mediana y P95.

## Supuestos resueltos para alfa

- Windows x64 y Direct3D 11 son la primera ruta de validación. Direct3D 12 se
  mide por separado antes de cambiar el valor predeterminado.
- La escena alfa es casa con patio de noche y lobby privado separado.
- Sangre es el único modo jugable en alfa. Los buses y eventos no adelantan
  voz espacial, inventario, herramientas múltiples ni contenido beta.
- El humano local usa una cámara única con cuerpo visible y cabeza local
  oculta. El mosquito usa tercera persona con colisión de cámara.
- Gameplay conserva transformaciones, colisiones y tiempos autoritativos.
  Presentation interpola y reproduce; nunca confirma golpes ni picaduras.
- El juego no limita FPS por defecto. VSync y límites configurables pertenecen
  a ajustes, no a los presets de calidad de esta carpeta.

## Fuentes primarias

- [URP en Unity 6](https://docs.unity3d.com/6000.3/Documentation/Manual/universal-render-pipeline.html)
- [Comparación de rutas URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/rendering-paths-comparison.html)
- [Asset de URP y sombras](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html)
- [Antialiasing en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/anti-aliasing.html)
- [Reflection Probes en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/reflection-probes-introduction.html)
- [AudioSource](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioSource.html)
- [Audio Mixer](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioMixer.html)
