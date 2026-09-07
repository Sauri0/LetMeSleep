class_name OnlinePacketCodec
extends RefCounted
## Bounded packet framing only. This class opens no connection or online service.
## The caller supplies the authenticated transport sender and delivery channel.
const MAGIC := 0x534C
const VERSION := 1
const HEADER_BYTES := 32
const CHUNK_BYTES := 1000
const MAX_FRAME_BYTES := HEADER_BYTES + CHUNK_BYTES
const MAX_RAW_BYTES := 65536
const MAX_COMPRESSED_BYTES := 65792
const MAX_FRAGMENTS := 66
const MAX_PARTIALS_PER_SENDER := 4
const MAX_PARTIALS := 64
const MAX_SENDERS := 16
const UNRELIABLE_TTL_MS := 300
const RELIABLE_TTL_MS := 3000
enum Kind { PUBLIC = 1, PRIVATE = 2, CONTROL = 3, INPUT = 4, ACTION = 5 }
var _epoch := 0
var _partials: Dictionary = {}
var _latest: Dictionary = {}
var _senders: Dictionary = {}

func reset(epoch: int) -> void:
	_epoch = epoch
	_partials.clear()
	_latest.clear()
	_senders.clear()

static func _checksum(data: PackedByteArray) -> int:
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(data)
	return context.finish().decode_u32(0)

static func _safe(value: Variant, depth: int = 0) -> bool:
	if depth > 12: return false
	match typeof(value):
		TYPE_NIL, TYPE_BOOL, TYPE_INT: return true
		TYPE_FLOAT: return is_finite(value)
		TYPE_STRING, TYPE_STRING_NAME: return str(value).to_utf8_buffer().size() <= 8192
		TYPE_VECTOR2, TYPE_VECTOR3: return value.is_finite()
		TYPE_PACKED_BYTE_ARRAY: return value.size() <= MAX_RAW_BYTES
		TYPE_ARRAY:
			if value.size() > 4096: return false
			for item: Variant in value:
				if not _safe(item,depth+1): return false
			return true
		TYPE_DICTIONARY:
			if value.size() > 4096: return false
			for key: Variant in value:
				if not (key is String or key is StringName or key is int) or not _safe(key,depth+1) or not _safe(value[key],depth+1): return false
			return true
	return false

static func encode(payload: Dictionary, kind: int, epoch: int, sequence: int, tick: int) -> Dictionary:
	if kind < Kind.PUBLIC or kind > Kind.ACTION or epoch < 0 or epoch > 0xFFFFFFFF or sequence < 0 or sequence > 0xFFFFFFFF or tick < 0 or tick > 0xFFFFFFFF:
		return {"ok":false,"error":"invalid_metadata"}
	if not _safe(payload): return {"ok":false,"error":"unsafe_payload"}
	var raw := var_to_bytes(payload)
	if raw.size() > MAX_RAW_BYTES: return {"ok":false,"error":"raw_too_large"}
	var compressed := raw.compress(FileAccess.COMPRESSION_DEFLATE)
	if compressed.is_empty() or compressed.size() > MAX_COMPRESSED_BYTES: return {"ok":false,"error":"compressed_too_large"}
	var count := int(ceil(float(compressed.size())/CHUNK_BYTES))
	var checksum := _checksum(compressed)
	var frames: Array[PackedByteArray] = []
	for index: int in count:
		var frame := PackedByteArray()
		frame.resize(HEADER_BYTES)
		frame.encode_u16(0,MAGIC)
		frame[2] = VERSION
		frame[3] = kind
		frame.encode_u32(4,epoch)
		frame.encode_u32(8,sequence)
		frame.encode_u32(12,tick)
		frame.encode_u16(16,index)
		frame.encode_u16(18,count)
		frame.encode_u32(20,compressed.size())
		frame.encode_u32(24,raw.size())
		frame.encode_u32(28,checksum)
		frame.append_array(compressed.slice(index*CHUNK_BYTES,mini((index+1)*CHUNK_BYTES,compressed.size())))
		frames.append(frame)
	return {"ok":true,"frames":frames,"raw_bytes":raw.size(),"compressed_bytes":compressed.size()}

func expire(now_ms: int) -> int:
	var removed := 0
	for key: String in _partials.keys():
		if now_ms >= int(_partials[key].expires):
			_partials.erase(key)
			removed += 1
	return removed

func forget_sender(sender: int) -> void:
	_senders.erase(sender)
	for key: String in _partials.keys():
		if int(_partials[key].sender) == sender: _partials.erase(key)
	for key: String in _latest.keys():
		if key.begins_with(str(sender)+":"): _latest.erase(key)

