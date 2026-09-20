"""Independent source .blend to exported JSON group-transform check."""
import bpy,json,math,copy
from pathlib import Path
from mathutils import Quaternion,Vector
ROOT=Path(__file__).resolve().parents[2]
def inspect(root,data):
 original=[]
 def walk(o):
  if o.type!='MESH':original.append(o)
  for child in o.children:walk(child)
 walk(root);nodes=[n for n in data['nodes'] if not n['vertices']]
 assert len(nodes)==len(original),'group count'
 changed=0
 for o,n in zip(original,nodes):
  assert o.name.split('.')[0]==n['name'],(o.name,n['name'])
  actual=Vector((o.location.x,o.location.z,-o.location.y))
  assert (actual-Vector(n['position'])).length<.00001,(o.name,'position')
  q=o.rotation_euler.to_quaternion();q=Quaternion((q.w,q.x,q.z,-q.y));v=n.get('rotation',[0,0,0,1]);out=Quaternion((v[3],v[0],v[1],v[2]))
  assert q.rotation_difference(out).angle<.001,(o.name,'rotation')
  scale=Vector((o.scale.x,o.scale.z,o.scale.y));assert (scale-Vector(n.get('scale',[1,1,1]))).length<.00001,(o.name,'scale')
  if q.angle>.01 or (scale-Vector((1,1,1))).length>.01:changed+=1
 return changed
count=0;negative=False
for p in sorted((ROOT/'art/blender/characters').glob('*.blend')):
 if '_' in p.stem:continue
 bpy.ops.wm.open_mainfile(filepath=str(p));data=json.loads((ROOT/'unity/Assets/_Project/Resources/BlenderModels'/f'{p.stem}.json').read_text());root=bpy.data.objects[p.stem]
 count+=inspect(root,data)
 if not negative:
  broken=copy.deepcopy(data)
  for n in broken['nodes']:
   if not n['vertices']:n.pop('rotation',None);n.pop('scale',None)
  try:inspect(root,broken)
  except AssertionError:negative=True;print('NEGATIVE_CONTROL_DETECTED_DROPPED_TRS',p.stem,flush=True)
 print('SOURCE_EXPORT_TRANSFORMS_PASS',p.stem,flush=True)
assert negative,'negative control did not catch missing group transforms'
print('SOURCE_EXPORT_TRS_ALL_PASS',count,'nonidentity groups',flush=True)
