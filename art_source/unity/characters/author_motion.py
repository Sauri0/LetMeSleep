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
IDLE_FIST_ANGLES=(.55,.65,.45)
RELAXED_THUMB=.55
VICTORY_FIST_ANGLES=(1.25,1.15,.95)
# v0.3.0 review: one sneaking crouch shared by Human_Crouch (static) and Human_CrouchWalk. Gait
# parameters follow human_locomotion_contract (contacts L at 0 and R at .5, 0.96875 m per cycle = the
# Walk profile at the 1.55 m/s crouch speed); 'hip' is the UpperLeg origin height.
CROUCH=dict(clip='Human_CrouchWalk',speed=1.55,contacts=3.2,duty=.56,hip=.395,rise=.012,lift=.07,ramp=.22)
CROUCH_SPINE,CROUCH_CHEST,CROUCH_NECK,CROUCH_HEAD=.46,.62,-.56,-.24
CROUCH_HIPS_BACK=.07
CROUCH_HEEL=.09

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

def human(c):
    p=Pose(c)
    rad=math.radians
    HANG_UPPER=lambda s:Vector((s*math.sin(rad(ARM_ABDUCTION)),-math.sin(rad(ARM_FORWARD)),-1)).normalized()
    HANG_LOWER=lambda s:Vector((s*math.sin(rad(FOREARM_ABDUCTION)),-math.sin(rad(FOREARM_FORWARD)),-1)).normalized()
    HANG_NORMAL=lambda s:Vector((-s*math.sin(rad(UPPER_ARM_TWIST)),-math.cos(rad(UPPER_ARM_TWIST)),0))
    HANG_PALM=lambda s:Vector((-s,0,0))
    def arm(side,s,upper,lower,palm,upper_normal=None):
        """Arm authored as directions in the (rest) chest frame; they follow the animated chest."""
        p.update()
        chest=p.rig.pose.bones['Chest']
        turn=(chest.matrix@p.rest['Chest'].inverted()).to_3x3()
        upper_normal=HANG_NORMAL(s) if upper_normal is None else Vector(upper_normal)
        bone=p.rig.pose.bones['UpperArm.'+side]
        p.point(bone.name,bone.head.copy(),turn@Vector(upper).normalized(),turn@upper_normal)
        bone=p.rig.pose.bones['LowerArm.'+side]
        p.point(bone.name,bone.head.copy(),turn@Vector(lower).normalized(),turn@Vector(palm))
    def relaxed_fingers(side,scale=1.0):
        for digit in ['Index','Middle','Ring','Little']:
            for i,(angle,full) in enumerate(zip(IDLE_FIST_ANGLES,FINGER_JOINT_ANGLES),1):
                p.rotate(f'{digit}{i:02d}.{side}',(angle*scale-full*FINGER_REST_AMOUNT,0,0))
        for i,angle in enumerate(FINGER_JOINT_ANGLES,1):
            p.rotate(f'Thumb{i:02d}.{side}',(angle*(RELAXED_THUMB*scale-FINGER_REST_AMOUNT)*THUMB_CURL_FACTOR,0,0))
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
    def base(squat=0,lean=0,breathe=0,feet=None):
        p.reset(); p.translate('Hips',(0,.11*squat,-.42*squat-STAND_DROP))
        p.rotate('Chest',(lean+breathe,0,0));p.rotate('Neck',(-lean*.35,0,0));p.update()
        for side,s in [('L',1),('R',-1)]:
            hang(side,s)
        legs(feet)
        return p
    def idle(t):
        # Breathing: chest and shoulders rise together, the relaxed arms follow the chest.
        breath=math.sin(TAU*t)
        base(breathe=.018*breath)
        for side,s in [('L',1),('R',-1)]:
            p.rotate('Shoulder.'+side,(0,0,s*.012*breath))
        p.update()
        for side,s in [('L',1),('R',-1)]:
            hang(side,s)
        p.rotate('Head',(-.010*breath,0,0))
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
        for side,s in [('L',1),('R',-1)]:
            p.rotate('UpperArm.'+side,(s*(.28 if run else .16)*math.sin(TAU*t),0,-s*1.18))
            p.rotate('LowerArm.'+side,(.6 if run else .2,0,0));p.fingers(side,.2)
        return p.snapshot()
    sampled(c,'Walk',41,lambda t:gait(t,False));sampled(c,'Run',25,lambda t:gait(t,True))
    # v0.3.0 review: the static Crouch and the crouched gait share one sneaking pose (hips, torso, head
    # and paws in front of the knees), so stopping or starting to move while crouched never swaps between
    # a ~60 deg fold with arms hanging to the floor and an upright walk. Its eye (~0.93 m) stays inside the
    # 1.0 m crouched capsule and next to the crouched head volume (0.89 m) in first and third person.
    def crouch_torso(u=1.,bob=0.):
        p.rotate('Spine',(CROUCH_SPINE*u,0,0));p.rotate('Chest',((CROUCH_CHEST+bob)*u,0,0))
        p.rotate('Neck',(CROUCH_NECK*u,0,0));p.rotate('Head',(CROUCH_HEAD*u,0,0));p.update()
    def crouch_arms(u=1.,swing=0.):
        for side,s in [('L',1),('R',-1)]:
            sw=s*swing
            raised(side,s,(s*.30,-.42-sw,-.86),(s*.08,-.80-sw,-.52),(-s*.8,-.3,-.2),u)
            relaxed_fingers(side,1-.1*u)
    def crouch(t):
        u=smooth(min(1,t/.6))
        p.reset();p.translate('Hips',(0,CROUCH_HIPS_BACK*u,(CROUCH['hip']-.78)*u-STAND_DROP*(1-u)));p.update()
        crouch_torso(u)
        crouch_arms(u)
        legs({'L':(-.05*u,0),'R':(.07*u,0)},swing_roll=True)
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
    def swat(t):
        """v0.3.0 review: a whole-body comic swat. Anticipation (0-.30): the knees dip, the hips and chest
        turn the striking shoulder back (~23 deg) and the torso leans back while the right hand cocks
        above the ear. Strike (.30-.46): the chest whips across (~27 deg the other way) and forward
        (~16 deg), the hips follow and the knees give. Follow-through (.46-.72) with the arm across the
        body, then it settles (.72-1) into exactly the idle pose (hanging arm, relaxed hand), so the
        next clip never pops. The gameplay arm IK owns the arm during the authoritative sweep."""
        wind=pulse(t,0,.30,.46); hit=pulse(t,.30,.46,1.)
        dip=.07*pulse(t,.04,.28,.50)+.10*pulse(t,.34,.50,.92)
        yaw=-.40*wind+.47*hit; lean=-.06*wind+.28*hit
        p.reset();p.translate('Hips',(0,.05*dip,-.42*dip-STAND_DROP))
        p.rotate('Hips',(0,.30*yaw,0));p.rotate('Spine',(.30*lean,.30*yaw,0))
        p.rotate('Chest',(.70*lean,.40*yaw,.05*hit));p.rotate('Neck',(-.45*lean,-.45*yaw,0))
        p.rotate('Shoulder.R',(0,0,-.30*wind+.12*hit));p.rotate('Shoulder.L',(0,0,.06*wind))
        p.update()
        legs({'L':(-.02-.04*hit,0),'R':(.03+.02*wind,0)})
        # Upper-arm roll hints chosen so the roll never whips through the hint direction (<=31 deg per frame).
        rest=(HANG_UPPER(-1),HANG_LOWER(-1),HANG_PALM(-1),HANG_NORMAL(-1))
        cocked=(Vector((-.62,.30,.62)),Vector((.05,-.30,.95)),Vector((.3,-.9,0)),Vector((-.203,-.946,.254)))
        across=(Vector((.18,-.96,.18)),Vector((.45,-.88,.05)),Vector((.9,-.3,-.2)),Vector((.774,.027,-.632)))
        follow=(Vector((.40,-.60,-.55)),Vector((.62,-.25,-.65)),Vector((.5,.6,.1)),Vector((.5,.3,.8)))
        if t<.30: arm_between('R',-1,rest,cocked,smooth(t/.30))
        elif t<.52: arm_between('R',-1,cocked,across,smooth((t-.30)/.22))
        elif t<.72: arm_between('R',-1,across,follow,smooth((t-.52)/.20))
        else: arm_between('R',-1,follow,rest,smooth((t-.72)/.28))
        grip_fingers('R',1-smooth((t-.72)/.28) if t>.72 else smooth(t/.12))
        # The free arm counterbalances: out and back on the wind-up, forward with the strike.
        free=(Vector((.45,.35,-.82)),Vector((.30,.10,-.95)),HANG_PALM(1),HANG_NORMAL(1))
        brace=(Vector((.25,-.45,-.86)),Vector((.15,-.75,-.64)),HANG_PALM(1),HANG_NORMAL(1))
        hang('L',1)
        if wind>0 or hit>0:
            lu=HANG_UPPER(1);ll=HANG_LOWER(1)
            upper=_mix(_mix(lu,free[0],wind),brace[0],hit*(1-wind));lower=_mix(_mix(ll,free[1],wind),brace[1],hit*(1-wind))
            arm('L',1,upper,lower,HANG_PALM(1));relaxed_fingers('L')
        p.rotate('Brow.L',(0,.14*hit,0));p.rotate('Brow.R',(0,-.14*hit,0))
        return p.snapshot()
    sampled(c,'Swat',31,swat)
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
        p.reset()
        p.translate('Hips',(0,CROUCH_HIPS_BACK,gait_hip(t,CROUCH)-.78));p.update()
        crouch_torso(1,.015*math.sin(TAU*2*t))
        for side,s,offset in [('L',1,0.),('R',-1,.5)]:
            forward,z,_=gait_foot(t+offset,CROUCH)
            # The trailing heel peels off the floor (toes stay down) so the low hips never drop the rear
            # knee onto the floor; continuous in 'forward' across the stance/swing boundary.
            # The peel and the swing lift do not stack: the ankle rises by the larger of the two.
            heel=CROUCH_HEEL*smooth((-forward-.03)/.20)
            roll=Matrix.Rotation(math.atan2(heel,.225),4,'X')@p.rest['Foot.'+side]
            ankle=.12+max(z-.12,heel)
            p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*.125,-forward,ankle),(s*.125,-.6,.42),'Foot.'+side,roll,swing_roll=True)
        crouch_arms(1,.22*math.cos(TAU*t))
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
        base(squat=.26*dip,lean=.10*dip-.08*up)
        p.translate('Hips',(0,.11*.26*dip,-.42*.26*dip+.035*up*up-STAND_DROP));p.update()
        legs({'L':(0,.035*up*up),'R':(0,.035*up*up)})
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
        p.rotate('Jaw',(-.26-.12*up,0,0));brows(.004+.004*up)
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
