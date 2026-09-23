"""Frame-sampled alpha animation: explicit support targets, smooth timing, stable named rig."""
import bpy
import math
from mathutils import Vector, Matrix

TAU=2*math.pi
# v0.3.0 round 2 cartoon hands: the human bind pose already holds the relaxed
# idle curl (FINGER_REST_AMOUNT of the fist, ~35 deg at the fingertip), so
# Pose.fingers subtracts it and every authored amount keeps its absolute pose.
FINGER_JOINT_ANGLES=(.42,.90,1.20)
FINGER_REST_AMOUNT=.24
THUMB_CURL_FACTOR=.72
# v0.3.0 round 5 standing pose (PER-04 BASE CHARACTER): arms hang relaxed
# ~9 deg from the body with the elbows flexed ~13 deg, forearms pronated so
# the palms face the thighs (thumb forward), fingers curled into a loose fist
# and the thumb ~20 deg; knees nearly straight (<=5 deg). Degrees.
# Round 6 (art director): the idle hand is a loose fist with the thumb
# forward over the index (PER-04), not an open drooping claw: absolute
# knuckle/middle/tip flexion of the four fingers (radians; the bind already
# holds FINGER_REST_AMOUNT of FINGER_JOINT_ANGLES) and a firmer thumb.
# v0.3.0 animation pass (chars.md, director r6): the idle arms hang almost
# straight beside the thigh (abduction 5 deg, ~11 deg elbow flexion, forearm
# only ~10 deg forward so the hand sits beside the thigh, not in front of it),
# palms toward the leg with the thumb forward, and the fingers relaxed like a
# mitten (.55/.65/.45 rad) instead of the round-6 fist that read as a hook.
STAND_DROP=.0003
ARM_ABDUCTION,ARM_FORWARD=5.0,0.5
FOREARM_ABDUCTION,FOREARM_FORWARD=2.5,10.0
UPPER_ARM_TWIST=40.0
# v0.3.0 animation round 3 (chars-r10, anim-r3): the .55/.65/.45 curl (~94 deg at the fingertip) still read
# as a claw next to the thigh; the relaxed idle hand is now a half-open mitten (~60 deg at the tip).
IDLE_FIST_ANGLES=(.30,.40,.28)
RELAXED_THUMB=.42
VICTORY_FIST_ANGLES=(1.25,1.15,.95)
# v0.3.0 review: one sneaking crouch shared by Human_Crouch (static) and Human_CrouchWalk. Gait
# parameters follow human_locomotion_contract (contacts L at 0 and R at .5, 0.96875 m per cycle = the
# Walk profile at the 1.55 m/s crouch speed); 'hip' is the UpperLeg origin height.
# Round 3 (anim-r3): a comic stealth sneak instead of a duck walk. The first-person eye goes back to the
# pre-round-2 crouched height (~0.88 m, the authority's crouched aim eye is 0.89 m) so it stays well inside
# the 1.0 m crouched capsule under low ceilings; the torso leans ~50 deg with the head up, the hips stay
# higher (knees ~75-95 deg instead of a 50 deg squat), the feet stay on tiptoe with a wider track and
# outward knees (no knee up at the chest, the rear shin never touches the floor), and the arms are held
# bent close to the body with the hands up in front of the chest like paws.
# Round 4 (director r4, item 5): at the end of the stride the rear shin lay almost flat a few cm off the floor
# (a kneeling read). The sneak now takes shorter steps behind the hips: a 52% stance duty and the foot path
# shifted CROUCH_STANCE_SHIFT forward (the leaning torso carries the weight ahead of the hips), the hips 2.5 cm
# higher, and the knees aimed further out and up, so the rear knee stays >= ~20 cm off the floor, above its
# ankle, with both knees reading bent near a right angle. The torso leans a little further so the crouched
# eye stays ~0.90 m.
CROUCH=dict(clip='Human_CrouchWalk',speed=1.55,contacts=3.2,duty=.52,hip=.445,rise=.010,lift=.04,ramp=.22)
CROUCH_SPINE,CROUCH_CHEST,CROUCH_NECK,CROUCH_HEAD=.70,.80,-.70,-.44
CROUCH_HIPS_BACK=.08
CROUCH_HEEL=.04
CROUCH_TIPTOE=.035
CROUCH_TRACK=.16
CROUCH_KNEE_OUT=.36
CROUCH_POLE_Z=.50
CROUCH_STANCE_SHIFT=.10

def smooth(t):
    t=max(0,min(1,t)); return t*t*t*(t*(t*6-15)+10)

def pulse(t,a,b,c):
    return smooth((t-a)/(b-a)) if t<b else 1-smooth((t-b)/(c-b))

