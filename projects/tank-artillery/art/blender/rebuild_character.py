"""Rebuild one authored character, preserving the rest of the editable roster.
Blender --background --python rebuild_character.py -- Catapult
"""
import sys,json
from pathlib import Path
import bpy
script=Path(__file__).resolve().with_name('build_models.py')
kind=sys.argv[sys.argv.index('--')+1]
namespace={'__file__':str(script),'__name__':'character_builder'}
exec(compile(script.read_text().split('models=[]; stats=[]; libraries=[]')[0],str(script),'exec'),namespace)
assert kind in namespace['KINDS'],kind
root=namespace['tank'](kind);triangles=namespace['bake'](root)
# Bake uses role names; the editable source/FBX also receive the authored kind color.
color=namespace['COLORS'][namespace['KINDS'].index(kind)];linear=namespace['linear']
paint=namespace['MATS']['Body'].copy();paint.name=kind+' paint'
paint.diffuse_color=(*linear(color),1)
paint.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*linear(color),1)
if paint.node_tree.nodes.get('Authored paint'):paint.node_tree.nodes['Authored paint'].outputs[0].default_value=(*linear(color),1)
for child in root.children_recursive:
    if child.type=='MESH' and child.data.materials[0].name=='Body':child.data.materials[0]=paint
namespace['export_fbx'](root)
col=bpy.data.collections.new(kind);bpy.context.scene.collection.children.link(col)
for obj in [root]+list(root.children_recursive):
    for old in list(obj.users_collection):old.objects.unlink(obj)
    col.objects.link(obj)
art=script.parent;folder=art/'characters';folder.mkdir(exist_ok=True)
character_file=folder/(kind+'.blend')
bpy.ops.wm.save_as_mainfile(filepath=str(character_file))
roster=art/'tankfall_roster.blend';bpy.ops.wm.open_mainfile(filepath=str(roster))
old=bpy.data.objects[kind];pos=old.location.copy();rot=old.rotation_euler.copy()
collections=list(old.users_collection)
for obj in list(old.children_recursive)+[old]:bpy.data.objects.remove(obj,do_unlink=True)
for col in collections:
    if len(col.objects)==0:bpy.data.collections.remove(col)
with bpy.data.libraries.load(str(character_file),link=False) as (source,target):target.collections=[kind]
for col in target.collections:bpy.context.scene.collection.children.link(col)
bpy.data.objects[kind].location=pos;bpy.data.objects[kind].rotation_euler=rot
bpy.ops.wm.save_as_mainfile(filepath=str(roster))
manifest=art/'manifest.json';data=json.loads(manifest.read_text())
for row in data['models']:
    if row['name']==kind:row['triangles']=triangles
manifest.write_text(json.dumps(data,indent=2))
print('CHARACTER_REBUILD_PASS',kind,triangles,flush=True)
