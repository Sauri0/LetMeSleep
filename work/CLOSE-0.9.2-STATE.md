# Cierre con cupo limitado

Prioridad de Branko: publicar ZIP 0.9.2 completo y probado antes de gastar saldo
en la siguiente versión (interpretada como0.9.3). El último chequeo de cuenta
mostró32% semanal disponible. Los trabajos opcionales están pausados.

## Integración 0.9.1

Rama codex/0.9.1-video-polish, repositorio dejame-dormir. Todavía no hay EXE nuevo
exportado/publicado. Última descarga pública:0.9.0-rc.1.

- Corpus corregido de1000semillas, repetido en procesos nuevos: PASS;1000layouts
  únicos. Evidencia work/director091-corpus-r2/summary.json. Fix mobiliario5218132.
- Build2 en79d8b10 pasó TODA la fase headless y primeras nativas. Falló luego
  actor09_legacy_geometry_test porque comparaba presentación anterior con nueva.
- Fix aislado del fixture integrado5bf9fa1 (Worker1 74e36c9); aún falta ejecutarlo.
- work/resume-native091.ps1 permite repetir las nativas y exportar reutilizando
  headless únicamente si el diff de fuente respecto79d8b10 contiene ese fixture
  nativo y nada más. Verifica transcript y registra provenance explícita.
  No usar para0.9.2. Después faltan pruebasEXE, recaptura, paquete/hash/GitHub.
- No sobrescribir paquetes históricos. No declarar WAN probado; hostEOS real y
  pruebas locales sí pasaron. El usuario probará otra red después del pulido.
- work/voice09-acoustics-results.json tiene edición ajena anterior: preservar.
  Backup exacto work/director091-voice-results-prebuild.json. Los wrappers del
  Director restauran esa copia después de cada build; no commitear esa edición.

## Commits preparados para0.9.2, todavía separados

- Modelador1, lms092-house: dbef5bb,bd9dcf7,66aa8eb,74671ae,ad4dcd1,16a13f8.
  Import pasó. Primer contracts tuvo errores de tipo pese al contador42/0;
  16a13f8 corrige tipado. Pendientes contracts,structure,furnishing seed1 y luego
  verificación de mapas/rutas. No añadir reglas nuevas.
- Modelador2, lms091-characters: facial1e2f828+1cf4cad, aprobado visual y nativo.
  Mano palmar en preparación: reautoría distal/20huesos,IDs/longitudes/handframe
  preservados, versiónLMS092.palm1, espesor palmar local a18mm. Debe eliminar
  inversiones nuevas de triángulos, verificar contacto y entregar commit.
- Worker1: b138a99,e761261,889fecf (runtime de palma/agarre y evidencia), más
  contratos e600299,9d4fe2e. Integrar sólo junto a la piel palmar deModelador2.
  Runtime pasó; la piel anterior corregida sólo por poses produjo74caras
  invertidas, por eso se reautoriza el rest. Worker1 queda inactivo salvo fallo.
- UI: c1fc717 acompaña los rostros. UI queda inactivo.
- QA:9811809+c70a7fc preparan gate físico de accesos funcionales v3. Sin ejecutar.
  QA queda inactivo salvo fallo concreto. Expectativa de riglegacy habrá que
  actualizar aLMS092.palm1 al integrar, conservando invariantes.
- Worker2: experimentos de sombras/contactos quedan para después de publicar,
  sin runtime/default. Referencias en work/AFTER-0.9.2.md. Inactivo.

Una sola reserva de motor. M2 tiene turno de exportación palmar; se le pidió
liberar si aún edita. Siguiente M1 para contracts→structure→furnishing y luego
Director para fixture nativo/fin091. Confirmar liberación explícita antes de
iniciar; no inferirla sólo de Get-Process.
