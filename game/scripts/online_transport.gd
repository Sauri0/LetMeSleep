extends Node
signal send_failed(reason: String)
## All EOS gameplay RPCs carry bounded frames, including lobby and round states.
## Separate reliable/unreliable reassemblers prevent cross-channel supersession.
const Codec=preload("res://scripts/online_packet_codec.gd")
var _reliable:=Codec.new()
var _unreliable:=Codec.new()
var _epoch:=0
var _sequences: Dictionary={}
var _rates: Dictionary={}

const SCHEMAS={
	"_request_join":[TYPE_STRING,TYPE_INT,TYPE_STRING,TYPE_STRING,TYPE_BOOL,TYPE_STRING],
	"_request_lobby":[TYPE_STRING,-1],
	"_request_movement":[TYPE_INT,TYPE_VECTOR3,TYPE_FLOAT,TYPE_FLOAT,TYPE_BOOL,TYPE_BOOL,TYPE_BOOL,TYPE_BOOL],
	"_action":[TYPE_INT,TYPE_STRING],
	"_welcome":[TYPE_INT,TYPE_STRING],"_reject":[TYPE_STRING],"_server_notice":[TYPE_STRING],
	"_receive_lobby":[TYPE_DICTIONARY],"_receive_round":[TYPE_DICTIONARY],
	"_receive_snapshot":[TYPE_PACKED_BYTE_ARRAY],"_receive_snapshot_reliable":[TYPE_PACKED_BYTE_ARRAY],
	"_receive_private":[TYPE_DICTIONARY],"_receive_private_reliable":[TYPE_DICTIONARY],
	"voice:_request_control":[TYPE_STRING,TYPE_INT,TYPE_INT],
	"voice:_request_frame":[TYPE_INT,TYPE_INT,TYPE_PACKED_BYTE_ARRAY],
	"voice:_request_resume":[TYPE_INT,TYPE_INT,TYPE_INT],
	"voice:_request_mute":[TYPE_INT,TYPE_BOOL],
	"voice:_permission":[TYPE_INT,TYPE_DICTIONARY],
	"voice:_deliver":[TYPE_INT,TYPE_INT,TYPE_INT,TYPE_INT,TYPE_PACKED_BYTE_ARRAY],
	"voice:_end":[TYPE_INT,TYPE_INT,TYPE_INT,TYPE_INT]
}

static func valid_message(method: String, args: Array) -> bool:
	if not SCHEMAS.has(method) or args.size()!=SCHEMAS[method].size(): return false
	for index: int in args.size():
		var expected: int=SCHEMAS[method][index]
		if expected!=-1 and typeof(args[index])!=expected: return false
	return true

static func stream_kind(method: String) -> int:
	if method=="_request_movement": return Codec.Kind.INPUT
	if method in ["_receive_snapshot","_receive_snapshot_reliable","_receive_round"]: return Codec.Kind.PUBLIC
	if method in ["_receive_private","_receive_private_reliable"]: return Codec.Kind.PRIVATE
	if method in ["voice:_deliver","voice:_request_frame"]: return Codec.Kind.ACTION
	return Codec.Kind.CONTROL

static func reliable_method(method: String) -> bool:
	return not method in ["_request_movement","_receive_snapshot","_receive_private","voice:_deliver","voice:_request_frame"]

func reset(epoch: int) -> void:
	_epoch=epoch;_sequences.clear();_rates.clear()
	_reliable.reset(epoch);_unreliable.reset(epoch)

func forget_sender(sender: int) -> void:
	_reliable.forget_sender(sender);_unreliable.forget_sender(sender);_rates.erase(sender)

func send_message(target: int, method: String, args: Array) -> void:
	if not valid_message(method,args): return
	var kind:=stream_kind(method)
	var reliable:=reliable_method(method)
	var key: String="%d:%d:%s"%[target,kind,str(reliable)]
	var sequence: int=int(_sequences.get(key,0))+1
	_sequences[key]=sequence
	var encoded: Dictionary=Codec.encode({"method":method,"args":args},kind,_epoch,sequence,0)
	if not encoded.get("ok",false):
		send_failed.emit(str(encoded.get("error","invalid_packet")))
		return
	for frame: PackedByteArray in encoded.frames:
		if reliable: _frame_reliable.rpc_id(target,frame)
		else: _frame_unreliable.rpc_id(target,frame)

@rpc("any_peer","call_remote","reliable",0)
func _frame_reliable(frame: PackedByteArray) -> void:
	_ingest(multiplayer.get_remote_sender_id(),frame,true)

@rpc("any_peer","call_remote","unreliable",1)
func _frame_unreliable(frame: PackedByteArray) -> void:
	_ingest(multiplayer.get_remote_sender_id(),frame,false)

func _ingest(sender: int, frame: PackedByteArray, reliable: bool) -> void:
	var network: Node=get_parent()
	if not network._online or not network.online_member(sender): return
	if not network.is_server and sender!=1: return
	var now:=Time.get_ticks_msec()
	var rate: Dictionary=_rates.get(sender,{"time":now,"count":0})
	if now-int(rate.time)>=1000: rate={"time":now,"count":0}
	rate.count+=1;_rates[sender]=rate
	if int(rate.count)>1024: return
	var codec: RefCounted=_reliable if reliable else _unreliable
	var result: Dictionary=codec.ingest(sender,frame,now,reliable)
	if not result.get("ok",false) or not result.get("complete",false): return
	var message: Dictionary=result.payload
	if message.size()!=2 or not message.get("method") is String or not message.get("args") is Array: return
	var method: String=message.method
	if not valid_message(method,message.args) or stream_kind(method)!=int(result.kind) or reliable_method(method)!=reliable: return
	network._dispatch_message(sender,method,message.args)
