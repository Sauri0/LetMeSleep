extends SceneTree
## Production 16-player procedural round, encoded using the actual EOS envelope.
const Sim=preload("res://scripts/simulation.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Codec=preload("res://scripts/online_packet_codec.gd")
var checks:=0
var failures:=0
var largest_raw:=0
var largest_fragments:=0
func _initialize() -> void: run.call_deferred()
func encode(method: String,args: Array,kind: int) -> void:
	checks+=1
	var packet: Dictionary=Codec.encode({"method":method,"args":args},kind,1,1,1)
	if not packet.get("ok",false): failures+=1;push_error("Production payload does not fit: "+method);return
	largest_raw=maxi(largest_raw,int(packet.raw_bytes))
	largest_fragments=maxi(largest_fragments,packet.frames.size())
	for frame: PackedByteArray in packet.frames:
		if frame.size()>1032: failures+=1;push_error("Oversized frame: "+method)
func run() -> void:
	var generated: Dictionary=Maps.new_house(123456789)
	if generated.is_empty(): push_error("Map generation failed");quit(1);return
	var config: Dictionary=Sim.DEFAULT_CONFIG.duplicate(true)
	config.map_id=generated.id;config.human_count=5
	var roster: Dictionary={}
	for id: int in range(1,17): roster[id]={"name":"Player %d"%id,"role":"human" if id<=5 else "mosquito"}
	var simulation:=Sim.new();simulation.start(roster,config)
	if simulation.phase!="playing": push_error("16 player simulation failed to start");quit(1);return
	var state: Dictionary=simulation.public_snapshot();state.tick=1
	encode("_receive_round",[state],Codec.Kind.PUBLIC)
	encode("_receive_snapshot",[var_to_bytes(state).compress(FileAccess.COMPRESSION_DEFLATE)],Codec.Kind.PUBLIC)
	for id: int in roster:
		var private_state: Dictionary=simulation.private_for(id);private_state.tick=1
		encode("_receive_private_reliable",[private_state],Codec.Kind.PRIVATE)
	print("ONLINE_NETWORK_PAYLOAD_CHECKS checks=%d failures=%d actors=16 largest_raw=%d largest_fragments=%d"%[checks,failures,largest_raw,largest_fragments])
	quit(0 if failures==0 else 1)
