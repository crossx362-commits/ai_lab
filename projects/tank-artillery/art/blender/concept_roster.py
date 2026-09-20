"""Character-sheet reconstruction: shaped plates, readable faces and separate running gear.
Coordinates and aim pivots remain in gameplay space. No generated reference image is
used as a substitute for geometry. Source: art/concepts/tankfall-character-sheet.png.
"""
import bpy, bmesh, math
from mathutils import Vector
from types import SimpleNamespace

def build_tank(k,shape,api):
    a=SimpleNamespace(**api); api['CUTE']=False
    bl,bw,bh,tw,th,tr,tuh,L,cal,_,nw,*_=shape
    chassis='cart' if k in ('Catapult','CrossBow') else 'carriage' if k=='Cannon' else 'truck' if k in ('Missile','MultiMissile') else 'hover' if k in ('Laser','IonAttacker','Poseidon','SecWind') else 'track'
    wr=max(.42,min(th*.88,bl*.5/int(nw)*1.5));by=wr*.95
    if chassis=='cart':wr=max(.9,bh*.95);by=wr*.75
    if chassis=='carriage':wr=max(.8,bh*.85);by=wr*.8
    if chassis=='truck':wr=max(.5,th*.7);by=wr*1.05
    if chassis=='hover':by=.55+bh*.55*.9
    root=a.empty(k);root['kind']=k;root['wheelRadius']=wr;root['hover']=chassis=='hover'
    turret=a.empty('Turret',(0,by+bh,-bl*.08),root)
    barrel=a.empty('Barrel',(0,tuh*.52,tr*1.45*.45),turret)
    fz=L*2.2 if k=='Catapult' else L*1.25 if k=='Carrot' else L*.84+.6 if k=='Missile' else L+1 if k=='Laser' else L+.35 if k=='Poseidon' else L*1.05 if k=='SecWind' else L
    fire=a.empty('FirePoint',(0,0,fz),barrel)
    def prism(n,points,depth,mat,parent,p=(0,0,0),axis='z',bevel=.035):
        verts=[]
        for d in (-depth/2,depth/2):
            for u,v in points:
                q=(d,v,u) if axis=='x' else (u,d,v) if axis=='y' else (u,v,d)
                verts.append(tuple(a.v(q)))
        m=len(points);faces=[tuple(range(m-1,-1,-1)),tuple(range(m,2*m))]
        faces.extend((i,(i+1)%m,(i+1)%m+m,i+m) for i in range(m))
        mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new(n,mesh);bpy.context.collection.objects.link(o);o.location=a.v(p);return a.finish(o,n,mat,parent,bevel)
    def rivet(p,parent,mat='Edge',r=.07,axis='z'):
        return a.cyl('Rivet',p,r,.055,mat,parent,axis,seg=12)
    def eye(parent,p,size=.48,skin='Body',yaw=0,angry=False):
        group=a.empty('Eye',p,parent);group.rotation_euler[2]=math.radians(yaw)
        a.ball('Eye socket',(0,0,-.035),(size*1.24,size*1.29,size*.34),skin,group)
        a.ball('Ivory sclera',(0,0,.025),(size,size*1.13,size*.28),'EyeWhite',group)
        gaze=a.empty('PupilGaze',(0,0,0),group) if k=='Carrot' else group
        a.ball('Deep pupil',(.06,-.015,.025+size*.29),(size*.44,size*.72,size*.14),'Rubber',gaze)
        a.ball('Eye highlight',(-.015,size*.32,.025+size*.44),(size*.13,size*.17,size*.035),'EyeWhite',gaze)
        if angry:
            brow=prism('Sculpted brow',[(-size*1.12,.10),(size*1.12,-.12),(size*1.03,.12),(-size*.88,.28)],.22,skin,group,p=(0,size*.84,.27))
        return group
    def panel(n,p,w,h,depth,mat,parent):
        return prism(n,[(-w*.5,-h*.28),(-w*.32,-h*.5),(w*.32,-h*.5),(w*.5,-h*.28),(w*.5,h*.28),(w*.32,h*.5),(-w*.32,h*.5),(-w*.5,h*.28)],depth,mat,parent,p)
    def wheel(parent,x,y,z,r,width,paint,wood=False):
        root['wheelRadius']=r
        w=a.empty('Wheel',(x,y,z),parent);a.cyl('Wheel tire',(0,0,0),r,width,'Metal' if wood else 'Rubber',w,'x',seg=32)
        side=1 if x>0 else -1;ox=side*(width*.5+.02)
        a.cyl('Wheel inset',(ox,0,0),r*.80,.06,paint,w,'x',seg=24)
        a.torus('Wheel rim',(ox+side*.04,0,0),r*.75,.055,'Edge' if wood else 'Metal',w,'x')
        a.cyl('Wheel hub',(ox+side*.09,0,0),r*.28,.13,'Metal',w,'x',seg=16)
        if wood:
            for i in range(8):
                q=i*math.tau/8
                a.beam('Wheel wooden spoke',(ox,0,0),(ox,math.sin(q)*r*.72,math.cos(q)*r*.72),.12,paint,w)
        for i in range(6):
            q=i*math.tau/6;rivet((ox+side*.1,math.sin(q)*r*.58,math.cos(q)*r*.58),w,r=.045,axis='x')
    def tracks(width=2.9,length=3.4,r=.57,paint='Body',y=None):
        root['wheelRadius']=r*.71
        yy=r+.08 if y is None else y
        for side in (-1,1):
            x=side*width*.5
            # Belt is narrow inside the exposed road wheels; individual links form its perimeter.
            a.sculpt('Inner rubber belt',(x,yy,0),(.23,r*.98,length*.5),'Rubber',root,.48)
            count=3 if k in ('Carrot','Missile','MineLander') else 4
            for j in range(count):wheel(root,x+side*.09,yy,-length*.32+j*length*.64/(count-1),r*.81 if count==3 else r*.71,.49,paint)
            half=length*.5-r
            for j in range(9):
                z=-half+j*(2*half/8)
                for sign in (-1,1):
                    a.box('Straight tread link',(x,yy+sign*r,z),(.69,.16,.25),'Metal',root,.035)
            for end in (-1,1):
                for j in range(1,8):
                    q=-math.pi*.5+j*math.pi/8
                    z=end*(half+math.cos(q)*r);y2=yy+math.sin(q)*r
                    link=a.box('End tread link',(x,y2,z),(.69,.16,.25),'Metal',root,.035)
                    link.rotation_euler[0]=math.radians(end*(90-math.degrees(q)))
            for segment in (-1,1):
                z=segment*length*.28
                if k in ('Carrot','Missile','SuperTank'):
                    # Separate curved front/rear fenders follow the outer track instead of flat slabs.
                    pts=[(.035,yy+r+.27),(length*.27,yy+r+.27),(length*.43,yy+r*.81),(length*.50,yy+r*.24),(length*.37,yy+r*.26),(length*.32,yy+r*.64),(length*.22,yy+r*.81),(.035,yy+r*.81)]
                    prism('Curved armored track fender',[(segment*zz,yy2) for zz,yy2 in pts],.88,paint,root,p=(x,0,0),axis='x',bevel=.05)
                else:a.box('Armored fender pad',(x,yy+r+.14,z),(.83,.28,length*.44),paint,root,.10)
                for zz in ((-.31,) if k in ('Carrot','Missile','SuperTank') else (-.31,.31)):
                    if k in ('Carrot','Missile','SuperTank'):
                        t=.18 if zz<0 else .37
                        rivet((x+side*.455,yy+r+.10 if zz<0 else yy+r*.73,segment*length*t),root,mat='Edge',r=.065,axis='x')
                    else:rivet((x+side*.43,yy+r+.11,z+zz),root,mat='Edge',r=.065,axis='x')
            for j in range(9):
                z=-half+j*(2*half/8)
                a.box('Tread transverse cleat',(x,yy-r-.035,z),(.73,.12,.075),'Rubber',root,.02)
    def chassis_plate(width,length,y,mat='Body'):
        a.box('Chassis frame',(0,y,0),(width,.39,length),mat,root,.12)
    def gun(r=.35,mat='Ivory',length=None):
        length=fz if length is None else length
        a.cyl('Cannon body',(0,0,length*.46),r,length*.90,mat,barrel,'z',seg=32)
        a.cyl('Recessed bore',(0,0,length-.08),r*.70,.045,'Rubber',barrel,'z',seg=32)
        a.torus('Thick muzzle rim',(0,0,length-.04),r*.87,r*.16,mat,barrel)
        for z in (.12,length*.35):a.torus('Barrel collar',(0,0,z),r*1.04,.06,'Metal',barrel)
    def curve(n,points,r,mat,parent):
        data=bpy.data.curves.new(n,'CURVE');data.dimensions='3D';data.resolution_u=12
        data.bevel_depth=r;data.bevel_resolution=2
        spline=data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
        for bp,p in zip(spline.bezier_points,points):
            bp.co=a.v(p);bp.handle_left_type='AUTO';bp.handle_right_type='AUTO'
        o=bpy.data.objects.new(n,data);bpy.context.collection.objects.link(o)
        bpy.context.view_layer.objects.active=o;o.select_set(True)
        for other in bpy.context.selected_objects:
            if other!=o:other.select_set(False)
        bpy.ops.object.convert(target='MESH');return a.finish(bpy.context.object,n,mat,parent)
    def petal(n,start,end,width,depth,mat,parent,bend=.12):
        # A lofted, convex leaf/feather, not an extruded flat polygon.
        start,end=Vector(start),Vector(end);direction=end-start
        side=Vector((direction.y,-direction.x,0)).normalized()
        if side.length<.1:side=Vector((1,0,0))
        verts=[];steps=14;ring=10
        for j in range(steps+1):
            t=j/steps;profile=max(.018,math.sin(math.pi*t)**.72)*(1-.42*t)
            center=start+direction*t+Vector((0,0,bend*math.sin(math.pi*t)))
            for q in range(ring):
                ang=q*math.tau/ring
                v=center+side*(width*profile*math.cos(ang))+Vector((0,0,depth*profile*math.sin(ang)))
                verts.append(tuple(a.v(v)))
        faces=[]
        for j in range(steps):
            for q in range(ring):faces.append((j*ring+q,j*ring+(q+1)%ring,(j+1)*ring+(q+1)%ring,(j+1)*ring+q))
        faces.extend([tuple(range(ring-1,-1,-1)),tuple(steps*ring+q for q in range(ring))])
        mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new(n,mesh);bpy.context.collection.objects.link(o);o.parent=parent;mesh.materials.append(a.MATS[mat])
        for face in mesh.polygons:face.use_smooth=len(face.vertices)==4
        return o
    def armor(n,p,r,mat,parent,lat=5,lon=12):
        # Curved plate surfaces and real recessed seams survive FBX and Unity export.
        a.ball(n+' seam foundation',p,tuple(v*.994 for v in r),'Metal',parent)
        verts=[];faces=[]
        for i in range(lat):
            lo=-math.pi/2+i*math.pi/lat+.009;hi=-math.pi/2+(i+1)*math.pi/lat-.009
            for j in range(lon):
                left=j*math.tau/lon+.006;right=(j+1)*math.tau/lon-.006;base=len(verts)
                for u in range(5):
                    theta=lo+(hi-lo)*u/4
                    for v in range(5):
                        phi=left+(right-left)*v/4
                        q=(p[0]+r[0]*math.cos(theta)*math.sin(phi),p[1]+r[1]*math.sin(theta),p[2]+r[2]*math.cos(theta)*math.cos(phi))
                        verts.append(tuple(a.v(q)))
                for u in range(4):
                    for v in range(4):
                        q=base+u*5+v;faces.append((q,q+1,q+6,q+5))
        mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new(n,mesh);bpy.context.collection.objects.link(o);a.finish(o,n,mat,parent)
        return o
    def leaf(n,p,scale,mat,parent,tilt=0):
        angle=math.radians(tilt)
        return petal(n,(p[0],p[1]-.45*scale[1],p[2]),(p[0]+math.sin(angle)*scale[1],p[1]+math.cos(angle)*scale[1]*.85,p[2]-.13),scale[0]*.40,scale[2],mat,parent)
    from orthographic_forms import build as build_orthographic
    updated=build_orthographic(k,locals())
    if updated is not None:return updated
    # Each tank is reconstructed separately instead of sharing a car body and visor.
    if k=='Catapult':
        # The reference has an upright coopered barrel and a REAR raised sling.
        # The aim pivot is at the sling: pitch changes launch direction, not the parked frame.
        barrel.location=a.v((0,.87,-1.82));fire.location=a.v((0,0,0))
        root['wheelRadius']=.64
        chassis_plate(2.20,2.20,.55,'Wood')
        profiles=[(.64,.89),(.77,1.01),(1.15,1.13),(1.65,1.14),(1.98,1.04),(2.12,.92)]
        for j in range(16):
            verts=[];faces=[];lo=j*math.tau/16+.009;hi=(j+1)*math.tau/16-.009
            for inset in (0,.105):
                for y,r in profiles:
                    for t in range(4):
                        q=lo+(hi-lo)*t/3;verts.append(tuple(a.v(((r-inset)*math.sin(q),y,.16+(r-inset)*.92*math.cos(q)))))
            count=len(profiles)*4
            for inner in (0,1):
                off=inner*count
                for yi in range(len(profiles)-1):
                    for t in range(3):
                        n=off+yi*4+t;faces.append((n,n+1,n+5,n+4))
            for t in range(3):
                faces.append((t,t+1,count+t+1,count+t))
                n=(len(profiles)-1)*4+t;faces.append((n,n+count,n+count+1,n+1))
            for yi in range(len(profiles)-1):
                for t in (0,3):
                    n=yi*4+t;faces.append((n,n+4,n+4+count,n+count))
            mesh=bpy.data.meshes.new('Coopered oak stave');mesh.from_pydata(verts,[],faces);mesh.update()
            bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
            o=bpy.data.objects.new('Coopered oak stave',mesh);bpy.context.collection.objects.link(o);a.finish(o,'Coopered oak stave','WoodLight',root,.012)
        a.cyl('Oak top lid',(0,2.08,.16),.93,.13,'Wood',root,seg=40)
        for y,r in ((.83,1.045),(1.92,1.080)):
            band=a.torus('Forged barrel hoop',(0,y,.16),r,.055,'Metal',root,'y');band.scale.y=.92
            for j in range(12):
                q=j*math.tau/12
                a.ball('Hoop rivet',(math.sin(q)*r,y,.16+math.cos(q)*r*.92),(.042,.044,.042),'Edge',root)
        for side in (-1,1):
            for z in (-.89,.86):
                w=a.empty('Wheel',(side*1.20,.66,z),root);r=.64;ox=side*.235
                a.cyl('Red wooden tire',(0,0,0),r,.43,'Crimson',w,'x',seg=40)
                for xx in (-.17,.17):a.cyl('Narrow iron tire band',(xx,0,0),r+.012,.065,'Metal',w,'x',seg=40)
                # Solid red wooden wheel, split into radial boards, with a narrow iron tire.
                for j in range(12):
                    lo=j*math.tau/12+.014;hi=(j+1)*math.tau/12-.014
                    pts=[(0,0)]+[(math.sin(lo+(hi-lo)*t/3)*.582,math.cos(lo+(hi-lo)*t/3)*.582) for t in range(4)]
                    prism('Red wooden wheel segment',pts,.065,'Crimson',w,p=(ox,0,0),axis='x',bevel=.008)
                a.cyl('Inner red wheel face',(-ox,0,0),.583,.065,'Crimson',w,'x',seg=40)
                a.torus('Inner wheel edge',(-ox-side*.015,0,0),.608,.035,'Edge',w,'x')
                a.cyl('Inner axle cap',(-ox-side*.05,0,0),.17,.09,'Metal',w,'x',seg=24)
                a.torus('Wheel iron edge',(ox+side*.015,0,0),.608,.035,'Edge',w,'x')
                a.cyl('Axle boss',(ox+side*.06,0,0),.20,.15,'Metal',w,'x',seg=24)
                a.cyl('Axle pin',(ox+side*.15,0,0),.075,.035,'Edge',w,'x',seg=16)
                for j in range(10):
                    q=j*math.tau/10;rivet((ox+side*.04,math.sin(q)*.61,math.cos(q)*.61),w,r=.032,axis='x')
            cat_eye=eye(root,(side*.59,1.42,1.06),.40,'WoodLight',-5);cat_eye.scale.y=.91
            prism('Oak eyebrow',[(-.44,.02),(.40,.02),(.37,.24),(-.36,.30)],.18,'WoodLight',root,p=(side*.59,1.82,.98),bevel=.04)
            for yy in (1.12,1.91):rivet((side*.92,yy,.76),root,'Metal',.065)
            curve('Center wood grain',[(side*.10,.91,1.15),(side*.12,1.41,1.21),(side*.09,1.91,1.13)],.009,'Wood',root)
            # Low triangular trunnion mounts on the lid, with two distinct long oak rails.
            prism('Oak trunnion cheek',[(-.35,-.26),(.40,-.26),(.30,.30),(-.12,.38)],.23,'WoodLight',turret,p=(side*.54,.06,.10),axis='x',bevel=.05)
            a.cyl('Trunnion iron pivot',(side*.69,.14,.13),.16,.10,'Metal',turret,'x',seg=24)
            a.beam('Raised rear sling rail',(side*.44,.17,.35),(side*.62,.83,-1.88),.23,'WoodLight',turret)
            a.beam('Rail reinforcing strip',(side*.46,.10,.31),(side*.63,.73,-1.86),.055,'Wood',turret)
            a.beam('Rear frame support',(side*.59,-.15,-.70),(side*.54,.38,-.13),.23,'Wood',turret)
        a.beam('Cross shaft',(-.80,.16,.14),(.80,.16,.14),.20,'Metal',turret)
        # Canvas hemispherical pocket follows the bottom of the stone instead of a wooden box.
        verts=[];faces=[];rings=8;segments=32
        for j in range(rings+1):
            theta=-math.pi/2+.015+j*(math.pi/2-.015)/rings
            for t in range(segments):
                q=t*math.tau/segments
                verts.append(tuple(a.v((.77*math.cos(theta)*math.cos(q),.71+.61*math.sin(theta),-1.82+.76*math.cos(theta)*math.sin(q)))))
        for j in range(rings):
            for t in range(segments):
                n=j*segments+t;faces.append((n,n+segments,(j+1)*segments+(t+1)%segments,j*segments+(t+1)%segments))
        mesh=bpy.data.meshes.new('Canvas sling pocket');mesh.from_pydata(verts,[],faces);mesh.update()
        o=bpy.data.objects.new('Canvas sling pocket',mesh);bpy.context.collection.objects.link(o);a.finish(o,'Canvas sling pocket','Ivory',turret)
        curve('Sling braided U rope',[(-.69,1.08,-1.99),(-.81,.57,-2.01),(-.45,.19,-2.12),(0,.10,-2.12),(.45,.19,-2.12),(.81,.57,-2.01),(.69,1.08,-1.99)],.080,'WoodLight',turret)
        for strand in range(3):
            points=[]
            for j in range(65):
                t=j/64;theta=math.pi+t*math.pi
                q=t*math.tau*11+strand*math.tau/3
                points.append((.78*math.cos(theta)+.023*math.cos(q),.92+.78*math.sin(theta)+.023*math.sin(q),-2.00-.10*math.sin(math.pi*t)+.032*math.sin(q)))
            curve('Twisted sling rope strand',points,.025,'WoodLight',turret)
        for side in (-1,1):
            a.torus('Sling rope eye',(side*.69,.88,-1.87),.16,.058,'WoodLight',turret,'x')
            for j in range(3):a.torus('Sling knot',(side*.66,.83+j*.055,-1.86),.095,.025,'WoodLight',turret,'y')
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.72,location=a.v((0,.91,-1.82)));stone=bpy.context.object
        for vert in stone.data.vertices:vert.co*=1+.17*math.sin(vert.index*3.74)
        stone.scale=(1.02,1.0,.94);a.finish(stone,'Faceted sling boulder','Stone',turret)
        for face in stone.data.polygons:face.use_smooth=False
    elif k=='CrossBow':
        chassis_plate(2.25,2.85,.59,'Wood')
        a.sculpt('Beetle belly',(0,1.01,0),(1.12,.52,1.32),'Teal',root,.7)
        for s in (-1,1):
            for z in (-.91,.91):wheel(root,s*1.27,.66,z,.65,.42,'Teal',True)
            for j in range(3):
                petal('Sculpted beetle leaf',(s*.10,1.60,-.87+j*.47),(s*(1.46-.09*j),1.35, -.45+j*.53),.43,.14,'Teal',root,.22)
                curve('Leaf golden vein',[(s*.10,1.71,-.87+j*.47),(s*.66,1.69,-.66+j*.50),(s*(1.41-.09*j),1.41,-.45+j*.53)],.021,'Gold',root)
            eye(root,(s*.62,1.23,1.25),.44,'Teal',s*17,True)
        a.box('Crossbow stock',(0,-.02,fz*.45),(.36,.28,fz*.98),'WoodLight',barrel,.045)
        for s in (-1,1):
            curve('Recurved oak bow',[(0,0,fz*.50),(s*.80,-.08,fz*.47),(s*1.53,-.13,fz*.34),(s*1.90,.13,fz*.61)],.17,'WoodLight',barrel)
            curve('Bow inner laminate',[(0,.04,fz*.53),(s*.80,-.02,fz*.50),(s*1.53,-.07,fz*.38),(s*1.87,.17,fz*.61)],.025,'Wood',barrel)
            a.beam('Bowstring',(s*1.90,.10,fz*.61),(0,.10,.27),.026,'Ivory',barrel)
            a.box('Bow steel clamp',(s*1.47,0,fz*.35),(.26,.38,.32),'Metal',barrel,.025)
        a.cyl('Loaded arrow',(0,.18,fz*.47),.055,fz*.91,'Wood',barrel,'z',seg=12)
        a.cyl('Arrow spearhead',(0,.18,fz-.20),.17,.42,'Edge',barrel,'z',0,4)
    elif k=='Cannon':
        chassis_plate(2.1,2.5,.66,'WoodLight')
        for s in (-1,1):
            for z in (-.86,.78):wheel(root,s*1.24,.70,z,.66,.39,'WoodLight',True)
            a.box('Carriage side brace',(s*.96,1.04,-.07),(.25,.5,2.34),'Wood',root,.04)
        armor('Spherical bombard',(0,.02,.05),(1.16,1.16,1.18),'Navy',barrel)
        a.cyl('Heavy navy muzzle',(0,0,fz*.61),.62,fz*.74,'Navy',barrel,'z',seg=32)
        for z in (.65,fz-.30):a.torus('Brass muzzle ring',(0,0,z),.65,.14,'Gold',barrel)
        a.cyl('Deep cannon bore',(0,0,fz-.06),.43,.06,'Rubber',barrel,'z',seg=32)
        for s in (-1,1):
            eye(barrel,(s*.91,.19,.66),.46,'Navy',s*24)
            for z in (-.25,.32):rivet((s*1.12,.02,z),barrel,axis='x')
        a.box('Captain hatch',(0,1.03,-.12),(1.18,.16,.70),'Navy',barrel,.07,(-10,0,0))
        a.box('Hatch handle',(0,1.20,-.23),(.36,.17,.16),'Metal',barrel,.03)
        # Small embossed skull on either cheek of the naval shell.
        for s in (-1,1):
            a.ball('Pirate skull',(s*1.12,.05,-.30),(.06,.23,.19),'Ivory',barrel)
            for y in (-.18,.13):a.beam('Crossbones',(s*1.17,y-.11,-.53),(s*1.17,y+.11,-.09),.055,'Ivory',barrel)
    elif k=='Carrot':
        tracks(2.85,3.12,.57,'Gold');chassis_plate(2.65,2.7,1.05)
        armor('Orange character head',(0,.42,-.03),(1.12,1.07,1.08),'Body',turret)
        for s in (-1,1):
            eye(turret,(s*.73,.50,.73),.49,'Body',s*22,True)
            panel('Cheek cheekplate',(s*1.05,.07,-.06),.50,.56,.22,'Gold',turret)
            for z in (-1.0,.65):rivet((s*1.38,1.50,z),root,mat='Gold',axis='x')
        for end,width in [((-.72,2.47,-1.13),.55),((-.89,1.82,-1.63),.48),((.02,2.82,-1.11),.52),((.63,2.40,-.85),.44),((.70,1.89,-1.43),.39)]:
            start=(end[0]*.12,1.08,-.40)
            petal('Broad carrot crown leaf',start,end,width,.13,'LeafGreen',turret,.05)
            curve('Leaf folded midrib',[start,((start[0]+end[0])*.5,(start[1]+end[1])*.5,(start[2]+end[2])*.5+.10),end],.018,'LeafGreen',turret)
        gun(.42,'Ivory')
        a.cyl('Broad cream muzzle',(0,0,fz-.26),.55,.51,'Ivory',barrel,'z',seg=32)
        a.cyl('Muzzle opening',(0,0,fz+.003),.37,.03,'Rubber',barrel,'z',seg=32)
        for z in (.65,1.13):a.torus('Gun step',(0,0,z),.44,.09,'Metal',barrel)
    elif k=='Duke':
        tracks(3.10,3.35,.62,'Body');chassis_plate(2.70,2.94,1.06)
        armor('Frog shoulders',(0,1.74,-.08),(1.38,.67,1.43),'Body',root)
        a.sculpt('Wide frog snout',(0,1.82,1.10),(1.15,.44,.63),'Body',root,.8)
        panel('Frog chin armor',(0,1.11,1.27),1.75,1.0,.39,'Body',root)
        for s in (-1,1):
            eye(root,(s*.94,2.44,.94),.49,'Body',s*17)
            a.ball('Nostril',(s*.32,1.99,1.72),(.07,.047,.03),'Navy',root)
            a.cyl('Chemical cylinder',(s*.81,2.61,-1.15),.38,1.78,'Poison',root,seg=24)
            for y in (1.79,3.39):a.cyl('Gas bottle cap',(s*.81,y,-1.15),.44,.16,'Metal',root,seg=24)
            a.cyl('Gas neck',(s*.81,3.55,-1.15),.18,.22,'Edge',root)
            a.box('Bottle spine',(s*1.18,2.62,-1.15),(.09,1.44,.12),'Metal',root,.01)
            a.beam('Chemical hose',(s*.81,1.87,-1.15),(s*1.25,1.53,-.35),.13,'Rubber',root)
            prism('Chemical warning badge',[(-.18,-.15),(.18,-.15),(0,.19)],.025,'Navy',root,p=(s*.81,2.63,-.754),bevel=.007)
            a.ball('Warning dot',(s*.81,2.57,-.729),(.032,.033,.015),'Poison',root)
            for j in range(4):a.ball('Bottle bubbles',(s*.81+math.sin(j*2)*.16,2.04+j*.30,-.77),(.045,.045,.025),'Ivory',root)
        a.sculpt('Frog smile',(0,1.58,1.66),(.79,.045,.055),'Navy',root)
        a.cyl('Recessed chemical nozzle',(0,0,.09),.23,.22,'Metal',barrel,'z',seg=24)
        a.cyl('Nozzle dark inset',(0,0,.205),.15,.015,'Rubber',barrel,'z',seg=24)
    elif k=='MineLander':
        tracks(3.16,3.17,.55,'Body');chassis_plate(2.9,2.8,.99)
        prism('Excavator hood',[(-1.17,-.37),(-1.27,.14),(-.77,.67),(.78,.67),(1.26,.14),(1.17,-.37)],1.77,'Body',root,p=(0,1.38,-.23))
        for s in (-1,1):
            a.cyl('Goggle metal housing',(s*.52,1.97,1.04),.36,.30,'Metal',root,'z',seg=24)
            eye(root,(s*.52,1.97,1.19),.26,'Metal',s*9)
            a.beam('Blade hydraulic',(s*1.17,.9,.1),(s*1.39,.55,1.93),.17,'Edge',root)
        prism('Curved shovel',[(-1.68,-.51),(-1.87,.40),(-1.62,.64),(1.62,.64),(1.87,.40),(1.68,-.51)],.34,'Metal',root,p=(0,.71,2.03),bevel=.09)
        for x in (-1.35,-.68,0,.68,1.35):prism('Dozer tooth',[(-.17,.16),(.17,.16),(.14,-.18),(0,-.37),(-.14,-.18)],.54,'Edge',root,p=(x,.28,2.17))
        gun(.54,'Metal');a.torus('Mortar lip',(0,0,fz-.035),.55,.11,'Edge',barrel)
    elif k=='Missile':
        turret.location=a.v((0,1.65,0));barrel.location=a.v((0,0,0));fire.location=a.v((0,0,fz-1.0))
        barrel=a.empty('Rocket body group',(0,0,-1.0),barrel)
        tracks(2.08,2.31,.39,'Navy');chassis_plate(1.90,2.09,.75,'Navy')
        a.cyl('Launch swivel',(0,.97,-.11),.53,.22,'Metal',root,seg=32)
        for side in (-1,1):
            prism('Pitch support cheek',[(-.45,-.30),(.44,-.30),(.20,.35),(-.19,.35)],.19,'Navy',root,p=(side*.36,1.26,-.05),axis='x',bevel=.05)
            a.cyl('Pitch bearing',(side*.48,1.40,-.06),.19,.12,'Metal',root,'x',seg=24)
            a.cyl('Pitch bearing pin',(side*.55,1.40,-.06),.085,.06,'Edge',root,'x',seg=20)
        a.box('Compact centered launch cradle',(0,-.81,1.0),(.66,.16,1.05),'Metal',barrel,.04)
        a.cyl('Red rocket shell',(0,0,fz*.30),.84,fz*.69,'Rocket',barrel,'z',seg=48)
        a.ball('Rocket rounded tail',(0,0,-.08),(.83,.83,.34),'Rocket',barrel)
        # Rounded ogive with a long ivory point, rather than a short straight cone.
        profiles=[(0,.84),(.20,.71),(.45,.55),(.70,.37),(.91,.20),(1.09,.06),(1.17,.005)]
        verts=[];faces=[];segments=48
        for z,r in profiles:
            for j in range(segments):
                q=j*math.tau/segments;verts.append(tuple(a.v((r*math.sin(q),r*math.cos(q),fz-1.17+z))))
        for j in range(len(profiles)-1):
            for t in range(segments):
                n=j*segments+t;faces.append((n,j*segments+(t+1)%segments,(j+1)*segments+(t+1)%segments,n+segments))
        faces.extend([tuple(range(segments-1,-1,-1)),tuple((len(profiles)-1)*segments+j for j in range(segments))])
        mesh=bpy.data.meshes.new('Ivory ogive');mesh.from_pydata(verts,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new('Long ivory ogive',mesh);bpy.context.collection.objects.link(o);o.parent=barrel;mesh.materials.append(a.MATS['Ivory'])
        for face in mesh.polygons:face.use_smooth=len(face.vertices)==4
        a.torus('Nose seam',(0,0,fz-1.17),.84,.025,'Metal',barrel)
        for z in (.12,1.05):a.torus('Rocket panel seam',(0,0,z),.844,.008,'Metal',barrel)
        for q in (.65,2.50,3.65,5.7):
            curve('Longitudinal shell seam',[(math.sin(q)*.847,math.cos(q)*.847,z) for z in (.05,.55,1.25,1.91)],.007,'Metal',barrel)
        for side in (-1,1):
            eye(barrel,(side*.69,.53,fz*.53),.41,'Rocket',side*45)
            fin=prism('Swept ivory tail fin',[(side*.55,-.60),(side*1.25,-.83),(side*1.40,.10),(side*.66,.74)],.12,'Ivory',barrel,p=(0,-.48,0),axis='y',bevel=.03)
            for vert in fin.data.vertices:vert.co.z-=.32*max(0,abs(vert.co.x)-.55)
        prism('Dorsal rocket fin',[(-.55,-.40),(.58,-.40),(.15,1.10),(-.42,.83)],.18,'Ivory',barrel,p=(0,.65,-.01),axis='x',bevel=.045)
        a.cyl('Rocket exhaust',(0,0,-.48),.51,.30,'Metal',barrel,'z',seg=32)
    elif k=='MultiMissile':
        chassis_plate(2.42,2.77,.73,'Teal')
        for side in (-1,1):
            for z in (-1.05,1.07):
                a.sculpt('Turtle wheel boot',(side*1.20,.63,z),(.45,.44,.60),'Body',root,.55)
                wheel(root,side*1.38,.45,z,.40,.30,'Gold')
        a.sculpt('Turtle dome',(0,1.40,-.18),(1.43,.75,1.42),'Body',root,.9)
        a.ball('Turtle white jaw',(0,1.18,2.23),(.91,.39,.87),'Ivory',root)
        a.ball('Turtle rounded head',(0,1.59,2.25),(.92,.57,.86),'Body',root)
        for s in (-1,1):
            eye(root,(s*.60,1.95,2.56),.43,'Body',s*24)
            for z in (-.97,.57):panel('Turtle leg armor',(s*1.40,1.0,z),.65,.64,.62,'Body',root)
        a.sculpt('Turtle mouth',(0,1.29,3.05),(.48,.045,.05),'Navy',root)
        prism('Hexagonal missile pod',[(-1.25,-.89),(-1.35,.64),(-1.03,.98),(1.02,.98),(1.35,.64),(1.25,-.89)],fz*.77,'Teal',barrel,p=(0,.10,fz*.40),bevel=.13)
        for x in (-.73,0,.73):
            for y in (-.56,.10,.76):
                a.cyl('Deep launch socket',(x,y,fz-.05),.29,.10,'Rubber',barrel,'z',seg=24)
                a.torus('Launcher thick rim',(x,y,fz),.29,.052,'Body',barrel)
    elif k=='SuperTank':
        barrel.location=a.v((0,tuh*.52-.42,tr*1.45*.45))
        tracks(3.48,3.79,.68,'Gold');chassis_plate(3.05,3.15,1.17,'Gold')
        a.sculpt('Lion heavy armored hull',(0,1.57,-.15),(1.42,.55,1.33),'Body',root,.65)
        prism('Lion sloped front apron',[(-1.04,.50),(-.86,-.50),(.86,-.50),(1.04,.50)],.28,'Gold',root,p=(0,1.18,1.58),bevel=.09)
        prism('Front recessed armor inset',[(-.65,.29),(-.48,-.31),(.48,-.31),(.65,.29)],.08,'WoodLight',root,p=(0,1.19,1.76),bevel=.045)
        for side in (-1,1):
            a.cyl('Apron rivet',(side*.77,1.42,1.77),.07,.08,'Metal',root,'z',seg=16)
            for zz in (-.65,.65):a.sculpt('Rounded lion side armor',(side*1.37,1.58,zz),(.43,.39,.59),'Gold',root,.65)
        armor('Lion head',(0,1.12,.08),(1.02,1.05,.93),'Gold',turret,lat=4,lon=10)
        petal('Swept forehead mane',(-.75,1.88,.62),(.53,2.07,.64),.29,.19,'Crimson',turret)
        for s in (-1,1):
            for j in range(6):
                x=.58+j*.032;y=1.97-j*.21
                points=[(s*x,y),(s*(x+.30),y+.12),(s*(x+.65),y-.02),(s*(x+.87-.055*j),y-.45),(s*(x+.67-.055*j),y-.81),(s*(x+.36),y-.43),(s*(x+.06),y-.25)]
                prism('Swept pointed mane plate',points,.32,'Crimson',turret,p=(0,0,.73+j*.052),bevel=.055)
                curve('Mane ridge',[(s*(x+.14),y-.08,.915+j*.052),(s*(x+.43),y-.16,.925+j*.052),(s*(x+.65-.045*j),y-.52,.92+j*.052)],.021,'Rocket',turret)
            lion_eye=eye(turret,(s*.57,1.35,.91),.36,'Gold',s*18);lion_eye.scale.z=.58
            curve('Furrowed lion brow',[(s*.21,1.52,1.13),(s*.60,1.65,1.07),(s*.94,1.63,.91)],.115,'Gold',turret)
            a.ball('Lion cream jowl',(s*.31,.87,1.08),(.41,.34,.35),'Ivory',turret)
            panel('Heavy shoulder plate',(s*1.34,1.80,.7),.85,.88,.50,'Gold',root)
            a.box('Shoulder missile pack',(s*1.43,1.05,-.35),(.95,1.14,1.16),'Gold',turret,.13,(0,0,s*-9))
            for dx in (-.23,.23):
                for yy in (.80,1.27):
                    a.cyl('Shoulder launch cell',(s*1.43+dx,yy,.25),.18,.07,'Rubber',turret,'z',seg=20)
                    a.cyl('Crimson missile',(s*1.43+dx,yy,.31),.135,.16,'Crimson',turret,'z',.04,20)
        prism('Triangular lion nose',[(-.22,.11),(.22,.11),(0,-.18)],.15,'Navy',turret,p=(0,1.07,1.43),bevel=.035)
        a.box('Crown circlet',(0,2.17,-.07),(1.12,.18,.62),'Gold',turret,.04)
        for x,h in ((-.62,.32),(-.34,.48),(0,.67),(.34,.48),(.62,.32)):
            prism('Crown peak',[(-.14,0),(-.18,h*.53),(0,h),(.18,h*.53),(.14,0)],.17,'Gold',turret,p=(x,2.24,-.04))
            a.ball('Crown jewel',(x,2.42,-.04+.11),(.065,.10,.045),'Crimson',turret)
        curve('Lion mouth',[(-.48,.69,1.28),(0,.60,1.41),(.48,.69,1.28)],.035,'Navy',turret)
        for side in (-1,1):
            for j in range(3):a.ball('Lion whisker pore',(side*(.22+.10*j),.87-j*.05,1.415),(.025,.025,.02),'Navy',turret)
        gun(.46,'Gold')
    elif k=='Laser':
        a.sculpt('Manta inner hull',(0,-.19,.22),(1.07,.34,1.18),'Navy',barrel,.60)
        sections=[(-1.35,.55,.23),(-.91,.96,.43),(-.20,1.15,.48),(.57,1.05,.33),(1.17,.79,.12),(1.40,.43,.035)]
        vertices=[];faces=[]
        for z,w,h in sections:
            for x,y in [(-w*.73,h), (w*.73,h),(w,h-.18),(w*.90,-.26),(w*.48,-.37),(-w*.48,-.37),(-w*.90,-.26),(-w,h-.18)]:vertices.append(tuple(a.v((x,y,z))))
        for j in range(len(sections)-1):
            for q in range(8):faces.append((j*8+q,j*8+(q+1)%8,(j+1)*8+(q+1)%8,(j+1)*8+q))
        faces.extend([tuple(range(7,-1,-1)),tuple((len(sections)-1)*8+i for i in range(8))])
        mesh=bpy.data.meshes.new('Faceted manta helmet');mesh.from_pydata(vertices,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new('Faceted manta helmet',mesh);bpy.context.collection.objects.link(o);a.finish(o,'Faceted manta helmet','Body',barrel,.045)
        for side in (-1,1):
            prism('Swept rear wing',[(side*.67,-.93),(side*2.47,-1.62),(side*1.98,-.57),(side*1.32,.42),(side*.78,.65)],.20,'Plum',barrel,p=(0,-.15,0),axis='y',bevel=.035)
            prism('Integrated forward blade',[(side*.73,-.61),(side*1.47,-.90),(side*1.52,.62),(side*1.86,fz),(side*1.55,fz-.12),(side*.76,.36)],.29,'Body',barrel,p=(0,-.25,0),axis='y',bevel=.035)
            curve('Blade edge seam',[(side*1.41,-.085,-.73),(side*1.37,-.085,.64),(side*1.68,-.085,fz-.10)],.015,'Plum',barrel)
            prism('Raked dorsal fin',[(-1.54,-.23),(.20,-.23),(-.36,.17),(-.91,.62),(-1.46,.83),(-1.97,.98),(-1.69,.51)],.14,'Body',barrel,p=(side*.70,.14,0),axis='x',bevel=.03)
            a.cyl('Hover connector',(side*1.08,1.69,-.36),.24,.40,'Metal',root,seg=24)
            a.cyl('Tapered hover skirt',(side*1.08,1.49,-.36),.36,.24,'Metal',root,r2=.64,seg=40)
            a.torus('Hover turquoise edge',(side*1.08,1.38,-.36),.41,.046,'Energy',root,'y')
            for z in (-.49,.07):rivet((side*.99,.25,z),barrel,'Metal',.052,axis='y')
        a.sculpt('Recessed dark visor',(0,-.23,1.16),(.91,.23,.32),'Navy',barrel,.62)
        prism('Cyan visor',[(-.67,.10),(-.43,-.08),(.43,-.08),(.67,.10)],.035,'Energy',barrel,p=(0,-.23,1.47),bevel=.015)
        curve('Upper visor armor',[(-.88,-.03,1.20),(0,-.04,1.51),(.88,-.03,1.20)],.075,'Body',barrel)
    elif k=='IonAttacker':
        armor('Orbital pink sphere',(0,1.93,0),(1.39,1.34,1.23),'Body',root)
        a.torus('Golden orbital ring',(0,1.32,0),1.86,.12,'Gold',root,'y')
        a.torus('Cyclops golden bezel',(0,2.02,1.07),.89,.12,'Gold',root)
        a.ball('Cyclops recessed socket',(0,2.02,1.13),(.85,.87,.23),'Navy',root)
        a.ball('Great cyan eye',(0,2.02,1.32),(.70,.73,.17),'Energy',root)
        a.ball('Cyclops pupil',(0,2.02,1.47),(.19,.54,.07),'Rubber',root)
        a.ball('Cyclops catchlight',(-.23,2.36,1.54),(.14,.17,.025),'EyeWhite',root)
        for s in (-1,1):
            for y in (1.01,2.91):
                a.ball('Orbital eye pod',(s*1.58,y,.17),(.42,.42,.40),'Body',root)
                a.torus('Satellite bezel',(s*1.58,y,.47),.29,.075,'Gold',root)
                a.ball('Satellite pupil',(s*1.58,y,.54),(.23,.24,.12),'Energy',root)
                a.beam('Orbital pod bracket',(s*1.02,y,.17),(s*1.50,y,.17),.13,'Gold',root)
        a.cyl('Orb top hatch',(0,3.23,-.1),.41,.12,'Gold',root,seg=24)
        a.ball('Pink hatch cap',(0,3.29,-.1),(.36,.15,.36),'Body',root)
        # No rings obscuring the eye: energy launch anchor remains on the articulated rig.
    elif k=='Poseidon':
        armor('Whale great body',(0,1.62,-.08),(1.43,1.13,1.60),'Body',root)
        a.sculpt('Whale white belly',(0,1.13,.64),(1.32,.70,1.06),'Ivory',root,.91)
        for s in (-1,1):
            eye(root,(s*.93,2.07,1.03),.48,'Body',s*28)
            prism('Whale pectoral fin',[(s*.90,-.45),(s*1.90,-.90),(s*1.75,.44),(s*1.17,.77)],.18,'Body',root,p=(0,.97,0),axis='y',bevel=.09)
            a.sculpt('Small blue wheelpod',(s*1.20,.51,-.65),(.42,.42,.88),'Teal',root,.5)
            for z in (-1.05,-.32):wheel(root,s*1.40,.51,z,.35,.23,'Body')
            petal('Whale tail fluke',(0,2.30,-2.07),(s*.98,2.98,-2.10),.45,.16,'Body',root)
        a.beam('Whale rising tail',(0,1.52,-1.27),(0,2.47,-2.02),.40,'Body',root)
        petal('Whale dorsal fin',(0,2.17,-.65),(0,3.52,-1.09),.39,.13,'Body',root)
        a.sculpt('Whale smile',(0,1.58,1.65),(.81,.045,.045),'Navy',root)
        for x in (-.70,-.35,0,.35,.70):a.beam('Belly panel seam',(x,.74,1.41),(x,1.19,1.62),.026,'Gold',root)
        gun(.25,'Gold')
        for s in (-1,1):
            a.beam('Golden trident branch',(0,0,fz*.37),(s*.56,0,fz*.69),.20,'Gold',barrel)
            a.beam('Trident tine',(s*.56,0,fz*.69),(s*.56,0,fz-.11),.20,'Gold',barrel)
            a.cyl('Trident tip',(s*.56,0,fz-.12),.16,.29,'Gold',barrel,'z',0,4)
    else:
        armor('Bird torso',(0,1.56,.01),(1.10,.88,1.18),'Body',root)
        for s in (-1,1):
            eye(root,(s*.62,1.93,.91),.46,'Body',s*18)
            a.sculpt('Bird track pod',(s*.93,.48,-.30),(.39,.36,1.03),'Teal',root,.5)
            for z in (-.91,-.28,.35):wheel(root,s*1.10,.46,z,.32,.26,'Body')
            for j in range(6):
                petal('Primary green flight feather',(s*.78,1.11+j*.11,-.44),(s*(2.36+.18*j),1.07+j*.35,-.78),.40,.13,'Body',root,.12)
            for j in range(4):
                petal('Cream secondary feather',(s*.83,1.28+j*.12,-.18),(s*(1.77+.13*j),1.55+j*.33,-.30),.30,.12,'Ivory',root,.13)
            a.torus('Large turbine ring',(s*1.33,2.14,-.35),.61,.13,'Ivory',root)
            a.cyl('Turbine dark interior',(s*1.33,2.14,-.40),.51,.22,'Metal',root,'z',seg=32)
            for j in range(7):
                q=j*math.tau/7
                a.beam('Turbine blade',(s*1.33,2.14,-.22),(s*1.33+math.cos(q)*.46,2.14+math.sin(q)*.46,-.18),.15,'Edge',root)
            a.ball('Turbine center',(s*1.33,2.14,-.16),(.17,.17,.10),'Metal',root)
        # Tapered hooked beak: wide at its crown, sharp and forward/down at the tip.
        verts=[];faces=[];rings=[(1.88,1.15,.43,.29),(1.77,1.50,.42,.28),(1.43,1.83,.30,.21),(1.07,2.07,.13,.12),(.90,2.15,.012,.012)]
        for y,z,w,d in rings:
            for j in range(12):
                q=j*math.tau/12;verts.append(tuple(a.v((w*math.cos(q),y+d*math.sin(q),z))))
        for i in range(len(rings)-1):
            for j in range(12):faces.append((i*12+j,i*12+(j+1)%12,(i+1)*12+(j+1)%12,(i+1)*12+j))
        faces.extend([tuple(range(11,-1,-1)),tuple(48+j for j in range(12))])
        mesh=bpy.data.meshes.new('Hooked beak');mesh.from_pydata(verts,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        beak=bpy.data.objects.new('Great ivory beak',mesh);bpy.context.collection.objects.link(beak);a.finish(beak,'Great ivory beak','Ivory',root)
        for side in (-1,1):curve('Beak side ridge',[(side*.32,1.79,1.48),(side*.23,1.40,1.87),(0,.91,2.15)],.016,'Gold',root)
        for s in (-1,0,1):leaf('Bird head crest',(s*.26,2.76,-.50),(.77,.78,.15),'Teal',root,s*30)
    # Small team marks retain affiliation without replacing the character's authored palette.
    for side in (() if k in ('Catapult','Missile','Laser') else (-1,1)):a.box('Team insignia',(side*bw*.47,1.20,-bl*.36),(.08,.18,.28),'Team',root,.025)
    a.box('Snow hull',(0,by+bh+.025,-bl*.32),(bw*.65,.06,bl*.24),'Snow',root,.025)
    return root
