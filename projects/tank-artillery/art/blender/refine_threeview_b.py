"""Three-view solid corrections. Only render meshes change; rig/control data is immutable.

Coordinates below are cross-sections of the source silhouettes in the existing
registration, not a second global bounding-box fit. Existing parent inverses are
used so the corrected solids continue to articulate with their original rigs.
"""
import math,json
from pathlib import Path
import bpy,bmesh
from mathutils import Vector

KINDS=('Duke','MineLander','Missile','MultiMissile')

def apply(kind,root,api):
 if kind not in KINDS:return root
 bpy.context.view_layer.update()
 registration=json.loads(Path(__file__).with_name('threeview-registration.json').read_text())[kind]
 def source_point(view,px,py):
  rec=registration[view];x,y,w,h=rec['reference_crop'];u=rec['units_per_reference_pixel'];c=rec['center']
  return (c[0]+(px-x-w/2)*u,c[2]+(y+h/2-py)*u) if view=='front' else (c[1]-(px-x-w/2)*u,c[2]+(y+h/2-py)*u) if view=='side' else (c[0]+(px-x-w/2)*u,c[1]+(y+h/2-py)*u)
 objects=list(root.children_recursive)
 def named(name):return [o for o in root.children_recursive if o.name.split('.')[0]==name]
 def delete(names):
  for o in list(root.children_recursive):
   if o.name.split('.')[0] in names or any(o.name.startswith('UV decal '+n) for n in names):
    bpy.data.objects.remove(o,do_unlink=True)
 def solid(name,verts,faces,host,material=None,smooth=True):
  inv=host.parent.matrix_world.inverted() if host.parent else root.matrix_world.inverted()
  data=bpy.data.meshes.new(name);data.from_pydata([inv@Vector(v) for v in verts],[],faces);data.update()
  bm=bmesh.new();bm.from_mesh(data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
  for p in data.polygons:p.use_smooth=smooth
  data.materials.append(material or host.data.materials[0]);o=bpy.data.objects.new(name,data);host.users_collection[0].objects.link(o);o.parent=host.parent
  return o
 def loft(name,sections,host,axis='y',material=None,sides=40):
  # y axis: (depth, halfwidth, halfheight, centerheight), z axis:
  # (height, halfwidth, halfdepth, centerdepth). Closed volumetric rings.
  vv=[];ff=[]
  for pos,rx,rr,center in sections:
   for j in range(sides):
    t=math.tau*j/sides
    vv.append((rx*math.cos(t),pos,center+rr*math.sin(t)) if axis=='y' else (rx*math.cos(t),center+rr*math.sin(t),pos))
  for i in range(len(sections)-1):
   for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;ff.append((a,b,b+sides,a+sides))
  ff.extend([tuple(range(sides-1,-1,-1)),tuple((len(sections)-1)*sides+j for j in range(sides))])
  return solid(name,vv,ff,host,material)
 def deform(names,fn):
  for o in list(root.children_recursive):
   if o.type!='MESH' or not any(o.name.split('.')[0]==n or o.name.startswith('UV decal '+n) for n in names):continue
   mw=o.matrix_world.copy();inv=mw.inverted()
   for v in o.data.vertices:v.co=inv@Vector(fn(mw@v.co))
   o.data.update()
 def seat_eyes(depth_shift,up_slope,out_slope,height_shift=0):
  # Affine depth shear of every mesh in each eye's subtree preserves the
  # established FRONT X exactly (height has an explicit optional offset), while turning the white face upward
  # and around the head's side. Pupils/glints get the same map, so eye thickness
  # and contact survive this correction rather than floating independently.
  for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
   group=[o for o in eye.children_recursive if o.type=='MESH']
   whites=[o for o in group if o.name.startswith('Ivory sclera')]
   if not whites:continue
   pts=[o.matrix_world@v.co for o in whites for v in o.data.vertices]
   cx=(min(p.x for p in pts)+max(p.x for p in pts))*.5;cz=(min(p.z for p in pts)+max(p.z for p in pts))*.5
   side=1 if cx>0 else -1
   for obj in group:
    matrix=obj.matrix_world.copy();inverse=matrix.inverted()
    for vertex in obj.data.vertices:
     point=matrix@vertex.co
     point.y+=depth_shift+up_slope*(point.z-cz)+out_slope*side*(point.x-cx)
     point.z+=height_shift
     vertex.co=inverse@point
    obj.data.update()
 if kind=='Duke':
  h=named('Faceted broad frog snout')[0]
  cheek=loft('Three view frog cheek dome',[(1.22,.82,.62,-.95),(1.39,.99,.91,-.87),(1.59,.93,1.02,-.80),(1.78,.77,.97,-.70),(1.89,.55,.70,-.58),(1.98,.22,.32,-.50),(2.0,.01,.01,-.50)],h,'z',sides=24)
  h2=named('Faceted frog chin')[0]
  jaw=loft('Three view frog lower jaw',[(.237,.43,.28,-.96),(.49,.62,.46,-1.05),(.89,.80,.67,-1.10),(1.24,.87,.74,-1.09),(1.40,.83,.70,-1.07)],h2,'z',sides=16)
  delete(['Faceted broad frog snout','Faceted frog chin','Surface fitted chin plate joint','Surface fitted lower chin seam','Small chin plate rivet','Inset frog nostril'])
  for polygon in jaw.data.polygons:polygon.use_smooth=False
  from mathutils.bvhtree import BVHTree
  bpy.context.view_layer.update()
  trees=[BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in (cheek,jaw)]
  pts=[]
  for i in range(41):
   x=-.88+i*1.76/40;z=1.30+.16*(1-min(1,abs(x)/.6))
   hits=[t.ray_cast(Vector((x,-5,z)),Vector((0,1,0)))[0] for t in trees];hits=[p for p in hits if p is not None]
   if hits:p=min(hits,key=lambda p:p.y);pts.append(p+Vector((0,-.012,0)))
  curve=bpy.data.curves.new('Three view fitted frog mouth','CURVE');curve.dimensions='3D';curve.bevel_depth=.014;curve.bevel_resolution=2
  spline=curve.splines.new('POLY');spline.points.add(len(pts)-1)
  for pt,co in zip(spline.points,pts):pt.co=(*co,1)
  obj=bpy.data.objects.new(curve.name,curve);root.users_collection[0].objects.link(obj);obj.parent=root;curve.materials.append(named('Flexible poison hose')[0].data.materials[0])
  for selected in bpy.context.selected_objects:selected.select_set(False)
  bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False)
  # Shared SIDE/TOP source eye depth; FRONT width/height are kept.
  target_eye_y=(source_point('side',1034,383)[0]+source_point('top',1409,572)[1])*.5
  whites=named('Ivory sclera');allpts=[o.matrix_world@v.co for o in whites for v in o.data.vertices]
  current_eye_y=(min(p.y for p in allpts)+max(p.y for p in allpts))*.5
  seat_eyes(target_eye_y-current_eye_y,.15,.35,.12)
  # Keep a continuous cheek surface behind the rearward eye. A source-shaped
  # depression seats its rim without cutting a black cavity through the hull.
  bpy.context.view_layer.update();beds=[]
  for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
   white=next(o for o in eye.children_recursive if o.name.startswith('Ivory sclera'))
   pp=[white.matrix_world@v.co for v in white.data.vertices]
   beds.append(((min(p.x for p in pp)+max(p.x for p in pp))*.5,(min(p.z for p in pp)+max(p.z for p in pp))*.5,(max(p.x for p in pp)-min(p.x for p in pp))*.5+.025,(max(p.z for p in pp)-min(p.z for p in pp))*.5+.026))
  for host in [cheek]+named('Frog low armored back')+named('Frog low armored back seam foundation'):
   bm=bmesh.new();bm.from_mesh(host.data);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=2,use_grid_fill=True);bm.to_mesh(host.data);bm.free()
   mw=host.matrix_world.copy();inv=mw.inverted()
   for vertex in host.data.vertices:
    point=mw@vertex.co
    for cx,cz,rx,rz in beds:
     q=((point.x-cx)/(rx*1.5))**2+((point.z-cz)/(rz*1.45))**2
     if q<1 and point.z>1.48:
      weight=min(1,max(0,(1-q)/.60));weight=weight*weight*(3-2*weight)
      rear=target_eye_y+.12+.15*(point.z-cz)+.35*(1 if cx>0 else -1)*(point.x-cx)
      point.y+=(max(point.y,rear)-point.y)*weight
    vertex.co=inv@point
   bm=bmesh.new();bm.from_mesh(host.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(host.data);bm.free();host.data.update()
  deform(['Frog chemical nozzle'],lambda p:(p.x,p.y+.55,p.z))
  # Original TOP shows a large glazed opening and a smaller rear filling cap.
  # Replace the existing solid disks, keeping their measured FRONT dimensions.
  glass=bpy.data.materials.new('Duke clear inspection lid');glass.use_nodes=True
  shader=glass.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.94,1,.93,1);shader.inputs['Roughness'].default_value=.035;shader.inputs['IOR'].default_value=1.45;shader.inputs['Transmission Weight'].default_value=1
  cap_hosts=named('Bottle neck cap')+[o for o in named('Bottle metal collar') if sum((o.matrix_world@v.co).z for v in o.data.vertices)/len(o.data.vertices)>3]
  for host in cap_hosts:
   pts=[host.matrix_world@v.co for v in host.data.vertices];lo=[min(p[j] for p in pts) for j in range(3)];hi=[max(p[j] for p in pts) for j in range(3)]
   cx=(lo[0]+hi[0])*.5;cy=(lo[1]+hi[1])*.5;rx=(hi[0]-lo[0])*.5;ry=(hi[1]-lo[1])*.5
   small=host.name.startswith('Bottle neck cap')
   # Cap depth is preserved: FRONT/SIDE versus TOP plug placement conflicts.
   inset=.25 if small else .16;vv=[];ff=[];N=64
   for z,factor in [(lo[2],1),(hi[2],1),(hi[2],1-inset),(lo[2],1-inset)]:
    vv += [(cx+rx*factor*math.cos(math.tau*j/N),cy+ry*factor*math.sin(math.tau*j/N),z) for j in range(N)]
   for row in range(4):
    for j in range(N):a=row*N+j;b=row*N+(j+1)%N;ff.append((a,b,((row+1)%4)*N+(j+1)%N,((row+1)%4)*N+j))
   solid('Duke annular filling rim' if small else 'Duke annular inspection rim',vv,ff,host,smooth=False)
   lid=[]
   for z in (hi[2]-.018,hi[2]-.006):lid += [(cx+rx*(1-inset)*math.cos(math.tau*j/N),cy+ry*(1-inset)*math.sin(math.tau*j/N),z) for j in range(N)]
   faces=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]+[(j,(j+1)%N,(j+1)%N+N,j+N) for j in range(N)]
   solid('Duke transparent filling lid' if small else 'Duke transparent inspection lid',lid,faces,host,glass,smooth=False)
   bpy.data.objects.remove(host,do_unlink=True)
 elif kind=='Missile':
  shell=named('Red rocket shell')[0]
  loft('Three view tapered rocket shoulder',[(1.23,.84,.69,2.13),(.85,.84,.70,2.13),(.10,.90,.735,2.13),(-.40,.84,.70,2.13),(-.68,.71,.65,2.09),(-.865,.50,.57,2.025)],shell,sides=64)
  nose=named('Long ivory ogive')[0]
  loft('Three view continuous ivory ogive',[(-.863,.501,.571,2.025),(-1.08,.410,.466,2.077),(-1.34,.285,.327,2.139),(-1.63,.142,.168,2.207),(-1.941,.002,.002,2.28)],nose,sides=64)
  delete(['Red rocket shell','Long ivory ogive','Nose seam'])
  deform(['Dorsal rocket fin'],lambda p:(p.x*.65,p.y,2.1+(p.z-2.1)*.62))
  deform(['Swept ivory tail fin'],lambda p:(p.x,p.y,(2.07-.25*max(0,abs(p.x)-.50))+.65*(p.z-(2.07-.25*max(0,abs(p.x)-.50)))))
 elif kind=='MineLander':
  cab=named('Rounded mole armored cab')[0]
  new_cab=loft('Rounded mole armored cab three view',[(1.13,.01,.02,1.52),(.96,.65,.40,1.62),(.50,1.04,.67,1.70),(.05,1.18,.65,1.73),(-.40,1.12,.62,1.76),(-.79,1.04,.54,1.75),(-1.16,.76,.43,1.71)],cab,sides=40)
  loft('Three view layered mole lower chassis',[(.86,.82,1.02,.02),(1.02,1.01,1.16,.02),(1.23,1.04,1.10,.02),(1.43,.90,.88,.02)],cab,'z',sides=16)
  deform(['Large goggle housing','Goggle metal rim','Black goggle lens','Goggle glint','Goggle bridge'],lambda p:(p.x,p.y+.035+.15*(p.z-1.88),p.z))
  delete(['Rounded mole armored cab','Rounded mole armored cab seam foundation'])
  host=named('Concave steel digging scoop')[0]
  # Source SIDE has a deep inward bowl and a broad forward horizontal cutting lip.
  profile=[(-1.43,1.49),(-1.42,1.18),(-1.49,.83),(-1.66,.48),(-1.98,.20),(-2.18,.13),(-2.18,.22),(-2.00,.30),(-1.79,.57),(-1.68,.91),(-1.62,1.21),(-1.62,1.49)]
  verts=[]
  for x in [-1.26,1.26]:verts += [(x,y,z) for y,z in profile]
  n=len(profile);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  solid('Three view concave excavator bowl',verts,faces,host,smooth=False)
  for side in (-1,1):
   wing=[(-1.43,1.49),(-1.34,1.46),(-1.60,.71),(-1.91,.22),(-2.18,.13),(-2.20,.24),(-1.83,1.43)]
   vv=[(side*1.23+d,y,z) for d in (-.055,.055) for y,z in wing];n=len(wing)
   solid('Three view excavator side cheek',vv,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],host,smooth=False)
  delete(['Concave steel digging scoop','Scoop raised side wing'])
  # Reconstruct the hollow barrel along its authored longitudinal axis. The old
  # front-only deformation flattened its side and separated it from the cab.
  tube=named('Cannon tube')[0];barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel')
  # Pose-independent tube centerline is derived from the barrel parent frame.
  bw=barrel.matrix_world.copy();axis=(bw.to_3x3()@Vector((0,-1,0))).normalized()
  base=bw.translation+axis*.05;end=bw.translation+axis*1.22
  u=Vector((1,0,0));v=axis.cross(u).normalized();vv=[];ff=[];N=64
  for center,radius in [(base,.40),(end,.40),(end,.31),(base+axis*.17,.31)]:
   vv += [center+radius*(u*math.cos(math.tau*j/N)+v*math.sin(math.tau*j/N)) for j in range(N)]
  for row in range(3):
   for j in range(N):a=row*N+j;b=row*N+(j+1)%N;ff.append((a,b,b+N,a+N))
  solid('Three view open mortar tube',vv,ff,tube,smooth=True)
  # The source's yellow mortar cradle is a thick collar rooted into the cab,
  # not the detached tube implied by FRONT-only fitting. It shares the barrel
  # parent, so its base stays attached through the reference firing pose.
  shroud=[];faces=[]
  for distance,radius in [(-.36,.41),(-.10,.50),(.14,.52),(.31,.43)]:
   center=bw.translation+axis*distance
   shroud += [center+radius*(u*math.cos(math.tau*j/40)+v*math.sin(math.tau*j/40)) for j in range(40)]
  for row in range(3):
   for j in range(40):a=row*40+j;b=row*40+(j+1)%40;faces.append((a,b,b+40,a+40))
  faces.extend([tuple(range(39,-1,-1)),tuple(range(120,160))])
  solid('Three view rooted mortar cradle',shroud,faces,tube,new_cab.data.materials[0],smooth=False)

  dark=named('Dark open bore')[0]
  center=base+axis*.70;disk=[center+.309*(u*math.cos(math.tau*j/N)+v*math.sin(math.tau*j/N)) for j in range(N)]
  solid('Three view recessed mortar darkness',disk,[tuple(range(N))],dark,smooth=False)
  delete(['Cannon tube','Broad muzzle','Muzzle lip','Dark open bore','Mole machined muzzle edge','Stepped cannon base'])
 elif kind=='MultiMissile':
  head=named('Turtle forward head')[0]
  loft('Three view turtle head',[(-1.36,.31,.30,1.22),(-1.65,.52,.40,1.31),(-1.99,.65,.45,1.33),(-2.30,.59,.42,1.30),(-2.52,.39,.30,1.23),(-2.61,.03,.09,1.20)],head,sides=48)
  throat=named('Turtle ivory throat')[0]
  loft('Three view turtle lower smile',[(-1.50,.30,.16,.88),(-1.83,.49,.23,.89),(-2.13,.55,.24,.94),(-2.40,.40,.19,1.02),(-2.55,.06,.025,1.11)],throat,sides=48)
  neck=named('Visible extended turtle neck')[0]
  neck_root_y=(source_point('side',971,539)[0]+source_point('top',1488,533)[1])*.5
  neck_head_y=(source_point('side',1029,499)[0]+source_point('top',1488,588)[1])*.5
  loft('Three view rising connected turtle neck',[(neck_root_y+.23,.45,.37,.76),(neck_root_y,.40,.33,.83),((neck_root_y+neck_head_y)*.5,.35,.31,.96),(neck_head_y,.33,.30,1.12),(neck_head_y-.15,.35,.31,1.23)],neck,sides=40)
  loft('Three view cream neck underside',[(-.96,.30,.10,.56),(-1.16,.34,.12,.61),(-1.42,.34,.13,.72),(-1.69,.35,.13,.85),(-1.84,.37,.12,.92)],throat,sides=40)
  delete(['Turtle forward head','Turtle ivory throat','Turtle connected ivory chest','Visible extended turtle neck','Neck segmented collar'])
  # Real side panel: one broad sheet per side, replacing two floating square tiles.
  panels=named('Launcher side panel')
  for o in panels:
   if o in bpy.data.objects.values():bpy.data.objects.remove(o,do_unlink=True)
  # Fill the lower launcher-to-shell bearing gap with an attached housing.
  shell=named('Low tiled turtle shell')[0]
  deform(['Low tiled turtle shell','Low tiled turtle shell seam foundation'],lambda p:(p.x,p.y,.37+(p.z-.37)*1.12))
  target_turtle_eye_y=-2.25  # source depth-only candidate is retained separately; it loses FRONT eye visibility
  # Turtle whites are inset on the head flanks in SIDE/TOP. Seat the entire
  # existing eye stack on the actual head surface, including a small positive
  # layer thickness, rather than leaving a floating disc at the nose tip.
  from mathutils.bvhtree import BVHTree
  bpy.context.view_layer.update()
  host=named('Three view turtle head')[0]
  tree=BVHTree.FromPolygons([host.matrix_world@v.co for v in host.data.vertices],[tuple(p.vertices) for p in host.data.polygons])
  for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
   group=[o for o in eye.children_recursive if o.type=='MESH']
   white=next(o for o in group if o.name.startswith('Ivory sclera'))
   reference=[white.matrix_world@v.co for v in white.data.vertices]
   cx=(min(p.x for p in reference)+max(p.x for p in reference))*.5;cz=(min(p.z for p in reference)+max(p.z for p in reference))*.5
   for obj in group:
    matrix=obj.matrix_world.copy();inverse=matrix.inverted();pts=[matrix@v.co for v in obj.data.vertices]
    side=1 if sum(p.x for p in pts)>0 else -1
    low=min(p.y for p in pts);high=max(p.y for p in pts)
    offset=.006 if obj.name.startswith('Eye socket') else .018 if obj.name.startswith('Ivory sclera') else .060 if obj.name.startswith('Deep pupil') else .089
    for vertex,point in zip(obj.data.vertices,pts):
     old_depth=point.y
     point.y=target_turtle_eye_y+side*(point.x-cx)*1.90+.12*(point.z-cz)
     if obj.name.startswith(('Deep pupil','Eye highlight')):point.y-=.075
     hit,_,_,_=tree.ray_cast(Vector((side*4,point.y,point.z)),Vector((-side,0,0)))
     if hit is None:
      original_y=point.y;original_z=point.z
      for factor in (.98,.94,.90,.85,.80,.70,.60):
       point.y=target_turtle_eye_y+(original_y-target_turtle_eye_y)*factor;point.z=cz+(original_z-cz)*factor
       hit,_,_,_=tree.ray_cast(Vector((side*4,point.y,point.z)),Vector((-side,0,0)))
       if hit is not None:break
     if hit is not None:
      depth=offset+(.009 if obj.name.startswith('Eye socket') else .015 if obj.name.startswith(('Deep pupil','Eye highlight')) else .035)*(high-old_depth)/max(1e-6,high-low)
      point.x=hit.x-side*.045 if obj.name.startswith('Eye socket') else hit.x+side*depth
      vertex.co=inverse@point
    obj.data.update()
 for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
  for obj in [o for o in eye.children_recursive if o.type=='MESH']:
   if obj.data.has_custom_normals:obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))
   bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
 root['threeViewSolidCorrection']='mechanical-b-v2'
 bpy.context.view_layer.update()
 return root
