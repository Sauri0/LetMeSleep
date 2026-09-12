"""Frame-sampled alpha animation: explicit support targets, smooth timing, stable named rig."""
import bpy
import math
from mathutils import Vector, Matrix

TAU=2*math.pi

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
    def chain(self,upper,lower,target,pole,end=None,end_rotation=None):
        self.update(); u=self.rig.pose.bones[upper]; l=self.rig.pose.bones[lower]
        origin=u.head.copy(); a=u.bone.length; b=l.bone.length
        d=Vector(target)-origin; length=d.length; direction=d.normalized()
        self.minimum_reach_margin=min(self.minimum_reach_margin,a+b-length)
        length=min(a+b-.0002,max(abs(a-b)+.0002,length))
        target=origin+direction*length
        along=(a*a-b*b+length*length)/(2*length)
        pole=Vector(pole)-origin; bend=(pole-direction*pole.dot(direction)).normalized()
        joint=origin+direction*along+bend*math.sqrt(max(0,a*a-along*along))
        self.point(upper,origin,joint-origin,Vector((0,-1,0)))
        self.point(lower,joint,target-joint,Vector((0,-1,0)))
        if end:
            matrix=(end_rotation if end_rotation is not None else self.rest[end]).copy()
            matrix.translation=target; self.rig.pose.bones[end].matrix=matrix; self.update()
    def fingers(self,side,amount):
        for digit in ['Index','Middle','Ring','Little','Thumb']:
            for i,angle in enumerate([.42,.90,1.20],1):
                self.rotate(f'{digit}{i:02d}.{side}',(angle*amount*(.72 if digit=='Thumb' else 1),0,0))
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

def human(c):
    p=Pose(c)
    def base(squat=0,lean=0,breathe=0,feet=None):
        p.reset(); p.translate('Hips',(0,.11*squat,-.42*squat-.018))
        p.rotate('Chest',(lean+breathe,0,0));p.rotate('Neck',(-lean*.35,0,0));p.update()
        for side,s in [('L',1),('R',-1)]:
            p.rotate('UpperArm.'+side,(.04,0,-s*1.20));p.rotate('LowerArm.'+side,(.16,0,0));p.fingers(side,.10)
            offset=feet[side] if feet else (0,0)
            p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*.125,offset[0],.12+offset[1]),
                    (s*.125,-.6,.42),'Foot.'+side)
        return p
    def idle(t): base(breathe=.014*math.sin(TAU*t));return p.snapshot()
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
    def crouch(t):base(squat=smooth(min(1,t/.6)),lean=.95*smooth(min(1,t/.6)));return p.snapshot()
    sampled(c,'Crouch',31,crouch)
    def jump(t):
        squat=.48*pulse(t,0,.18,.38)+.40*pulse(t,.65,.82,1)
        base(squat=squat,lean=.15*squat)
        airborne=.12*math.sin(math.pi*max(0,min(1,(t-.30)/.42)))**2 if .30<t<.72 else 0
        if airborne:
            p.translate('Hips',(0,0,airborne-.018));p.update()
            for side,s in [('L',1),('R',-1)]:
                p.chain('UpperLeg.'+side,'LowerLeg.'+side,(s*.125,.03,.12+airborne+.015),(s*.125,-.5,.45),'Foot.'+side)
                p.rotate('UpperArm.'+side,(-.15,0,-s*1.0))
        return p.snapshot()
    sampled(c,'Jump',37,jump)
    def land(t):base(squat=.4*(1-smooth(t)),lean=.22*(1-smooth(t)));return p.snapshot()
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
    def swat(t):
        base();p.fingers('R',1)
        a=pulse(t,0,.30,.78); strike=pulse(t,.3,.48,1)
        p.rotate('UpperArm.R',(-.8*a+.85*strike,.15*a-.25*strike,1.20-.45*strike))
        p.rotate('LowerArm.R',(.35+.5*a-.25*strike,0,0));p.rotate('Chest',(.10*strike,-.10*a+.15*strike,0))
        return p.snapshot()
    sampled(c,'Swat',31,swat)
    def hit(t):base();a=math.sin(math.pi*t)**2;p.rotate('Chest',(-.13*a,0,-.08*a));p.rotate('Head',(.12*a,0,.10*a));return p.snapshot()
    sampled(c,'Hit',19,hit)
    def fallen(t,with_faint=False):
        u=smooth(t);base(squat=.7*math.sin(math.pi*u),lean=.2*math.sin(math.pi*u))
        p.rotate('Hips',(-1.48*u,0,.08*u));p.translate('Hips',(0,0,-.58*u-.018))
        p.rotate('UpperLeg.L',(.1*u,0,0));p.rotate('UpperLeg.R',(.14*u,0,0))
        p.rotate('LowerLeg.L',(-.12*u,0,0));p.rotate('LowerLeg.R',(-.16*u,0,0))
        p.rotate('Head',(.16*u,0,.12*u));p.update();p.grounded(.002)
        return p.snapshot()
    sampled(c,'Fall',37,lambda t:fallen(t))
    def faint(t):return fallen(smooth(max(0,(t-.12)/.88)),True)
    sampled(c,'Faint',55,faint)
    sampled(c,'Recover',61,lambda t:fallen(1-smooth(t)))
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
