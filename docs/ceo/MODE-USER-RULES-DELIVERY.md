# MODE-USER-RULES — reglas personales de Tareas

Fuente: `definicion-v020/respuestas-20260920-065440/DECISIONES.md`, J13/J14/J15/D04/D05 y encargo CEO del 20/09/2026. Propiedad: `Gameplay/ModeRules.cs`, `Tests/EditMode/GameplayModeAuthorityTests.cs` e informe propio. No cambios a autoridad, contratos, mundo Unity ni codecs por este trabajador.

## Comportamiento

- Cadencia 40 s, plazo inicial 30 s, mínimo 15 s y meta `ceil(2/3)` conservados.
- Cada fallo resta 5 s al plazo personal futuro, con piso de 15 s. Cada éxito recupera 3 s hasta 30 s. El contador histórico de fallos sólo aumenta: recuperar plazo no borra ni modifica ese historial.
- El plazo personal efectivo se almacena separado del historial. Una secuencia 30 → fallo 25 → éxito 28 → fallo 23 no se recalcula como 20 a partir de dos fallos históricos.
- Un cambio de plazo no altera retroactivamente el vencimiento de la asignación ya emitida. La siguiente oportunidad usa el valor actualizado. Capturas privadas y eventos públicos conservan sus contratos y privacidad.
- Completar una asignación sólo recompensa una vez; no adelanta cadencia ni termina anticipadamente la ronda.

## Default CEO reversible para D04

La respuesta aprobó pérdida gradual tras gracia sin fijar cifras. CEO concretó 1 s de gracia y pérdida del 10% del trabajo total por segundo, únicamente durante interrupción. Se representan a 30 Hz como 30 ticks de gracia y 1000 puntos básicos por segundo.

La gracia es un presupuesto acumulado por asignación, consumido sólo al estar interrumpido con progreso positivo. Mantener trabajo válido suma progreso y no decae. Reanudar brevemente no repone gracia ni descarta decaimiento fraccionario acumulado; se evita eludir la pérdida pulsando y soltando. Una asignación nueva sí recibe nueva gracia. La pérdida se satura en cero sin underflow; al llegar a cero se descarta el residuo fraccionario, sin reponer gracia.

Una ruta authored temporalmente no disponible conserva la política previa no penalizante: congela progreso/gracia y extiende el plazo hasta el límite de la oportunidad. No reinicia el presupuesto ni reduce la meta colectiva. Obstáculos de interacción, soltar la acción, salir del radio o perder capacidad de trabajar sí cuentan como interrupción; la autoridad existente determina estas condiciones.

## Contrato de compatibilidad

`ModeRuleProfile` añade tres propiedades y parámetros opcionales al final del constructor:

```csharp
uint taskSuccessRecoveryTicks = 90,
uint taskInterruptionGraceTicks = 30,
uint taskDecayBasisPointsPerSecond = 1000
```

Los nombres públicos son `TaskSuccessRecoveryTicks`, `TaskInterruptionGraceTicks`, `TaskDecayBasisPointsPerSecond`. El valor por defecto de `taskFailurePenaltyTicks` cambia de 90 a 150. Los tres nuevos valores admiten cero para perfiles de prueba; recuperación/gracia están acotadas a 54000 y tasa a 10000 puntos básicos por segundo. Los perfiles online canónicos quedan sujetos a la validación del propietario de red.

`ModeRuleProfile.Hash` conserva el orden anterior y agrega recuperación, gracia y decaimiento al final. `GameplayRoundConfig.BalanceHash` los incorpora por composición. El parser de `GameplayWireCodec` debe esperar 16 componentes totales en vez de 13, y Begin de `OnlineGameplaySession` debe transportar los tres uint adicionales después de penalidad. CEO posee estas adaptaciones; no afirmar interoperabilidad hasta su prueba. `DeadlineFor(failures)` queda como cálculo hipotético de una secuencia de fallos sin éxitos para compatibilidad de API; TaskRules ya no lo usa para el plazo efectivo.

## Validación

Harness externo: `N:/LetMeSleep/Validation/V020/ModeUserRules`. Usa directamente las fuentes centrales reales de Gameplay/Core y los fixtures NUnit de autoridad; no reemplaza la implementación de TaskRules. Durante la ventana de importación se trabajó con copia temporal de los dos archivos propios; después de liberarla, las pruebas se aplicaron y ejecutaron contra archivos centrales.

Resultado CPU final: **39 PASS / 0 FAIL** (29 modos, 2 picadura, 8 puertas), compilación con **0 advertencias / 0 errores**. Siete tests nuevos cubren identidad de balance y límites, fallos/aciertos encadenados, piso/techo e historial, independencia personal, gracia/decaimiento, conservación de residuos entre pulsos, ruta temporalmente bloqueada y recompensa única. `compile.log`, `checks.log` y recibo con hashes en la carpeta externa. Prueba nativa pendiente del próximo turno CEO; no Unity ni aceptación online ejecutados por este trabajador.