class Pose:
    def __init__(self,character):
        self.c=character; self.rig=character.rig
        self.rig.animation_data_create()
        self.rest={b.name:b.bone.matrix_local.copy() for b in self.rig.pose.bones}
        self.minimum_reach_margin=1
    def reset(self):
        self.rig.animation_data.action=None
        for b in self.rig.pose.bones:
            b.rotation_mode='XYZ'; b.matrix_basis.identity()
        bpy.context.view_layer.update()
    def update(self): bpy.context.view_layer.update()
    def rotate(self,name,angles): self.rig.pose.bones[name].rotation_euler=angles
    def translate(self,name,world_delta):
        self.rig.pose.bones[name].location=self.rest[name].to_3x3().inverted()@Vector(world_delta)
    def point(self,name,origin,direction,normal):
        y=direction.normalized(); z=(normal-y*normal.dot(y)).normalized(); x=y.cross(z).normalized()
        matrix=Matrix((x,y,z)).transposed().to_4x4(); matrix.translation=origin
        self.rig.pose.bones[name].matrix=matrix; self.update()
    def point_swing(self,name,origin,direction):
        """Orient a bone along direction by the minimal swing from its rest axis in the current parent
        frame. Unlike point() with a fixed normal hint, the roll never flips by 180 deg when the bone
        crosses that hint (a thigh passing horizontal in a deep crouch), which Unity would interpolate
        between two keys as a limb swinging through an unrelated pose."""
        bone=self.rig.pose.bones[name]; parent=bone.parent
        base=self.rest[name].to_3x3()
        if parent is not None:
            base=(parent.matrix.to_3x3()@self.rest[parent.name].to_3x3().inverted())@base
        axis=base@Vector((0,1,0))
        matrix=(axis.rotation_difference(Vector(direction).normalized()).to_matrix()@base).to_4x4()
        matrix.translation=origin; bone.matrix=matrix; self.update()
    def chain(self,upper,lower,target,pole,end=None,end_rotation=None,swing_roll=False):
        self.update(); u=self.rig.pose.bones[upper]; l=self.rig.pose.bones[lower]
        origin=u.head.copy(); a=u.bone.length; b=l.bone.length
        d=Vector(target)-origin; length=d.length; direction=d.normalized()
        self.minimum_reach_margin=min(self.minimum_reach_margin,a+b-length)
        length=min(a+b-.0002,max(abs(a-b)+.0002,length))
        target=origin+direction*length
        along=(a*a-b*b+length*length)/(2*length)
        pole=Vector(pole)-origin; bend=(pole-direction*pole.dot(direction)).normalized()
        joint=origin+direction*along+bend*math.sqrt(max(0,a*a-along*along))
        if swing_roll:
            self.point_swing(upper,origin,joint-origin)
            self.point_swing(lower,joint,target-joint)
        else:
            self.point(upper,origin,joint-origin,Vector((0,-1,0)))
            self.point(lower,joint,target-joint,Vector((0,-1,0)))
        if end:
            matrix=(end_rotation if end_rotation is not None else self.rest[end]).copy()
            matrix.translation=target; self.rig.pose.bones[end].matrix=matrix; self.update()
    def fingers(self,side,amount):
        for digit in ['Index','Middle','Ring','Little','Thumb']:
            factor=THUMB_CURL_FACTOR if digit=='Thumb' else 1
            for i,angle in enumerate(FINGER_JOINT_ANGLES,1):
                self.rotate(f'{digit}{i:02d}.{side}',(angle*(amount-FINGER_REST_AMOUNT)*factor,0,0))
    def snapshot(self):
        self.update()
        return {b.name:{'rotation_euler':tuple(b.rotation_euler),'location':tuple(b.location),'scale':tuple(b.scale)}
                for b in self.rig.pose.bones}
    def grounded(self,minimum=0):
        """Keep authored fall contact above support plane while Root stays stationary."""
        self.update(); graph=bpy.context.evaluated_depsgraph_get(); lowest=999
        for obj in bpy.context.scene.objects:
            if obj.type!='MESH': continue
            evaluated=obj.evaluated_get(graph); mesh=evaluated.to_mesh()
            lowest=min(lowest,min((evaluated.matrix_world@v.co).z for v in mesh.vertices))
            evaluated.to_mesh_clear()
        if lowest<minimum:
            hips=self.rig.pose.bones['Hips']
            delta=self.rest['Hips'].to_3x3().inverted()@Vector((0,0,minimum-lowest))
            hips.location+=delta; self.update()

def sampled(c,name,end,pose_fn):
    poses=[]
    for f in range(1,end+1): poses.append((f,pose_fn((f-1)/(end-1))))
    c.clip(name,end,poses)
    action=bpy.data.actions[c.species+'_'+name]
    # Authored timing is sampled per frame with sign-continuous quaternion rotations.
    # Linear segments avoid spline overshoot and match FBX's quaternion interpolation.
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points: key.interpolation='LINEAR'

def keyed(t,keys):
    """Eased (quintic smoothstep) interpolation through (time, value) keys, holding both ends."""
    if t<=keys[0][0]: return keys[0][1]
    for (t0,v0),(t1,v1) in zip(keys,keys[1:]):
        if t<=t1: return v0+(v1-v0)*smooth((t-t0)/(t1-t0))
    return keys[-1][1]

def _mix(a,b,t):
    """Spherical blend of two direction vectors (t=0 -> a) at constant angular speed. A linear blend of
    nearly opposite directions (an arm going from hanging to straight up) passes near the zero vector and
    whips through the frames around it; Unity then interpolates those keys as a spin (v0.3.0 review)."""
    a=Vector(a).normalized(); b=Vector(b).normalized()
    dot=max(-1.,min(1.,a.dot(b)))
    if dot>.99995: return (a*(1-t)+b*t).normalized()
    theta=math.acos(dot); sine=math.sin(theta)
    if sine<1e-4: return (a*(1-t)+b*t).normalized()
    return ((a*math.sin((1-t)*theta)+b*math.sin(t*theta))/sine).normalized()

# ---- arm helpers shared by the base clips and the four-gait authoring (author_human_locomotion) ----
def HANG_UPPER(s):
    return Vector((s*math.sin(math.radians(ARM_ABDUCTION)),-math.sin(math.radians(ARM_FORWARD)),-1)).normalized()
def HANG_LOWER(s):
    return Vector((s*math.sin(math.radians(FOREARM_ABDUCTION)),-math.sin(math.radians(FOREARM_FORWARD)),-1)).normalized()
def HANG_NORMAL(s):
    return Vector((-s*math.sin(math.radians(UPPER_ARM_TWIST)),-math.cos(math.radians(UPPER_ARM_TWIST)),0))
def HANG_PALM(s):
    return Vector((-s,0,0))

def pose_arm(p,side,s,upper,lower,palm,upper_normal=None,world=False):
    """Arm authored as directions in the (rest) chest frame, so it follows the animated chest; with
    world=True the directions are armature-space (a sneak holds the paws level whatever the lean)."""
    p.update()
    chest=p.rig.pose.bones['Chest']
    turn=Matrix.Identity(3) if world else (chest.matrix@p.rest['Chest'].inverted()).to_3x3()
    upper_normal=HANG_NORMAL(s) if upper_normal is None else Vector(upper_normal)
    bone=p.rig.pose.bones['UpperArm.'+side]
    p.point(bone.name,bone.head.copy(),turn@Vector(upper).normalized(),turn@upper_normal)
    bone=p.rig.pose.bones['LowerArm.'+side]
    p.point(bone.name,bone.head.copy(),turn@Vector(lower).normalized(),turn@Vector(palm))

