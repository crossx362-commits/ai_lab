"""Actual three-axis contour/section modeling for the C roster, after legacy FRONT fitting.
Original FRONT/SIDE/TOP source pixels are the authority; no reference-image facade.
"""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
REG=json.loads(Path(__file__).with_name('threeview-registration.json').read_text())
def apply(kind,root,api):
 if kind not in ('SuperTank','IonAttacker','Poseidon','SecWind') or root.get('threeview_c'):return root
 bpy.context.view_layer.update();turret=next(o for o in root.children_recursive if o.name.split('.')[0]=='Turret');barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel')
 controls=[(o,o.matrix_basis.copy()) for o in root.children_recursive if o.name.split('.')[0] in ('Turret','Barrel','FirePoint')]
 def objs(*names):return [o for o in root.children_recursive if any(o.name.startswith(n) for n in names)]
 def delete(*names):
  for o in reversed(objs(*names)):
   if o.name in bpy.data.objects:bpy.data.objects.remove(o,do_unlink=True)
 def mesh(name,verts,faces,mat,parent=turret,smooth=True):
  inv=parent.matrix_world.inverted();data=bpy.data.meshes.new(name);data.from_pydata([inv@Vector(p) for p in verts],[],faces);data.update();bm=bmesh.new();bm.from_mesh(data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
  data.materials.append(api['MATS'][mat]);o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.parent=parent
  for f in data.polygons:f.use_smooth=smooth
  return o
 def bounds(group):
  pts=[o.matrix_world@v.co for o in group if o.type=='MESH' for v in o.data.vertices];return Vector([min(p[j] for p in pts) for j in range(3)]),Vector([max(p[j] for p in pts) for j in range(3)])
 def ellipsoid(name,c,r,mat,parent=turret):
  vs=[];fs=[];n=48;m=25
  for j in range(m):
   t=math.pi*j/(m-1)
   for i in range(n):
    q=math.tau*i/n;vs.append((c[0]+r[0]*math.sin(t)*math.cos(q),c[1]+r[1]*math.sin(t)*math.sin(q),c[2]+r[2]*math.cos(t)))
  for j in range(m-1):
   for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  return mesh(name,vs,fs,mat,parent)
 def leaf(name,start,tip,width,thickness,mat,parent=turret,normal=(0,-1,1),bend=.10):
  a=Vector(start);d=Vector(tip)-a;n=Vector(normal).normalized();s=d.cross(n).normalized();n=s.cross(d).normalized();vs=[];fs=[];rows=20;ring=12
  for j in range(rows):
   t=j/(rows-1);shape=max(.002,math.sin(math.pi*t)**.72);c=a+d*t+n*bend*math.sin(math.pi*t)
   for i in range(ring):
    q=math.tau*i/ring;vs.append(c+s*(width*shape*math.cos(q))+n*(thickness*shape*math.sin(q)))
  for j in range(rows-1):
   for i in range(ring):fs.append((j*ring+i,j*ring+(i+1)%ring,(j+1)*ring+(i+1)%ring,(j+1)*ring+i))
  fs.extend([tuple(range(ring-1,-1,-1)),tuple((rows-1)*ring+i for i in range(ring))]);return mesh(name,vs,fs,mat,parent)
 def front(px,py):
  r=REG[kind]['front'];x,y,w,h=r['reference_crop'];u=r['units_per_reference_pixel'];return ((px-x-w/2)*u,r['center'][2]+(y+h/2-py)*u)
 def side(px,py):
  r=REG[kind]['side'];x,y,w,h=r['reference_crop'];u=r['units_per_reference_pixel'];return (r['center'][1]-(px-x-w/2)*u,r['center'][2]+(y+h/2-py)*u)
 def modify_depth(group,fn):
  for o in group:
   if o.type!='MESH':continue
   m=o.matrix_world.copy();inv=m.inverted()
   for v in o.data.vertices:
    p=m@v.co;p.y=fn(p);v.co=inv@p
   o.data.update()
 def carved_contour(name,xz,depth,mat,bulge=.10,sweep=False):
  # Interpolate the traced contour, then loft concentric sections into a closed,
  # convex lock. Unlike a flat extrusion this casts its own curved surface shade.
  points=[Vector(p) for p in xz];curve=[]
  for i,b in enumerate(points):
   a=points[(i-1)%len(points)];c=points[(i+1)%len(points)];d=points[(i+2)%len(points)]
   for j in range(6):
    t=j/6;curve.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
  center=sum(curve,Vector((0,0)))/len(curve);n=len(curve);vs=[];fs=[]
  for scale,offset in [(1,0),(.79,-bulge*.68),(.43,-bulge),(.015,-bulge*1.10),(.015,.14),(1,.14)]:
   for p in curve:
    q=center+(p-center)*scale
    y=depth+offset
    if sweep:y+=.85*(1-max(0,min(1,(abs(q.x)-.35)/.8)))+.14*(q.y-zc)
    vs.append((q.x,y,q.y))
  for row in range(6):
   for i in range(n):fs.append((row*n+i,row*n+(i+1)%n,((row+1)%6)*n+(i+1)%n,((row+1)%6)*n+i))
  return mesh(name,vs,fs,mat)
 if kind=='SuperTank':
  from super_barrel_refinement import apply as open_bore
  open_bore(root,api)
  # FRONT traces the circular mane and double muzzle; SIDE supplies its forward
  # cheek projection and a continuous mane volume. TOP supplies the posterior width.
  delete('Layered descending crimson mane','Front framing mane','Central swept forehead lock','Lion cranium','Lion broad cream muzzle','Lion split muzzle','Ivory lion lower jaw','Lion triangular nose','Lion whisker dot','Lion mouth right')
  zc=front(330,351)[1];fy=side(1090,350)[0];back=side(911,350)[0]
  ellipsoid('Threeview crimson mane core',(0,(fy+back)/2,zc),(.74,(fy-back)*-.5,.85),'Crimson')
  # Literal FRONT mane-lock contours. Each traced lock has a closed thickness,
  # beveled edge and the SIDE-derived depth; these are not billboard polygons.
  locks=[[(213,229),(255,220),(283,253),(268,276),(225,285),(195,307),(206,266)],
   [(268,282),(229,278),(187,294),(170,323),(194,314),(218,311),(246,307)],
   [(249,306),(217,308),(185,331),(166,369),(186,356),(211,346),(240,334)],
   [(245,335),(209,338),(180,370),(164,409),(190,390),(218,376),(247,355)],
   [(255,360),(225,374),(199,407),(198,451),(216,429),(236,407),(268,386)],
   [(266,389),(238,406),(222,443),(237,479),(252,458),(264,434),(287,417)],
   [(279,417),(263,443),(270,477),(288,499),(292,471),(300,441)]]
  for row in range(3):
   for sign in (-1,1):
    for j,outline in enumerate(locks):
     xz=[front(x,y) for x,y in outline];depth=fy-.12+row*.27
     # Successive locks recede around the face toward the shoulder in SIDE.
     depth+=.055*math.sin(j*math.pi/6)
     carved_contour('Layered descending crimson mane traced',[(sign*abs(x)*(1-.075*row),zc+(z-zc)*(1-.08*row)) for x,z in xz],depth,'Crimson',.11,True)
  for sign in (-1,1):
   xz=[front(x,y) for x,y in [(296,231),(320,234),(330,243),(330,290),(308,272),(300,254)]]
   carved_contour('Central swept forehead lock traced',[(sign*abs(x),z) for x,z in xz],fy-.43,'Crimson',.06)
  ellipsoid('Threeview lion face',(0,fy-.06,zc+.06),(.58,.27,.59),'Gold')
  for s in (-1,1):ellipsoid('Lion broad cream muzzle',(s*.205,fy-.30,zc-.14),(.29,.19,.25),'Ivory')
  ellipsoid('Ivory lion lower jaw',(0,fy-.22,zc-.39),(.33,.18,.22),'Ivory')
  ellipsoid('Lion triangular nose',(0,fy-.49,zc+.045),(.145,.052,.085),'Rubber')
  # Front eyes remain at traced FRONT coordinates, but now sit on the actual face.
  modify_depth(objs('Eye'),lambda p:p.y) # eye empties handled via descendants below
  eyes=[o for o in root.children_recursive if o.name.split('.')[0]=='Eye']
  for eye in eyes:
   group=[o for o in eye.children_recursive if o.type=='MESH'];lo,hi=bounds(group);delta=(fy-.31)-(lo.y+hi.y)/2;modify_depth(group,lambda p,d=delta:p.y+d)
  delete('Lion heavy angry brow')
  for sign in (-1,1):
   points=[(233,289),(257,272),(314,293),(306,318),(243,303)];xz=[front(x,y) for x,y in points];verts=[(sign*abs(x),fy-.40+d,z) for d in (-.035,.095) for x,z in xz];n=len(xz);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
   o=mesh('Lion heavy angry brow',verts,faces,'Gold',smooth=False);bevel=o.modifiers.new('Brow small bevel','BEVEL');bevel.width=.035;bevel.segments=3
   bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=bevel.name)
  # Fill the muzzle seam with a real thin dark groove, no image mouth.
  for s in (-1,1):leaf('Lion mouth groove',(0,fy-.489,zc-.015),(s*.22,fy-.469,zc-.22),.018,.012,'Rubber',normal=(0,-1,0),bend=0)
  # Closed crown band is visible from TOP; existing points remain FRONT registered.
  crown=objs('Royal crown rounded band');lo,hi=bounds(crown);c=(lo+hi)*.5
  # Current crown was a front-only shallow wall. Thicken its rear into a real circlet.
  modify_depth(objs('Upright five point crown','Crown side peak','Royal crown rounded band'),lambda p:c.y+(p.y-c.y)*1.25)
  # The legacy front-fit put the gems behind the thickened crown panel. Seat
  # the original gem meshes on its front surface, preserving their traced X/Z.
  panel_lo,panel_hi=bounds(objs('Upright five point crown'))
  for ruby in objs('Crown inset ruby'):
   lo,hi=bounds([ruby]);delta=panel_lo.y-.015-hi.y
   modify_depth([ruby],lambda p,d=delta:p.y+d)
 elif kind=='IonAttacker':
  # Remove protruding stacked discs: rebuild nested curved optical shells and a
  # rounded three-step keel from the SIDE outline, keeping existing gaze controls.
  eye=objs('Great cyan cyclops iris')[0];lo,hi=bounds([eye]);center=(lo+hi)*.5;front_y=-1.05;zc=front(310,438)[1]
  for names,cy,rx,ry,rz in [(('Thick pink central eye bezel',),-.90,1.0,.22,1.02),(('Deep cyclops blue lens',),-1.075,.89,.14,.93),(('Great cyan cyclops iris',),-1.165,.80,.085,.85)]:
   old=objs(*names);name=old[0].name.split('.')[0];material=old[0].data.materials[0].name;delete(*names);ellipsoid(name,(0,cy,zc),(rx,ry,rz),material)
  # Existing moving central pupil and highlight hug the lens instead of floating.
  for o in objs('Long dark vertical pupil','Central lens highlight'):
   if o.type!='MESH':continue
   m=o.matrix_world.copy();inv=m.inverted()
   for v in o.data.vertices:
    p=m@v.co;f=max(.03,1-(p.x/.80)**2-((p.z-zc)/.85)**2);p.y=-1.165-.085*math.sqrt(f)-(.018 if o.name.startswith('Central') else .006);v.co=inv@p
  delete('Satellite articulated upper arm')
  for shell in objs('Pink satellite eye shell'):
   lo,hi=bounds([shell]);c=(lo+hi)*.5;s=1 if c.x>0 else -1
   # Supporting solid bracket attaches to the BACK of each socket, not across lens.
   leaf('Satellite rear articulated bracket',(s*.93,c.y+.09,1.7),(c.x,c.y+.20,c.z-.18),.095,.075,'Gold',normal=(1,0,0),bend=.025)
  delete('Keel upper bowl','Keel lower tapered step','Keel small dark toe')
  profile=[(.346,.025),(.390,.13),(.480,.185),(.570,.235),(.590,.40),(.650,.45),(.780,.51),(.820,.63)];vs=[];fs=[];n=48
  for z,r in profile:
   for i in range(n):q=math.tau*i/n;vs.append((r*math.cos(q),r*math.sin(q),z))
  for j in range(len(profile)-1):
   for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  fs.extend([tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))]);mesh('Keel threeview turned bowl',vs,fs,'Plum',root)
 elif kind=='Poseidon':
  # SIDE body outline (tail to nose) paired with TOP width cross-sections. The
  # FRONT sets the belly roundness; this replaces the old pointy boat-shaped nose.
  delete('Smooth blue whale back','Smooth ivory whale belly','Curved ventral pleat','Swept whale dorsal fin','Horizontal whale tail fluke','Whale side flipper')
  # side image x, dorsal image y, ventral image y, TOP-derived halfwidth.
  sections=[(666,351,463,.10),(701,409,529,.22),(760,428,584,.43),(830,380,627,.70),(908,354,654,.96),(996,345,655,1.10),(1076,348,628,1.09),(1146,367,582,.73),(1200,402,532,.37),(1225,455,489,.018)]
  fine=[]
  for i in range(len(sections)-1):
   a=Vector(sections[i]);b=Vector(sections[i+1])
   for j in range(5):fine.append(a.lerp(b,j/5))
  fine.append(Vector(sections[-1]));n=33
  for lower,mat in [(False,'Body'),(True,'Ivory')]:
   vs=[];fs=[]
   for px,yt,yb,w in fine:
    dep,top=side(px,yt);_,bottom=side(px,yb);blend=max(0,min(1,(px-890)/120));blend=blend*blend*(3-2*blend);mid=bottom+(top-bottom)*(.40+.20*blend)
    for j in range(n):
     q=math.pi*j/(n-1);x=w*math.cos(q);z=mid+((top-mid) if not lower else -(mid-bottom))*math.sin(q)
     vs.append((x,dep,z))
   for j in range(len(fine)-1):
    for i in range(n-1):fs.append((j*n+i,j*n+i+1,(j+1)*n+i+1,(j+1)*n+i))
   fs.extend([tuple(range(n-1,-1,-1)),tuple((len(fine)-1)*n+i for i in range(n))])
   mesh('Smooth ivory whale belly' if lower else 'Smooth blue whale back',vs,fs,mat,root)
  # Dorsal fin has curved SIDE sweep and narrow TOP root, a real biconvex solid.
  profile=[(822,204),(862,206),(910,231),(948,273),(980,343),(846,378),(850,288)]
  yz=[side(px,py) for px,py in profile];verts=[(sign*(.032+.09*max(0,(2.7-z)/1.1)),y,min(z,2.66)) for sign in (-1,1) for y,z in yz];n=len(yz);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  fin=mesh('Threeview swept whale dorsal',verts,faces,'Body',root,False);bevel=fin.modifiers.new('Fin edge radius','BEVEL');bevel.width=.02;bevel.segments=3;bpy.context.view_layer.objects.active=fin;bpy.ops.object.modifier_apply(modifier=bevel.name)
  # Raised caudal peduncle and twin flukes read as whale tail in SIDE and TOP.
  tailY,tailZ=side(667,367)
  for s in (-1,1):leaf('Horizontal whale tail fluke',(0,tailY-.1,tailZ-.1),(s*.69,tailY+.30,tailZ+.34),.25,.075,'Body',root,normal=(0,0,1),bend=.1)
  for s in (-1,1):leaf('Whale side flipper',(s*.82,.20,1.24),(s*1.43,.73,.94),.21,.065,'Body',root,normal=(0,-1,1),bend=.03)
  # Attach the entire eye socket to the new head surface, with side-facing volume.
  from mathutils.bvhtree import BVHTree
  body=objs('Smooth blue whale back')[0];tree=BVHTree.FromPolygons([body.matrix_world@v.co for v in body.data.vertices],[tuple(f.vertices) for f in body.data.polygons])
  for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
   group=[o for o in eye.children_recursive if o.type=='MESH'];lo,hi=bounds(group);sign=1 if (lo.x+hi.x)>0 else -1
   gaze=next((o for o in eye.children_recursive if o.name.split('.')[0]=='PupilGaze'),eye)
   for o in group:bpy.data.objects.remove(o,do_unlink=True)
   c=Vector((sign*.84,-.70,1.49));normal=Vector((sign*.55,-.75,.36)).normalized();up=Vector((0,0,1));up=(up-normal*up.dot(normal)).normalized();right=normal.cross(up).normalized()
   eye_world=eye.matrix_world.copy();eye_world.translation=c;eye.matrix_world=eye_world;bpy.context.view_layer.update()
   if gaze!=eye:
    gaze_world=gaze.matrix_world.copy();gaze_world.translation=c+normal*.132;gaze.matrix_world=gaze_world;bpy.context.view_layer.update()
   # Rebuild closed curved sockets, not a depth-sheared sheet. Source eye pose
   # conflicts are recorded separately; all three views use this same surface.
   for name,mat,offset,rx,ry,rz,parent in [('Eye socket blue','Body',0,.25,.105,.34,eye),('Ivory sclera','Ivory',.035,.218,.11,.30,eye),('Deep pupil','Rubber',.132,.110,.035,.208,gaze),('Eye highlight','Ivory',.171,.042,.018,.058,gaze)]:
    center=c+normal*offset
    if name=='Eye highlight':center+=up*.112+right*.037
    o=ellipsoid(name,(0,0,0),(rx,ry,rz),mat,parent)
    bpy.context.view_layer.update()
    inverse=o.matrix_world.inverted()
    for v in o.data.vertices:
     # ellipsoid helper initially constructs world-axis shape at origin.
     q=o.matrix_world@v.co;v.co=inverse@(center+right*q.x+normal*q.y+up*q.z)
    o.data.update()
  # The source's belly pleats are curves on the closed belly, not a flat face map.
  for j in range(1,11):
   q=math.pi*j/11;points=[]
   for px,yt,yb,w in fine:
    if px<802 or px>1215:continue
    dep,top=side(px,yt);_,bottom=side(px,yb);blend=max(0,min(1,(px-890)/120));blend=blend*blend*(3-2*blend);mid=bottom+(top-bottom)*(.40+.20*blend)
    points.append((w*math.cos(q)*1.008,dep,mid-(mid-bottom)*math.sin(q)-.008))
   curve=bpy.data.curves.new('Curved ventral pleat','CURVE');curve.dimensions='3D';curve.bevel_depth=.007;curve.bevel_resolution=2
   spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
   for p,co in zip(spline.points,points):p.co=(*co,1)
   o=bpy.data.objects.new('Curved ventral pleat',curve);bpy.context.collection.objects.link(o);o.parent=root;curve.materials.append(api['MATS']['Metal']);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False)
 elif kind=='SecWind':
  delete('Broad cambered primary feather','Layered ivory wing feather','Swept bird crest')
  # Closed hooked beak: SIDE supplies the concave hook, FRONT its width at each
  # height. The former straight triangular wedge did not have the source hook.
  old_beak=objs('Great hooked ivory eagle beak')[0];beak_parent=old_beak.parent
  delete('Great hooked ivory eagle beak')
  sections=[(398,1170,1182,.22),(415,1157,1207,.34),(444,1148,1225,.40),(467,1150,1231,.35),(491,1190,1233,.25),(520,1210,1230,.13),(548,1224,1225,.008)]
  fine=[]
  for j in range(len(sections)-1):
   for i in range(6):fine.append(Vector(sections[j]).lerp(Vector(sections[j+1]),i/6))
  fine.append(Vector(sections[-1]));verts=[];faces=[];n=48
  for py,xb,xf,width in fine:
   back,z=side(xb,py);fore,_=side(xf,py);center=(back+fore)/2;depth=(back-fore)/2
   for i in range(n):
    q=math.tau*i/n;verts.append((width*math.cos(q),center+depth*math.sin(q),z))
  for j in range(len(fine)-1):
   for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  faces.extend([tuple(range(n-1,-1,-1)),tuple((len(fine)-1)*n+i for i in range(n))]);mesh('Great hooked ivory eagle beak',verts,faces,'Ivory',beak_parent)
  # Front-traced feather tips, paired with their SIDE traced depths. These fans
  # fill SIDE as well as FRONT/TOP; each feather is a closed cambered volume.
  tips=[(688,209,753),(691,272,745),(675,332,760),(646,388,792),(604,438,845),(555,471,891),(510,484,930)]
  for s in (-1,1):
   for j,(px,py,side_x) in enumerate(tips):
    x,z=front(px,py);dep=[2.28,1.70,.99,.28,-.29,-.60,-.83][j];base=(s*(.85+.018*j),.53,1.66-.07*j)
    leaf('Broad cambered primary feather',base,(s*x,dep,z),.35+.012*j,.065,'Body',normal=(0,1,1),bend=.07)
   for j in range(6):
    t=j/5;x,z=front(603-99*t,248+207*t);dep=[1.42,.93,.46,.02,-.35,-.63][j]
    leaf('Layered ivory wing feather',(s*.96,.37,1.96-.11*j),(s*x,dep,z),.24,.055,'Ivory',normal=(0,1,1),bend=.035)
  for i,(start,tip) in enumerate([((0,-.27,2.0),(.05,.73,2.96)),((-.15,-.1,2.08),(-.38,.92,2.60)),((.16,-.1,2.1),(.30,.63,2.62))]):leaf('Swept bird crest',start,tip,.19,.065,'Body',normal=(0,-1,1),bend=.07)
  # Tilt the turbine plane up 35 degrees. Its circular lip becomes visible in TOP
  # while keeping the fan/short housing attached in SIDE (no top-facing decal).
  names=('High wing-root green turbine','Ivory turbine front ring','Turbine dark well','Pitched radial turbine blade','Turbine spinner')
  for s in (-1,1):
   group=[]
   for o in objs(*names):
    lo,hi=bounds([o])
    if ((lo.x+hi.x)>0)==(s>0):group.append(o)
   lo,hi=bounds(group);c=(lo+hi)*.5;angle=math.radians(35)
   for o in group:
    m=o.matrix_world.copy();inv=m.inverted()
    for v in o.data.vertices:
     p=m@v.co;d=p-c;p.y=c.y+d.y*math.cos(angle)+d.z*math.sin(angle)+.40;p.z=c.z-d.y*math.sin(angle)+d.z*math.cos(angle)-.12;v.co=inv@p
    o.data.update()
 bpy.context.view_layer.update()
 for o,original in controls:
  assert all(abs(o.matrix_basis[i][j]-original[i][j])<1e-6 for i in range(4) for j in range(4)),kind+' control moved '+o.name
 root['threeview_c']=True
 return root
