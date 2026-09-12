# Suite Core RoomSession — Unity 0.9.4 alfa

Estado: tests EditMode implementados; ejecución oficial con Unity Test Runner pendiente del Director. Fuente bajo prueba: bootstrap original `0d6c3fa8b1ddb7173570de67c67ea5b900fc5e83`, integrado en la rama QA como `e1fc1ba`.

## Cobertura independiente

La asamblea `LetMeSleep.Tests.EditMode` referencia sólo `LetMeSleep.Core`, usa `optionalUnityReferences: TestAssemblies`, se limita a Editor y no referencia UnityEngine.

Los 19 casos cubren:

- protocolo exacto, identidades/nombres inválidos, duplicados y no mutación tras rechazo;
- permisos de dueño/invitado y transiciones Waiting → Playing → Results → Waiting;
- requisito de dos participantes listos y rechazo de incorporación a mitad de ronda;
- reglas finitas/acotadas, mapa alfa único y conservación de ready ante regla inválida;
- invalidación de todos los ready al cambiar reglas válidas;
- capacidad exacta 16 y rechazo del participante 17;
- cantidad humana fija sin clamp y automática entre 1–5 con ambos equipos;
- dos participantes: cualquiera puede resultar humano; una nueva lotería puede cambiar o repetir roles según RNG, sin alternancia forzada;
- cierre irreversible al salir dueño y continuidad de fase al salir invitado para que Gameplay resuelva el resultado;
- vistas anteriores inmutables, roster de sólo lectura, snapshots nuevos y revisión monotónica sólo ante mutaciones.

Los casos usan entradas inválidas y RNG controlado para distinguir regresiones; no inspeccionan campos privados ni duplican la implementación de Fisher-Yates.

## Verificación realizada sin abrir Unity

Se compiló `RoomSession.cs` y la suite contra el `nunit.framework.dll` resuelto por el proyecto importado, usando .NET SDK 10 como harness externo. Los 19 métodos NUnit terminaron sin fallos. Este resultado comprueba sintaxis y comportamiento C# puro; se etiqueta `unity_runner=false` y no sustituye la corrida EditMode del proyecto.

## Corrida oficial pendiente

Después de integrar el commit QA en el repositorio principal, el Director ejecuta Unity Test Runner en batchmode para la asamblea `LetMeSleep.Tests.EditMode`, conserva XML/log y registra commit, versión exacta `6000.3.24f1`, código de proceso y stderr. Un fallo se devuelve a Core; QA no modifica `RoomSession.cs`.

No se observaron defectos de Core en el harness externo. Esta suite acredita sólo U094-01, U094-10 y U094-12 en su parte de dominio local cuando la corrida Unity pase; no acredita UI, EOS, WAN, gameplay, render o build.
