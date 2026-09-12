# Revisión de entrega Gameplay `68353fe`

Entrega revisada: `68353fe776f4f4b3fcc164235fcabe8e1accb00a`, “Define Unity alfa gameplay authority contracts and migration acceptance”. Revisión estática de `docs/unity/gameplay/**`; no compila ni acredita runtime Unity.

Dictamen: **BLOCK para integrar el contrato sin corrección**. El alcance y la autoridad están bien orientados, pero falta la interfaz que permite cumplir el gate de puertas alfa.

## Hallazgos

### F1 — Alta — La autoridad no tiene contrato operativo de puerta

`INTERFACES.md` declara `ActionKind.Use`, `DoorChanged` y una colección `GameSessionState.Doors`, pero `IGameplayWorldQuery` sólo ofrece movimiento, superficie, picadura, golpe y recuperación. Tampoco define `DoorSnapshot`, identidad/revisión/ángulo de hoja o resultado de interacción.

Efecto: Gameplay no puede validar mirada, alcance, estado, ocupación y transición de una puerta mediante la interfaz acordada. La implementación tendría que acceder a objetos Unity/mapa fuera del límite, duplicar estado o dejar una hoja visual sin autoridad. U094-05 no resulta implementable de extremo a extremo.

Corrección requerida:

1. Definir una consulta/operación host de puerta con actor, origen/mira derivados por host, alcance, `DoorId`, revisión y resultado.
2. Definir `DoorSnapshot` mínimo con transform/ángulo o estado reproducible, objetivo, movimiento/bloqueo y revisión.
3. Fijar el lugar de la operación en el orden del tick y el responsable de mutar/publicar el estado.
4. Añadir gate positivo y mutante para abrir/cerrar, ocupado, cerrado/45°/abierto, hoja-collider y bloqueo de movimiento/cámara/LOS/golpe/picadura.

### F2 — Media — G10 aún no tiene un dominio finito auditable

G10 exige certificar “todo contacto habilitado” mediante un “dominio continuo”, pero no define cómo convertir las superficies del rig a una enumeración o cota reproducible. Es una dependencia válida de M1/W2, aunque no puede pasar con muestreo oportunista.

Antes de implementar el gate, el contrato de superficies debe fijar primitivas o malla, poses, resolución máxima de muestra/cota de cobertura y un mutante con región inaccesible. El reporte debe demostrar qué fracción/dominio fue cubierto y no sólo ocho centroides.

### F3 — Baja — Precisar el uso público del contacto actual

El `BiteAttachment` público con `AnatomicalSurfaceId` y punto local es necesario para representar al mosquito unido. Debe declarar de forma expresa que sólo describe contacto vigente para réplica visual, que no se entrega antes de la adhesión y que UI/bots no pueden convertirlo en marcador u objetivo futuro.

## Conformidades observadas

- Alcance restringido a Sangre/casa-patio/práctica; mecánicas beta/omega quedan fuera.
- Retira assignments, markers y rotación de zonas; adquisición nueva por contacto/LOS host.
- Cliente no suministra posición, víctima, punto de piel, impacto ni sangre autoritativos.
- PUID autenticado se vincula a `ActorId`; rondas/epochs/secuencias impiden reutilizar comandos viejos.
- Reloj host de 30 Hz, snapshots inmutables y presentación separada del resultado físico.
- Cámara humana libre, cuerpo/manos visibles, vuelo hacia la mira y cámara mosquito con colisión.
- Sangre, extracción, caída, ayuda y desmayo tienen perfil versionado y se identifican como hipótesis de balance.
- Bots usan comandos y observación filtrada; práctica reutiliza autoridad y crea `RoundId` nuevo.
- Las referencias Godot se presentan como intención histórica, nunca como evidencia Unity.
- `DOCUMENT-CHECK.json` identifica el Core leído por SHA-256 `0d183bce645a86bc34cbf0d62b282ca09390392be1ac03ae7cd60960c97b0af4` y declara correctamente su alcance documental.

## Condición de reverificación

Revisar el commit sucesor de W1 y confirmar F1 cerrado. F2 puede quedar como dependencia explícita PREPARADA hasta el contrato real de superficies; F3 requiere una aclaración breve. Ningún PASS documental sustituirá EditMode/PlayMode de U094-02…09 y U094-14.
