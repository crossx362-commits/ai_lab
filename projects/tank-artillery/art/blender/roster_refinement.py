"""Owner-approved Laser process extended to the other twelve authored models.
Blender-only source processing. No runtime mesh generation or gameplay dimensions.
"""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
GROUPS={
 'Catapult':'refine_organic_a','CrossBow':'refine_organic_a','Cannon':'refine_organic_a','Carrot':'refine_organic_a',
 'Duke':'refine_mechanical_b','MineLander':'refine_mechanical_b','Missile':'refine_mechanical_b','MultiMissile':'refine_mechanical_b',
 'SuperTank':'refine_fantasy_c','IonAttacker':'refine_fantasy_c','Poseidon':'refine_fantasy_c','SecWind':'refine_fantasy_c'}
TEAM_HOSTS={'Catapult':'Oak end face','CrossBow':'Owl rounded feather body','Cannon':'Great spherical navy cannon','Carrot':'Smooth tapered carrot armor','Duke':'Frog low armored back','MineLander':'Rounded mole armored cab','Missile':'Rocket rounded tail','MultiMissile':'Nine cell dorsal rocket pack','SuperTank':'Broad lion lower hull','IonAttacker':'Pink orbital sphere','Poseidon':'Blue whale upper shell','SecWind':'Green bird rounded head'}
def team_badges(kind,root,api):
 bpy.context.view_layer.update()
 candidates=[o for o in root.children_recursive if o.type=='MESH' and o.name.startswith(TEAM_HOSTS[kind])]
 if not candidates:raise RuntimeError('Missing team badge host '+kind)
 host=candidates[0];world=host.matrix_world;inv=world.inverted()
 points=[world@v.co for v in host.data.vertices];polys=[tuple(p.vertices) for p in host.data.polygons]
 tree=BVHTree.FromPolygons(points,polys)
 low=Vector([min(p[j] for p in points) for j in range(3)]);high=Vector([max(p[j] for p in points) for j in range(3)])
 center=(low+high)*.5
 # A badge follows actual curved surfaces; no giant floating banner above the model.
 for view in ('back','top'):
  vertices=[];faces=[];n=5
  width=min(.65,(high.x-low.x)*.43);height=min(.32,(high.z-low.z)*.35)
  if view=='top':height=min(.43,(high.y-low.y)*.35)
  for j in range(n):
   for i in range(n):
    x=center.x+(i/(n-1)-.5)*width
    if view=='back':origin=Vector((x,high.y+1,center.z+(j/(n-1)-.5)*height));direction=Vector((0,-1,0))
    else:origin=Vector((x,center.y+(j/(n-1)-.5)*height,high.z+1));direction=Vector((0,0,-1))
    hit,normal,_,_=tree.ray_cast(origin,direction)
    vertices.append(None if hit is None else inv@(hit+normal*.012))
  for j in range(n-1):
   for i in range(n-1):
    a=j*n+i;f=(a,a+1,a+n+1,a+n)
    if all(vertices[q] is not None for q in f):faces.append(f)
  if not faces:continue
  import bmesh
  data=bpy.data.meshes.new(kind+' team '+view);data.from_pydata([v if v is not None else (0,0,0) for v in vertices],[],faces);data.update()
  bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
  # Open surfaces need an explicit outward normal, not an island-volume guess.
  direction=Vector((0,1,0)) if view=='back' else Vector((0,0,1));nm=world.to_3x3().inverted().transposed()
  for p in data.polygons:
   if (nm@p.normal).dot(direction)<0:p.flip()
  data.update();data.materials.append(api['MATS']['Team'])
  obj=bpy.data.objects.new('Team enamel '+view,data);bpy.context.collection.objects.link(obj);obj.parent=host.parent;obj.matrix_local=host.matrix_local.copy()

def apply(kind,root,api):
 if kind=='Laser':return root
 import importlib
 importlib.import_module(GROUPS[kind]).apply(kind,root,api)
 # Keep gaze offsets in the eye frame, and all the body's existing rig ancestry.
 for eye in [o for o in root.children_recursive if o.name.split('.')[0]=='Eye']:
  if any(c.name.startswith('PupilGaze') for c in eye.children):continue
  moving=[c for c in eye.children if c.name.startswith(('Deep pupil','Eye highlight'))]
  if not moving:continue
  group=api['empty']('PupilGaze',(0,0,0),eye)
  for child in moving:child.parent=group
 team_badges(kind,root,api)
 from roster_decals import apply as decals
 decals(kind,root,api)
 return root
