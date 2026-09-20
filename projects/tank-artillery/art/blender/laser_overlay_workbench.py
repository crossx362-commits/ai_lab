"""Reference image empties directly over editable Laser geometry in orthographic views.
Run in the live dedicated workbench. Original crop pixels are packed unchanged.
"""
import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
scene=bpy.context.scene
col=bpy.data.collections.get('LASER ORIGINAL OVERLAYS')
if col:
 for o in list(col.objects):bpy.data.objects.remove(o,do_unlink=True)
else:
 col=bpy.data.collections.new('LASER ORIGINAL OVERLAYS');scene.collection.children.link(col)
# Owner-approved FRONT authority. Side/top source planes are retained but hidden, because their conflicting layouts are not position targets. No mesh-dependent fitting.
records=[('front',(0,-4,1.423),(math.pi/2,0,0),3.4),('side',(-4,-.60,1.423),(math.pi/2,0,-math.pi/2),3.76),('top',(0,-.393,4),(0,0,0),4.426)]
for name,pos,rot,size in records:
 img=bpy.data.images.load(str(ROOT/'output/laser-overlay-fit'/f'{name}-reference.png'),check_existing=True);img.pack()
 obj=bpy.data.objects.new('REFERENCE OVERLAY '+name,None);col.objects.link(obj)
 obj.empty_display_type='IMAGE';obj.data=img;obj.empty_display_size=size;obj.location=pos;obj.rotation_euler=rot
 obj.use_empty_image_alpha=True;obj.color=(1,1,1,.45);obj.empty_image_depth='FRONT';obj.show_empty_image_perspective=False;obj.show_empty_image_orthographic=True;obj.show_empty_image_only_axis_aligned=True
 obj.hide_render=True;obj.hide_select=True
 obj.hide_set(name != 'front')
 obj['role']='Authoritative position overlay' if name=='front' else 'Non-authoritative source view; layout conflicts approved in favor of front'
 obj['registration']='Fixed before editing; uniform image scale; original source pixels'
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  area.spaces.active.overlay.show_overlays=True
  area.spaces.active.overlay.show_floor=False;area.spaces.active.overlay.show_axis_x=False;area.spaces.active.overlay.show_axis_y=False
  for rv in area.spaces.active.region_quadviews:
   direction=rv.view_rotation @ Vector((0,0,1))
   rv.view_location=(0,-.393,1.423);rv.view_distance=6.2 if abs(direction.x)>.9 else 5.3
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/blender/laser_reference_workbench.blend'))
