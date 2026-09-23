"""Human pupil pivots and actual eyelid geometry, shared by menu/game exports.

v0.3.0 caricature face (PER-01/PER-04): huge protruding eyeballs centred at the
first-person camera height (Socket.Eye z=1.53, required by Unity). Open lids
rest rotated back into the forehead/cheek so the neutral eye reads as a clean
white sphere; the piecewise morph samples sweep them over the front.
Round 3: 121 mm wide x 126 mm tall (slightly vertical) eyeballs set 11 mm
deeper into a shallow socket so about half of each globe sits in the face.
Round 5 (art director): the open upper lid returns to the hidden round-3 rest
(rotated back into the forehead) so no skin rim covers the globe, and the
globes grow to 132 mm wide x 148 mm tall (radius +9%, height factor 1.12)
around the unchanged centres (Socket.Eye z=1.53).
"""
import math

EYE_RADIUS=.066
EYE_HEIGHT_FACTOR=1.12
EYE_CENTERS={'L':(.067,-.132,1.53),'R':(-.067,-.132,1.53)}
# Lid shells (1.09 r) sit outside the globe and the pupil lens (max radius r+.003).
LID_RADII=(EYE_RADIUS*1.09,EYE_RADIUS*1.09,EYE_RADIUS*EYE_HEIGHT_FACTOR*1.07)
LID_SIDE_WRAP=.25
LID_OPEN_TILT=.62
# Round 5: the round-4 8-10 mm lid rim read as tired eyes; the open upper lid
# is back at the round-3 rest, folded into the forehead (hidden).
UPPER_OPEN_ANGLES=[.035,.09,.15,.21,.27,.33]
UPPER_OPEN_TILT=LID_OPEN_TILT

CONTRACT={
    'renderer':'HumanHead','head_bone':'Head','neck_bone':'Neck',
    'head_forward_local':[0,0,1],'head_up_local':[0,1,0],
    'eye_bones':['Eye.L','Eye.R'],'eye_forward_local':[0,1,0],'eye_up_local':[0,0,1],
    'eye_bind_centers_source_m':{side:list(center) for side,center in EYE_CENTERS.items()},
    'blink_shapes':{'L':'Blink.L','R':'Blink.R'},'unity_blink_range':[0,100],
    'blink_samples':{'L':['Basis','Blink25.L','Blink50.L','Blink75.L','Blink.L'],
                     'R':['Basis','Blink25.R','Blink50.R','Blink75.R','Blink.R']},
    'blink_interpolation':'Piecewise adjacent samples at closure0/.25/.50/.75/1; two neighboring morph weights sum to100. Basis needs no weight. This keeps lid paths outside the globe.',
    'writer':'One Presentation facial controller after animation, shared with gameplay',
    'tracking':'Live target, smoothed/clamped offset from this frame animated base; never accumulate last frame offset',
    'suggested_limits_degrees':{'head_yaw':55,'head_pitch':25,'neck_yaw':20,'eye_yaw':22,'eye_pitch':15},
    'legacy_blink_clip':'Eye local scale Z is a legacy signal only; read before resetting scale to1, map (1-z)/.93 to lid closure',
    'eye_scale_runtime':[1,1,1],
    'eye_radius_m':EYE_RADIUS,
    'scope':'Same rig names/hierarchy; v0.3.0 sketch face: large protruding eyes centred at Socket.Eye height 1.53; head/cap/skin material customization retained'}


def eyelids(c, mesh, skin):
    c.facial_contract=CONTRACT
    names=lambda side:['Blink25.'+side,'Blink50.'+side,'Blink75.'+side,'Blink.'+side]
    c.eyelid_targets={name:{} for side in ['L','R'] for name in names(side)}
    rx,ry,rz=LID_RADII
    for side,sign in [('L',1),('R',-1)]:
        center=EYE_CENTERS[side]
        def point(phi,theta,tilt):
            # Front shell of the lid ellipsoid, wrapped slightly past the eye
            # equator, then pitched about the eye's X axis (tilt>0 folds the
            # upper lid back over the top; the lower lid folds under/back).
            x=rx*math.sin(phi)*math.sin(theta)
            y=-ry*math.sin(phi)*math.cos(theta)
            z=rz*math.cos(phi)
            y,z=y*math.cos(tilt)+z*math.sin(tilt),-y*math.sin(tilt)+z*math.cos(tilt)
            return (center[0]+x,center[1]+y,center[2]+z)
        wrap=math.pi/2+LID_SIDE_WRAP
        for upper in [True,False]:
            # Open lids rest rotated into the head; closed lids meet below centre.
            open_angles=list(UPPER_OPEN_ANGLES) if upper else [math.pi-v for v in [.035,.09,.15,.21,.27,.33]]
            closed_angles=[.035,.365,.700,1.035,1.37,1.70] if upper else [math.pi-.035,2.825,2.54,2.26,1.98,1.70]
            open_tilt=UPPER_OPEN_TILT if upper else -LID_OPEN_TILT
            count=13
            thetas=[-wrap+j*2*wrap/(count-1) for j in range(count)]
            vertices=[point(phi,theta,open_tilt) for phi in open_angles for theta in thetas]
            faces=[(r*count+j,r*count+j+1,(r+1)*count+j+1,(r+1)*count+j)
                   for r in range(len(open_angles)-1) for j in range(count-1)]
            obj=mesh('HeadLid'+('Upper.' if upper else 'Lower.')+side,vertices,faces,skin,'Head')
            for name,closure in zip(names(side),[.25,.50,.75,1]):
                angles=[a+(b-a)*closure for a,b in zip(open_angles,closed_angles)]
                tilt=open_tilt*(1-closure)
                targets=[point(phi,theta,tilt) for phi in angles for theta in thetas]
                for original,target in zip(vertices,targets):
                    key=tuple(round(v,6) for v in original)
                    assert key not in c.eyelid_targets[name]
                    c.eyelid_targets[name][key]=target


