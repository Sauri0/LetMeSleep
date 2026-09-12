# Human reference9 — fuente, seis vistas y auditorías numéricas

Geometría de `27e7195`, Blender 5.2.1 LTS, generada exclusivamente en `characters` con turno del Director. **No aprobado artísticamente.** El revisor independiente comparó las seis imágenes y mantiene defectos de cintura/cadera, caída del pijama, boca/nuca y acabado de mano/calzado; informe `N:/LetMeSleep/Validation/TeamRecovery/visual/HUMAN-REFERENCE9-REVIEW-20260912.md`.

## Identidad entregada

- `human/LMS_Human_alpha.blend`: `d0a1a3042171b5320761a2ef30a26be2368c35e39154367ee59398156cc6c615`.
- `human/LMS_Human_alpha.fbx`: `348c0f48f5247102a8c873ff5f9d131a06e6e260421262185e24bf3a9db5a46d`.
- 7.980 triángulos, 65 huesos, 15 clips. Audit geométrico sin pesos inválidos, vértices no finitos ni triángulos degenerados.
- `review/human-reference9/`: front, three_quarter, face_front, face_profile, hand_open, slipper; seis PNG abiertos y seis recibos con el SHA de esta fuente. Cycles CPU2, 16 muestras, 576×720.
- `human/fbx_roundtrip.json`, `human/motion_audit.json` y `human/motion_gate.json`: **reports actuales de Human únicamente**. Los reports combinados en la raíz permanecen históricos; no acreditan este humano ni deben usarse para mezclar PASS de especies.
- `HUMAN-REFERENCE9-EVIDENCE.json`: identidad, procesos, alcance, recibos y señales de deformación. Logs/stdout/stderr/exit en `evidence/reference9/`.

## Ejecución y alcance

| Etapa | PID | Segundos | Salida |
|---|---:|---:|---:|
| Generación Human | 36224 | 14,787 | 0 |
| Seis vistas | 35940 | 54,539 | 0 |
| Roundtrip Human | 28156 | 3,467 | 0 |
| Auditoría todos los frames Human | 37184 | 37,067 | 0 |

109,860 segundos de procesos nativos, ejecutados secuencialmente, CPU2 BelowNormal. Turno liberado al Director y todos los procesos propios terminados. Los seis archivos/audits de mosquito y matamoscas conservaron sus SHA. No se abrió Unity ni se generaron variantes adicionales.

Roundtrip PASS: dimensiones, triángulos, 65 huesos, sockets y acciones conservados. Motion gate numérico PASS: 30 evaluaciones fuente/FBX, 1.278 muestras con cada frame entero, checkpoints y Clap23,5. Diferencia máxima de posición ósea fuente/FBX: 0,000000945 m. Jaw Hit desciende 10,877 mm en espacio Head; Blink conserva 8,073 % de altura al cerrar. Floor, Root, loops y desplazamiento de dedos pasan sus gates existentes. El report incluye secuencias Jaw mixtas y casos Swat en espacio Hand.

## Señales abiertas de deformación — no cubiertas por el PASS numérico

| Clip fuente | Malla / arista | Fase | Estiramiento | Longitud original → deformada |
|---|---|---:|---:|---|
| FingerCurl | HandSkin.L / 150–189 | 0,266667 | **3,334443×** | 4,417 → 14,727 mm |
| Swat | HandSkin.R / 790–751 | 0,066667 | **2,997197×** | 3,154 → 9,453 mm |
| Clap | HumanBody / 37–49 | 0,666667 | 1,933079× | 43,822 → 84,711 mm |
| Crouch | HumanBody / 197–209 | 0,433333 | 1,989767× | 40,112 → 79,814 mm |

`worst_stretched_edge` guarda extremos deformados y pesos para localizar cada señal. El gate no tiene umbral de aceptación de estiramiento: pasar otros controles **no resuelve estas señales**. Director pidió corregir pesos/topología/curvas responsables de los dos casos de mano sin elevar tolerancias; siguiente trabajo sólo de fuente hasta otro turno. Se coordinó con Revisar animaciones.

Faltan clips visuales completos, agarre con matamoscas real, piel/contacto de palmada, revisión temporal de labios/cavidad, otras vistas de cuerpo/manos/abertura de pantufla y reproducción Unity del mismo hash. Las seis imágenes son fases 0 de Idle/FingerCurl, no prueba de cierre, movimiento o grip. El revisor visual cierra únicamente el recorte de pantufla y la separación insuficiente de dedos observada en la vista anterior; no hay cierre global H1–H5.

## Integración y siguiente delta

Integrar esta revisión como un conjunto de fuente/FBX/audit/recibos. Los selectores `--species Human` añadidos a verify_fbx/audit_motion/check_motion_audit permiten repetir sólo esta especie y escribir reports bajo `human/`. No se selló la colección completa ni se declaró verificación Unity.

Próximo delta: combinar el informe visual reference9 con corrección fundamentada de la deformación de manos. Sin cambios cosméticos, sin modificar mosquito/herramienta, sin Blender hasta nueva concesión. El dueño Mosquitos mantiene sus módulos más recientes; este lote no cambia su import ni sus archivos.
