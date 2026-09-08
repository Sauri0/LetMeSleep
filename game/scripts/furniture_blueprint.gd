class_name FurnitureBlueprint
extends RefCounted
## Stable semantic IDs, independent of translated/display room and item names.
## Bounds measured from the current GLBs; units are metres, front is local -Z.
## size is the physical furniture body. visual_size includes tap/pot/decor above
## counters. Rotation is supplied by placement; +90deg swaps the X/Z footprint.
const THEME_IDS: Array[String] = ["kitchen","dining","laundry","bathroom","bedroom_blue","bedroom_rose","guest_room","library","living_room","music_room","sewing_room","game_room","pantry","study","entry","bedroom_green"]
const ASSETS := {
	"table":{"size":Vector3(1.45,.72,.85),"height":.72},
	"desk":{"size":Vector3(1.45,.72,.85),"height":.72},
	"bed":{"size":Vector3(1.98531,.75,1.18),"height":.75},
	"wardrobe":{"size":Vector3(1.12,1.35,.600392),"height":1.35},
	"dresser":{"size":Vector3(1.12,1.35,.578),"height":1.35},
	"bookcase":{"size":Vector3(1.12,1.35,.52),"height":1.35},
	"sink":{"size":Vector3(1.72,1.119985,.760392),"height":.86},
	"stove":{"size":Vector3(1.72,1.154,.773),"height":.86},
	"fridge":{"size":Vector3(.72,1.46,.787321),"height":1.46},
	"bath_vanity":{"size":Vector3(1.6,1.088454,.724392),"height":.78},
	"toilet":{"size":Vector3(.614,.98,.7519),"height":.98},
	"bath_shower":{"size":Vector3(1.6,2.15,.82976),"height":2.15},
	"washer":{"size":Vector3(1.5,1.013,.728),"height":1.013},
	"hamper":{"size":Vector3(.756948,.905,.7),"height":.905},
	"sofa":{"size":Vector3(2.474019,1.015,.999953),"height":1.015},
	"armchair":{"size":Vector3(1.324019,1.015,.999953),"height":1.015},
	"piano":{"size":Vector3(1.46,1.415,.745),"height":1.415},
	"sewing_table":{"size":Vector3(1.45,1.265,.85),"height":.72},
	"game_table":{"size":Vector3(1.45,.808,.85),"height":.72},
	"pantry_shelf":{"size":Vector3(1.125,1.5,.52),"height":1.5},
	"pantry_crates":{"size":Vector3(2.505,.51,1.132),"height":.51},
	"dining_set":{"size":Vector3(3.0,1.216027,.93),"height":.805},
	"nightstand":{"size":Vector3(.6,.59,.52),"height":.59},
}
# asset, display label, functional style, UNIFORM model scale.
const PLANS := {
	"kitchen":[["table","Mesa de preparación","counter",.86/.72],["sink","Mesada con pileta","counter",1.0],["stove","Cocina y horno","counter",1.0],["fridge","Heladera","cabinet",1.15]],
	"dining":[["table","Mesa de apoyo","table",1.0],["dining_set","Mesa de comedor","table",.85],["dresser","Vajillero","dresser",.80],["armchair","Sillón de comedor","sofa",.90]],
	"laundry":[["table","Mesa para doblar ropa","table",1.0],["washer","Lavarropas","cabinet",1.0],["hamper","Canasto de ropa","hamper",1.0],["wardrobe","Armario de limpieza","cabinet",1.15]],
	"bathroom":[["table","Mesa auxiliar de baño","table",1.0],["bath_vanity","Vanitory con lavamanos","counter",1.0],["toilet","Inodoro","toilet",1.0],["bath_shower","Ducha","shower",1.0]],
	"bedroom_blue":[["desk","Escritorio","desk",1.0],["bed","Cama","bed",1.0],["wardrobe","Ropero","cabinet",1.25],["dresser","Cómoda","dresser",.74]],
	"bedroom_rose":[["table","Mesa de dormitorio","table",1.0],["bed","Cama","bed",1.0],["dresser","Cómoda","dresser",.80],["nightstand","Mesa de luz","table",1.0]],
	"guest_room":[["table","Mesa de visitas","table",1.0],["bed","Cama de visitas","bed",1.0],["wardrobe","Ropero de visitas","cabinet",1.15],["nightstand","Mesa de luz","table",1.0]],
	"library":[["desk","Mesa de lectura","desk",1.0],["bookcase","Biblioteca","cabinet",1.25],["armchair","Sillón de lectura","sofa",1.0],["nightstand","Mesa de apoyo","table",1.0]],
	"living_room":[["table","Mesa auxiliar","table",1.0],["sofa","Sofá","sofa",.95],["armchair","Sillón","sofa",1.0],["bookcase","Biblioteca baja","cabinet",.85]],
	"music_room":[["table","Mesa de partituras","table",1.0],["piano","Piano","piano",1.0],["bookcase","Estante de partituras","cabinet",1.0],["armchair","Sillón","sofa",.95]],
	"sewing_room":[["table","Mesa de corte","table",1.0],["sewing_table","Máquina de coser","desk",1.0],["wardrobe","Armario de telas","cabinet",1.15],["dresser","Cajonera de costura","dresser",.8]],
	"game_room":[["table","Mesa auxiliar de juegos","table",1.0],["game_table","Mesa de juegos","table",1.0],["bookcase","Estante de juegos","cabinet",.95],["armchair","Sillón de juegos","sofa",.95]],
	"pantry":[["table","Mesa de provisiones","table",1.0],["pantry_shelf","Estante de alimentos","cabinet",1.0],["pantry_crates","Cajones de verduras","crates",.75],["dresser","Alacena de reserva","dresser",.80]],
	"study":[["desk","Escritorio de trabajo","desk",1.0],["bookcase","Biblioteca de estudio","cabinet",1.20],["armchair","Sillón de estudio","sofa",.95],["dresser","Archivador","dresser",.85]],
	"entry":[["table","Mesa de entrada","table",1.0],["wardrobe","Guardarropa","cabinet",1.20],["armchair","Asiento de entrada","sofa",.85],["dresser","Mueble de entrada","dresser",.75]],
	"bedroom_green":[["desk","Escritorio de dormitorio","desk",1.0],["bed","Cama","bed",1.0],["wardrobe","Ropero","cabinet",1.20],["nightstand","Mesa de luz","table",1.0]],
}

static func for_theme(theme_id: String) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for index: int in range(Array(PLANS.get(theme_id,[])).size()):
		var item: Array = PLANS[theme_id][index]
		var asset: String = item[0]
		var scale := float(item[3])
		var measured: Vector3 = ASSETS[asset].size
		var height := float(ASSETS[asset].height)*scale
		result.append({"asset_id":asset,"label":str(item[1]),"style":str(item[2]),
			"size":Vector3(measured.x*scale,height,measured.z*scale),"visual_size":measured*scale,
			"visual_scale":scale,"surface_height":height,"pickup_surface":index==0,
			"pickup_point":Vector3.ZERO,"front":Vector3.FORWARD,"theme_id":theme_id})
	return result
