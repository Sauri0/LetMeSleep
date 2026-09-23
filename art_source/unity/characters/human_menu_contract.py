"""Presentation-only seated motion targets; plain Python, actor-local metres.
Actor Unity axes follow the existing corrected model: source(x,y,z)->(-x,z,-y).
The declared contact is a target, never evidence that skin/cloth supports it.
"""
import math

FPS=30
SEATED_CLIPS={'MenuSeatedIdle':8.0,'MenuLook':4.0,'MenuSwat':1.4,'MenuReturn':1.8}
# v0.3.0 (UI-06 screen 1): asleep in bed. MenuSleep loops (slow breathing, one dream stir); MenuSleepSwat is the
# clumsy, still-asleep slap at a buzzing mosquito that starts and ends on the MenuSleep t=0 pose.
SLEEP_CLIPS={'MenuSleep':10.0,'MenuSleepSwat':1.6}
CLIPS={**SEATED_CLIPS,**SLEEP_CLIPS}
CONTRACT={
    'version':'human-menu-seated-source-2-joints','root_world_unity_m':[3.3,0,5.35],
    'root_yaw_degrees':180,'scale':1,'root_motion':False,
    'seat_surface_actor_m':[0,.575,.345],'seat_front_actor_z_m':.40,
    'seat_flat_front_actor_z_m':.355,'seat_back_actor_z_m':-.32,
    'seat_half_width_m':.91,'back_support_actor_m':[0,.93,-.265],
    'back_support_required':False,'posture':'Seated near the front edge; back support is informational',
    'sole_actor_m':{'L':[-.16,0,.70],'R':[.16,0,.70]},
    'hips_actor_m':[0,.651,.345],
    'hips_height_note':'Raised7mm after first native menu showed cloth5.6mm below seat; remeasurement/visual review required',
    'left_wrist_actor_m':[-.245,.599,.28],
    'left_palm_note':'Fingers point toward the back of the seat; avoid forced forearm pronation and measure palm support',
    'tool_socket_bone':'Socket.Grip.R','tool_runtime_anchor':'ToolSocket_R',
    'tool_mount':'Align ToolView.Grip to ToolSocket_R with production position/rotation correction',
    'grip_pose_note':'Palm follows the forearm with authored wrist tilt; skin and cuff share lower-arm/hand weights',
    'tool_grip_to_impact_m':.365,'tool_grip_section_m':[.034,.024],
    'swat_contact_normalized':.5,'swat_contact_seconds':.7,
    'mosquito_pass_actor_m':[.69625,1.50,.62875],
    'mosquito_pass_note':'Elementos/Presentation agreed flight pass; measure separation from the real tool, no actual hit',
    'reduced_motion':'Hold MenuSeatedIdle at time0; no gesture',
    'transitions':'Idle full cycles -> Look -> Swat -> Return -> Idle0; .18s fade only at sequence entry/exit',
    'combat_ids_modified':False,'artistic_approval':False}


def source_point(point):
    x,y,z=point
    return (-x,-z,y)


def smooth(t):
    t=max(0,min(1,t));return t*t*t*(t*(t*6-15)+10)


REST={'wrist':[.38,.83,.53],'tilt':0.0,'head_yaw':0.0,'head_pitch':0.0,
      'chest_yaw':0.0,'lean':.05,'sway':0.0,'spine_pitch':0.0,'shoulder_lift':0.0,
      'weight_shift':0.0,'knee_settle':0.0,'blink':0.0,'look_weight':0.0}
ATTENTION={**REST,'wrist':[.38,.855,.53],'chest_yaw':-.13,'lean':.075,'look_weight':1.0,'shoulder_lift':.003}
WINDUP={**ATTENTION,'wrist':[.41,1.10,.42],'tilt':math.radians(-24),'chest_yaw':-.23,'lean':.02,'shoulder_lift':.007}
CONTACT={**ATTENTION,'wrist':[.54,1.10,.58],'tilt':math.radians(6),'chest_yaw':.04,'lean':.13,'spine_pitch':.018}
FOLLOW={**ATTENTION,'wrist':[.47,.86,.63],'tilt':math.radians(32),'chest_yaw':.10,'lean':.105,'shoulder_lift':-.003}


def blend(a,b,t):
    u=smooth(t)
    return {key:[x+(y-x)*u for x,y in zip(a[key],b[key])] if isinstance(a[key],list)
            else a[key]+(b[key]-a[key])*u for key in a}


def strike_curve(t):
    # Pause at anticipation; carry velocity through the strike instead of
    # stopping at the mosquito pass, then decelerate into the tired follow.
    knots=[(0,ATTENTION),(.28,WINDUP),(.5,CONTACT),(1,FOLLOW)]
    for i,((a,left),(b,right)) in enumerate(zip(knots,knots[1:])):
        if t>b:continue
        u=(t-a)/(b-a);h00=2*u**3-3*u*u+1;h10=u**3-2*u*u+u
        h01=-2*u**3+3*u*u;h11=u**3-u*u
        def value(key,x,y,index=None):
            pick=lambda state:state[key][index] if index is not None else state[key]
            speed=(pick(FOLLOW)-pick(WINDUP))/.72*.65
            tangent_a=speed if i==2 else 0
            tangent_b=speed if i==1 else 0
            return h00*x+h10*(b-a)*tangent_a+h01*y+h11*(b-a)*tangent_b
        return {key:[value(key,x,y,j) for j,(x,y) in enumerate(zip(left[key],right[key]))]
                if isinstance(left[key],list) else value(key,left[key],right[key]) for key in left}
    return FOLLOW.copy()