def finish_shapes(c, obj):
    if obj.name!='HumanHead' or not getattr(c,'eyelid_targets',None):return
    obj.shape_key_add(name='Basis',from_mix=False)
    for name,targets in c.eyelid_targets.items():
        shape=obj.shape_key_add(name=name,from_mix=False)
        found=set()
        for vertex in obj.data.vertices:
            # Blender stores float32 coordinates; rounding each side separately
            # can straddle a decimal boundary after object join. Match within
            # 2 micrometres and require a unique one-to-one correspondence.
            matches=[key for key in targets if all(abs(a-b)<.000002 for a,b in zip(key,vertex.co))]
            assert len(matches)<=1,(name,vertex.index,'Ambiguous eyelid coordinate')
            if matches:
                key=matches[0];assert key not in found,(name,key,'Duplicate eyelid target')
                shape.data[vertex.index].co=targets[key];found.add(key)
        assert len(found)==len(targets),(name,len(found),len(targets),'Eyelid target mapping changed during join')
        shape.slider_min=0;shape.slider_max=1;shape.value=0


def _outward_measure(points, center):
    a=tuple(points[1][i]-points[0][i] for i in range(3))
    b=tuple(points[2][i]-points[0][i] for i in range(3))
    normal=(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
    radial=tuple(sum(p[i] for p in points)/len(points)-center[i] for i in range(3))
    return sum(a*b for a,b in zip(normal,radial))


def orient_lid_faces(c, obj):
    """Run AFTER global normal consistency: open shells need an eye-center reference.

    Flips polygon winding only. Vertex order, shape coordinates, skin and
    materials stay unchanged. A double-sided ray check cannot verify this.
    """
    if obj.name!='HumanHead':return None
    result={'flipped_faces':0,'validated_faces':0,'samples_per_face':5,'sides':{}}
    for side,sign in [('L',1),('R',-1)]:
        targets=c.eyelid_targets['Blink.'+side]
        ids={v.index for v in obj.data.vertices if any(
            all(abs(a-b)<.000002 for a,b in zip(key,v.co)) for key in targets)}
        assert len(ids)==156,(side,len(ids),'Eyelid vertex mapping changed')
        faces=[p for p in obj.data.polygons if all(i in ids for i in p.vertices)]
        assert len(faces)==120,(side,len(faces),'Eyelid topology changed')
        center=EYE_CENTERS[side];flipped=0
        for polygon in faces:
            points=[obj.data.vertices[i].co for i in polygon.vertices]
            if _outward_measure(points,center)<0:
                polygon.flip();flipped+=1
        # Validate all authored shapes, including the formerly culled lower lid.
        for name in CONTRACT['blink_samples'][side]:
            coordinates=obj.data.shape_keys.key_blocks[name].data
            for polygon in faces:
                assert _outward_measure([coordinates[i].co for i in polygon.vertices],center)>1e-12,(
                    side,name,polygon.index,'Inward or degenerate eyelid face')
        result['sides'][side]={'faces':len(faces),'flipped':flipped}
        result['flipped_faces']+=flipped;result['validated_faces']+=len(faces)
    obj.data.update()
    return result


def preview_pose(rig,head,closure,yaw=0,pitch=0):
    """Reference mapping for Blender witnesses; runtime has one separate owner."""
    from mathutils import Quaternion
    closure=max(0,min(1,closure));scaled=closure*4;lower=min(3,int(scaled));mix=scaled-lower
    for side,names in CONTRACT['blink_samples'].items():
        for name in names[1:]:head.data.shape_keys.key_blocks[name].value=0
        if lower>0:head.data.shape_keys.key_blocks[names[lower]].value=1-mix
        head.data.shape_keys.key_blocks[names[lower+1]].value=mix
        eye=rig.pose.bones['Eye.'+side];eye.rotation_mode='QUATERNION'
        eye.rotation_quaternion=Quaternion((0,0,1),math.radians(yaw))@Quaternion((1,0,0),math.radians(pitch))
        eye.scale=(1,1,1)
