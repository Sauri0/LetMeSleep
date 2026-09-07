extends SceneTree

func _initialize() -> void:
	var destination := "res://../outputs/Let-me-sleep-0.4.0-Windows/LICENCIAS-GODOT.txt"
	var text := "Let me sleep 0.4.0 — Godot 4.5.2 estable\n\n"
	text += "Arte, sonidos y código de juego originales del prototipo.\nNo se han incorporado recursos de otros juegos.\nFuentes externas Bangers y Atkinson Hyperlegible: créditos y licencias OFL en Licencias-fuentes/.\n\nMOTOR GODOT\n\n"
	text += Engine.get_license_text() + "\n\nCOMPONENTES INCLUIDOS POR GODOT\n\n"
	for info: Dictionary in Engine.get_copyright_info():
		text += str(info) + "\n\n"
	for key: String in Engine.get_license_info():
		text += key + "\n" + str(Engine.get_license_info()[key]) + "\n\n"
	var file := FileAccess.open(destination, FileAccess.WRITE)
	file.store_string(text)
	print("LICENSES_WRITTEN bytes=" + str(text.length()))
	quit()
