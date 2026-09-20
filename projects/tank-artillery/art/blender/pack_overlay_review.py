"""Pack unretouched reference/model overlay evidence into an editable Blender review file."""
import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/orthographic_review.blend'))
scene=bpy.context.scene
for o in bpy.data.objects:o.hide_render=True
collection=bpy.data.collections.new('ACTUAL MODEL OVERLAY EVIDENCE');scene.collection.children.link(collection)
for i,p in enumerate(sorted((ROOT/'output/orthographic-review').glob('*/overlay.png'))):
 im=bpy.data.images.load(str(p),check_existing=True);im.pack()
 mat=bpy.data.materials.new(p.parent.name+' 50 percent actual render overlay');mat.use_nodes=True;n=mat.node_tree.nodes;n.clear()
 tex=n.new('ShaderNodeTexImage');tex.image=im;em=n.new('ShaderNodeEmission');out=n.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(tex.outputs['Color'],em.inputs[0]);mat.node_tree.links.new(em.outputs[0],out.inputs[0])
 bpy.ops.mesh.primitive_plane_add(size=1,location=(i*20,0,0),rotation=(math.pi/2,0,0));o=bpy.context.object;o.name=p.parent.name+' front side top overlay';o.scale=(18,18*im.size[1]/im.size[0],1);o.data.materials.append(mat)
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o)
 bpy.ops.object.camera_add(location=(i*20,-20,0));cam=bpy.context.object;cam.name=p.parent.name+' Overlay Camera';cam.rotation_euler=(Vector((i*20,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=19
 if p.parent.name=='Catapult':scene.camera=cam
scene.view_settings.view_transform='Standard';scene.render.resolution_x=1800;scene.render.resolution_y=700
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/blender/orthographic_overlay_review.blend'))
print('PACKED_13_ORTHOGRAPHIC_OVERLAYS',flush=True)
