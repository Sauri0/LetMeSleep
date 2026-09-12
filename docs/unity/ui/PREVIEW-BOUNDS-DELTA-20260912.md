# Visor: encuadre por bounds y especie

Delta solicitado por Director tras UI3. Solo cambia `CharacterPreviewOrbit.cs`; no modifica modelos, escala, Bootstrap, luces ni Presentation global.

- Mosquito abre a 35 grados (tres cuartos); humano conserva frente. Centrar restaura el ángulo inicial de la especie y zoom 1. Frente/Perfil/Espalda siguen siendo 0/90/180 grados.
- Guarda las ocho esquinas de los bounds locales de cada Renderer habilitado, transformadas al stage. Centra sobre su unión. Evita el exceso de un único AABB mundial rotado otra vez.
- Calcula distancia por esquina y perspectiva para el ángulo actual, con margen del 10% sobre la proyección. Reajusta al girar conservando zoom relativo; Centrar mantiene el modelo completo por bounds, incluso al cambiar de frente a perfil.
- Sustituye mínimos métricos fijos de distancia/radio/rueda por proporciones del personaje. Near clip de la cámara exclusiva del visor: 2% del radio, acotado a 0.0001–0.01. No cambia cámara de partida ni escala de prefabs.
- Zoom conserva rango relativo 0.55–2.2, limitado además por plano cercano. Acercar puede recortar el personaje por elección del usuario; Centrar recupera encuadre completo. La rueda avanza 8% de la distancia de encuadre por paso.
- Cámara continúa a nivel para conservar vistas ortogonales de orientación (perspectiva, sin inclinación añadida); usa aspect de la RenderTexture.

## Comprobación realizada

Compilación externa del conjunto UI contra assemblies Unity 6000.3.24f1 y dependencias del repositorio central: 0 errores / 0 advertencias. `N:/LetMeSleep/Validation/UI-PreviewBounds-20260912/compile.log`. No se abrió Unity ni se ejecutó render. Diff de fuente sin errores de whitespace.

## Comprobación pendiente con slot del Director

En capturas 1280×720 y 1920×1080 registrar versión del modelo y revisar: entrada en Humano, cambio a Mosquito (35 grados), Frente/Perfil/Espalda, giro completo, ambos extremos de zoom, Centrar, cambio repetido de especie y reentrada al personalizador. En vista centrada comprobar ojos, probóscide, puntas de alas y seis patas dentro del visor. Zoom debe permitir inspección sin cortar por near clip.

La captura debe confirmar mejora de tamaño y lectura; la compilación no prueba calidad visual. Bounds se obtienen al instanciar cada especie: poses animadas que excedan los bounds del export requieren revisión con el modelo final. La iluminación actual viene de CustomizationKey en Bootstrap; si la nueva captura sigue oscura, coordinar ajuste de luz exclusiva del visor con Director/Presentation, sin modificar luces globales desde UI.
