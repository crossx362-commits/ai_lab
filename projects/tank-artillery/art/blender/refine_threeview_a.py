"""Three-axis volume corrections for the four organic roster vehicles.
Original FRONT/SIDE/TOP inspected together; coordinates are Blender world XYZ.
No camera fitting, image planes masquerading as geometry, or rig-anchor edits.
"""
import math
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
KINDS=('Catapult','CrossBow','Cannon','Carrot')

def apply(kind,root,api):
    if kind not in KINDS:return root
    bpy.context.view_layer.update()
    objects=list(root.children_recursive)
    def named(s):return [o for o in objects if o.name.split('.')[0]==s]
    def deform(obj,fn):
        world=obj.matrix_world.copy(); inv=world.inverted()
        for v in obj.data.vertices:v.co=inv@Vector(fn(world@v.co))
        obj.data.update()
    def replace(obj,vertices,faces):
        inv=obj.matrix_world.inverted();data=bpy.data.meshes.new(obj.name+' three-axis solid')
        data.from_pydata([inv@Vector(p) for p in vertices],[],faces)
        for mat in obj.data.materials:data.materials.append(mat)
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        obj.data=data
        for p in data.polygons:p.use_smooth=True
    def leaf(obj,start,end,width,normal=(0,0,1),fold=.12):
        # Closed loft: broad asymmetric source leaf, tapered root and sharp tip;
        # separate raised midrib and curved edge give real SIDE thickness.
        a=Vector(start);b=Vector(end);axis=b-a;n=Vector(normal).normalized();across=axis.cross(n).normalized()
        verts=[];faces=[];rows=20;cols=8
        for layer in (-1,1):
            for i in range(rows+1):
                t=i/rows;center=a+axis*t
                w=width*math.sin(math.pi*t)**.8*(.80+.30*t)
                for j in range(cols+1):
                    u=2*j/cols-1
                    h=fold*math.sin(math.pi*t)*(1-abs(u)) + layer*.032*math.sin(math.pi*t)**.5
                    verts.append(center+across*w*u+n*h)
        count=(rows+1)*(cols+1)
        for layer in range(2):
            for i in range(rows):
                for j in range(cols):
                    q=layer*count+i*(cols+1)+j;faces.append((q,q+1,q+cols+2,q+cols+1))
        border=list(range(cols+1))+[i*(cols+1)+cols for i in range(1,rows+1)]+[rows*(cols+1)+j for j in range(cols-1,-1,-1)]+[i*(cols+1) for i in range(rows-1,0,-1)]
        for i,q in enumerate(border):r=border[(i+1)%len(border)];faces.append((q,r,r+count,q+count))
        replace(obj,verts,faces)
    if kind=='Catapult':
        api['sculpt']('Closed coopering backing',(0,1.10,0),(.89,.66,1.045),'WoodLight',root,.48)
        # SIDE body becomes the source's convex coopering rather than a box with
        # disconnected front end. Same field includes end boards and iron straps.
        for o in objects:
            if o.type=='MESH' and o.name.startswith(('Longitudinal oak stave','Oak end face','Flat forged barrel strap','End plank joint','Strap rivet')):
                def barrel(p):
                    x,y,z=p; y*=1-.065*max(0,1-((z-1.1)/.75)**2)
                    z+=.075*max(0,1-(y/1.1)**2)*max(0,(z-1.1)/.75)
                    return x,y,z
                deform(o,barrel)
        for o in named('Faceted loaded boulder'):
            # FRONT polygon is taller and has a narrower apex than the baseline dome.
            def stone(p):
                x,y,z=p;t=max(0,min(1,(z-2.86)/.81));return x*(1-.12*t*t),1.30+(y-1.30)*(1-.06*t),2.86+(z-2.86)*1.20
            deform(o,stone)
        for eye in named('Eye'):
            eye.scale.x*=1.18;eye.location.y+=.055
        # Red wheels have barrel-shaped timber tread shoulders in all three
        # drawings. A straight cylinder produced rectangular FRONT silhouettes.
        for o in named('Red timber wheel'):
            pp=[v.co for v in o.data.vertices];lo=[min(p[j] for p in pp) for j in range(3)];hi=[max(p[j] for p in pp) for j in range(3)]
            cx,cy=(lo[0]+hi[0])/2,(lo[1]+hi[1])/2;rx,ry=(hi[0]-lo[0])/2,(hi[1]-lo[1])/2
            vertices=[];faces=[];n=64;rows=12
            for i in range(rows+1):
                t=i/rows;ax=lo[2]+(hi[2]-lo[2])*t;shoulder=1-.10*abs(2*t-1)**3
                for j in range(n):
                    a=j*math.tau/n;vertices.append(o.matrix_world@Vector((cx+rx*shoulder*math.cos(a),cy+ry*shoulder*math.sin(a),ax)))
            for i in range(rows):
                for j in range(n):q=i*n+j;r=i*n+(j+1)%n;faces.append((q,r,r+n,q+n))
            faces.extend([tuple(range(n-1,-1,-1)),tuple(rows*n+j for j in range(n))]);replace(o,vertices,faces)
            for polygon in o.data.polygons:
                if len(polygon.vertices)>4:polygon.use_smooth=False

    elif kind=='CrossBow':
        # TOP reference central spear leaf extends almost the whole roof. Drape
        # it over the curved body instead of the previous low broad dome.
        for o in named('Owl dorsal central leaf'):
            leaf(o,(0,-1.37,.78),(0,.68,1.53),.43,(0,-.30,1),.17)
            # TOP roof occluded the straight loft; sample the body cross-section
            # envelope so the spear actually lies above, and follows, that roof.
            def roof(p):
                q=max(0,1-(abs(p.x)/.85)**(2/.85)-(abs((p.y-.08)/1.20))**(2/.85))
                return p.x,p.y,.925+.565*q**(.85/2)+.04+.05*max(0,1-abs(p.x)/.43)+.30*(p.z-(.78+(p.y+1.37)*.75/2.05))
            deform(o,roof)
        # Three overlapping side tiers. Source tips point rearward/downward, with
        # broad blades visible from both TOP and SIDE; no coplanar feather fans.
        leaves=named('Overlapping owl leaf')
        for i,o in enumerate(leaves):
            side=-1 if i<9 else 1;j=i%9;row=j//3;col=j%3
            start=(side*(.20+.19*col),-.74+.30*col,1.47-.16*row)
            end=(side*(1.24-.10*col),.88+.10*col-.27*row,1.13-.22*row)
            leaf(o,start,end,.28,(side*.65,0,.76),.095)
        # Remove obsolete old mesh midribs; new folds are modeled in the blades.
        for o in named('Leaf midrib'):
            objects.remove(o);bpy.data.objects.remove(o,do_unlink=True)
        # SIDE original stem is upright directly under clamp, not a rear leaning beam.
        clamp=named('Central steel bow clamp')[0]
        cp=[clamp.matrix_world@v.co for v in clamp.data.vertices];cy=sum(p.y for p in cp)/len(cp);high=min(p.z for p in cp)+.08
        for o in named('Owl bow connected mounting stem'):
            pp=[o.matrix_world@v.co for v in o.data.vertices];low=min(p.z for p in pp);hi=max(p.z for p in pp);ymin=min(p.y for p in pp);ymax=max(p.y for p in pp)
            deform(o,lambda p:(p.x,cy+((p.y-ymin)/(ymax-ymin)-.5)*.24,1.20+(p.z-low)/(hi-low)*(high-1.20)))
        # TOP recurves must follow the same S path as FRONT, not a straight strip.
        for o in named('Flat laminated recurved oak limb')+named('Bow longitudinal lamination'):
            deform(o,lambda p:(p.x,p.y+.18*math.sin(min(1,abs(p.x)/1.55)*math.pi),p.z))
        # SIDE source clamp at original x834 corresponds to world Y=.423 in
        # the immutable SIDE registration. The previous assembly centered at .970
        # and string apex 1.758 pushed the whole bow outside the body silhouette.
        # Translate every actual bow part together; rig control empties stay fixed.
        bow_parts=('Bow support','Short crossbow stock','Flat laminated recurved oak limb',
                   'Bow longitudinal lamination','Bow tip metal cap','Taut bow string',
                   'Central steel bow clamp','Raised clamp strap','Clamp round rivet')
        for label in bow_parts:
            for o in named(label):
                if label=='Taut bow string':
                    deform(o,lambda p:(p.x,.970+(p.y-.970)*.29-.547,p.z))
                else:deform(o,lambda p:(p.x,p.y-.547,p.z))
        for o in named('Owl bow connected mounting stem'):
            vertices=[(x,y,z) for z in (1.22,2.08) for y in (.313,.533) for x in (-.115,.115)]
            replace(o,vertices,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)])
    elif kind=='Cannon':
        # Build actual annular solids. The former closed cylinders plus a black
        # disc at the rim could never show a bore wall at any camera angle.
        barrel_control=named('Barrel')[0];bw=barrel_control.matrix_world.copy();bi=bw.inverted()
        for label in ('Flared brass barrel','Brass rim','Dark gun neck'):
            for o in named(label):
                pp=[bi@(o.matrix_world@v.co) for v in o.data.vertices];lo=[min(p[j] for p in pp) for j in range(3)];hi=[max(p[j] for p in pp) for j in range(3)]
                cx,cz=(lo[0]+hi[0])/2,(lo[2]+hi[2])/2;rx,rz=(hi[0]-lo[0])/2,(hi[2]-lo[2])/2
                # FRONT aperture stays at the existing black-mouth radius; the
                # inner throat tapers farther back, as visible in the source.
                inner_front=.745 if label=='Brass rim' else .78
                inner_back=.745 if label=='Brass rim' else .69
                vertices=[];faces=[];n=96
                profiles=[(lo[1],1.0),(hi[1],.85 if label=='Flared brass barrel' else 1.0),
                          (hi[1],inner_back),(lo[1],inner_front)]
                for y,r in profiles:
                    for j in range(n):
                        a=j*math.tau/n;vertices.append(bw@Vector((cx+rx*r*math.cos(a),y,cz+rz*r*math.sin(a))))
                for row in range(4):
                    for j in range(n):faces.append((row*n+j,row*n+(j+1)%n,((row+1)%4)*n+(j+1)%n,((row+1)%4)*n+j))
                replace(o,vertices,faces)
                for polygon in o.data.polygons:
                    if polygon.index//n in (1,3):polygon.use_smooth=False
                # Exporter assigns one material per object. Use a separate closed
                # thin sleeve, not a per-polygon material that Unity would discard.
                sleeve=o.copy();sleeve.data=o.data.copy();bpy.context.collection.objects.link(sleeve)
                sleeve.name='Recessed metal bore sleeve';bpy.context.view_layer.update()
                sleeve_vertices=[]
                for y,r in [(hi[1],inner_back-.004),(lo[1],inner_front-.004),
                            (lo[1],inner_front-.008),(hi[1],inner_back-.008)]:
                    for j in range(n):
                        a=j*math.tau/n;sleeve_vertices.append(bw@Vector((cx+rx*r*math.cos(a),y,cz+rz*r*math.sin(a))))
                replace(sleeve,sleeve_vertices,faces)
                sleeve.data.materials.clear();sleeve.data.materials.append(api['MATS']['Metal'])
        for o in named('Deep cannon mouth'):
            o.location.y+=.60
            # Recessed dark terminus fits the inner taper and is never at the lip.
            for v in o.data.vertices:v.co.x*=.80;v.co.y=-.140+(v.co.y+.140)*.80
        # The seam foundation protruded above the armor and created a second dome.
        shell=named('Great spherical navy cannon')[0]
        for o in named('Great spherical navy cannon seam foundation'):
            deform(o,lambda p:(p.x*.99,p.y*.99,.65+(p.z-.62)*(2.83-.65)/(2.98-.62)))
        # SIDE tilted hatch shares actual hinge and crown, TOP stays circular.
        for o in named('Round hinged captain lid'):
            o.rotation_euler.x=math.radians(-20);o.location.y+=.14;o.location.z-=.12
        for o in named('Captain hatch seat'):
            objects.remove(o);bpy.data.objects.remove(o,do_unlink=True)
        for eye in named('Eye'):
            eye.scale.x*=1.12;eye.location.y-=.10
        # Round lower cheek to remove the sharp shelf above the carriage.
        for o in named('Great spherical navy cannon')+named('Great spherical navy cannon seam foundation'):
            deform(o,lambda p:(p.x,p.y*(1-.075*max(0,(1.35-p.z)/.7)),p.z))
    elif kind=='Carrot':
        # Original FRONT tracks are about .31 body-width each. Previous FRONT
        # per-object fitting expanded pads inward into .5-body-width flat slabs.
        # Correct the entire physical running gear together, retaining SIDE path.
        gear=('Inner track belt','Flat track link','Curved track link','Wrapped curved fender',
              'Front gold track cheek','Front track cheek fastener','Wheel tread','Solid wheel face',
              'Thin iron rim','Wheel hub','Radial board joint')
        for label in gear:
            for o in named(label):
                if o.type=='MESH':
                    def track_shoulder(p):
                        x=1.50+(abs(p.x)-1.50)*.70
                        bottom=max(0,min(1,(.27-p.z)/.24))
                        x=1.16+(x-1.16)*(1-.20*bottom*bottom)
                        return math.copysign(x,p.x),p.y,p.z
                    deform(o,track_shoulder)
        # Front fender shoulders roll down toward outer edges instead of a single
        # broad flat rectangular brow across each belt.
        for o in named('Front gold track cheek'):
            pts=[o.matrix_world@v.co for v in o.data.vertices];cx=sum(p.x for p in pts)/len(pts);half=max(abs(p.x-cx) for p in pts)
            deform(o,lambda p:(p.x,p.y,p.z-.055*(abs(p.x-cx)/max(.001,half))**4))
        # Source TOP is an egg tapering toward the muzzle, SIDE broad upper root.
        # Local cross-section field rounds the abrupt lower shoulder while keeping
        # all existing armor grooves conformal to its real surface.
        for o in named('Smooth tapered carrot armor')+named('Inset carrot armor joint'):
            def carrot(p):
                x,y,z=p;t=max(0,min(1,(z-.25)/1.97));f=1-.20*(1-t)**2
                return x*f,(y-.10)*(1-.14*(1-t)**2)+.10,z+.10*math.sin(math.pi*t)
            deform(o,carrot)
        # The SIDE source fan shows three broad blades, the TOP fan five. Model
        # three-dimensional lenticular blades with distinct backward lean.
        leaves=named('Broad folded carrot crown leaf')
        specs=[((0,.52,2.08),(0,1.28,3.53),.33,(0,-.75,.66)),
               ((.12,.55,2.08),(.96,1.23,3.20),.32,(.10,-.8,.60)),
               ((-.12,.55,2.08),(-.96,1.23,3.20),.32,(-.10,-.8,.60)),
               ((.22,.61,2.07),(1.20,1.06,2.72),.29,(.2,-.75,.65)),
               ((-.22,.61,2.07),(-1.20,1.06,2.72),.29,(-.2,-.75,.65)),
               ((0,.68,2.08),(.04,1.61,2.93),.39,(1,0,0))]
        for o,s in zip(leaves,specs):leaf(o,*s,fold=.10)
        # Preview applies -10 degrees AFTER this hook. Convert only unambiguous
        # art-pose corrections back into rest-world coordinates explicitly.
        control=named('Barrel')[0];rest=control.matrix_world.copy()
        pose_basis=Matrix.LocRotScale(control.location,Euler((-math.radians(10),control.rotation_euler.y,control.rotation_euler.z)).to_quaternion(),control.scale)
        posed=control.parent.matrix_world@pose_basis;to_art=posed@rest.inverted();to_rest=to_art.inverted()
        # FRONT central tip (288,160) -> z3.300556; SIDE (700,185)
        # -> z3.310210. Agreement is within two source pixels. Preserve all depth
        # coordinates because TOP/SIDE tip depth differs by 1.634 world units.
        central=leaves[0];pp=[to_art@(central.matrix_world@v.co) for v in central.data.vertices];low=min(p.z for p in pp);high=max(p.z for p in pp)
        target=1.6907099485397339+(424.5-160)*.006086375109702077
        def common_tip(p):
            q=to_art@p;q.z-=max(0,min(1,(q.z-low)/(high-low)))*(high-target);return to_rest@q
        deform(central,common_tip)
        # Paired brows use the same FRONT silhouette. Mirror the left physical
        # solid in art-pose space; this removes the right lower-edge offset from
        # its yawed eye parent without changing eyebrow depth or control pivots.
        brows=named('Orange root eyebrow')
        if len(brows)==2:
            left=min(brows,key=lambda o:(o.matrix_world.translation.x));right=max(brows,key=lambda o:(o.matrix_world.translation.x))
            # Their origins sit at the eye, so source side is determined by mesh.
            if sum((left.matrix_world@v.co).x for v in left.data.vertices)>0:left,right=right,left
            verts=[]
            for v in left.data.vertices:
                q=to_art@(left.matrix_world@v.co);q.x=-q.x;verts.append(to_rest@q)
            replace(right,verts,[tuple(reversed(p.vertices)) for p in left.data.polygons])
        # Real small top access plate from TOP source instead of plain body seam.
        host=named('Smooth tapered carrot armor')[0]
        # API takes game XYZ. Parent under same pitch rig; keep world placement.
        plate=api['box']('Carrot rounded crown access hatch',(0,0,0),(.60,.055,.42),'Body',host.parent,.05)
        bpy.context.view_layer.update();plate.matrix_world.translation=Vector((0,.17,2.21))
        for sx in (-1,1):
            for sy in (-1,1):
                bolt=api['cyl']('Carrot crown hatch bolt',(0,0,0),.025,.025,'Metal',host.parent,seg=12)
                bpy.context.view_layer.update();bolt.matrix_world.translation=Vector((sx*.23,.17+sy*.145,2.25))
    root['threeViewGeometry']='a-v1: closed leaf lofts and cross-section corrections from original three views'
    bpy.context.view_layer.update()
    return root