func partial_count() -> int: return _partials.size()

func ingest(sender: int, frame: PackedByteArray, now_ms: int = -1, reliable: bool = false) -> Dictionary:
	if now_ms < 0: now_ms = Time.get_ticks_msec()
	expire(now_ms)
	if sender < 1 or frame.size() < HEADER_BYTES+1 or frame.size() > MAX_FRAME_BYTES:
		return {"ok":false,"error":"frame_size_or_sender"}
	if frame.decode_u16(0) != MAGIC or frame[2] != VERSION or frame[3] < Kind.PUBLIC or frame[3] > Kind.ACTION:
		return {"ok":false,"error":"header"}
	var kind := int(frame[3])
	var epoch := int(frame.decode_u32(4))
	var sequence := int(frame.decode_u32(8))
	var tick := int(frame.decode_u32(12))
	var index := int(frame.decode_u16(16))
	var count := int(frame.decode_u16(18))
	var total := int(frame.decode_u32(20))
	var raw_size := int(frame.decode_u32(24))
	var checksum := int(frame.decode_u32(28))
	if epoch != _epoch: return {"ok":false,"error":"epoch"}
	if count < 1 or count > MAX_FRAGMENTS or index >= count or total < 1 or total > MAX_COMPRESSED_BYTES or raw_size < 1 or raw_size > MAX_RAW_BYTES or count != int(ceil(float(total)/CHUNK_BYTES)):
		return {"ok":false,"error":"bounds"}
	if frame.size()-HEADER_BYTES != mini(CHUNK_BYTES,total-index*CHUNK_BYTES): return {"ok":false,"error":"chunk_size"}
	var stream := "%d:%d" % [sender,kind]
	var previous: Dictionary = _latest.get(stream,{})
	if not previous.is_empty() and (sequence <= int(previous.sequence) or tick < int(previous.tick)):
		return {"ok":true,"complete":false,"dropped":"stale"}
	var key := "%s:%d" % [stream,sequence]
	if not _partials.has(key):
		if not _senders.has(sender) and _senders.size() >= MAX_SENDERS: return {"ok":false,"error":"sender_limit"}
		var sender_count := 0
		for partial: Dictionary in _partials.values():
			if int(partial.sender) == sender: sender_count += 1
		if _partials.size() >= MAX_PARTIALS or sender_count >= MAX_PARTIALS_PER_SENDER: return {"ok":false,"error":"partial_limit"}
		_senders[sender] = true
		_partials[key] = {"sender":sender,"kind":kind,"sequence":sequence,"tick":tick,"count":count,"total":total,"raw_size":raw_size,"checksum":checksum,"reliable":reliable,"expires":now_ms+(RELIABLE_TTL_MS if reliable else UNRELIABLE_TTL_MS),"chunks":{}}
	var partial: Dictionary = _partials[key]
	if partial.tick != tick or partial.count != count or partial.total != total or partial.raw_size != raw_size or partial.checksum != checksum or partial.reliable != reliable:
		_partials.erase(key)
		return {"ok":false,"error":"conflicting_header"}
	var bytes := frame.slice(HEADER_BYTES)
	if partial.chunks.has(index):
		if partial.chunks[index] != bytes:
			_partials.erase(key)
			return {"ok":false,"error":"conflicting_duplicate"}
		return {"ok":true,"complete":false,"dropped":"duplicate"}
	partial.chunks[index] = bytes
	if partial.chunks.size() != count: return {"ok":true,"complete":false}
	_partials.erase(key)
	var compressed := PackedByteArray()
	for part: int in count: compressed.append_array(partial.chunks[part])
	if compressed.size() != total or _checksum(compressed) != checksum: return {"ok":false,"error":"checksum"}
	var raw := compressed.decompress_dynamic(MAX_RAW_BYTES,FileAccess.COMPRESSION_DEFLATE)
	if raw.size() != raw_size: return {"ok":false,"error":"decompressed_size"}
	# Godot 4's bytes_to_var has object decoding disabled; never use
	# bytes_to_var_with_objects for network data.
	var payload: Variant = bytes_to_var(raw)
	if not payload is Dictionary or not _safe(payload): return {"ok":false,"error":"unsafe_payload"}
	_latest[stream] = {"sequence":sequence,"tick":tick}
	# Once a newer message is complete, older partial messages cannot be useful.
	for older: String in _partials.keys():
		var entry: Dictionary = _partials[older]
		if entry.sender == sender and entry.kind == kind and entry.sequence <= sequence: _partials.erase(older)
	return {"ok":true,"complete":true,"sender":sender,"kind":kind,"epoch":epoch,"sequence":sequence,"tick":tick,"payload":payload}
