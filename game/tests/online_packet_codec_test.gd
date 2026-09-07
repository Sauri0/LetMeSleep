extends SceneTree
const Codec = preload("res://scripts/online_packet_codec.gd")
var checks := 0
var failures := 0
func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("CODEC_FAIL " + label)
func receive(codec: RefCounted, frames: Array, sender: int = 2, now: int = 0, reliable: bool = false) -> Dictionary:
	var result: Dictionary = {}
	for frame: PackedByteArray in frames: result=codec.ingest(sender,frame,now,reliable)
	return result
func _initialize() -> void:
	var random := RandomNumberGenerator.new()
	random.seed=5016
	var noise := PackedByteArray()
	noise.resize(60000)
	for index: int in noise.size(): noise[index]=random.randi_range(0,255)
	var payload := {"noise":noise,"actor":{"p":Vector3(1.5,2.5,3.5),"name":"Árbol ñ","alive":true},"array":[null,3,0.4],&"tick":3,&"state":&"flying"}
	var encoded: Dictionary = Codec.encode(payload,Codec.Kind.PUBLIC,7,1,10)
	check(encoded.ok and encoded.frames.size()>50,"Large payload is fragmented")
	var bounded := true
	for frame: PackedByteArray in encoded.frames: bounded=bounded and frame.size()<=1032
	check(bounded,"Every frame stays at or below 1032 bytes before RPC/EOS headers")
	var codec := Codec.new()
	codec.reset(7)
	var frames: Array = encoded.frames.duplicate()
	frames.reverse()
	var completed := receive(codec,frames)
	check(completed.get("complete",false) and completed.payload==payload,"Out-of-order fragments restore nested data and vectors exactly")
	check(codec.partial_count()==0,"Completed message releases assembly storage")
	check(codec.ingest(2,frames[0],1).get("dropped")=="stale","Completed duplicate cannot deliver twice")
	codec.reset(7)
	check(not codec.ingest(2,frames[0],0).get("complete",false),"First fragment does not expose partial data")
	check(codec.ingest(2,frames[0],200).get("dropped")=="duplicate","Identical duplicate is harmless")
	check(codec.expire(300)==1 and codec.partial_count()==0,"Duplicates do not extend a lossy message TTL")
	codec.ingest(2,frames[0],500,true)
	check(codec.expire(801)==0 and codec.expire(3500)==1,"Reliable assembly has its own bounded TTL")
	codec.reset(7)
	var damaged: PackedByteArray=frames[0].duplicate()
	damaged[32]^=1
	codec.ingest(2,frames[0],0)
	check(codec.ingest(2,damaged,1).get("error")=="conflicting_duplicate","Conflicting duplicate invalidates the assembly")
	codec.reset(7)
	var corrupt_frames: Array=encoded.frames.duplicate(true)
	corrupt_frames[0][32]^=1
	check(receive(codec,corrupt_frames).get("error")=="checksum","Corrupt data fails integrity before decompression")
	for field: Array in [[0,0,2],[2,9,1],[3,99,1],[4,8,4],[16,66,2],[18,67,2],[20,Codec.MAX_COMPRESSED_BYTES+1,4],[24,Codec.MAX_RAW_BYTES+1,4]]:
		codec.reset(7)
		var altered: PackedByteArray=encoded.frames[0].duplicate()
		match int(field[2]):
			1: altered[field[0]]=field[1]
			2: altered.encode_u16(field[0],field[1])
			4: altered.encode_u32(field[0],field[1])
		check(not codec.ingest(2,altered,0).ok,"Malformed header offset %d rejected" % field[0])
	for size: int in [0,1,31,32,1033,5000]:
		var invalid := PackedByteArray()
		invalid.resize(size)
		check(not codec.ingest(2,invalid,0).ok,"Invalid frame length %d rejected" % size)
	codec.reset(7)
	for sequence: int in range(1,5):
		var part: PackedByteArray=encoded.frames[0].duplicate()
		part.encode_u32(8,sequence)
		check(codec.ingest(2,part,0).ok,"Allowed sender partial %d" % sequence)
	var extra: PackedByteArray=encoded.frames[0].duplicate()
	extra.encode_u32(8,5)
	check(codec.ingest(2,extra,0).get("error")=="partial_limit","Per-sender partial quota is enforced")
	codec.forget_sender(2)
	check(codec.partial_count()==0,"Disconnect frees that sender's incomplete messages")
	codec.reset(7)
	for sender: int in range(1,17):
		for sequence: int in range(1,5):
			var part: PackedByteArray=encoded.frames[0].duplicate()
			part.encode_u32(8,sequence)
			check(codec.ingest(sender,part,0).ok,"Bounded partial slot")
	check(codec.partial_count()==64 and not codec.ingest(17,extra,0).ok,"Global storage and sender quotas are enforced")
	codec.reset(7)
	check(codec.partial_count()==0,"New session clears all partial data")
	var recent: Dictionary=Codec.encode({"n":2},Codec.Kind.PUBLIC,7,5,20)
	check(receive(codec,recent.frames).get("complete",false),"New snapshot completes")
	var stale: Dictionary=Codec.encode({"n":1},Codec.Kind.PUBLIC,7,4,21)
	check(receive(codec,stale.frames).get("dropped")=="stale","Older sequence is dropped even with a higher claimed tick")
	var old_tick: Dictionary=Codec.encode({"n":1},Codec.Kind.PUBLIC,7,6,19)
	check(receive(codec,old_tick.frames).get("dropped")=="stale","Older tick is dropped even with a newer sequence")
	var personal: Dictionary=Codec.encode({"secret":"personal"},Codec.Kind.PRIVATE,7,1,1)
	check(receive(codec,personal.frames,3).payload.secret=="personal","Sender and stream sequencing remain independent")
	check(not codec.ingest(0,recent.frames[0],0).ok,"Unidentified sender cannot allocate an assembly")
	var object := RefCounted.new()
	check(not Codec.encode({"object":object},1,7,1,0).ok,"Object values cannot be serialized into network frames")
	# Bypass our encoder to verify a received Godot object-ID value is refused;
	# bytes_to_var never enables instantiation from serialized objects.
	var hostile_raw := var_to_bytes({"object":object})
	var hostile_compressed := hostile_raw.compress(FileAccess.COMPRESSION_DEFLATE)
	var hostile_frame: PackedByteArray=recent.frames[0].slice(0,Codec.HEADER_BYTES)
	hostile_frame.encode_u32(8,100)
	hostile_frame.encode_u32(20,hostile_compressed.size())
	hostile_frame.encode_u32(24,hostile_raw.size())
	hostile_frame.encode_u32(28,Codec._checksum(hostile_compressed))
	hostile_frame.append_array(hostile_compressed)
	check(codec.ingest(2,hostile_frame,0).get("error")=="unsafe_payload","Received object-ID payload is rejected without enabling object decoding")
	check(not Codec.encode({"callback":check},1,7,1,0).ok,"Callables cannot be serialized")
	check(not Codec.encode({"number":NAN},1,7,1,0).ok,"Non-finite values cannot be serialized")
	check(not Codec.encode({"bytes":noise+noise},1,7,1,0).ok,"Oversized payload cannot be serialized")
	var deep: Dictionary={}
	for index: int in 14: deep={"nested":deep}
	check(not Codec.encode(deep,1,7,1,0).ok,"Excessive nesting is rejected")
	for metadata: Array in [[0,7,1,0],[6,7,1,0],[1,-1,1,0],[1,7,-1,0],[1,7,1,-1],[1,7,0x100000000,0]]:
		check(not Codec.encode({},metadata[0],metadata[1],metadata[2],metadata[3]).ok,"Invalid encode metadata rejected")
	print("ONLINE_PACKET_CODEC_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