def pose_relaxed_fingers(p,side,scale=1.0):
    for digit in ['Index','Middle','Ring','Little']:
        for i,(angle,full) in enumerate(zip(IDLE_FIST_ANGLES,FINGER_JOINT_ANGLES),1):
            p.rotate(f'{digit}{i:02d}.{side}',(angle*scale-full*FINGER_REST_AMOUNT,0,0))
    for i,angle in enumerate(FINGER_JOINT_ANGLES,1):
        p.rotate(f'Thumb{i:02d}.{side}',(angle*(RELAXED_THUMB*min(scale,1.6)-FINGER_REST_AMOUNT)*THUMB_CURL_FACTOR,0,0))

def swing_about_x(v,angle):
    """Rotate a direction about source X; a positive angle swings a hanging (-Z) direction forward (-Y)."""
    v=Vector(v);c,sn=math.cos(angle),math.sin(angle)
    return Vector((v.x,v.y*c+v.z*sn,-v.y*sn+v.z*c))

def transported(ref_dir,ref_normal,direction,roll_deg=0.):
    """A normal for 'direction' carried from (ref_dir, ref_normal) by the minimal swing, then rolled about
    'direction' by roll_deg. Continuous in 'direction' (away from -ref_dir), so bone rolls never flip between
    keys the way a blended hint vector does when it passes near the bone axis."""
    from mathutils import Quaternion
    ref_dir=Vector(ref_dir).normalized();d=Vector(direction).normalized()
    n=Vector(ref_normal);n=(n-ref_dir*n.dot(ref_dir)).normalized()
    n=ref_dir.rotation_difference(d)@n
    return Quaternion(d,math.radians(roll_deg))@n

def arm_frame(s,upper,lower,upper_roll_deg,twist_deg):
    """Upper-arm normal and palm of an arm given as directions (rest chest frame): the upper arm's roll is an
    angle relative to the hanging arm's frame carried along its swing, and the palm is that frame carried
    through the elbow flexion and twisted (pronation) by twist_deg. Both stay continuous for any path that
    keeps the upper arm away from straight up (a forearm pointing up never flips the palm)."""
    normal=transported(HANG_UPPER(s),HANG_NORMAL(s),upper,upper_roll_deg)
    return normal,transported(upper,normal,lower,twist_deg)

def arm_twist_for(s,upper,lower,upper_roll_deg,palm,near=0.):
    """The twist (degrees, unwrapped next to 'near') that turns arm_frame's palm onto the wanted palm."""
    normal=transported(HANG_UPPER(s),HANG_NORMAL(s),upper,upper_roll_deg)
    base=transported(upper,normal,lower,0.)
    d=Vector(lower).normalized();want=Vector(palm);want=(want-d*want.dot(d)).normalized()
    angle=math.degrees(math.atan2(base.cross(want).dot(d),base.dot(want)))
    return angle+360*round((near-angle)/360)

def gait_arms(p,phase,swing_deg,elbow_back_deg,elbow_front_deg,abduction_deg=8.,forward_abduction_deg=None,
              forward_scale=1.,twist=0.):
    """v0.3.0 round 3 (anim-r3): arms swing opposite to the legs, the right arm fully forward at phase 0
    (left heel strike) and back at .5, by +-swing_deg from the shoulder; the elbow bends elbow_back_deg
    with the arm behind and elbow_front_deg in front (a relaxed walk 20-35 deg, a pumping trot more). The
    palms keep facing the thighs and the arms hang 8 deg out so the hands clear the hips.
    Round 4 (director r4, item 2): the forward swing reached 0.44 m ahead of the hip at hip height and, seen in
    three-quarter view, the hand passed in front of the crotch. The forward half of the swing is now scaled by
    forward_scale, the arm opens to forward_abduction_deg as it comes forward (the forearm keeps the upper
    arm's abduction instead of half of it), and the swing plane is carried against the chest's counter-twist
    ('twist' rad, the same value the clip gives the Chest at this phase), so each hand passes in front of its
    own thigh, symmetric left and right."""
    forward_abduction_deg=abduction_deg if forward_abduction_deg is None else forward_abduction_deg
    for side,s,offset in (('L',1,.5),('R',-1,0.)):
        w=math.cos(2*math.pi*(phase+offset))
        forward=max(0.,w)
        swing=math.radians(swing_deg)*w*(forward_scale if w>0 else 1.)
        flex=math.radians(elbow_back_deg+(elbow_front_deg-elbow_back_deg)*(w+1)/2)
        abduction=math.radians(abduction_deg+(forward_abduction_deg-abduction_deg)*forward)
        # The chest yaws by 'twist' (right shoulder forward at phase 0); turning the arm directions back by it
        # keeps the swing in the body's sagittal plane instead of carrying the forward hand toward the midline.
        unturn=Matrix.Rotation(-twist,3,'Z')
        upper=unturn@swing_about_x(Vector((s*math.sin(abduction),0,-1)).normalized(),swing)
        lower=unturn@swing_about_x(Vector((s*math.sin(abduction),0,-1)).normalized(),swing+flex)
        pose_arm(p,side,s,upper,lower,unturn@HANG_PALM(s),unturn@swing_about_x(HANG_NORMAL(s),swing))