def parameters(clip,t):
    assert clip in CLIPS and 0<=t<=1
    if clip=='MenuSeatedIdle':
        breath=math.sin(2*math.pi*t)**2;settle=math.sin(2*math.pi*t)**3
        return {**REST,'lean':REST['lean']+.035*breath,'spine_pitch':-.010*breath,
                'sway':.024*settle,'shoulder_lift':.004*breath,
                'weight_shift':.003*settle,'knee_settle':.014*settle,
                'head_pitch':.020*breath,'head_yaw':.024*settle,
                'wrist':[REST['wrist'][0]+.004*settle,REST['wrist'][1]+.006*breath,REST['wrist'][2]]}
    if clip=='MenuLook':
        result=blend(REST,ATTENTION,t)
        result['head_yaw']-=.035*math.sin(math.pi*t)**2
        return result
    if clip=='MenuSwat':
        return strike_curve(t)
    result=blend(FOLLOW,REST,t);fatigue=math.sin(math.pi*t)**2
    result['lean']+=.035*fatigue;result['head_pitch']+=.06*fatigue
    result['shoulder_lift']-=.004*fatigue
    return result


# ---------------------------------------------------------------------------------------------------------------
# v0.3.0 sleeping menu pose (UI-06 screen 1). Authored in the upright actor space above (Blender: Z up, face -Y, left
# +X); Unity lays the actor on the bed through the Prop_BedSleeper anchor "sleeper_root". Mapping to the bed space of
# Prop_BedSleeper (Blender bed coordinates: head toward +Y, Z up): bed = R @ actor + root, lying on the LEFT side and
# facing bed +X (Unity: the menu camera sees the face with the headboard on the screen's right, as in UI-06).
# export_human_menu.py solves root so the head centre lands on the pillow (SLEEP_CONTRACT['head_bed_m']) and writes
# the posed-body cover height field (menu/sleep_envelope.json) that props_catalog.bed_sleeper turns into the quilt.
SLEEP_CONTRACT={
    'version':'human-menu-sleep-1',
    'posture':'Side-lying on the left side under the quilt, knees drawn up, arms folded in front of the chest',
    'lying_side':'left',
    'actor_to_bed_rows':[[0,-1,0],[0,0,1],[-1,0,0]],
    'head_centre_rest_actor_m':[0.0,0.0,1.53],
    # Fluffy BedSleeper pillow (top ~0.854 m) + half the head's width; the nightcap clears the headboard.
    'head_bed_m':[0.0,0.70,0.985],
    'mattress_top_bed_m':0.62,
    'quilt_fold_bed_y_m':0.40,
    'eyes':'Closed by the Unity menu scene (Blink blend shapes); the clips keep the Eye bones at rest scale',
    'combat_ids_modified':False,'artistic_approval':False}

# neck/head roll stay near zero: with the right shoulder down, the pillow alone fills the gap under the head.
# UI-06: the head is propped on a big pillow (neck/head roll lift the crown toward the room, ~37 deg) and the face is
# turned up toward the camera (neck_yaw), so the closed eyes read almost level instead of stacked.
SLEEP_REST={'spine':0.14,'chest':0.10,'neck_roll':0.30,'head_roll':0.35,'head_pitch':0.06,'neck_yaw':0.35,
            'jaw':-0.035,'breath':0.0,'top_hand':[0.04,-0.24,1.02],'bottom_hand':[-0.06,-0.22,1.18],
            'top_fingers':0.55,'bottom_fingers':0.6,'top_knee_extend':0.0}


def sleep_parameters(clip,t):
    """Presentation-only sleep pose parameters for clip at normalized time t (both clips start/end on MenuSleep t=0)."""
    assert clip in SLEEP_CLIPS and 0<=t<=1
    state=dict(SLEEP_REST);state['top_hand']=list(SLEEP_REST['top_hand']);state['bottom_hand']=list(SLEEP_REST['bottom_hand'])
    if clip=='MenuSleep':
        # Three slow breaths per 10 s loop (continuous at the loop seam).
        breath=math.sin(3*math.pi*t)**2
        state['breath']=breath
        state['jaw']=SLEEP_REST['jaw']-.02*breath
        # Dream stir between 55 % and 78 %: head nuzzles the pillow, top hand twitches, top foot kicks once.
        d=max(0.0,min(1.0,(t-.55)/.23));dream=math.sin(math.pi*d)**2
        state['neck_yaw']=SLEEP_REST['neck_yaw']+.10*math.sin(2*math.pi*d)*dream
        state['head_pitch']=SLEEP_REST['head_pitch']+.05*dream
        state['top_fingers']=SLEEP_REST['top_fingers']+.35*math.sin(3*math.pi*d)*dream
        state['top_knee_extend']=.16*math.sin(math.pi*min(1.0,d*2))**2
        return state
    # MenuSleepSwat: stir, lift the top arm out of the quilt above the ear, a sloppy slap, then flop back asleep.
    def s(a,b):return smooth((t-a)/(b-a))
    up=s(.12,.42);slap=s(.42,.52);back=s(.58,.90)
    lift=[.26,-.10,1.62];hit=[.20,-.02,1.50];rest=SLEEP_REST['top_hand']
    hand=[r+(l-r)*up for r,l in zip(rest,lift)]
    hand=[h+(x-h)*slap for h,x in zip(hand,hit)]
    hand=[h+(r-h)*back for h,r in zip(hand,rest)]
    state['top_hand']=hand
    stir=math.sin(math.pi*min(1.0,t/.9))**2
    state['neck_yaw']=SLEEP_REST['neck_yaw']-.18*stir
    state['head_pitch']=SLEEP_REST['head_pitch']-.06*stir
    state['top_fingers']=SLEEP_REST['top_fingers']-.4*(up-back if up>back else 0)
    state['jaw']=SLEEP_REST['jaw']-.03*stir
    return state
