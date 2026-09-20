"""Editable 12-tank Blender workbench with packed original FRONT overlays."""
import bpy,json,math
from pathlib import Path
from mathutils import Quaternion,Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/roster-approved-method';regs=json.loads((OUT/'fixed-registration.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.name='Approved method — twelve tank review'
for index,(kind,views) in enumerate(regs.items()):
 with bpy.data.libraries.load(str(ROOT/'art/blender/characters'/f'{kind}.blend'),link=False) as (src,dst):dst.collections=[kind]
 col=dst.collections[0];scene.collection.children.link(col)
 root=next(o for o in col.objects if o.parent is None);root.location=(index%4*9,0,-(index//4)*7);root.rotation_euler=(0,0,0)
 barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel');barrel.rotation_euler.x=-math.radians({'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}.get(kind,0))
 for o in col.objects:
  if o.type=='EMPTY':o.hide_set(True)
 rec=views['front'];image=bpy.data.images.load(str(OUT/'fixed-review'/kind/'front-reference.png'));image.pack()
 ref=bpy.data.objects.new(kind+' original FRONT overlay',None);scene.collection.objects.link(ref);ref.empty_display_type='IMAGE';ref.data=image
 ref.location=(root.location.x+rec['center'][0],-5,root.location.z+rec['center'][2]);ref.rotation_euler=(math.pi/2,0,0);ref.empty_display_size=rec['ortho_scale'];ref.use_empty_image_alpha=True;ref.color[3]=.4;ref.empty_image_depth='FRONT';ref.show_empty_image_orthographic=True;ref.show_empty_image_perspective=False;ref.show_empty_image_only_axis_aligned=True;ref.hide_render=True;ref.hide_select=True
 source=bpy.data.images.load(str(ROOT/'art/concepts/orthographic-v1'/f'{kind}.png'));source.pack()
 root['originalThreeViewImage']=source.name
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   space=area.spaces.active;space.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2);space.region_3d.view_perspective='ORTHO';space.region_3d.view_distance=35;space.region_3d.view_location=(13.5,0,-5)
   space.shading.type='MATERIAL';space.overlay.show_floor=False;space.overlay.show_axis_x=False;space.overlay.show_axis_y=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/blender/roster_reference_workbench.blend'))
print('ROSTER_REFERENCE_WORKBENCH_SAVED')