def human(c):
    p=Pose(c)
    rad=math.radians
    def arm(side,s,upper,lower,palm,upper_normal=None,world=False):
        pose_arm(p,side,s,upper,lower,palm,upper_normal,world)
    def relaxed_fingers(side,scale=1.0):
        pose_relaxed_fingers(p,side,scale)
    def hang(side,s):
        """Relaxed hanging arm beside the thigh, palm toward the leg, thumb forward."""
        arm(side,s,HANG_UPPER(s),HANG_LOWER(s),HANG_PALM(s))
        relaxed_fingers(side)
    def raised(side,s,upper,lower,palm,amount,upper_normal=(0,-1,0)):
        """Blend from the hanging arm (amount 0) to the given raised directions (amount 1)."""
        arm(side,s,_mix(HANG_UPPER(s),upper,amount),_mix(HANG_LOWER(s),lower,amount),
            _mix(HANG_PALM(s),palm,amount),_mix(HANG_NORMAL(s),upper_normal,amount))
    def legs(feet=None,swing_roll=False):
        for side,s in [('L',1),('R',-1)]:
            offset=feet[side] if feet else (0,0)
            p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*.125,offset[0],.12+offset[1]),
                    (s*.125,-.6,.42),'Foot.'+side,swing_roll=swing_roll)
    def base(squat=0,lean=0,breathe=0,feet=None,sway=0.):
        p.reset(); p.translate('Hips',(sway,.11*squat,-.42*squat-STAND_DROP))
        p.rotate('Chest',(lean+breathe,0,0));p.rotate('Neck',(-lean*.35,0,0));p.update()
        for side,s in [('L',1),('R',-1)]:
            hang(side,s)
        legs(feet)
        return p
    def idle(t):
        # Breathing: chest and shoulders rise together, the relaxed arms follow the chest. Round 3: the weight
        # shifts from foot to foot once per loop (hips 14 mm, chest counter-roll), the head drifts a little
        # and the half-open hands sway with the body, so a 2 s idle never reads frozen.
        breath=math.sin(TAU*2*t)
        shift=math.sin(TAU*t)
        base(breathe=.022*breath,sway=.014*shift)
        p.rotate('Hips',(0,0,-.035*shift));p.rotate('Spine',(0,0,.030*shift))
        p.rotate('Chest',(.022*breath,.020*math.sin(TAU*t+.8),.020*shift));p.update()
        for side,s in [('L',1),('R',-1)]:
            p.rotate('Shoulder.'+side,(0,0,s*.014*breath))
        p.update()
        for side,s in [('L',1),('R',-1)]:
            hang(side,s)
        legs()
        p.rotate('Head',(-.012*breath,.06*math.sin(TAU*t+1.9),-.025*shift))
        p.rotate('Jaw',(-.012*math.sin(math.pi*t)**2,0,0))
        return p.snapshot()
    sampled(c,'Idle',61,idle)
    def finger(t):
        p.reset(); amount=smooth(t/.25) if t<.25 else 1 if t<.75 else 1-smooth((t-.75)/.25)
        for side in ['L','R']:p.fingers(side,amount)
        return p.snapshot()
    sampled(c,'FingerCurl',61,finger)
    def blink(t):
        base(); amount=pulse(t,.30,.45,.60)
        for side in ['L','R']:p.rig.pose.bones['Eye.'+side].scale=(1,1,1-.93*amount)
        return p.snapshot()
    sampled(c,'Blink',31,blink)
    def gait(t,run):
        stride=.28 if run else .20; duty=.48 if run else .62; lift=.075 if run else .045
        feet={}
        for side,phase in [('L',0),('R',.5)]:
            q=(t+phase)%1
            if q<duty: y=-stride*.5+stride*q/duty; z=0
            else:
                u=(q-duty)/(1-duty); y=stride*.5-stride*smooth(u);z=lift*math.sin(math.pi*u)**2
            feet[side]=(y,z)
        base(feet=feet,lean=.08 if run else .025)
        # Round 3: these two clips are the hand/finger reference of the four gaits (GRIP_BONES): relaxed
        # half-open hands while walking, a loose fist for the trot and run (the open palms read as A-pose).
        gait_arms(p,t,38 if run else 30,40 if run else 20,62 if run else 30,8. if run else 8.,14. if run else 16.,
                  .9 if run else .8)
        for side in ('L','R'):relaxed_fingers(side,1.9 if run else 1.0)
        return p.snapshot()
    sampled(c,'Walk',41,lambda t:gait(t,False));sampled(c,'Run',25,lambda t:gait(t,True))
    # v0.3.0 review: the static Crouch and the crouched gait share one sneaking pose (hips, torso, head
    # and paws in front of the knees), so stopping or starting to move while crouched never swaps between
    # a ~60 deg fold with arms hanging to the floor and an upright walk. Its eye (~0.93 m) stays inside the
    # 1.0 m crouched capsule and next to the crouched head volume (0.89 m) in first and third person.
    def crouch_torso(u=1.,bob=0.,sway=0.):
        p.rotate('Spine',(CROUCH_SPINE*u,0,.5*sway));p.rotate('Chest',((CROUCH_CHEST+bob)*u,0,sway))
        p.rotate('Neck',(CROUCH_NECK*u,0,-.6*sway));p.rotate('Head',(CROUCH_HEAD*u,0,-.4*sway));p.update()
    # Sneaking paws (armature space, whatever the lean): upper arms hang close to the ribs, elbows bent
    # ~100 deg, forearms level in front of the chest, wrists drooping, fingers loosely curled.
    PAW_UPPER=lambda s,sw:Vector((s*.14,.22-sw,-.96)).normalized()
    PAW_LOWER=lambda s,sw:Vector((-s*.26,-.88-.5*sw,.40)).normalized()
    PAW_PALM=lambda s:Vector((s*.25,.25,-1)).normalized()
    PAW_NORMAL=lambda s:Vector((-s*.25,-.95,.20)).normalized()
    def crouch_arms(u=1.,swing=0.):
        for side,s in [('L',1),('R',-1)]:
            sw=s*swing
            if u>=1-1e-9:
                arm(side,s,PAW_UPPER(s,sw),PAW_LOWER(s,sw),PAW_PALM(s),PAW_NORMAL(s),world=True)
            else:
                # Blend from the hanging idle arm (chest frame) toward the world-space paws.
                p.update()
                chest=p.rig.pose.bones['Chest']
                inverse=(chest.matrix@p.rest['Chest'].inverted()).to_3x3().inverted()
                raised(side,s,inverse@PAW_UPPER(s,sw),inverse@PAW_LOWER(s,sw),inverse@PAW_PALM(s),u,inverse@PAW_NORMAL(s))
            p.rotate('Hand.'+side,(.55*u,0,0))
            relaxed_fingers(side,1+.4*u)
    def crouch_leg(side,s,forward,rise,u=1.,swing_roll=True):
        """Tiptoe leg: ankle at CROUCH_TRACK out and 'rise' above its rest height, the foot rolled onto the
        toes by the same rise, the knee aimed outward so it passes beside the leaning torso."""
        roll=Matrix.Rotation(math.atan2(rise,.225),4,'X')@p.rest['Foot.'+side]
        track=.125+(CROUCH_TRACK-.125)*u
        p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*track,-forward,.12+rise),
                (s*(.125+(CROUCH_KNEE_OUT-.125)*u),-.6,.42+(CROUCH_POLE_Z-.42)*u),'Foot.'+side,roll,swing_roll=swing_roll)
    # Round 4 (director r4, item 1): Human_Crouch is a crouch-AMOUNT axis, u = normalized time (0 standing, 1 the
    # full sneak crouch). ActorVisualBinding scrubs it from the authoritative CrouchFraction and holds it, so
    # stopping a sneak (or striking while crouched) never replays a stand-to-crouch ramp from t = 0: the old
    # clip started standing and took 0.6 s to crouch, popping the body and the first-person eye upright.
    def crouch(t):
        u=t
        p.reset();p.translate('Hips',(0,CROUCH_HIPS_BACK*u,(CROUCH['hip']-.78)*u-STAND_DROP*(1-u)));p.update()
        crouch_torso(u)
        crouch_arms(u)
        for side,s,forward in (('L',1,.07),('R',-1,-.06)):
            crouch_leg(side,s,forward*u,CROUCH_TIPTOE*u,u)
        return p.snapshot()
    sampled(c,'Crouch',31,crouch)
    def jump(t):
        squat=.48*pulse(t,0,.18,.38)+.40*pulse(t,.65,.82,1)
        base(squat=squat,lean=.15*squat)
        airborne=.12*math.sin(math.pi*max(0,min(1,(t-.30)/.42)))**2 if .30<t<.72 else 0
        if airborne:
            p.translate('Hips',(0,0,airborne-STAND_DROP));p.update()
            for side,s in [('L',1),('R',-1)]:
                p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*.125,.03,.12+airborne+.015),(s*.125,-.5,.45),'Foot.'+side)
                p.rotate('UpperArm.'+side,(-.15,0,-s*1.0))
        return p.snapshot()
    sampled(c,'Jump',37,jump)
    def land(t):
        # Knees absorb the impact while the arms, still out from the fall, settle down to the thighs.
        u=smooth(t)
        base(squat=.4*(1-u),lean=.22*(1-u))
        balance=1-smooth(min(1,t/.8))
        for side,s in [('L',1),('R',-1)]:
            raised(side,s,(s*.75,-.35,-.55),(s*.55,-.55,-.62),(-s*.2,-.2,-1),balance)
        return p.snapshot()
    sampled(c,'Land',19,land)
    def turn(t):
        base(); a=.32*math.sin(math.pi*t)**2;p.rotate('Chest',(0,a,0));p.rotate('Head',(0,a*.55,0));return p.snapshot()
    sampled(c,'Turn',31,turn)
    def clap(t):
        base(); amount=smooth(t/.30) if t<.30 else 1 if t<.70 else 1-smooth((t-.70)/.30)
        separation=.15-.125*pulse(t,.30,.46,.66)
        for side,s in [('L',1),('R',-1)]:
            p.update(); hand=p.rig.pose.bones['Hand.'+side]
            target=hand.head.lerp(Vector((s*separation,-.38,1.05)),amount)
            p.chain('UpperArm.'+side,'LowerArm.'+side,target,(s*.62,-.15,.95))
            normal=Vector((-s,0,0)); direction=Vector((0,0,1))
            y=direction;z=normal;x=y.cross(z)
            rotation=Matrix((x,y,z)).transposed().to_quaternion()
            current=hand.matrix.to_quaternion(); mat=current.slerp(rotation,amount).to_matrix().to_4x4();mat.translation=target
            hand.matrix=mat;p.fingers(side,0);p.update()
        return p.snapshot()
    sampled(c,'Clap',31,clap)
    # Contact is measured from the actual sampled action, not a separate solve.
    p.rig.animation_data.action=bpy.data.actions['Human_Clap'];bpy.context.scene.frame_set(15);p.update()
    centers=[p.rig.pose.bones['Hand.'+s].matrix@Vector((0,.042,0)) for s in ['L','R']]
    c.contact={'clap_palm_center_distance_m':(centers[0]-centers[1]).length,'approximate_palm_thickness_m':.048,
               'target_surface_gap_m':.002,'sample_frame':15}
    def arm_between(side,s,a,b,u):
        """Arm between two direction sets (upper, lower, palm, upper normal); u=0 -> a."""
        arm(side,s,_mix(a[0],b[0],u),_mix(a[1],b[1],u),_mix(a[2],b[2],u),_mix(a[3],b[3],u))
    def grip_fingers(side,g):
        """Relaxed idle fingers (g=0) to the closed grip of the swat (g=1)."""
        for digit in ['Index','Middle','Ring','Little']:
            for i,(angle,full) in enumerate(zip(IDLE_FIST_ANGLES,FINGER_JOINT_ANGLES),1):
                p.rotate(f'{digit}{i:02d}.{side}',(angle*(1-g)+full*g-full*FINGER_REST_AMOUNT,0,0))
        for i,angle in enumerate(FINGER_JOINT_ANGLES,1):
            p.rotate(f'Thumb{i:02d}.{side}',(angle*(RELAXED_THUMB*(1-g)+g-FINGER_REST_AMOUNT)*THUMB_CURL_FACTOR,0,0))
    # Round 3 (anim-r3): a comic overhand slap whose normalized time t spans 0.526 authority seconds
    # (ActorVisualBinding plays it at clip length / 0.526), so the keys meet the strike timeline: cocked at t=.23 (the
    # authority sweep starts at .08 s = t .15; the arm IK already holds the hand cocked by then while the clip
    # finishes turning the arm), impact at t=.47 (the sweep ends at .25 s), follow-through to t=.72 and a
    # settle that ends exactly on the idle pose. The runtime arm IK drives the hand along a diagonal arc to
    # the authoritative contact; this clip gives the whole body and the elbow plane.
    SWAT_KEYS=(0.,.23,.36,.47,.72,1.)
    def swat(t):
        """Anticipation: the knees dip, hips and chest wind the right shoulder back (~24 deg), the clavicle
        lifts, the elbow goes up and the hand cocks behind the ear with the wrist bent back, the free arm
        points ahead. Strike: the chest whips across (~23 deg) and leans ~18 deg FORWARD into the slap, the
        knees give, the elbow stays bent (~120 deg) and the wrist snaps; the head tilts away from the arm
        (never the torso leaning back). Follow-through down across the body, then the idle pose."""
        yaw=keyed(t,[(0,0),(.23,-.42),(.47,.40),(.72,.30),(1,0)])
        lean=keyed(t,[(0,0),(.23,-.04),(.47,.46),(.72,.34),(1,0)])
        dip=keyed(t,[(0,0),(.23,.08),(.47,.20),(.72,.14),(1,0)])
        tilt=keyed(t,[(0,0),(.23,.10),(.47,.20),(.72,.10),(1,0)])
        clavicle=keyed(t,[(0,0),(.23,-.34),(.47,.10),(.72,.06),(1,0)])
        p.reset();p.translate('Hips',(0,.05*dip,-.42*dip-STAND_DROP))
        p.rotate('Hips',(0,.28*yaw,0));p.rotate('Spine',(.40*lean,.30*yaw,0))
        p.rotate('Chest',(.60*lean,.42*yaw,0));p.rotate('Neck',(-.40*lean,-.45*yaw,-.5*tilt))
        p.rotate('Head',(-.15*lean,-.25*yaw,-tilt))
        p.rotate('Shoulder.R',(0,0,clavicle));p.rotate('Shoulder.L',(0,0,.05*keyed(t,[(0,0),(.23,1),(.47,0)])))
        p.update()
        legs()
        # Keys: (upper arm, forearm, upper-arm roll, palm). The upper-arm roll is relative to the hanging arm's
        # frame carried along the swing, chosen so the elbow crease nearly faces its fold (-130 deg cocked: the
        # arm rotates outward to fold the forearm behind the head); the palm becomes a pronation twist carried
        # through the elbow, unwrapped key to key, so no bone roll flips between two frames.
        right=((HANG_UPPER(-1),HANG_LOWER(-1),0.,HANG_PALM(-1)),
               (Vector((-.80,.20,.56)),Vector((.35,.35,.87)),-130.,Vector((0,-1,.15))),
               (Vector((-.45,-.45,.77)),Vector((.10,-.55,.83)),-115.,Vector((0,-.83,-.55))),
               (Vector((-.18,-.93,.12)),Vector((.18,-.75,-.60)),-99.,Vector((-.20,-.62,.76))),
               (Vector((.25,-.55,-.80)),Vector((.75,-.35,-.55)),-55.,Vector((0,.6,-.4))),
               (HANG_UPPER(-1),HANG_LOWER(-1),0.,HANG_PALM(-1)))
        left=((HANG_UPPER(1),HANG_LOWER(1),0.,HANG_PALM(1)),
              (Vector((.30,-.80,.35)),Vector((.20,-.95,.20)),74.,HANG_PALM(1)),
              (Vector((.30,-.60,-.40)),Vector((-.05,-.95,-.05)),25.,Vector((-.6,0,-.8))),
              (Vector((.25,-.30,-.92)),Vector((-.35,-.85,.30)),-23.,Vector((0,.3,1))),
              (Vector((.25,-.30,-.92)),Vector((-.35,-.85,.30)),-23.,Vector((0,.3,1))),
              (HANG_UPPER(1),HANG_LOWER(1),0.,HANG_PALM(1)))
        for side,sign,keys in (('R',-1,right),('L',1,left)):
            twists=[];near=0.
            for upper,lower,roll,palm in keys:
                near=arm_twist_for(sign,upper,lower,roll,palm,near);twists.append(near)
            for k in range(5):
                if t<=SWAT_KEYS[k+1] or k==4:
                    u=(t-SWAT_KEYS[k])/(SWAT_KEYS[k+1]-SWAT_KEYS[k])
                    u=u*u*(3-2*u) if k<3 else smooth(u)   # wind-up and whip turn at most ~1.5x their mean rate
                    a,b=keys[k],keys[k+1]
                    upper,lower=_mix(a[0],b[0],u),_mix(a[1],b[1],u)
                    normal,palm=arm_frame(sign,upper,lower,a[2]+(b[2]-a[2])*u,twists[k]+(twists[k+1]-twists[k])*u)
                    arm(side,sign,upper,lower,palm,normal)
                    break
        p.rotate('Hand.R',(keyed(t,[(0,0),(.23,-.45),(.40,-.25),(.49,.55),(.72,.30),(1,0)]),0,0))
        grip_fingers('R',1-smooth((t-.72)/.28) if t>.72 else smooth(t/.10))
        relaxed_fingers('L')
        frown=keyed(t,[(0,0),(.23,1),(.72,1),(1,0)])
        p.rotate('Brow.L',(0,.14*frown,0));p.rotate('Brow.R',(0,-.14*frown,0))
        return p.snapshot()
    # Round 3: 61 samples (a 2 s source clip) so the whip keeps every 1/30 s key step under ~55 deg; the
    # runtime plays it 3.8x (clip length / 0.526 authority seconds), the same timeline as before.
    sampled(c,'Swat',61,swat)
    def hit(t):
        base();a=math.sin(math.pi*t)**2;p.rotate('Chest',(-.13*a,0,-.08*a));p.rotate('Head',(.12*a,0,.10*a))
        p.rotate('Jaw',(-.18*a,0,0))
        for side in ['L','R']:p.translate('Brow.'+side,(0,0,.012*a))
        return p.snapshot()
    sampled(c,'Hit',19,hit)
    def fallen(t,with_faint=False):
        u=smooth(t);base(squat=.7*math.sin(math.pi*u),lean=.2*math.sin(math.pi*u))
        p.rotate('Hips',(-1.48*u,0,.08*u));p.translate('Hips',(0,0,-.58*u-STAND_DROP))
        p.rotate('UpperLeg.L',(.1*u,0,0));p.rotate('UpperLeg.R',(.14*u,0,0))
        p.rotate('LowerLeg.L',(-.12*u,0,0));p.rotate('LowerLeg.R',(-.16*u,0,0))
        p.rotate('Head',(.16*u,0,.12*u));p.rotate('Jaw',(-.13*u,0,0))
        for side in ['L','R']:
            p.rig.pose.bones['Eye.'+side].scale=(1,1,1-.8*u)
            p.translate('Brow.'+side,(0,0,-.008*u))
        p.update();p.grounded(.002)
        return p.snapshot()
    sampled(c,'Fall',37,lambda t:fallen(t))
    def faint(t):return fallen(smooth(max(0,(t-.12)/.88)),True)
    sampled(c,'Faint',55,faint)
    sampled(c,'Recover',61,lambda t:fallen(1-smooth(t)))
    # ---- v0.3.0 animation pass: new clips, appended after every existing one (stable IDs) ----
    def brows(lift):
        for side in ['L','R']:p.translate('Brow.'+side,(0,0,lift))
    def jump_air(t):
        """Rising half of a jump (loop): knees tucked, arms flung up and out, a little flutter."""
        w=math.sin(TAU*t)
        base(lean=-.05+.015*w)
        legs({'L':(.05+.012*w,.17+.020*w),'R':(.08-.012*w,.13-.020*w)})
        for side,s in [('L',1),('R',-1)]:
            raised(side,s,(s*.80,-.12,.42+.06*s*w),(s*.35,-.30,.88),(-s*.35,-.9,.2),1)
            relaxed_fingers(side,.6)
        p.rotate('Head',(-.06,0,0));p.rotate('Jaw',(-.06,0,0));brows(.004)
        return p.snapshot()
    sampled(c,'JumpAir',31,jump_air)
    def fall_air(t):
        """Falling half of a jump (loop): legs pedal down toward the ground, arms windmill up (comic panic)."""
        base(lean=.06)
        feet={}
        for side,phase in [('L',0.),('R',.5)]:
            a=TAU*(t+phase)
            feet[side]=(.09*math.sin(a),.06+.05*math.cos(a))
        legs(feet)
        for side,s,phase in [('L',1,0.),('R',-1,.5)]:
            a=TAU*(t+phase)
            raised(side,s,(s*.55,-.30*math.cos(a),.78+.10*math.sin(a)),
                   (s*.30,-.35*math.cos(a+.9),.90),(-s*.3,-.95,0),1)
            relaxed_fingers(side,.3)
        p.rotate('Neck',(.10,0,0));p.rotate('Head',(.06,0,.03*math.sin(TAU*t)))
        p.rotate('Jaw',(-.13,0,0));brows(.008)
        return p.snapshot()
    sampled(c,'FallAir',31,fall_air)
    # Crouched walk sampled by the same gait clock as the four gaits (see CROUCH above).
    from human_locomotion_contract import foot as gait_foot, hip_height as gait_hip, distance as gait_distance
    c.contact['crouch_walk_distance_per_cycle_m']=gait_distance(CROUCH)
    def crouch_walk(t):
        # Round 3 sneak: hips sway over the stance foot, the torso rocks a little per step, the paws bob.
        w=math.cos(TAU*t)
        p.reset()
        p.translate('Hips',(.012*w,CROUCH_HIPS_BACK,gait_hip(t,CROUCH)-.78));p.update()
        crouch_torso(1,.015*math.sin(TAU*2*t),-.05*w)
        for side,s,offset in [('L',1,0.),('R',-1,.5)]:
            forward,z,_=gait_foot(t+offset,CROUCH)
            forward+=CROUCH_STANCE_SHIFT
            # On tiptoe; the trailing heel peels further (toes stay down) so the rear knee never drops onto
            # the floor; continuous in 'forward' across the stance/swing boundary. The peel and the swing
            # lift do not stack: the ankle rises by the larger of the two.
            heel=CROUCH_TIPTOE+CROUCH_HEEL*smooth((-forward-.03)/.20)
            crouch_leg(side,s,forward,max(z-.12+CROUCH_TIPTOE,heel))
        crouch_arms(1,.12*w)
        return p.snapshot()
    sampled(c,'CrouchWalk',61,crouch_walk)
    def yawn(t):
        """Long idle: a big stretching yawn (3 s). The clavicles lift and both arms reach straight up so the
        fists end above the head (v0.3.0 review), the chest arches back and the jaw opens wide."""
        a=pulse(t,.05,.36,.86); mouth=pulse(t,.14,.42,.80)
        base(lean=-.14*a,breathe=.02*math.sin(math.pi*min(1,t/.4)))
        for side,s in [('L',1),('R',-1)]:
            p.rotate('Shoulder.'+side,(0,0,s*.34*a))
        p.update()
        for side,s in [('L',1),('R',-1)]:
            raised(side,s,(s*.30,.10,.95),(-s*.12,.04,1.),(-s*.2,.9,.3),a)
            relaxed_fingers(side,1-.2*a)
        p.rotate('Neck',(-.10*a,0,0));p.rotate('Head',(-.24*a,0,.05*a))
        p.rotate('Jaw',(-.38*mouth,0,0));brows(.005*a)
        return p.snapshot()
    sampled(c,'Yawn',91,yawn)
    def victory(t):
        """Results (loop, 1 s): two springy cheers per loop. The knees dip deep and push up onto the toes
        of a tiny hop, the fists pump from shoulder height to straight up, the head bobs and the mouth
        shouts (v0.3.0 review: the first version moved the hips only 3 cm)."""
        w=TAU*2*t
        dip=.5+.5*math.cos(w)          # 1 at t=0 and .5: the knees are bent
        up=1-dip
        # Round 3: a real knee bounce (hips 16 cm down) and a 6 cm hop off the floor at the top.
        base(squat=.38*dip,lean=.12*dip-.08*up)
        p.translate('Hips',(0,.11*.38*dip,-.42*.38*dip+.06*up*up-STAND_DROP));p.update()
        legs({'L':(0,.06*up*up),'R':(0,.06*up*up)})
        for side,s in [('L',1),('R',-1)]:
            p.rotate('Shoulder.'+side,(0,0,s*.22*up))
        p.update()
        for side,s in [('L',1),('R',-1)]:
            raised(side,s,_mix((s*.80,-.30,.35),(s*.55,-.15,.82),up),_mix((s*.05,-.35,.94),(s*.12,-.20,.97),up),
                   (-s*.45,-.88,0),1)
            # Tight fists (knuckles folded, not the claw of a partial curl), thumb over the fingers.
            for digit in ['Index','Middle','Ring','Little']:
                for i,(angle,full) in enumerate(zip(VICTORY_FIST_ANGLES,FINGER_JOINT_ANGLES),1):
                    p.rotate(f'{digit}{i:02d}.{side}',(angle-full*FINGER_REST_AMOUNT,0,0))
            for i,angle in enumerate(FINGER_JOINT_ANGLES,1):
                p.rotate(f'Thumb{i:02d}.{side}',(angle*(.95-FINGER_REST_AMOUNT)*THUMB_CURL_FACTOR,0,0))
        p.rotate('Neck',(-.04*up,0,0));p.rotate('Head',(-.10*up+.06*dip,0,.04*math.sin(TAU*t)))
        # Round 3: a wide grin rather than an O (the runtime Smile morph pulls the corners up and out).
        p.rotate('Jaw',(-.14-.06*up,0,0));brows(.004+.004*up)
        return p.snapshot()
    sampled(c,'Victory',31,victory)
    c.contact['minimum_leg_reach_margin_m']=p.minimum_reach_margin

