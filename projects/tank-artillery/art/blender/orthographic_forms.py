"""Character-specific geometry based on art/concepts/orthographic-v1 sheets.

Game coordinates: X width, Y height, Z forward. Every view uses the same mesh.
"""
import math
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from types import SimpleNamespace

def build(kind, context):
    g=SimpleNamespace(**context);a=g.a;root=g.root;turret=g.turret;barrel=g.barrel;fire=g.fire
    prism,eye,curve,armor=g.prism,g.eye,g.curve,g.armor
    def rig(t,b,tip):
        turret.location=a.v(t);barrel.location=a.v(b);fire.location=a.v((0,0,tip))
        root['authoredTurret']=list(t);root['authoredBarrel']=list(b);root['authoredFire']=tip
    def mesh(name,verts,faces,mat,parent,smooth=True,bevel=0):
        m=bpy.data.meshes.new(name);m.from_pydata([tuple(a.v(p)) for p in verts],[],faces);m.update()
        bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
        o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o)
        if bevel:return a.finish(o,name,mat,parent,bevel)
        o.parent=parent;m.materials.append(a.MATS[mat])
        for f in m.polygons:f.use_smooth=smooth and len(f.vertices)<=4
        return o
    def loft(name,sections,mat,parent,axis='y',sides=32):
        # Sections = longitudinal position, transverse radii, offset of remaining axis.
        verts=[]
        for u,w,h,c in sections:
            for j in range(sides):
                t=math.tau*j/sides
                verts.append((w*math.cos(t),u,c+h*math.sin(t)) if axis=='y' else (w*math.cos(t),c+h*math.sin(t),u))
        faces=[tuple(range(sides-1,-1,-1))]
        for i in range(len(sections)-1):
            for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
        faces.append(tuple((len(sections)-1)*sides+j for j in range(sides)))
        return mesh(name,verts,faces,mat,parent,smooth=sides>12)
    def feather(name,start,end,width,depth,mat,parent,normal=(0,0,1),bend=.14):
        start,end=Vector(start),Vector(end);d=end-start;n=Vector(normal).normalized();side=d.cross(n).normalized();verts=[]
        for j in range(17):
            t=j/16;profile=max(.006,math.sin(math.pi*t)**.75)*(1-.50*t)
            center=start+d*t+n*(bend*math.sin(math.pi*t))
            for q in range(12):
                ang=math.tau*q/12;v=center+side*(width*profile*math.cos(ang))+n*(depth*profile*math.sin(ang));verts.append(tuple(v))
        faces=[tuple(range(11,-1,-1)),tuple(192+j for j in range(12))]
        for j in range(16):
            for q in range(12):faces.append((j*12+q,j*12+(q+1)%12,(j+1)*12+(q+1)%12,(j+1)*12+q))
        return mesh(name,verts,faces,mat,parent)
    def disk_wheel(x,y,z,r,width,paint,wood=False,tread=True):
        w=a.empty('Wheel',(x,y,z),root);root['wheelRadius']=r;side=1 if x>0 else -1
        a.cyl('Wheel tread',(0,0,0),r,width,'WoodLight' if wood and kind!='CrossBow' else 'Rubber',w,'x',seg=40)
        for face in (-1,1):
            xx=face*(width*.5+.012)
            a.cyl('Solid wheel face',(xx,0,0),r*.85,.04,paint,w,'x',seg=32)
            a.torus('Thin iron rim',(xx,0,0),r*.93,.035,'Metal',w,'x')
            a.cyl('Wheel hub',(xx+face*.045,0,0),r*.25,.08,'Metal',w,'x',seg=24)
            if kind=='CrossBow' and face==side:
                # Small inset team enamel; keep the metal hub rim and owl palette.
                a.cyl('Team enamel hub',(xx+face*.091,0,0),r*.18,.018,'Team',w,'x',seg=24)
            if wood:
                for j in range(12):
                    q=j*math.tau/12;curve('Radial board joint',[(xx+face*.025,0,0),(xx+face*.025,math.sin(q)*r*.81,math.cos(q)*r*.81)],.009,'Wood',w)
        if not wood and tread:
            for j in range(18):
                q=j*math.tau/18;o=a.box('Wheel individual tread',(0,math.sin(q)*r,math.cos(q)*r),(width+.035,.11,.14),'Metal',w,.016);o.rotation_euler[0]=-q
        return w
    def fender(x,r,length,y,paint,width=.67):
        # Thick arch contour in the side projection; cheeks wrap both curved ends.
        h=length*.5-r;pts=[]
        for yy in (r+.16,r-.04):
            seq=range(13) if yy>r else range(12,-1,-1)
            for j in seq:
                q=math.pi*j/12;z=(h if math.cos(q)>0 else -h)+math.cos(q)*yy
                pts.append((z,y+math.sin(q)*yy))
        prism('Wrapped curved fender',pts,width,paint,root,p=(x,0,0),axis='x',bevel=.035)
    def tracks(width,length,r,paint):
        y=r+.09;h=length*.5-r
        for side in (-1,1):
            x=side*width*.5
            a.sculpt('Inner track belt',(x,y,0),(.20,r*.97,length*.49),'Rubber',root,.5)
            for z in (-h,0,h):disk_wheel(x+side*.04,y,z,r*.78,.43,paint,tread=False)
            for j in range(11):
                z=-h+2*h*j/10
                for sy in (-1,1):a.box('Flat track link',(x,y+sy*r,z),(.65,.14,max(.14,h*.18)),'Metal',root,.018)
            for end in (-1,1):
                for j in range(1,10):
                    q=-math.pi/2+j*math.pi/10;o=a.box('Curved track link',(x,y+math.sin(q)*r,end*(h+math.cos(q)*r)),(.65,.14,.19),'Metal',root,.018);o.rotation_euler[0]=end*(math.pi/2-q)
            fender(x,r,length,y,paint)
        return y
    def cannon(parent,length,r,paint):
        a.cyl('Stepped cannon base',(0,0,.15),r*1.12,.34,'Metal',parent,'z',seg=40)
        a.cyl('Cannon tube',(0,0,length*.46),r,length*.87,paint,parent,'z',seg=40)
        a.cyl('Broad muzzle',(0,0,length-.18),r*1.23,.36,paint,parent,'z',seg=40)
        a.cyl('Dark open bore',(0,0,length+.01),r*.78,.022,'Rubber',parent,'z',seg=40)
        a.torus('Muzzle lip',(0,0,length+.015),r*.98,r*.12,paint,parent)
    if kind=='Catapult':
        # Dimensions traced from the front and side concept silhouettes.
        rig((0,2.0,0),(0,1.03,-1.30),0)
        root['wheelRadius']=.60
        # Dedicated team plates: preserve the oak and crimson wheel palette.
        a.box('Rear team badge',(0,1.25,-1.13),(.48,.25,.055),'Team',root,bev=.025)
        a.box('Top team badge',(0,1.89,.45),(.48,.055,.32),'Team',root,bev=.025)
        def signedpow(x,p):return math.copysign(abs(x)**p,x)
        def section(t,z,inset=0):
            taper=1-.13*(abs(z)/1.04)**3
            return ((1.00-inset)*taper*signedpow(math.cos(t),.48),1.11+(.76-inset)*taper*signedpow(math.sin(t),.48),z)
        # Horizontal coopered body: rounded rectangular cross-section, convex ends.
        zs=[-1.04,-.96,-.78,-.45,0,.45,.78,.96,1.04]
        for j in range(20):
            verts=[];faces=[]
            for z in zs:
                for q in range(5):verts.append(section((j+(q/4)*.984)*math.tau/20,z))
            for row in range(len(zs)-1):
                for q in range(4):
                    n=row*5+q;faces.append((n,n+1,n+6,n+5))
            mesh('Longitudinal oak stave',verts,faces,'WoodLight',root,True)
        # Solid front with genuine plank divisions. Front is gently convex, not an upright lid.
        for zsign in (-1,1):
            verts=[(0,1.11,zsign*1.10)];faces=[]
            for j in range(81):
                p=section(j*math.tau/80,zsign*1.04);verts.append(p)
            for j in range(80):faces.append((0,j+1,j+2))
            mesh('Oak end face',verts,faces,'WoodLight',root,True)
            for x in [-.65,-.39,-.13,.13,.39,.65]:
                ymax=.76*(1-(abs(x)/.90)**4)**.25
                curve('End plank joint',[(x,1.11-ymax*.88,zsign*1.055),(x,1.11,zsign*(1.105-.035*abs(x))),(x,1.11+ymax*.88,zsign*1.055)],.008,'Wood',root)
        for z in (-.64,.42):
            points=[section(j*math.tau/64,z,-.018) for j in range(65)]
            # Flat wide straps follow the barrel cross-section.
            verts=[];faces=[]
            for x,y,_ in points:verts.extend([(x,y,z-.09),(x,y,z+.09)])
            for j in range(64):faces.append((2*j,2*j+1,2*j+3,2*j+2))
            mesh('Flat forged barrel strap',verts,faces,'Metal',root)
            for side in (-1,1):
                for y in (.65,1.1,1.6):a.cyl('Strap rivet',(side*1.025,y,z),.035,.036,'Edge',root,'x',seg=12)
        for side in (-1,1):
            for z,r in [(-.68,.67),(.74,.52)]:
                w=a.empty('Wheel',(side*1.16,r+.025,z),root)
                a.cyl('Red timber wheel',(0,0,0),r,.37,'Crimson',w,'x',seg=48)
                a.cyl('Central iron tire strip',(0,0,0),r+.016,.105,'Metal',w,'x',seg=48)
                for s in (-1,1):
                    ox=s*.197
                    a.torus('Thin outer iron edge',(ox,0,0),r-.018,.023,'Metal',w,'x')
                    for j in range(14):
                        q=j*math.tau/14;curve('Wheel radial joint',[(ox,math.sin(q)*r*.20,math.cos(q)*r*.20),(ox,math.sin(q)*r*.94,math.cos(q)*r*.94)],.007,'Wood',w)
                    a.cyl('Broad axle cap',(ox+s*.018,0,0),r*.25,.05,'Metal',w,'x',seg=24)
                    a.cyl('Oak axle end',(ox+s*.05,0,0),r*.15,.04,'WoodLight',w,'x',seg=24)
                for j in range(12):
                    q=j*math.tau/12;a.ball('Iron tire nail',(0,math.sin(q)*(r+.019),math.cos(q)*(r+.019)),(.022,.025,.025),'Edge',w)
            eyeobj=eye(root,(side*.61,1.21,1.015),.39,'WoodLight',side*47)
            eyeobj.scale.z=1.26
            # Inner ends lower: eyebrows form a clear angry V in front projection.
            pts=[(side*.22,1.62),(side*.84,1.88),(side*.87,1.68),(side*.32,1.42)]
            prism('Sloping carved oak eyebrow',pts,.17,'WoodLight',root,p=(0,0,1.07),bevel=.045)
            # Rail pivot and arm in root coordinates; sling remains the aiming origin.
            prism('Iron triangular bearing',[(-.28,-.15),(.25,-.15),(.10,.30),(-.10,.30)],.14,'Metal',turret,p=(side*.52,0,.05),axis='x',bevel=.035)
            a.cyl('Bearing axle',(side*.65,.06,.06),.12,.10,'Edge',turret,'x',seg=24)
            a.beam('Raised oak sling rail',(side*.50,.04,.10),(side*.72,.97,-1.31),.22,'WoodLight',turret)
            for offset in (-.05,.04):curve('Rail wood grain',[(side*.50+offset,.13,.05),(side*.60+offset,.54,-.61),(side*.71+offset,1.05,-1.27)],.006,'Wood',turret)
        a.cyl('Cross shaft',(0,.06,.06),.12,1.48,'Metal',turret,'x',seg=24)
        # Deep leather hemispherical sling with an actual rim and seams.
        verts=[];faces=[]
        for j in range(13):
            t=-math.pi/2+.012+j*(math.pi/2-.012)/12
            for q in range(64):
                ang=q*math.tau/64;verts.append((.76*math.cos(t)*math.cos(ang),.98+.65*math.sin(t),-1.30+.71*math.cos(t)*math.sin(ang)))
        for j in range(12):
            for q in range(64):faces.append((j*64+q,j*64+(q+1)%64,(j+1)*64+(q+1)%64,(j+1)*64+q))
        mesh('Deep leather sling',verts,faces,'Wood',turret)
        curve('Leather rolled rim',[(.76*math.cos(q*math.tau/64),.98,-1.30+.71*math.sin(q*math.tau/64)) for q in range(65)],.028,'Wood',turret)
        for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
            curve('Leather panel seam',[(.768*math.cos(t)*math.cos(angle),.98+.656*math.sin(t),-1.30+.719*math.cos(t)*math.sin(angle)) for t in [-1.52,-1.25,-1,-.75,-.5,-.25,0]],.009,'Rubber',turret)
        for side in (-1,1):
            for strand in range(3):
                pts=[]
                for j in range(81):
                    t=j/80;q=t*math.tau*9+strand*math.tau/3
                    pts.append((side*(.72-.43*math.sin(math.pi*t))+.024*math.cos(q),1.02-.56*t-.65*math.sin(math.pi*t)+.024*math.sin(q),-1.28+.95*t+.02*math.sin(q)))
                curve('Twisted hemp sling rope',pts,.029,'WoodLight',turret)
            for j in range(4):
                ring=a.torus('Thick rope binding',(side*.71,.97+j*.052,-1.28),.135,.05,'WoodLight',turret,'y');ring.rotation_euler[0]+=.35
            for n in range(2):
                curve('Looped knot strands',[(side*.65,1.10,-1.38),(side*.82,1.17,-1.32),(side*.85,1.02,-1.16),(side*.67,.91,-1.18),(side*.65,1.10,-1.38)],.043,'WoodLight',turret)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=a.v((0,1.10,-1.30)))
        o=bpy.context.object
        for v in o.data.vertices:v.co*=1+.055*math.sin(v.index*3.74)
        o.scale=(.70,.66,.62);a.finish(o,'Faceted loaded boulder','Stone',turret)
        for face in o.data.polygons:face.use_smooth=False
        return root
    elif kind=='CrossBow':
        rig((0,1.90,-.30),(0,.25,-1.05),.70)
        a.sculpt('Low curved owl carriage',(0,.55,0),(1.03,.18,1.03),'WoodLight',root,.5)
        a.sculpt('Owl rounded feather body',(0,1.13,-.08),(1.04,.65,1.20),'Teal',root,.85)
        for s in (-1,1):
            for z in (-.76,.76):
                wheel=disk_wheel(s*1.10,.39,z,.37,.34,'WoodLight',True)
                for j in range(12):
                    q=j*math.tau/12
                    block=a.box('Owl scalloped rubber tire',(0,math.sin(q)*.359,math.cos(q)*.359),(.36,.064,.13),'Rubber',wheel,.014);block.rotation_euler[0]=-q
            for z in (-.76,.76):
                # Independent little wheel covers, not track fenders.
                cover=a.empty('Owl wheel cover',(s*1.10,.40,z),root)
                pts=[(-.43,0),(-.40,.27),(-.23,.44),(.23,.44),(.40,.27),(.43,0),(.30,0),(.26,.22),(.15,.30),(-.15,.30),(-.26,.22),(-.30,0)]
                prism('Teal wheel arch',pts,.42,'Teal',cover,axis='x',bevel=.035)
            e=eye(root,(s*.43,1.02,1.04),.32,'Teal',s*33);e.scale.x=1.02;e.scale.z=1.04
            for row in range(3):
                for j in range(3):
                    start=(s*(.20+.22*j),1.72-.16*row,.94-.38*j)
                    end=(s*(1.27-.10*j),1.15-.16*row,-1.02+.44*row-.32*j)
                    leaf_normal=Vector((s*.8,.35,.35)).normalized()
                    feather('Overlapping owl leaf',start,end,.36,.075,'Teal',root,normal=tuple(leaf_normal),bend=.12)
                    mid=tuple((Vector(start)+Vector(end))*.5+leaf_normal*.17)
                    curve('Leaf midrib',[start,mid,end],.012,'LeafGreen',root)
            feather('Pointed owl brow leaf',(s*.10,1.12,1.25),(s*.87,1.82,1.15),.29,.085,'Teal',root,bend=.10)
        feather('Central owl forehead leaf',(0,1.15,1.32),(0,1.79,1.22),.23,.075,'Teal',root,bend=.12)
        # Rear camera must see layered leaves rather than the bare body ellipsoid.
        for column in (-1,0,1):
            start=(column*.30,1.74,-.42)
            end=(column*.58,.70,-1.26)
            feather('Rear owl overlapping leaf',start,end,.43,.07,'Teal',root,normal=(0,.22,-1),bend=.16)
            mid=(column*.44,1.22,-1.06)
            curve('Rear leaf vein',[start,mid,end],.014,'LeafGreen',root)
        loft('Small hooked owl beak',[(1.15,.14,.12,1.0),(1.36,.17,.13,.95),(1.50,.04,.035,.75)],'Gold',root,axis='z',sides=16)
        a.box('Owl front iron clasp',(0,.50,1.11),(.44,.13,.10),'Metal',root,.02)
        a.beam('Owl bow connected mounting stem',(0,1.51,-.63),(0,2.20,-.99),.26,'Metal',root)
        a.box('Bow support',(0,-.08,.33),(.30,.38,.32),'Metal',barrel,.045)
        a.box('Short crossbow stock',(0,0,.23),(.28,.25,.54),'Wood',barrel,.035)
        for s in (-1,1):
            # Rectangular laminated limbs, with tapered depth instead of round hose geometry.
            path=[(0,0,.42),(s*.40,-.06,.46),(s*.85,-.17,.52),(s*1.30,-.20,.50),(s*1.62,-.14,.38),(s*1.83,-.01,.26),(s*1.91,.14,.20)]
            vertices=[]
            for j,(x,y,z) in enumerate(path):
                h=.12-.027*j/(len(path)-1);depth=.10-.047*j/(len(path)-1)
                vertices.extend([(x,y-h,z-depth),(x,y+h,z-depth),(x,y+h,z+depth),(x,y-h,z+depth)])
            faces=[(3,2,1,0),tuple(range(len(vertices)-4,len(vertices)))]
            for j in range(len(path)-1):
                for q in range(4):faces.append((j*4+q,j*4+(q+1)%4,(j+1)*4+(q+1)%4,(j+1)*4+q))
            mesh('Flat laminated recurved oak limb',vertices,faces,'WoodLight',barrel,False,.022)
            for line in (-.045,.045):curve('Bow longitudinal lamination',[(x,y+line,z+.104-.047*j/(len(path)-1)) for j,(x,y,z) in enumerate(path)],.009,'Wood',barrel)
            a.box('Bow tip metal cap',(s*1.87,.07,.35),(.20,.28,.28),'Metal',barrel,.03)
            curve('Taut bow string',[(s*1.88,.12,.35),(0,.12,-.39)],.019,'Ivory',barrel)
        a.box('Central steel bow clamp',(0,.04,.38),(.36,.46,.44),'Metal',barrel,.03)
        for x in (-.12,.12):
            a.box('Raised clamp strap',(x,.04,.62),(.075,.48,.04),'Edge',barrel,.012)
            for yy in (-.10,.18):a.cyl('Clamp round rivet',(x,yy,.649),.025,.02,'Metal',barrel,'z',seg=12)
        # The loaded projectile is spawned by gameplay; reference resting bow has no projecting bolt.
    elif kind=='Cannon':
        rig((0,1.80,0),(0,-.20,0),1.932)
        a.sculpt('Compact wooden gun carriage',(0,.49,0),(.88,.18,.86),'WoodLight',root,.4)
        for s in (-1,1):
            for z in (-.70,.68):
                wheel=disk_wheel(s*.99,.43,z,.42,.32,'WoodLight',True)
                a.cyl('Carriage wheel iron hoop',(0,0,0),.427,.105,'Metal',wheel,'x',seg=48)
                for j in range(12):
                    q=j*math.tau/12;nail=a.cyl('Iron hoop nail',(0,math.sin(q)*.429,math.cos(q)*.429),.023,.018,'Edge',wheel,'z',seg=8);nail.rotation_euler=a.v((0,math.sin(q),math.cos(q))).to_track_quat('Z','Y').to_euler()
                for sign in (-1,1):a.cyl('Carriage heavy axle boss',(sign*.218,0,0),.15,.10,'Metal',wheel,'x',seg=24)
            a.box('Wooden carriage cheek',(s*.83,.78,0),(.20,.44,1.62),'Wood',root,.05)
        armor('Great spherical navy cannon',(0,0,0),(1.18,1.19,1.18),'Navy',turret,lat=5,lon=12)
        for s in (-1,1):
            e=eye(turret,(s*.87,.13,.73),.41,'Navy',s*53)
            prism('Thick gold captain eyebrow',[(-.42,-.08),(.38,-.15),(.32,.06),(-.26,.19)],.15,'Gold',e,p=(0,.43,.15),bevel=.04)
        a.cyl('Dark gun neck',(0,0,1.10),.50,.42,'Metal',barrel,'z',seg=40)
        a.cyl('Flared brass barrel',(0,0,1.46),.49,.65,'Gold',barrel,'z',r2=.58,seg=40)
        a.cyl('Brass rim',(0,0,1.80),.64,.22,'Gold',barrel,'z',seg=40)
        a.cyl('Deep cannon mouth',(0,0,1.918),.48,.02,'Rubber',barrel,'z',seg=40)
        for j in range(10):
            q=j*math.tau/10;a.cyl('Brass muzzle stud',(math.cos(q)*.57,math.sin(q)*.57,1.932),.028,.018,'WoodLight',barrel,'z',seg=12)
        lid=a.empty('Round hinged captain lid',(0,1.04,-.28),turret);lid.rotation_euler[0]=math.radians(16)
        a.cyl('Navy round hatch',(0,0,0),.73,.15,'Navy',lid,seg=48)
        a.ball('Curved hatch crown',(0,.055,0),(.69,.15,.69),'Navy',lid)
        prism('Hinge tab',[(-.13,0),(.13,0),(.13,.34),(-.13,.34)],.15,'Navy',lid,p=(0,.09,-.53),bevel=.025)
        a.cyl('Hinge hole',(0,.32,-.442),.052,.014,'Rubber',lid,'z',seg=20)
        a.torus('Rear brass towing eye',(0,.10,-1.23),.15,.048,'Gold',turret,'x')
        a.cyl('Rear towing attachment',(0,.10,-1.13),.09,.16,'Metal',turret,'z',seg=24)
        # Full left-side skull with eye holes and crossed bones, attached to the spherical side.
        emblem=a.empty('Captain skull emblem',(-1.105,.04,-.25),turret)
        skull=[(-.27,.08),(-.25,.32),(-.13,.43),(.12,.43),(.26,.31),(.27,.08),(.15,-.02),(.14,-.18),(-.13,-.18),(-.15,-.02)]
        prism('Ivory skull',skull,.035,'Ivory',emblem,axis='x',bevel=.02)
        for z in (-.115,.115):a.ball('Skull eye hollow',(-.03,.19,z),(.023,.087,.063),'Navy',emblem)
        prism('Skull triangular nose',[(-.042,-.018),(.042,-.018),(0,.070)],.043,'Navy',emblem,p=(-.030,0,0),axis='x',bevel=.006)
        for z in (-.065,0,.065):a.box('Skull teeth gap',(-.031,-.157,z),(.044,.057,.018),'Navy',emblem,.002)
        for s in (-1,1):
            curve('Crossed pirate bone',[(-.035,-.60,s*.31),(-.035,-.38,0),(-.035,-.22,-s*.31)],.040,'Ivory',emblem)
            for yy,zz in ((-.60,s*.31),(-.22,-s*.31)):a.ball('Bone knob',(-.035,yy,zz),(.045,.065,.065),'Ivory',emblem)
        bpy.context.view_layer.update()
        for child in list(emblem.children):
            rel=emblem.matrix_local@child.matrix_local
            points=[rel@v.co for v in child.data.vertices];low=min(p.x for p in points);high=max(p.x for p in points)
            child.parent=turret;child.location=(0,0,0);child.rotation_euler=(0,0,0);child.scale=(1,1,1)
            for v,p in zip(child.data.vertices,points):
                y,z=p.z,-p.y;offset=.014+.025*(high-p.x)/max(.001,high-low)
                v.co=a.v((-math.sqrt(max(.05,1.18**2-y*y-z*z))-offset,y,z))
            child.data.normals_split_custom_set([(0,0,0)]*len(child.data.loops));child.data.update()
    elif kind=='Carrot':
        rig((0,1.70,0),(0,.06,.75),1.30)
        tracks(2.28,2.48,.43,'Gold')
        a.sculpt('Carrot undercarriage',(0,.64,-.45),(.80,.17,.58),'Gold',root,.5)
        sections=[(-1.13,.29,.36,.28),(-.80,.60,.69,.16),(-.24,.94,1.08,-.05),(.32,1.02,1.15,-.13),(.66,.78,.90,-.24),(.81,.28,.34,-.35)]
        smooth=[]
        for i in range(len(sections)-1):
            p0=Vector(sections[max(0,i-1)]);p1=Vector(sections[i]);p2=Vector(sections[i+1]);p3=Vector(sections[min(len(sections)-1,i+2)])
            for j in range(10):
                t=j/10;v=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
                v[0]=p1[0]+(p2[0]-p1[0])*t;smooth.append(tuple(v))
        smooth.append(sections[-1])
        carrot=loft('Smooth tapered carrot armor',smooth,'Body',turret,sides=120)
        # Recessed joints are actual geometry, not lines floating above the body.
        for i,vertex in enumerate(carrot.data.vertices):
            row=i//120;column=i%120;u,w,h,c=smooth[row]
            angular=min(column%10,10-column%10)
            groove=.012 if angular==0 else 0
            groove+=.014*max(0,1-min(abs(u-level) for level in (-.57,.08,.54))/.042)
            vertex.co.x*=1-groove
            vertex.co.y=-c+(vertex.co.y+c)*(1-groove)
        carrot.data.update()
        for seam in range(12):
            angle=seam*math.tau/12;verts=[];faces=[]
            for u,w,h,c in smooth:
                for offset in (-.003,.003):
                    t=angle+offset;verts.append((w*.990*math.cos(t),u,c+h*.990*math.sin(t)))
            for j in range(len(smooth)-1):faces.append((2*j,2*j+1,2*j+3,2*j+2))
            mesh('Inset carrot armor joint',verts,faces,'Wood',turret)
        for s in (-1,1):
            e=eye(turret,(s*.72,.12,.68),.40,'Body',s*39)
            prism('Orange root eyebrow',[(-.42,.02),(.39,-.11),(.32,.14),(-.36,.24)],.18,'Body',e,p=(0,.38,.17),bevel=.045)
            a.cyl('Carrot side pivot',(s*.93,-.39,-.52),.23,.16,'Gold',turret,'x',seg=32)
        leaf_specs=[((0,1.76,-.90),.47,(0,.12,1)),((-.78,1.44,-.78),.47,(-.32,.10,.95)),((.76,1.40,-.76),.47,(.32,.10,.95)),((-.36,1.05,-1.56),.42,(-.95,.12,.25)),((.15,1.55,-1.40),.45,(.94,.10,.30)),((.65,1.04,-1.31),.40,(.66,.15,.73))]
        for end,w,normal in leaf_specs:
            start=(end[0]*.13,.61,-.51)
            feather('Broad folded carrot crown leaf',start,end,w,.080,'LeafGreen',turret,normal=normal,bend=.13)
        cannon(barrel,1.30,.34,'Ivory')
        # The carrot is the weapon: armor, eyes and crown must pitch with its tube.
        # Preserve the authored rest pose while separating visual motion from TankShape.
        bpy.context.view_layer.update()
        for child in list(turret.children):
            if child==barrel:continue
            world=child.matrix_world.copy()
            child.parent=barrel
            child.matrix_world=world
        # Pitch about the visible side axle, so the root does not sink into the tracks.
        bpy.context.view_layer.update()
        parked={child:child.matrix_world.copy() for child in barrel.children}
        barrel.location=a.v((0,-.39,-.52))
        bpy.context.view_layer.update()
        for child,world in parked.items():child.matrix_world=world
        root['authoredBarrel']=[0,-.39,-.52]
    elif kind=='Duke':
        rig((0,1.60,0),(0,.12,1.0),.35)
        tracks(2.65,2.90,.49,'Body')
        armor('Frog low armored back',(0,1.27,-.22),(1.20,.59,1.25),'Body',root,lat=4,lon=10)
        frog_chin=loft('Faceted frog chin',[(.37,.52,.24,1.04),(.66,.73,.34,1.13),(1.05,.98,.44,1.13),(1.43,1.07,.43,1.11)],'Body',root,sides=8)
        frog_snout=loft('Faceted broad frog snout',[(1.40,1.02,.52,1.05),(1.70,1.00,.53,1.04),(1.89,.72,.30,.98)],'Body',root,sides=10)
        bpy.context.view_layer.update()
        frog_surfaces=[BVHTree.FromObject(o,bpy.context.evaluated_depsgraph_get()) for o in (frog_chin,frog_snout)]
        def frog_surface(x,y,offset=.006):
            hits=[]
            for surface in frog_surfaces:
                hit,normal,_,_=surface.ray_cast(a.v((x,y,5)),a.v((0,0,-1)))
                if hit is not None:hits.append((hit,normal))
            if not hits:return None
            hit,normal=max(hits,key=lambda item:-item[0].y);q=hit+normal*offset
            return (q.x,q.z,-q.y)
        mouth=[]
        for j in range(41):
            x=-.93+j*1.86/40;y=1.495+.055*(1-abs(x)/.93)
            point=frog_surface(x,y)
            if point:mouth.append(point)
        curve('Surface fitted frog mouth',mouth,.012,'Navy',root)
        for s in (-1,1):
            eye(root,(s*.60,1.96,1.12),.34,'Body',s*34)
            nostril=frog_surface(s*.34,1.77,.012)
            if nostril:a.ball('Inset frog nostril',nostril,(.028,.022,.014),'Navy',root)
            a.cyl('Transparent poison glass',(s*.65,2.39,-.95),.34,1.46,'GlassBottle',root,seg=48)
            a.cyl('Poison liquid inside',(s*.65,2.21,-.95),.307,1.06,'Poison',root,seg=48)
            a.cyl('Visible liquid surface',(s*.65,2.748,-.95),.31,.02,'Poison',root,seg=48)
            for y in (1.64,3.12):a.cyl('Bottle metal collar',(s*.65,y,-.95),.385,.14,'Metal',root,seg=40)
            a.cyl('Bottle neck cap',(s*.65,3.27,-.95),.18,.19,'Edge',root,seg=24)
            for dx in (-.30,.30):a.beam('Bottle cage rail',(s*.65+dx,1.66,-1.10),(s*.65+dx,3.09,-1.10),.055,'Metal',root)
            curve('Flexible poison hose',[(s*.64,1.72,-.99),(s*1.12,1.59,-.56),(s*1.08,1.27,.14)],.09,'Rubber',root)
            prism('Toxic warning triangle',[(-.14,-.12),(.14,-.12),(0,.16)],.012,'Navy',root,p=(s*.65,2.43,-.608),bevel=.005)
            for j in range(5):a.ball('Liquid bubble',(s*.65+math.sin(j*2.1)*.16,1.88+j*.16,-.65),(.038,.04,.038),'Ivory',root)
        for sign in (-1,1):
            points=[]
            for j in range(31):
                y=.46+j*.97/30;x=sign*(.25+.11*j/30);point=frog_surface(x,y,.004)
                if point:points.append(point)
            curve('Surface fitted chin plate joint',points,.007,'Metal',root)
        points=[frog_surface(-.58+j*1.16/30,.74,.004) for j in range(31)]
        curve('Surface fitted lower chin seam',[p for p in points if p],.007,'Metal',root)
        for x,y in [(-.50,.78),(.50,.78),(-.62,1.25),(.62,1.25)]:
            point=frog_surface(x,y,.009)
            if point:a.ball('Small chin plate rivet',point,(.022,.022,.012),'Metal',root)
        a.cyl('Frog chemical nozzle',(0,0,.14),.20,.29,'Metal',barrel,'z',seg=24)
    elif kind=='MineLander':
        rig((0,1.93,-.70),(0,.12,0),1.23)
        tracks(2.60,2.50,.45,'Body')
        armor('Rounded mole armored cab',(0,1.35,-.12),(1.07,.69,1.01),'Body',root,lat=4,lon=10)
        a.sculpt('Mole muzzle',(0,1.31,.82),(.68,.29,.40),'Body',root,.55)
        for s in (-1,1):
            a.cyl('Large goggle housing',(s*.48,1.76,.80),.34,.32,'Metal',root,'z',seg=40)
            a.torus('Goggle metal rim',(s*.48,1.76,.98),.28,.055,'Edge',root)
            a.cyl('Black goggle lens',(s*.48,1.76,1.005),.245,.04,'Rubber',root,'z',seg=40)
            a.ball('Goggle glint',(s*.48-.07,1.84,1.034),(.06,.08,.015),'Glass',root)
            a.cyl('Cab side axle',(s*1.03,1.38,-.42),.25,.12,'Gold',root,'x',seg=24)
            a.beam('Hydraulic black sleeve',(s*1.11,1.22,.53),(s*1.19,.80,1.35),.17,'Metal',root)
            a.beam('Hydraulic chrome piston',(s*1.19,.80,1.35),(s*1.22,.54,1.81),.10,'Edge',root)
        a.box('Goggle bridge',(0,1.76,.95),(.27,.15,.10),'Metal',root,.025)
        prism('Mole nose',[(-.15,.10),(.15,.10),(.12,-.08),(-.12,-.08)],.18,'Metal',root,p=(0,1.40,1.12),bevel=.03)
        # Concave scoop section: lower lip comes forward, middle curves inward, upper lip back.
        scoop=[(1.47,1.40),(1.43,1.20),(1.48,.87),(1.64,.50),(1.94,.22),(2.13,.18),(2.12,.30),(1.77,.59),(1.62,.90),(1.59,1.20),(1.62,1.42)]
        prism('Concave steel digging scoop',scoop,2.58,'Metal',root,axis='x',bevel=.045)
        for s in (-1,1):prism('Scoop raised side wing',[(1.45,1.40),(1.95,.20),(2.18,.21),(1.63,1.44)],.12,'Edge',root,p=(s*1.27,0,0),axis='x',bevel=.025)
        for x in (-1.02,-.51,0,.51,1.02):
            mesh('Sculpted triangular digging tooth',[(x-.15,.22,1.91),(x+.15,.22,1.91),(x-.15,.15,2.36),(x+.15,.15,2.36),(x,.56,2.08)],[(0,2,3,1),(0,1,4),(1,3,4),(3,2,4),(2,0,4)],'Edge',root,smooth=False,bevel=.022)
        cannon(barrel,1.23,.40,'Metal')
    elif kind=='MultiMissile':
        rig((0,2.48,-.30),(0,.10,0),.88)
        armor('Low tiled turtle shell',(0,1.10,-.24),(1.15,.66,1.28),'Body',root,lat=4,lon=10)
        a.sculpt('Turtle belly',(0,.62,-.13),(.96,.26,1.12),'Ivory',root,.8)
        for s in (-1,1):
            for z in (-.92,.81):
                disk_wheel(s*1.11,.39,z,.38,.34,'Teal')
                leg=a.empty('Separate turtle wheel leg',(s*1.11,.39,z),root)
                prism('Turtle wheel boot',[(-.47,-.01),(-.41,.29),(-.23,.44),(.26,.44),(.42,.29),(.47,-.01),(.30,-.01),(.25,.23),(-.25,.23),(-.30,-.01)],.45,'Body',leg,axis='x',bevel=.06)
        neck=loft('Visible extended turtle neck',[(.82,.37,.30,1.0),(1.16,.35,.31,1.19),(1.49,.34,.32,1.40),(1.72,.37,.33,1.52)],'Body',root,axis='z')
        for z,y in ((1.15,1.18),(1.43,1.35)):
            a.torus('Neck segmented collar',(0,y,z),.32,.018,'Teal',root)
        a.ball('Turtle ivory throat',(0,1.39,1.90),(.62,.27,.64),'Ivory',root)
        a.ball('Turtle forward head',(0,1.70,1.96),(.65,.46,.64),'Body',root)
        for s in (-1,1):eye(root,(s*.43,1.92,2.27),.30,'Body',s*39)
        curve('Turtle friendly mouth',[(-.50,1.49,2.18),(0,1.47,2.58),(.50,1.49,2.18)],.024,'Navy',root)
        a.cyl('Dorsal launcher swivel',(0,1.80,-.35),.54,.18,'Metal',root,seg=32)
        a.box('Dorsal pitch bracket',(0,-.55,-.18),(.71,.37,.77),'Metal',barrel,.08)
        prism('Nine cell dorsal rocket pack',[(-1.06,-.76),(-1.15,.49),(-.90,.76),(.90,.76),(1.15,.49),(1.06,-.76)],1.87,'Teal',barrel,p=(0,0,-.10),bevel=.12)
        for x in (-.64,0,.64):
            for y in (-.48,0,.48):
                a.cyl('Deep dorsal launch bore',(x,y,.843),.217,.08,'Rubber',barrel,'z',seg=32)
                a.torus('Dorsal bore lip',(x,y,.89),.225,.035,'Body',barrel)
        for s in (-1,1):
            for z in (-.62,.10):prism('Launcher side panel',[(-.29,-.38),(.29,-.38),(.29,.36),(-.29,.36)],.035,'Body',barrel,p=(s*1.15,0,z),axis='x',bevel=.035)
    elif kind=='SuperTank':
        rig((0,1.35,0),(0,.16,.94),1.15)
        tracks(2.92,3.10,.52,'Gold')
        a.sculpt('Broad lion lower hull',(0,.91,-.05),(1.22,.43,1.36),'Gold',root,.48)
        prism('Thick sloped frontal armor',[(-.85,.44),(-.61,-.36),(.61,-.36),(.85,.44)],.32,'Gold',root,p=(0,.97,1.25),bevel=.07)
        a.cyl('Armored lion neck connection',(0,1.41,-.08),.58,.63,'Gold',root,seg=32)
        a.sculpt('Lion cranium',(0,1.02,.02),(.77,.77,.65),'Gold',turret,.8)
        # Many separately curved pointed locks; roots encircle the face and tips sweep backward/down.
        for s in (-1,1):
            for row in range(3):
                for j in range(6):
                    angle=.27+j*.31
                    start=(s*(.35+math.sin(angle)*.31),1.69-j*.175,.53-row*.23)
                    end=(s*(1.19-.067*j+row*.11),1.73-j*.28,.13-row*.33)
                    feather('Swept sculpted lion mane',start,end,.24+row*.03,.085,'Crimson',turret,bend=.15)
            for j in range(7):feather('Front pointed mane lock',(s*.49,1.64-j*.16,.67),(s*(1.14-.045*j),1.47-j*.205,.70),.20,.060,'Crimson',turret,bend=.055)
            feather('Lion forehead swept lock',(s*.07,1.58,.58),(s*.75,1.86,.15),.25,.09,'Crimson',turret,bend=.1)
            e=eye(turret,(s*.43,1.10,.63),.29,'Gold',s*18);e.scale.z=.70
            prism('Lion heavy angry brow',[(s*.11,.01),(s*.69,.18),(s*.69,.38),(s*.19,.25)],.19,'Gold',turret,p=(0,1.12,.87),bevel=.06)
            a.sculpt('Lion broad cream muzzle',(s*.25,.69,.92),(.31,.27,.31),'Ivory',turret,.73)
            for j in range(3):a.ball('Lion whisker dot',(s*(.19+j*.09),.70-j*.043,1.219),(.017,.02,.013),'Navy',turret)
            rocket=a.empty('Shoulder rocket assembly',(s*1.18,1.22,-.50),turret);rocket.rotation_euler[1]=math.radians(-s*12)
            a.box('Golden shoulder launcher',(0,0,0),(.82,.92,.83),'Gold',rocket,.08)
            a.box('Dark four rocket inset',(0,0,.424),(.68,.77,.025),'Rubber',rocket,.035)
            for x in (-.185,.185):
                for y in (-.22,.22):
                    a.cyl('Shoulder rocket red body',(x,y,.46),.137,.15,'Crimson',rocket,'z',seg=24)
                    a.cyl('Pointed red missile tip',(x,y,.63),.137,.21,'Rocket',rocket,'z',0,24)
        prism('Lion triangular nose',[(-.20,.10),(.20,.10),(.14,-.02),(0,-.14),(-.14,-.02)],.15,'Navy',turret,p=(0,.91,1.20),bevel=.028)
        a.sculpt('Ivory lion lower jaw',(0,.42,.87),(.39,.19,.28),'Ivory',turret,.78)
        curve('Lion split muzzle',[(0,.87,1.29),(0,.60,1.27),(-.28,.53,1.16)],.018,'Navy',turret)
        curve('Lion mouth right',[(0,.60,1.27),(.28,.53,1.16)],.018,'Navy',turret)
        a.cyl('Round crown circlet',(0,1.85,-.04),.43,.17,'Gold',turret,seg=40)
        for j in range(5):
            q=j*math.tau/5;cg=a.empty('Crown pointed panel',(math.sin(q)*.36,1.82,-.04+math.cos(q)*.36),turret);cg.rotation_euler[2]=q
            prism('Royal crown point',[(-.18,0),(.18,0),(.16,.12),(0,.31),(-.16,.12)],.07,'Gold',cg,bevel=.023)
            a.ball('Crown ruby',(0,.14,.055),(.065,.084,.035),'Crimson',cg)
        cannon(barrel,1.15,.34,'Gold')
    elif kind=='Laser':
        from laser_front_form import build as build_laser_front
        build_laser_front(a,root,turret,barrel,fire,mesh)
    elif kind=='IonAttacker':
        rig((0,1.89,0),(0,0,.80),.65)
        armor('Pink orbital sphere',(0,1.82,0),(1.14,1.17,1.02),'Body',root,lat=5,lon=12)
        a.torus('Thick pink central eye bezel',(0,1.86,.83),.87,.13,'Body',root)
        a.ball('Deep cyclops blue lens',(0,1.86,.96),(.81,.86,.33),'Glass',root)
        a.ball('Great cyan cyclops iris',(0,1.86,1.19),(.68,.75,.13),'Energy',root)
        a.ball('Long dark vertical pupil',(0,1.87,1.325),(.15,.49,.045),'Rubber',root)
        a.ball('Central lens highlight',(-.25,2.22,1.33),(.12,.14,.025),'EyeWhite',root)
        ring=a.torus('Gold orbital band',(0,1.06,0),1.53,.105,'Gold',root,'y');ring.scale.z=1.33
        for s in (-1,1):
            for yy,zz in ((.97,.55),(2.94,-.12)):
                x=s*1.39
                a.ball('Pink satellite eye shell',(x,yy,zz),(.36,.37,.34),'Body',root)
                a.torus('Pink satellite front bezel',(x,yy,zz+.28),.25,.065,'Body',root)
                a.ball('Satellite dark lens',(x,yy,zz+.30),(.24,.25,.10),'Glass',root)
                a.ball('Satellite cyan iris',(x,yy,zz+.385),(.14,.17,.03),'Energy',root)
                for side in (-1,1):a.cyl('Gold pod side pivot',(x+side*.35,yy,zz),.17,.10,'Gold',root,'x',seg=24)
                a.beam('Gold angled pod bracket',(s*.91,1.65 if yy<2 else 2.53,0),(x,yy,zz),.12,'Gold',root)
        a.cyl('Top cap stem',(0,3.00,-.04),.17,.20,'Gold',root,seg=24)
        a.ball('Pink top cap',(0,3.11,-.04),(.35,.10,.30),'Body',root)
        a.cyl('Lower maroon keel',(0,.56,0),.12,.31,'Plum',root,r2=.36,seg=24)
        a.cyl('Keel dark tip',(0,.37,0),.045,.11,'Navy',root,r2=.13,seg=16)
    elif kind=='Poseidon':
        rig((0,2.47,.02),(0,0,.44),1.51)
        sections=[(-2.20,.12,.17,2.0),(-1.72,.35,.40,1.59),(-1.08,.87,.76,1.36),(-.40,1.18,.98,1.36),(.40,1.22,1.03,1.40),(1.02,1.02,.95,1.39),(1.51,.58,.68,1.40),(1.66,.03,.20,1.42)]
        whale=loft('Continuous whale body and belly',sections,'Body',root,axis='z',sides=48)
        whale.data.materials.append(a.MATS['Ivory'])
        for f in whale.data.polygons:
            center=f.center
            if center.z<1.44 and -center.y>-.9:f.material_index=1
        bpy.context.view_layer.update();whale_bvh=BVHTree.FromObject(whale,bpy.context.evaluated_depsgraph_get())
        positions=[(v.co.x,v.co.z,-v.co.y) for v in whale.data.vertices]
        for name,material,idx in [('Blue whale upper shell','Body',0),('White whale ventral body','Ivory',1)]:
            mesh(name,positions,[tuple(f.vertices) for f in whale.data.polygons if f.material_index==idx],material,root)
        bpy.data.objects.remove(whale,do_unlink=True)
        # Low running pods disappear under the great rounded whale belly.
        for s in (-1,1):
            for z in (-.67,-.15,.37):disk_wheel(s*1.02,.30,z,.25,.25,'Body')
            fender(s*1.02,.30,1.48,.31,'Body',.47)
            eye(root,(s*.72,1.82,1.26),.32,'Body',s*42)
            feather('Whale side flipper',(s*.84,1.23,-.38),(s*1.52,.85,.23),.30,.08,'Body',root,normal=(0,1,0),bend=.08)
            feather('Horizontal whale tail fluke',(0,2.02,-2.04),(s*.92,2.19,-2.26),.41,.09,'Body',root,normal=(0,1,0),bend=.1)
        prism('Swept whale dorsal fin',[(-1.05,0),(.30,0),(-.05,.72),(-.55,1.10),(-.91,1.12)],.17,'Body',root,p=(0,2.05,-.09),axis='x',bevel=.055)
        lip=[]
        for j in range(25):
            x=-1+2*j/24;hit,normal,_,_=whale_bvh.ray_cast(a.v((x,1.447,5)),a.v((0,0,-1)))
            if hit is not None:
                q=hit+normal*.012;lip.append((q.x,q.z,-q.y))
        curve('Whale smiling lip',lip,.013,'Navy',root)
        for x in (-.70,-.46,-.23,0,.23,.46,.70):
            groove=[]
            for j in range(25):
                yy=1.43-j*.041;hit,normal,_,_=whale_bvh.ray_cast(a.v((x,yy,5)),a.v((0,0,-1)))
                if hit is not None:
                    q=hit+normal*.006;groove.append((q.x,q.z,-q.y))
            if len(groove)>2:curve('Surface fitted ventral groove',groove,.009,'Stone',root)
        a.cyl('Blue trident shaft',(0,0,.40),.17,.94,'Body',barrel,'z',seg=32)
        for z in (.10,.68):a.torus('Gold trident shaft band',(0,0,z),.185,.05,'Gold',barrel)
        prism('Trident center spear',[(-.095,.59),(.095,.59),(.095,1.26),(.20,1.26),(0,1.54),(-.20,1.26),(-.095,1.26)],.15,'Gold',barrel,axis='y',bevel=.025)
        for s in (-1,1):
            prism('Hooked trident outer tine',[(0,.72),(s*.44,.73),(s*.67,1.19),(s*.80,1.42),(s*.53,1.30),(s*.42,1.37),(s*.46,1.12),(s*.32,.92),(0,.92)],.15,'Gold',barrel,axis='y',bevel=.025)
    elif kind=='SecWind':
        rig((0,1.60,0),(0,.12,1.0),.65)
        a.sculpt('Green bird rounded head',(0,1.70,.16),(.95,1.02,.95),'Body',root,.9)
        tracks(1.84,1.72,.29,'Body')
        for s in (-1,1):
            eye(root,(s*.62,1.93,.84),.40,'Body',s*30)
            for j in range(7):
                feather('Swept green primary wing',(s*.78,1.45+j*.10,-.50),(s*(2.05+j*.18),1.42+j*.30,-1.16-j*.24),.39,.065,'Body',root,bend=.12)
            for j in range(5):
                feather('Ivory overlapping secondary',(s*.83,1.74+j*.09,-.24),(s*(1.68+j*.16),1.84+j*.29,-.68-j*.22),.29,.065,'Ivory',root,bend=.09)
            a.cyl('High wing-root green turbine',(s*1.16,2.43,-.10),.51,.58,'Body',root,'z',seg=48)
            a.torus('Ivory turbine front ring',(s*1.16,2.43,.19),.455,.075,'Ivory',root)
            a.cyl('Turbine dark well',(s*1.16,2.43,.20),.40,.03,'Rubber',root,'z',seg=48)
            for j in range(8):
                q=math.tau*j/8
                pts=[]
                for r,d in ((.09,0),(.40,.05),(.40,.28),(.12,.49)):
                    t=q+d;pts.append((s*1.16+math.sin(t)*r,2.43+math.cos(t)*r))
                prism('Pitched radial turbine blade',pts,.043,'Edge',root,p=(0,0,.228),bevel=.012)
            a.ball('Turbine spinner',(s*1.16,2.43,.275),(.125,.125,.11),'Metal',root)
        loft('Great hooked ivory eagle beak',[(.85,.36,.33,1.85),(1.14,.40,.43,1.58),(1.40,.29,.42,1.20),(1.60,.12,.29,.78),(1.69,.01,.02,.40)],'Ivory',root,axis='z',sides=24)
        for s in (-1,0,1):feather('Swept bird crest',(s*.15,2.38,-.28),(s*.32,3.20,-1.05),.28,.055,'LeafGreen',root,bend=.08)
    else:
        return None
    return root
