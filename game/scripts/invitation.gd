class_name Invitation
extends RefCounted
## Versioned address envelope, not encryption or authentication.
const PREFIX := "DD5-"
const PROTOCOL := 12
const MAX_LENGTH := 512

static func validate_host(value: String) -> String:
	var host := value.strip_edges()
	if host.is_empty() or host.length() > 253:
		return "Escribí la dirección que usarán tus amigos."
	if host != value or host.contains("/") or host.contains("\\") or host.contains("@") or host.contains(" "):
		return "Usá una IP o nombre de servidor, sin enlaces ni espacios."
	if host.is_valid_ip_address():
		if host == "::1" or host == "::" or host == "0.0.0.0" or host.begins_with("127."):
			return "Esa dirección apunta a esta PC. Usá la IP de red o pública para compartir."
		return ""
	if host.to_lower() == "localhost" or host.to_lower().ends_with(".localhost"):
		return "localhost no sirve para invitar a otra computadora."
	var label_pattern := RegEx.new()
	label_pattern.compile("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")
	for label: String in host.split("."):
		if label_pattern.search(label) == null:
			return "La dirección no tiene un formato válido."
	# Reject malformed dotted numeric IPs instead of treating them as DNS names.
	var numeric := RegEx.new()
	numeric.compile("^[0-9.]+$")
	if numeric.search(host) != null:
		return "Revisá los números de la dirección IP."
	return ""

static func _room_ok(room: String) -> bool:
	var pattern := RegEx.new()
	pattern.compile("^[A-Z0-9]{6}$")
	return pattern.search(room) != null

static func classify_host(value: String) -> String:
	var host := value.strip_edges().to_lower()
	if host.begins_with("::ffff:") and host.substr(7).is_valid_ip_address():
		return classify_host(host.substr(7))
	if host == "localhost" or host.ends_with(".localhost") or host == "::1":
		return "loopback"
	if host.is_valid_ip_address():
		if host.contains(":"):
			if host == "::": return "unspecified"
			if host.begins_with("fc") or host.begins_with("fd"): return "private"
			if host.begins_with("fe8") or host.begins_with("fe9") or host.begins_with("fea") or host.begins_with("feb"): return "link_local"
			if host.begins_with("ff"): return "multicast"
			if host.begins_with("2001:db8:"): return "reserved"
			return "public_ipv6"
		var parts := host.split(".")
		var a := int(parts[0])
		var b := int(parts[1])
		var c := int(parts[2])
		if a == 127: return "loopback"
		if a == 0: return "unspecified"
		if a == 10 or (a == 172 and b >= 16 and b <= 31) or (a == 192 and b == 168): return "private"
		if a == 169 and b == 254: return "link_local"
		if a == 100 and b >= 64 and b <= 127: return "shared"
		if a >= 224: return "multicast"
		if (a == 192 and b == 0 and c == 2) or (a == 198 and b == 51 and c == 100) or (a == 203 and b == 0 and c == 113) or (a == 198 and b in [18,19]): return "reserved"
		return "public_ipv4"
	return "local_name" if not host.contains(".") or host.ends_with(".local") else "hostname"

static func validate_sharing_address(host: String, scope: String) -> String:
	var error := validate_host(host)
	if not error.is_empty(): return error
	if scope not in ["lan", "internet", "virtual"]: return "Elegí cómo se conectan tus amigos."
	var kind := classify_host(host)
	if kind in ["loopback", "unspecified", "multicast", "link_local", "reserved"]:
		return "Esa dirección no sirve para compartir una sala."
	if scope == "internet" and kind in ["private", "shared", "local_name"]:
		return "Esa dirección no es de Internet pública. Para otra casa usá un endpoint público, o elegí una red virtual compartida."
	return ""

static func encode(host: String, port: int, room: String, scope: String = "") -> String:
	if not validate_host(host).is_empty() or port < 1024 or port > 65535 or not _room_ok(room):
		return ""
	var data := {"v":1, "protocol":PROTOCOL, "host":host, "port":port, "room":room}
	if not scope.is_empty():
		if not validate_sharing_address(host, scope).is_empty(): return ""
		data["scope"] = scope
	return PREFIX + Marshalls.raw_to_base64(JSON.stringify(data).to_utf8_buffer()).replace("+", "-").replace("/", "_").trim_suffix("=").trim_suffix("=")

static func decode(value: String) -> Dictionary:
	var text := value.strip_edges()
	if text.length() > MAX_LENGTH:
		return {"ok":false, "error":"La invitación es demasiado larga."}
	if not text.begins_with(PREFIX):
		if text.begins_with("DD3-") or text.begins_with("DD4-"):return {"ok":false,"error":"Esta invitación pertenece a una versión anterior. Ambos necesitan la misma versión; pedile al anfitrión una invitación DD5 nueva."}
		return {"ok":false, "error":"Pegá una invitación DD5 completa de esta versión del juego."}
	var encoded := text.substr(PREFIX.length())
	var pattern := RegEx.new()
	pattern.compile("^[A-Za-z0-9_-]+$")
	if encoded.is_empty() or encoded.length() % 4 == 1 or pattern.search(encoded) == null:
		return {"ok":false, "error":"La invitación está incompleta o tiene caracteres extra."}
	var padded := encoded.replace("-", "+").replace("_", "/")
	while padded.length() % 4 != 0:
		padded += "="
	var raw := Marshalls.base64_to_raw(padded)
	var json := JSON.new()
	if raw.is_empty() or json.parse(raw.get_string_from_utf8()) != OK or not json.data is Dictionary:
		return {"ok":false, "error":"No se pudo leer la invitación. Copiala otra vez."}
	var data: Dictionary = json.data
	if data.get("v", 0) != 1:
		return {"ok":false, "error":"Esta invitación necesita otra versión del juego."}
	var protocol: Variant=data.get("protocol")
	# JSON numbers decode as floats; require the exact finite integer value.
	if not (protocol is int or protocol is float) or not is_finite(float(protocol)) or float(protocol)!=float(PROTOCOL):
		return {"ok":false,"error":"Esta invitación usa otro protocolo. Actualizá el juego y pedile al anfitrión una invitación nueva."}
	if not data.get("host") is String or not data.get("room") is String:
		return {"ok":false, "error":"A la invitación le faltan datos de conexión."}
	var host_error := validate_host(str(data.host))
	if not host_error.is_empty():
		return {"ok":false, "error":host_error}
	var port: Variant = data.get("port")
	if not (port is int or port is float) or not is_finite(float(port)) or float(port) != floorf(float(port)) or int(port) < 1024 or int(port) > 65535:
		return {"ok":false, "error":"El puerto de la invitación no es válido."}
	if not _room_ok(str(data.room)):
		return {"ok":false, "error":"El código de sala está incompleto."}
	var scope: Variant = data.get("scope", "")
	if not scope is String or (not str(scope).is_empty() and not validate_sharing_address(str(data.host), str(scope)).is_empty()):
		return {"ok":false,"error":"El alcance de esa invitación no coincide con su dirección. Pedile una nueva al anfitrión."}
	return {"ok":true, "error":"", "host":str(data.host), "port":int(port), "room":str(data.room), "scope":str(scope)}

static func local_addresses() -> Array[String]:
	var result: Array[String] = []
	for address: String in IP.get_local_addresses():
		if address.contains(":") or not validate_host(address).is_empty() or address.begins_with("169.254."):
			continue
		result.append(address)
	result.sort()
	return result