def mosquito(c):
    p=Pose(c)
    # Neutral joint locations are retained for a tripod stance on the authored plane.
    rest={b.name:b.head.copy() for b in p.rig.pose.bones}
    def stance(fold=.30):
        p.reset();p.rotate('Wing.L',(.12,fold,0));p.rotate('Wing.R',(.12,-fold,0));return p
    def wing(t,amplitude=.56):
        value=amplitude*math.cos(TAU*3*t)
        p.rotate('Wing.L',(value,0,0));p.rotate('Wing.R',(value,0,0))
    def flight(t,hover=False):
        stance(0);wing(t);p.rotate('Abdomen01',(-.06+.018*math.sin(TAU*t),0,0))
        for side in ['L','R']:
            for i in range(1,4):p.rotate(f'Leg{i}02.{side}',(.30,0,0))
        return p.snapshot()
    sampled(c,'Fly',13,lambda t:flight(t));sampled(c,'Hover',13,lambda t:flight(t,True))
    def idle(t):stance();p.rotate('Abdomen01',(.014*math.sin(TAU*t),0,0));return p.snapshot()
    sampled(c,'Idle',61,idle);sampled(c,'PerchIdle',61,idle)
    def perch(t):
        stance();u=smooth(t);flap=.56*(1-u)*math.cos(TAU*3*t)+.12*u
        p.rotate('Wing.L',(flap,.30*u,0));p.rotate('Wing.R',(flap,-.30*u,0))
        for side in ['L','R']:
            for i in range(1,4):p.rotate(f'Leg{i}02.{side}',(.30*(1-u),0,0))
        return p.snapshot()
    sampled(c,'PerchEnter',25,perch);sampled(c,'Land',25,perch)
    def brake(t):
        stance();u=smooth(t);flap=.56*(1-u)*math.cos(TAU*3*t)+.12*u
        p.rotate('Wing.L',(flap,.30*u,0));p.rotate('Wing.R',(flap,-.30*u,0))
        p.rotate('Thorax',(-.13*math.sin(math.pi*t),0,0));return p.snapshot()
    sampled(c,'Brake',25,brake)
    def surface(t):
        stance()
        for side,s in [('L',1),('R',-1)]:
            for i in range(1,4):
                phase=(t+(.5 if (i==2)==(s==1) else 0))%1; duty=.65;stride=.026
                if phase<duty:dy=-stride*.5+stride*phase/duty;dz=0
                else:
                    u=(phase-duty)/(1-duty);dy=stride*.5-stride*smooth(u);dz=.012*math.sin(math.pi*u)**2
                target=rest[f'Leg{i}03.{side}']+Vector((0,dy,dz))
                pole=rest[f'Leg{i}02.{side}']+Vector((s*.03,-.02,.02))
                p.chain(f'Leg{i}01.{side}',f'Leg{i}02.{side}',target,pole,f'Leg{i}03.{side}')
        return p.snapshot()
    sampled(c,'SurfaceWalk',31,surface)
    def bite(t,amount=1):
        stance();p.rotate('Head',(.08*amount,0,0));p.rotate('Proboscis',(-.08*amount,0,0))
        p.rotate('Abdomen01',(.014*math.sin(TAU*t)*amount,0,0));p.rotate('Abdomen02',(-.02*math.sin(TAU*t)*amount,0,0))
        return p.snapshot()
    sampled(c,'BiteStart',19,lambda t:bite(t,smooth(t)));sampled(c,'BiteLoop',31,bite)
    sampled(c,'Bite',37,lambda t:bite(t,math.sin(math.pi*t)**2))
    def detach(t):
        u=smooth(t);bite(t,1-u);flap=.56*u*math.cos(TAU*3*t)+.12*(1-u)
        p.rotate('Wing.L',(flap,.30*(1-u),0));p.rotate('Wing.R',(flap,-.30*(1-u),0));return p.snapshot()
    sampled(c,'Detach',19,detach)
    def hit(t):
        stance();u=smooth(t);p.rotate('Thorax',(.7*u,.12*math.sin(math.pi*t),.9*u));wing(t,.25*(1-u));return p.snapshot()
    sampled(c,'Hit',19,hit)
    def fall(t):
        stance();u=smooth(t);p.rotate('Thorax',(1.48*u,.15*math.sin(math.pi*t),1.1*u));p.rotate('Wing.L',(.12+.55*u,.3,0));p.rotate('Wing.R',(.12+.55*u,-.3,0));return p.snapshot()
    sampled(c,'Fall',31,fall);sampled(c,'Recover',37,lambda t:fall(1-smooth(t)))
    c.contact['minimum_surface_leg_reach_margin_m']=p.minimum_reach_margin
