"""Reference FRONT landmark fitting of real mesh geometry in a frozen camera frame.
Z/depth is retained. Control anchors and original artwork are not changed.
"""
import json,math
from pathlib import Path
import bpy
from mathutils import Vector
HERE=Path(__file__).parent
POSE={'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}
def apply(kind,root):
 files=[HERE/f'front_targets_{letter}.json' for letter in 'abc']
 spec={}
 for path in files:
  if path.exists():spec.update(json.loads(path.read_text()))
 if kind not in spec:return
 rec=json.loads((HERE/'roster_front_registration.json').read_text())[kind]['front']
 _,_,width,height=rec['reference_crop'];unit=rec['units_per_reference_pixel'];cx,_,cz=rec['center']
 objects=list(root.children_recursive);barrel=next(o for o in objects if o.name.split('.')[0]=='Barrel');parked=barrel.rotation_euler.copy();barrel.rotation_euler.x=-math.radians(POSE.get(kind,0));bpy.context.view_layer.update()
 def meshes(obj):return ([obj] if obj.type=='MESH' else [])+[o for o in obj.children_recursive if o.type=='MESH']
 def bounds(group):
  points=[o.matrix_world@v.co for o in group for v in o.data.vertices]
  if not points:return None
  return [min(p.x for p in points),min(p.z for p in points),max(p.x for p in points),max(p.z for p in points)]
 records=[]
 for target in spec[kind]:
  selected=[]
  for o in objects:
   name=o.name.split('.')[0]
   match=any(name==prefix if target.get('match')=='exact' else name.startswith(prefix) for prefix in target['prefixes'])
   if not match:continue
   group=meshes(o);b=bounds(group)
   if b is None:continue
   x=(b[0]+b[2])*.5;y=(b[1]+b[3])*.5
   if target.get('side')=='left' and x>=0:continue
   if target.get('side')=='right' and x<=0:continue
   if y<target.get('world_y_min',-1e9) or y>target.get('world_y_max',1e9):continue
   selected.extend(group)
  selected=list(dict.fromkeys(o for o in selected if not any(o.name.startswith(e) for e in target.get('exclude',[]))))
  b=bounds(selected)
  if b is None:raise RuntimeError(kind+' unmatched reference target '+str(target['prefixes']))
  u0,v0,u1,v1=target['box'];dest=[cx+(u0-.5)*width*unit,cz+(.5-v1)*height*unit,cx+(u1-.5)*width*unit,cz+(.5-v0)*height*unit]
  sx=(dest[2]-dest[0])/max(1e-6,b[2]-b[0]);sz=(dest[3]-dest[1])/max(1e-6,b[3]-b[1])
  if not(.2<sx<5 and .2<sz<5):raise RuntimeError('Unreasonable landmark fit '+kind+' '+str((sx,sz,target)))
  for obj in selected:
   mat=obj.matrix_world.copy();inverse=mat.inverted()
   for vertex in obj.data.vertices:
    p=mat@vertex.co;p.x=dest[0]+(p.x-b[0])*sx;p.z=dest[1]+(p.z-b[1])*sz;vertex.co=inverse@p
   obj.data.update()
  # Keep rotating wheel pivots centered after fitting their descendants.
  for wheel in objects:
   if wheel.name.split('.')[0]!='Wheel':continue
   if not any(o in selected for o in meshes(wheel)):continue
   worlds={child:child.matrix_world.copy() for child in wheel.children}
   matrix=wheel.matrix_world.copy();p=matrix.translation.copy();p.x=dest[0]+(p.x-b[0])*sx;p.z=dest[1]+(p.z-b[1])*sz;matrix.translation=p;wheel.matrix_world=matrix;bpy.context.view_layer.update()
   for child,world in worlds.items():child.matrix_world=world
  records.append({'prefixes':target['prefixes'],'box':target['box'],'mesh_count':len(selected),'scale_xz':[round(sx,5),round(sz,5)]})
 barrel.rotation_euler=parked;bpy.context.view_layer.update();root['frontLandmarkFit']=json.dumps(records,separators=(',',':'))
 print('FRONT_LANDMARKS_FIT',kind,len(records),flush=True)
