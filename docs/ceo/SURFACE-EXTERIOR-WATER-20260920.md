# Superficies exteriores y pozo: resultados nativos

Unity 6000.3.24f1, checkout aislado `a654d8b`. Comparación con central `b645a52`:
sin diferencias Git en Gameplay, Gameplay.Unity y Content/Environment. Ningún
cambio de política ni física del runtime en estos ensayos.

## Camp: tres rutas exteriores pasan

`N:/LetMeSleep/Validation/V020/SurfaceCampExterior01/native-results-02/surface-camp-exterior.json`
registra PASS_SCOPED 3/3, errors vacío, cleanup true. Cooler y ambas crates cruzan
una esquina vertical entre caras laterales 4→11, a media altura. Los centros
iniciales están 57 mm fuera de sus planos; corredores y muestras de recorrido
no presentan overlap de esfera de 54 mm. Cada caso conserva apoyo, registra una
transición y 57 muestras.

Estos casos nuevos no reemplazan los tres fallos Frozen85 que empezaban dentro
del sólido. No demuestran todas las esquinas ni corrigen orientación de las caras
inferiores. Native01 fue un fallo de invocación por salida preexistente; queda
preservado sin contar como prueba de gameplay. Detalles/hashes en REPORT.md externo.

## Pozo: desprendimiento correcto, recuperación pendiente

`N:/LetMeSleep/Validation/V020/WellWaterAuthority01/native-results-02/result.json`
registra FAIL, cleanup true y fuentes originales sin cambios. Conserva el origen
Frozen `(0, 3.46999931, 3)`, un PerchToggle y hasta 30 ticks de autoridad real.

- El ray selecciona Plaza_ContinuousPaving, superficie 1000246.
- Ticks 1 y 2: ApproachingSurface.
- Tick 3: BoundsRecoveryReported = NoSafeDestination; Flying, sin attachment,
  posición Y=3.404999.
- Hasta tick 30 sigue ahí. No aparece Surface sobre el paving, pero tampoco
  Recovered. Por eso no se acepta recuperación completa ni se cambia FAIL a PASS.

Native01 falló antes de física al navegar un JProperty del JSON. CEO preservó
fuente/DLL v1 y corrigió sólo la comprobación del ancestro mapa en v2; compila
sin errores/advertencias. Native02 es el primer ensayo físico de este ticket.

Siguiente diagnóstico: distinguir configuración del Fixture de ausencia efectiva
de destino seguro en el mapa, registrando candidatos y motivos de rechazo. No
afirmar todavía defecto confirmado del producto ni cambiar tolerancias. Propiedad
de diagnóstico externo: continuidad_tecnica. Frozen85 permanece intacto.
