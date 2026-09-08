"""Original shared facial channels for all six editable character faces.

Only facial vertices deform. Body anchors, skeleton names and hit surfaces are
unchanged. The same named channels exist in every cosmetic face variant.
"""
from math import sin, pi
from mathutils import Vector

CHANNELS = ['BlinkL','BlinkR','GazeX','GazeY','BrowUp','BrowDown',
            'MouthOpen','MouthSmile','MouthPress','CheekLift']

def build_face(api, species, expression):
    Mesh, curve, g = api['Mesh'], api['curve'], api['g']
    human = species == 'human'
    mesh = Mesh('%s_face_%d' % (species, expression))
    groups = {}
    def section(name, fn):
        begin = len(mesh.v)
        fn()
        groups[name] = list(range(begin, len(mesh.v)))
    eye_y, eye_z = (1.596, -.142) if human else (.033, -.129)
    eye_x = .068 if human else .043
    eye_scale = (.051,.043,.029) if human else (.043,.047,.026)
    for side, suffix in [(-1,'L'),(1,'R')]:
        section('eye'+suffix,lambda side=side: mesh.ellipsoid((side*eye_x,eye_y,eye_z),eye_scale,'eye_white','head',24,16))
        pupil_x = side*eye_x + (-.004 if human and expression==1 else 0)
        pupil_y = eye_y - (.006 if expression==1 else .001)
        pupil_z = eye_z - (.027 if human else .024)
        def pupil():
            mesh.ellipsoid((pupil_x,pupil_y,pupil_z),(.017 if human else .016,.022,.010 if human else .008),'pupil','head',20,12)
            mesh.ellipsoid((pupil_x-.004,pupil_y+.009,pupil_z-.010),(.0045,.006,.002),'eye_white','head',12,8)
        section('pupil'+suffix,pupil)
        if human:
            points=[(side*.020,1.603 if expression==1 else 1.616,-.156),(side*.066,1.613 if expression==1 else 1.637,-.173),(side*.115,1.606 if expression==1 else 1.615,-.148)]
        else:
            low = .014 if expression==2 else 0.0
            points=[(side*.004,.074-low,-.143),(side*.042,.081-low,-.151),(side*.082,.066-low,-.132)]
        section('lid'+suffix,lambda points=points:mesh.tube(curve(points,5),.008 if expression==1 else .006,'skin' if human else 'insect_primary','head',10))
        if human:
            points=[(side*.022,1.656 if expression!=2 else 1.646,-.136),(side*.064,1.670,-.135),(side*.112,1.650 if expression!=2 else 1.675,-.119)]
        else:
            points=[(side*.009,.087,-.131),(side*.040,.097+(.014 if expression==1 else 0),-.126),(side*.078,.083+(.008 if expression==2 else 0),-.118)]
        path=curve(points,5)
        section('brow'+suffix,lambda path=path:mesh.tube(path,[(.004 if human else .0025)+sin(pi*i/(len(path)-1))*(.008 if human else .0035) for i in range(len(path))],'hair' if human else 'insect_dark','head',10))
        if human:
            path=curve([(side*.025,1.566,-.154),(side*.065,1.557,-.161),(side*.105,1.568,-.14)],4)
            section('cheek'+suffix,lambda path=path:mesh.tube(path,.0025,'skin_shadow','head',8))
        else:
            groups['cheek'+suffix]=[]
    mouth_y,mouth_z=(1.452,-.151) if human else (-.023,-.145)
    width=.057 if human else .029
    height=.003 if human else .005
    # A shallow cavity with upper/lower lip curves reads as a mouth when opened,
    # instead of translating the former fixed grin around the face.
    section('cavity',lambda:mesh.ellipsoid((0,mouth_y,mouth_z),(.038 if human else .024,height,.003),'ink','head',24,12))
    def lips():
        curl = .006 if expression==0 else .001 if expression==1 else -.003
        upper=curve([(-width,mouth_y+curl,mouth_z+.014),(-width*.35,mouth_y+.003,mouth_z-.002),(width*.35,mouth_y+.003,mouth_z-.002),(width,mouth_y+curl,mouth_z+.014)],5)
        lower=curve([(-width,mouth_y+curl,mouth_z+.014),(-width*.35,mouth_y-.003,mouth_z-.003),(width*.35,mouth_y-.003,mouth_z-.003),(width,mouth_y+curl,mouth_z+.014)],5)
        mesh.tube(upper,.0032 if human else .0025,'lip' if human else 'insect_dark','head',8)
        mesh.tube(lower,.0032 if human else .0025,'lip' if human else 'insect_dark','head',8)
    section('mouth',lips)
    obj=mesh.finish()
    obj.shape_key_add(name='Basis')
    keys={name:obj.shape_key_add(name=name) for name in CHANNELS}
    def transform(channel, indices, operation):
        for index in indices:
            v=Vector(mesh.v[index]);q=Vector((v.x,v.z,-v.y))
            keys[channel].data[index].co=g(operation(q))
    for suffix in ['L','R']:
        anchor=eye_y-.012
        transform('Blink'+suffix,groups['eye'+suffix]+groups['pupil'+suffix],lambda q:Vector((q.x,anchor+(q.y-anchor)*.025,q.z)))
        transform('Blink'+suffix,groups['lid'+suffix],lambda q:Vector((q.x,q.y-(.028 if human else .038),q.z)))
        transform('GazeX',groups['pupil'+suffix],lambda q:q+Vector((.010 if human else .008,0,.002)))
        transform('GazeY',groups['pupil'+suffix],lambda q:q+Vector((0,.009 if human else .011,.002)))
        transform('BrowUp',groups['brow'+suffix],lambda q:q+Vector((0,(.019 if human else .014)*(1-.3*min(1,abs(q.x)/(eye_x*1.8))),0)))
        transform('BrowDown',groups['brow'+suffix],lambda q:q+Vector((0,-(.017 if human else .014)*(1-min(1,abs(q.x)/(eye_x*1.8))),-.001)))
        transform('CheekLift',groups['cheek'+suffix],lambda q:q+Vector((0,.006,-.001)))
    all_mouth=groups['cavity']+groups['mouth']
    def open_mouth(q):
        drop=(.018 if human else .012)
        envelope=max(0,1-(abs(q.x)/width)**2)
        return Vector((q.x*(1-.08*envelope),mouth_y+(q.y-mouth_y)*(1+(3.0 if human else 1.4)*envelope)-drop*.35*envelope,q.z-.001))
    transform('MouthOpen',all_mouth,open_mouth)
    transform('MouthSmile',all_mouth,lambda q:Vector((q.x*1.07,q.y+(abs(q.x)/width)**1.5*(.012 if human else .009),q.z)))
    transform('MouthPress',all_mouth,lambda q:Vector((q.x*.92,mouth_y+(q.y-mouth_y)*.18,q.z-.001)))
    obj['facial_channels']=','.join(CHANNELS)
    return obj
