# Mosquito planar R3: entrega nativa para integrar

**Candidato validado técnicamente en Blender 5.2.1 LTS, CPU dos hilos. No aprobado artísticamente ni ejecutado en Unity.** Director concedió generación/vistas/auditoría y una iteración R3 acotada a alas/paleta. Todos los procesos terminaron; se verificó CIM sin Blender/Unity al devolver el turno. No se publicaron assets ni se inició otra etapa.

## Identidad y resultados

| Artefacto final | SHA-256 |
|---|---|
| `mosquito/LMS_Mosquito_alpha.blend` | `0d3fe4436319a63c0d7177630f681ed64d7fc86726b962d4a7ee917e38d2c933` |
| `mosquito/LMS_Mosquito_alpha.fbx` | `0b32d7623a70259d2f9c7bed2262bd37a0296296d1cd6f175babd3df1de33abb` |
| `mosquito/audit.json` | `f8b2c7f8b55e02daca1f04d15f6090da9a2338e13c24068f7efaf63f6a37f752` |

Rutas relativas a `art_source/unity/characters/`. El recibo `NATIVE-VALIDATION-20260912.json` contiene hashes completos de módulos, renderer, outputs, PNG y auditorías; comparación de los seis archivos de humano/herramienta antes/después; igualdad AST de los seis helpers compartidos frente al builder del Director al verificar; y PIDs/horarios/salidas de todos los pasos.

- Geometría: 3 renderers, 33 huesos, 2116 triángulos y1158 vértices; cero vértices sin peso/sumas incorrectas/no finitos o triángulos degenerados.
- Temporal/roundtrip: **894 muestras**, 15 clips en cada formato,30 marcadores de malla y20 filas de empalmes. PASS; máxima diferencia de marcador source/FBX **0.0000007033 m fuente** y orientación **0.000145°**. Máximo salto de marcador en los endpoints comprobados:0.0000001184m fuente.
- Soporte: **636 muestras**, piso/pared/techo bajo la orientación acordada y offsets Root .056/.057m. PASS con mínimo3 patas apoyadas durante SurfaceWalk; peor clearance mundial -0.000865m, dentro del umbral existente1.5mm. Esto aplica transformaciones al modelo; no ejecuta adquisición/seguimiento real de superficies del juego.
- Root y7 sockets iguales a la base c888d96 dentro de1e-7m fuente, padres iguales; rig completo R2/R3 idéntico. Radio.055, escala.5, Mouth Unity[0,0,.095], GroundContact[0,-.057,0] conservados. Seis archivos humano/herramienta conservan hash exacto.
- SurfaceWalk mantiene contrato **D=.100m Unity/ciclo**, stride.116m fuente, duty.58, T1s (1–31 a30FPS), velocidad de juego.65m/s y cadencia nominal6.5Hz. Presentation27710c8 depende de este contrato; verificar local/remoto con SurfaceVisualProbe.
- Checks livianos finales: fuente PASS, estudio de nueve pares distancia/duty PASS y cuatro regresiones matemáticas del auditor PASS. Son evidencia separada de la ejecución Blender.

El auditor temporal muestrea frames enteros. Empalmes comparan último frame contra primero, con orientación de deformación y marcadores reales; no prueban salidas arbitrarias de loop ni blends de Unity. El auditor de soporte existente añade17 fases normalizadas de sus clips de apoyo, incluido muestreo fraccional donde corresponde. No hay videos nativos completos ni aceptación visual por pasar estos gates.

## Vistas y límites

R3 final tiene tres imágenes nuevas en `review/mosquito-planar-r3`: **front**, **three_quarter** y **wing_transmission**, Cycles CPU2,640×640,8 samples, Idle frame1. Se abrieron las tres. La membrana tiene plano transversal más visible de frente; la muestra sobre tablero permite ver transmisión. No equivale al material/culling/luz de URP.

R2 conserva diez vistas en `review/mosquito-planar-r2` a768×768,12 samples, incluyendo ambos perfiles, espalda y detalles. Su fuente es otro hash, **8f4a249f84e56c6b7b097aef8a16d6087c2c0b40d84c939d97bd16b441324759**; no atribuirlas al R3. Blend/FBX/audit/módulos originales R2 están conservados en `N:/LetMeSleep/Worktrees/mosquito/work/mosquito-candidate/r2/`. R3 conserva su rig/proporciones y modifica alas/paleta, pero sus perfiles/espalda finales no fueron recapturados. Se cerró el lote según el turno acotado del Director.

