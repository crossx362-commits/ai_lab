"""원화 3면도 기반 C군 형태 후처리. 공유 리그/발사점/게임 수치를 수정하지 않는다.

apply(kind, root, api): build_models의 원본 tank 결과에 한 번 적용한다.
모든 추가물은 실제 폐곡면 메시이며 원화 이미지 평면을 사용하지 않는다.
"""
import math
from types import SimpleNamespace
import bpy
import bmesh
from mathutils import Vector

KINDS = ('SuperTank', 'IonAttacker', 'Poseidon', 'SecWind')


def apply(kind, root, api):
    if kind not in KINDS or root.get('fantasy_c_refined'):
        return root
    a = SimpleNamespace(**api)
    turret = next(o for o in root.children_recursive if o.name.split('.')[0] == 'Turret')
    barrel = next(o for o in root.children_recursive if o.name.split('.')[0] == 'Barrel')
    fire = next(o for o in root.children_recursive if o.name.split('.')[0] == 'FirePoint')
    anchors = [(o, o.matrix_basis.copy()) for o in (turret, barrel, fire)]

    def matching(*prefixes):
        return [o for o in root.children_recursive if any(o.name.startswith(p) for p in prefixes)]

    def remove(*prefixes):
        objs = matching(*prefixes)
        for o in objs:
            for child in list(o.children_recursive):
                if child not in objs:
                    objs.append(child)
        for o in reversed(objs):
            if o.name in bpy.data.objects:
                bpy.data.objects.remove(o, do_unlink=True)

    def mesh(name, verts, faces, mat, parent, smooth=True):
        data = bpy.data.meshes.new(name)
        data.from_pydata([tuple(a.v(p)) for p in verts], [], faces)
        data.update()
        bm = bmesh.new(); bm.from_mesh(data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(data); bm.free()
        o = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(o)
        o.parent = parent; data.materials.append(a.MATS[mat])
        for f in data.polygons:
            f.use_smooth = smooth and len(f.vertices) == 4
        return o

    def prism(name, points, depth, mat, parent, z=0):
        verts = [(x,y,z+d) for d in (-depth/2,depth/2) for x,y in points]
        n=len(points); faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
        faces += [(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
        o=mesh(name,verts,faces,mat,parent,False)
        bevel=o.modifiers.new('Small edge radius','BEVEL'); bevel.width=.018; bevel.segments=2
        bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=bevel.name)
        return o

    def leaf(name, start, end, width, depth, mat, parent, normal=(0,0,1), bend=.1):
        start,end=Vector(start),Vector(end); d=end-start; n=Vector(normal).normalized()
        side=d.cross(n).normalized(); verts=[]; rows=15; ring=10
        for j in range(rows):
            t=j/(rows-1); profile=max(.006,math.sin(math.pi*t)**.70)*(1-.25*t)
            center=start+d*t+n*bend*math.sin(math.pi*t)
            for q in range(ring):
                ang=math.tau*q/ring
                verts.append(tuple(center+side*width*profile*math.cos(ang)+n*depth*profile*math.sin(ang)))
        faces=[tuple(range(ring-1,-1,-1)),tuple((rows-1)*ring+j for j in range(ring))]
        faces += [(j*ring+q,j*ring+(q+1)%ring,(j+1)*ring+(q+1)%ring,(j+1)*ring+q) for j in range(rows-1) for q in range(ring)]
        return mesh(name,verts,faces,mat,parent)

    def curve(name, pts, radius, mat, parent):
        data=bpy.data.curves.new(name,'CURVE'); data.dimensions='3D'; data.resolution_u=8
        data.bevel_depth=radius; data.bevel_resolution=2
        spline=data.splines.new('BEZIER'); spline.bezier_points.add(len(pts)-1)
        for bp,p in zip(spline.bezier_points,pts):
            bp.co=a.v(p); bp.handle_left_type='AUTO'; bp.handle_right_type='AUTO'
        o=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(o)
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
        bpy.ops.object.convert(target='MESH'); return a.finish(bpy.context.object,name,mat,parent)

    def reparent(o,parent):
        bpy.context.view_layer.update(); world=o.matrix_world.copy(); o.parent=parent; o.matrix_world=world

    if kind == 'SuperTank':
        # Broader muzzle, heavy descending locks and a visible upright crown.
        remove('Swept sculpted lion mane','Front pointed mane lock','Lion forehead swept lock',
               'Round crown circlet','Crown pointed panel')
        for s in (-1,1):
            for row in range(3):
                for j in range(7):
                    start=(s*(.35+.07*j),1.78-.115*j,.45-row*.24)
                    end=(s*(1.18-.050*j+row*.045),1.42-.205*j,.34-row*.32)
                    leaf('Layered descending crimson mane',start,end,.26,.11,'Crimson',turret,bend=.16)
            for j in range(6):
                leaf('Front framing mane',(s*.59,1.53-.19*j,.73),
                     (s*(1.11-.075*j),1.07-.21*j,.87),.25,.085,'Crimson',turret,bend=.06)
            leaf('Central swept forehead lock',(s*.05,1.55,.77),(s*.65,1.91,.39),.25,.10,'Crimson',turret)
        for o in matching('Lion cranium'):
            o.scale.x*=1.10; o.scale.z*=1.05
        for o in matching('Lion broad cream muzzle'):
            o.scale.x*=1.12; o.scale.z*=1.04
        for o in matching('Eye'):
            if o.type=='EMPTY' and o.parent==turret:
                o.location.x*=1.08; o.location.y-=.035; o.scale*=1.04
        # Wider high launch pods flank rather than cover the mane.
        for o in matching('Shoulder rocket assembly'):
            o.location.x*=1.10; o.location.z+=.035; o.scale*=1.04
        a.cyl('Royal crown rounded band',(0,1.87,-.025),.47,.15,'Gold',turret,seg=40)
        pts=[(-.49,1.91),(-.57,2.26),(-.30,2.11),(0,2.55),(.30,2.11),(.57,2.26),(.49,1.91)]
        prism('Upright five point crown',pts,.11,'Gold',turret,z=.18)
        for x,y,r in ((0,2.20,.10),(-.39,2.12,.055),(.39,2.12,.055)):
            a.ball('Crown inset ruby',(x,y,.252),(r,r*1.12,.035),'Crimson',turret)
        for x,y in ((0,2.55),(-.57,2.26),(.57,2.26)):
            a.ball('Crown gold finial',(x,y,.18),(.048,.064,.048),'Gold',turret)
        # Front hull should meet the mane and have distinct armored track shoulders.
        for s in (-1,1):
            a.box('Lion front track armor',(s*1.46,1.02,1.05),(.67,.36,.91),'Gold',root,.075)
            a.box('Lion fender inset panel',(s*1.46,1.213,1.02),(.47,.028,.58),'Gold',root,.018)
            for z in (.69,1.35):
                a.cyl('Lion shoulder bolt',(s*1.46,1.245,z),.046,.027,'Metal',root,seg=12)
            a.box('Lion rear deck shoulder',(s*.78,1.29,-.69),(.33,.30,.91),'Gold',root,.045)
        a.box('Lion chest plate',(0,1.36,1.11),(1.28,.57,.25),'Gold',root,.04)
        for j in range(4):a.box('Rear cooling grille',(0,1.37,-.83+j*.15),(.65,.028,.075),'Metal',root,.008)
        # Small embossed crown mark on the back of the yawing mane.
        prism('Rear royal crown badge',[(-.22,0),(-.24,.25),(-.12,.12),(0,.32),(.12,.12),(.24,.25),(.22,0)],.04,'Gold',turret,z=-.83)

    elif kind == 'IonAttacker':
        remove('Gold orbital band','Lower maroon keel','Keel dark tip')
        # Flat rectangular-section annulus, not a pipe crossing the entire eye.
        verts=[]; count=72
        for y in (1.015,1.20):
            for r in (1.47,1.67):
                for j in range(count):
                    q=math.tau*j/count; verts.append((r*math.cos(q),y,r*math.sin(q)))
        faces=[]
        for j in range(count):
            k=(j+1)%count
            faces += [(j,k,count+k,count+j),(2*count+j,3*count+j,3*count+k,2*count+k),
                      (j,2*count+j,2*count+k,k),(count+j,count+k,3*count+k,3*count+j)]
        mesh('Flat orbital gold belt',verts,faces,'Gold',root)
        for o in matching('Great cyan cyclops iris'):
            o.scale.x*=.88; o.scale.z*=.90; o.scale.y*=.65; o.location.y+=.045
        for o in matching('Central lens highlight'):
            o.location.x=.22; o.location.z=2.23; o.location.y=-1.34
        # Reference satellites fill the four plan quadrants, with full gimbal supports.
        satellite_names=('Pink satellite eye shell','Pink satellite front bezel','Satellite dark lens','Satellite cyan iris','Gold pod side pivot')
        for o in matching(*satellite_names):
            upper=o.location.z>2
            o.location.y+=1.00 if upper else -.51
            o.location.x*=.96
            if o.name.startswith('Pink satellite eye shell'):
                o.scale*=1.10
        remove('Gold angled pod bracket')
        for s in (-1,1):
            for yy,zz in ((.97,1.06),(2.94,-1.12)):
                a.beam('Satellite articulated upper arm',(s*.87,1.78 if yy<2 else 2.40,0),(s*1.27,yy,zz),.14,'Gold',root)
                a.cyl('Satellite bracket joint',(s*1.28,yy,zz),.17,.14,'Gold',root,'x',seg=24)
                a.ball('Satellite lens pinpoint',(s*1.334-.045,yy+.09,zz+.437),(.042,.047,.018),'EyeWhite',root)
        a.cyl('Keel upper bowl',(0,.60,0),.34,.25,'Plum',root,r2=.50,seg=40)
        a.cyl('Keel lower tapered step',(0,.39,0),.13,.25,'Plum',root,r2=.29,seg=32)
        a.cyl('Keel small dark toe',(0,.24,0),.065,.09,'Navy',root,r2=.13,seg=24)
        # Core stays fixed; orb, eyes and orbital belt yaw with the target.
        for o in list(root.children):
            if o not in (turret,) and not o.name.startswith(('Keel','Team','Snow')):
                reparent(o,turret)

    elif kind == 'Poseidon':
        remove('Blue whale upper shell','White whale ventral body','Whale smiling lip','Surface fitted ventral groove',
               'Trident center spear','Hooked trident outer tine')
        sections=[(-2.20,.12,.17,2.0),(-1.72,.35,.40,1.59),(-1.08,.87,.76,1.36),(-.40,1.18,.98,1.36),
                  (.40,1.22,1.03,1.40),(1.02,1.02,.95,1.39),(1.51,.58,.68,1.40),(1.66,.03,.20,1.42)]
        # Smooth interpolation replaces visibly faceted longitudinal joins and the staircase color boundary.
        fine=[]
        for i in range(len(sections)-1):
            p0=Vector(sections[max(0,i-1)]);p1=Vector(sections[i]);p2=Vector(sections[i+1]);p3=Vector(sections[min(len(sections)-1,i+2)])
            for j in range(5):
                t=j/5
                q=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
                fine.append(tuple(q))
        fine.append(sections[-1]); n=25
        for lower,mat in ((False,'Body'),(True,'Ivory')):
            verts=[]
            for z,w,h,c in fine:
                for j in range(n):
                    t=math.pi*j/(n-1)+(math.pi if lower else 0)
                    x=w*math.cos(t)
                    seam=c+.20*max(0,min(1,(z+1.0)/1.8))*(1-min(1,abs(x)/1.23)**2)
                    verts.append((x,seam+h*math.sin(t),z))
            faces=[(i*n+j,i*n+j+1,(i+1)*n+j+1,(i+1)*n+j) for i in range(len(fine)-1) for j in range(n-1)]
            mesh('Smooth ivory whale belly' if lower else 'Smooth blue whale back',verts,faces,mat,root)
        # Ventral grooves follow the actual convex surface, staying below the new curved lip.
        from mathutils.bvhtree import BVHTree
        belly=matching('Smooth ivory whale belly')[0]
        bpy.context.view_layer.update(); bvh=BVHTree.FromObject(belly,bpy.context.evaluated_depsgraph_get())
        for x in (-.78,-.52,-.26,0,.26,.52,.78):
            pts=[]
            for j in range(33):
                hit,normal,_,_=bvh.ray_cast(a.v((x,.38+j*.039,5)),a.v((0,0,-1)))
                if hit is not None: pts.append(tuple(Vector(a.uv(hit+normal*.008))))
            if len(pts)>2:curve('Curved ventral pleat',pts,.008,'Stone',root)
        # Raised actual tines expose the hooked silhouette in FRONT while retaining full depth.
        prism('Raised trident central spear',[(-.08,.0),(.08,.0),(.08,.66),(.19,.67),(0,1.01),(-.19,.67),(-.08,.66)],.16,'Gold',barrel,z=1.22)
        for s in (-1,1):
            pts=[(0,.03),(s*.34,.03),(s*.54,.39),(s*.52,.65),(s*.76,.78),(s*.82,.44),(s*.66,.53),(s*.48,-.12),(0,-.12)]
            prism('Raised hooked gold trident tine',pts,.17,'Gold',barrel,z=1.07)
            a.beam('Trident fork depth brace',(0,0,.69),(s*.43,.13,1.07),.17,'Gold',barrel)
        for o in matching('Horizontal whale tail fluke'):
            # Camber makes the tail a volume from SIDE as well as a TOP silhouette.
            for vtx in o.data.vertices:
                vtx.co.z += .17*abs(vtx.co.x)
        for s in (-1,1):
            a.box('Whale front pod armor',(s*1.05,.55,.29),(.48,.25,.48),'Body',root,.07)
            for dx in (-.13,.13):a.cyl('Whale pod cover bolt',(s*1.05+dx,.70,.29),.031,.025,'Metal',root,seg=12)

    elif kind == 'SecWind':
        remove('Swept green primary wing','Ivory overlapping secondary')
        # Broad cambered 3D feathers use a tilted normal so TOP also reads a full fan.
        for s in (-1,1):
            for j in range(8):
                t=j/7
                end=(s*(1.70+1.30*t),1.02+2.12*t,-.65-1.05*t)
                start=(s*.76,1.30+.22*t,-.25-.22*t)
                leaf('Broad cambered primary feather',start,end,.31+.075*t,.09,'Body',root,normal=(0,.65,1),bend=.10)
            for j in range(6):
                t=j/5
                leaf('Layered ivory wing feather',(s*.82,1.63+.14*t,.02),
                     (s*(1.46+.84*t),1.38+1.44*t,-.20-.90*t),.22,.075,'Ivory',root,normal=(0,.60,1),bend=.08)
            # Root covert rounds the wing/face junction instead of leaving isolated feathers.
            a.ball('Green wing root',(s*.86,1.69,-.19),(.43,.54,.37),'Body',root)
        for o in matching('High wing-root green turbine','Ivory turbine front ring','Turbine dark well','Pitched radial turbine blade','Turbine spinner'):
            # Existing turbine pieces are root-local, including blade meshes with baked positions.
            if o.name.startswith('Pitched radial turbine blade'):
                center=Vector((1.16 if sum(v.co.x for v in o.data.vertices)>0 else -1.16,-.228,2.43))
                for vert in o.data.vertices:vert.co=center+(vert.co-center)*1.20
                o.location.y-=.17
            else:
                o.scale*=1.20; o.location.y-=.17
        # Keep the lower chassis fixed; the entire bird and both turbines yaw together.
        movable=('Green bird rounded head','Eye','Broad cambered primary feather','Layered ivory wing feather','Green wing root',
                 'High wing-root green turbine','Ivory turbine front ring','Turbine dark well','Pitched radial turbine blade',
                 'Turbine spinner','Great hooked ivory eagle beak','Swept bird crest')
        for o in list(root.children):
            if any(o.name.startswith(p) for p in movable):reparent(o,turret)
        # Small body panels tie the head to the running gear without moving the wheels.
        for s in (-1,1):
            a.box('Bird outer track armor',(s*.94,.74,.35),(.62,.23,.50),'Body',root,.055)
            a.cyl('Bird wing axle',(s*.63,.78,.38),.13,.15,'Metal',root,'x',seg=24)
    # Front-visible pads have real depth and remain on the fixed running gear.
    if kind in ('SuperTank','Poseidon','SecWind'):
        x,yy,zz,width,count = {'SuperTank':(1.46,.17,1.54,.73,5), 'Poseidon':(1.05,.12,.65,.47,4), 'SecWind':(.94,.12,.86,.63,4)}[kind]
        for side in (-1,1):
            for row in range(count):
                a.box('Front visible tread pad',(side*x,yy+row*.125,zz), (width,.112,.14),'Metal',root,.018)
                a.box('Tread central raised shoe',(side*x,yy+row*.125,zz+.08),(width*.60,.052,.055),'Rubber',root,.008)
    if kind == 'IonAttacker':
        for eye in matching('Long dark vertical pupil','Satellite cyan iris'):
            gaze=a.empty('PupilGaze',(0,0,0),eye.parent)
            reparent(eye,gaze)
    bpy.context.view_layer.update()
    for obj,basis in anchors:
        assert all(abs(obj.matrix_basis[r][c]-basis[r][c])<1e-7 for r in range(4) for c in range(4)), obj.name
    root['fantasy_c_refined']=1
    return root
