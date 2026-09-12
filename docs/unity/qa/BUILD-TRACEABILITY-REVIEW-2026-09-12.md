# Auditoría de trazabilidad de evidencia — 2026-09-12

Dictamen: **la corrida Unity acredita sus 51 resultados; el build EOS limpio acredita build/arranque/host, pero ninguno identifica todavía una candidata reproducible sin cambios locales**.

## Build EOS limpio

El recibo `EOS-CLEAN-WINDOWS-BUILD-RECEIPT.json` integrado inicialmente declara `sourceBase: 5ae08d3`. Esa atribución es incorrecta:

- `N:/LetMeSleep/Artifacts/eos-probe-clean` fue creado entre `2026-09-12T10:05:12Z` y `10:05:32Z`; `build-result.txt` contiene `Succeeded:0`.
- El reflog del worktree principal fija `HEAD` en `814265cac3b6d6f06bc91b791645d01b66cd9e81` desde `10:03:26Z` hasta `10:08:02Z`.
- `26228a2` entró a las `10:08:02Z` y `5ae08d3` a las `10:14:16Z`, después del build. Ninguno puede ser su base.
- El artefacto contiene `LetMeSleep.Online.dll`, mientras `814265c` aún no versionaba `unity/Assets/LetMeSleep/Online/**`. La fuente del build estaba modificada y no quedó sellada por ese commit.

La corrección mínima del recibo es:

```json
{
  "sourceBase": "814265cac3b6d6f06bc91b791645d01b66cd9e81",
  "sourceDirty": true,
  "sourceCommit": null
}
```

`sourceBase` describe el `HEAD` comprobado durante la generación. `sourceCommit` debe permanecer nulo porque el conjunto exacto de fuentes locales no está identificado por hash o commit. El build no se atribuye retrospectivamente a `26228a2` aunque parte de esos archivos se haya confirmado después.

## Unity EditMode integrado

`ALFA-INTEGRATED-UNITY-TESTS-20260912.json` registra **51 total, 51 pass, 0 fallos, 0 omitidos**. El recibo se emitió a `2026-09-12T10:16:23Z` con `HEAD 5ae08d3` y declara cambios runtime no confirmados de `RoomWireCodec` y `OnlineRoomCoordinator`. Es evidencia válida de la ejecución y sus resultados, incluida la suite QA previa de 29 casos. No demuestra por sí sola que un checkout exacto de `5ae08d3` produzca el mismo resultado.

Los cambios declarados fueron versionados después en `493151a`, pero falta una nueva corrida limpia en ese commit o uno posterior para cerrar la relación fuente–resultado. Los cuatro casos `GameplayReplicaStateGateTests` agregados luego tampoco están incluidos en los 51.

## Alcance

- PASS parcial: Unity EditMode 51/51 sobre la fuente local declarada; build Windows limpio 0 errores; helper overlay excluido; arranque y host EOS.
- BLOCK: fuente candidata limpia y reproducible, transporte entre dos identidades, relay/WAN, PlayMode, física/visual, rendimiento y launcher.