Revisión independiente R2: `N:/LetMeSleep/Validation/TeamRecovery/visual/MOSQUITO-PLANAR-R2-REVIEW-20260912.md`. Reconoce mejora sustancial de silueta alta, probóscide y transmisión; los ojos saltones corresponden al referente. **P1 abierto: cruces en X de patas en frente/legs_front.** Comprobar escala real y movimiento en visor/entorno antes de cambiar patas. P2: ceja/cabeza, jerarquía abdominal y planos de alas. La paleta entre Unity viejo y Cycles no es directamente comparable; R3 es una elección de fuente pendiente de aceptación. No se alargaron otra vez patas, abdomen o probóscide.

## Correcciones durante este turno

La primera invocación del auditor falló al importar el módulo: se añadió su propia ruta a sys.path. Luego el auditor detectó saltos de unos48mm fuente y21.2° al entrar/salir de Fly. Se corrigió el vuelo con envolvente suave y pose aérea común en extremos; Fly y Hover mantienen comportamiento distinto dentro del ciclo. También se ajustaron abdomen en Perch/Detach y transición Brake. Se repitieron generación, temporal/roundtrip y soporte; no se relajaron tolerancias. Los recibos fallidos y los procesos correspondientes se conservan en `work/mosquito-candidate/r1`.

R2: generate36100, audit20076, surface33640, render15028, todos exit0. R3: generate9848, audit31084, surface37984 y render25824, todos exit0. Render produce sólo avisos de deprecación `use_nodes` para Blender6.0; no errores. Ningún proceso nativo propio quedó activo al devolver el turno.

## Integración exacta, sin sobrescribir humano9

Conservar `build_characters.py`, `author_motion.py`, manifiesto y auditores comunes/Human-only del Director. El candidato se generó con helpers que resultaron AST idénticos a los seis helpers centrales: Character/material/mesh/tube/ellipsoid/strip. Sólo hace falta conectar el **import dentro del wrapper mosquito** a la implementación propia:

```python
from author_mosquito_geometry import create_mosquito
from author_mosquito_motion import mosquito as animate_mosquito

c = create_mosquito(Character=Character, material=material, tube=tube,
                    ellipsoid=ellipsoid, strip=strip, mesh=mesh)
animate_mosquito(c)
return c.export()
```

No tocar wrapper/imports humanos. `author_mosquito_motion.mosquito(c)` importa helpers comunes dentro de la función; no necesita sustituir el archivo compartido. El Director reconcilia el manifest por las fuentes/outputs reales y actualiza sólo la entrada/exports mosquito. No copiar el manifest antiguo de este worktree ni regenerar otra especie para sellarlo.

Archivos propios de ejecución/autoría, todos bajo `art_source/unity/characters/`:

- `author_mosquito_geometry.py`, `author_mosquito_motion.py`.
- `build_mosquito_candidate.py`, `audit_mosquito_candidate.py`, `render_mosquito_witness.py`.
- `check_mosquito_source.py`, `check_mosquito_audit_math.py`, `study_mosquito_surface.py`.
- `mosquito/LMS_Mosquito_alpha.blend`, `mosquito/LMS_Mosquito_alpha.fbx`, `mosquito/audit.json`, `mosquito/candidate.json`, `mosquito/candidate_motion_audit.json`, `mosquito/candidate_surface_support_audit.json`.
- `review/mosquito-planar-r2/` (10 PNG + witness) y `review/mosquito-planar-r3/` (3 PNG + witness).

Documentos propios en `docs/unity/mosquito/`: receta, SOURCE-CHECK, SURFACE-STUDY, esta entrega y NATIVE-VALIDATION. El output común `surface_support_audit.json` se copió a la carpeta mosquito y se restauró únicamente el delta generado por este turno; no se entrega para sobrescribir otro informe central. Logs/archivos intermedios quedan locales bajo `work/mosquito-candidate/`, con exclusión limitada a esa carpeta.

Siguiente dependencia: Director importa en Unity y revisores verifican visor/entorno a escala, reproducción completa, contacto/bite, fase y blend local/remoto. R3 no es cierre artístico ni funcional, y el P1 de patas se reserva para el siguiente lote coordinado.
