class_name OnlineInvitation
extends RefCounted
## Local invitation format only: decoding never authenticates a lobby or opens EOS.
## Treat capability as an invitation bearer token; do not put it in public attributes.
const PREFIX := "LMS1-"
const PROTOCOL := 12
const VERSION := 1
const MAX_LENGTH := 512
const MAX_RAW_BYTES := 384
const MAX_LOBBY_ID_LENGTH := 64
const CAPABILITY_BYTES := 16

## This format's conservative ID policy is 1..64 ASCII letters/digits/_/-.
## Preserve case and treat the ID as opaque. This is not an EOS existence check
## or a claim to support every custom LobbyIdOverride accepted by the SDK.
static func valid_lobby_id(value: String) -> bool:
	return value.length() >= 1 and value.length() <= MAX_LOBBY_ID_LENGTH and _ascii_token(value, true)

static func valid_capability(value: String) -> bool:
	if value.length() != CAPABILITY_BYTES * 2:
		return false
	for index: int in value.length():
		var character := value.unicode_at(index)
		if not (character >= 48 and character <= 57) and not (character >= 97 and character <= 102):
			return false
	return true

static func generate_capability() -> String:
	var random := Crypto.new().generate_random_bytes(CAPABILITY_BYTES)
	return random.hex_encode() if random.size() == CAPABILITY_BYTES else ""

static func encode(lobby_id: String, capability: String) -> String:
	if not valid_lobby_id(lobby_id) or not valid_capability(capability):
		return ""
	var data := {"transport":"eos", "v":VERSION, "lobby_id":lobby_id, "protocol":PROTOCOL, "capability":capability}
	return PREFIX + _base64url(JSON.stringify(data).to_utf8_buffer())

static func decode(value: String) -> Dictionary:
	if value.length() > MAX_LENGTH or not value.begins_with(PREFIX):
		return _failure("Pegá una invitación LMS1 completa de esta versión del juego.")
	var encoded := value.substr(PREFIX.length())
	if encoded.is_empty() or encoded.length() % 4 == 1 or not _ascii_token(encoded, true):
		return _failure("La invitación está incompleta o tiene caracteres extra.")
	var padded := encoded.replace("-", "+").replace("_", "/")
	while padded.length() % 4 != 0:
		padded += "="
	var raw := Marshalls.base64_to_raw(padded)
	if raw.is_empty() or raw.size() > MAX_RAW_BYTES or _base64url(raw) != encoded:
		return _failure("La invitación tiene una codificación inválida.")
	# The format contains only ASCII fields. Reject malformed UTF-8 before parsing.
	for character: int in raw:
		if character < 32 or character > 126:
			return _failure("La invitación contiene datos inválidos.")
	var parser := JSON.new()
	if parser.parse(raw.get_string_from_ascii()) != OK or not parser.data is Dictionary:
		return _failure("No se pudo leer la invitación. Copiala otra vez.")
	var data: Dictionary = parser.data
	if data.size() != 5 or not data.get("transport") is String or data.get("transport") != "eos":
		return _failure("La invitación no tiene el formato online esperado.")
	if not _exact_number(data.get("v"), VERSION) or not _exact_number(data.get("protocol"), PROTOCOL):
		return _failure("Esta invitación necesita otra versión del juego. Pedile una nueva al anfitrión.")
	if not data.get("lobby_id") is String or not data.get("capability") is String:
		return _failure("A la invitación le faltan datos de sala.")
	if not valid_lobby_id(data.lobby_id) or not valid_capability(data.capability):
		return _failure("Los datos de la invitación están incompletos o son inválidos.")
	# One representation rejects duplicate JSON keys, extra fields, whitespace,
	# escaped aliases and noncanonical base64 padding bits. Never normalize a token.
	if encode(data.lobby_id, data.capability) != value:
		return _failure("La invitación fue modificada. Copiala otra vez desde el juego.")
	return {"ok":true, "error":"", "transport":"eos", "v":VERSION, "lobby_id":data.lobby_id, "protocol":PROTOCOL, "capability":data.capability}

## EOSG 2.3.0 EOSGSocket::_socket_id_is_valid (transport-source source inspected)
## accepts only ASCII alphanumerics and length below SOCKETNAME_SIZE. This
## 31-character name is compatible with its 32-character EOS socket-name limit.
## The capability is deliberately excluded: it is checked by the game handshake.
static func socket_id(lobby_id: String) -> String:
	if not valid_lobby_id(lobby_id):
		return ""
	return "LMS" + ("LetMeSleep/EOS/socket/v1/" + lobby_id).sha256_text().left(28)

static func _ascii_token(value: String, allow_separator: bool) -> bool:
	for index: int in value.length():
		var character := value.unicode_at(index)
		if (character >= 48 and character <= 57) or (character >= 65 and character <= 90) or (character >= 97 and character <= 122):
			continue
		if allow_separator and character in [45, 95]:
			continue
		return false
	return true

static func _exact_number(value: Variant, expected: int) -> bool:
	return (value is int or value is float) and is_finite(float(value)) and float(value) == float(expected)

static func _base64url(raw: PackedByteArray) -> String:
	return Marshalls.raw_to_base64(raw).replace("+", "-").replace("/", "_").trim_suffix("=").trim_suffix("=")

static func _failure(message: String) -> Dictionary:
	return {"ok":false, "error":message}
