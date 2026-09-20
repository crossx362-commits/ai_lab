"""Laser's editable solid geometry, registered to the owner-approved FRONT drawing.

XY outlines come from the original front view; Z remains real modeled depth.
The source artwork is never projected onto the mesh as a replacement for geometry.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def build(a, root, turret, barrel, fire, mesh):
    data = json.loads(Path(__file__).with_name('laser_front_shapes.json').read_text())
    outlines = data['polygons']
    scale = data['registration']['units_per_pixel']
    turret.location = a.v((0, 1.24, 0))
    barrel.location = a.v((0, 0, 0))
    fire.location = a.v((0, -.86, 2.606))
    root['authoredTurret'] = [0, 1.24, 0]
    root['authoredBarrel'] = [0, 0, 0]
    root['authoredFire'] = 2.606
    root['referenceAuthority'] = 'FRONT, owner approved 2026-09-20'

    def xy(px, py):
        return ((px - 295.5) * scale, 1.423 + (402 - py) * scale - 1.24)

    def mirrored(points):
        return [(591 - x, y) for x, y in reversed(points)]

    def visor_z(x, y=0):
        return 1.43 - 1.16 * min(abs(x), .43) - .56 * max(0, abs(x) - .43)

    def finish_surface(obj):
        # Scan sections collapse at silhouette tips. Weld those coincident points
        # and split hard armor rims before exporting corner normals to Unity.
        bm = bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-8)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        for edge in bm.edges:
            if len(edge.link_faces) == 2 and edge.calc_face_angle() > math.radians(45):
                edge.smooth = False
        if obj.name.startswith('Central brow crest'):
            bmesh.ops.triangulate(bm, faces=list(bm.faces))
            for face in bm.faces: face.smooth = False
        bm.to_mesh(obj.data); bm.free(); obj.data.update()
        return obj

    def solid(name, points, front, back, material='Body', parent=turret):
        # Scan-converted cross-sections retain the outline while evaluating real depth
        # at every column; large flat triangles must not bridge across a curved visor.
        xs = sorted(set([p[0] for p in points] + list(range(math.ceil(min(p[0] for p in points)), math.floor(max(p[0] for p in points)), 2))))
        sections = []
        for px in xs:
            cuts = []
            for (x0,y0),(x1,y1) in zip(points,points[1:]+points[:1]):
                if x0 != x1 and min(x0,x1) <= px <= max(x0,x1):
                    cuts.append(y0+(y1-y0)*(px-x0)/(x1-x0))
            if cuts:
                x,low=xy(px,max(cuts));_,high=xy(px,min(cuts));sections.append((x,low,high))
        jaw = name == 'Front registered V jaw'
        levels = [0,.065,.94,1] if jaw else ([0,1] if name=='Front registered visor hull' else [j/8 for j in range(9)])
        rows=len(levels)-1
        vertices=[];faces=[];count=len(sections)*(rows+1)
        for layer in (0,1):
            depth=front if layer==0 else back
            for x,low,high in sections:
                for j in range(rows+1):
                    y=low+(high-low)*levels[j]
                    z=depth(x,y)
                    if jaw and layer==0 and j in (0,rows): z-=.045
                    vertices.append((x,y,z))
        for layer in (0,1):
            for i in range(len(sections)-1):
                for j in range(rows):
                    n=layer*count+i*(rows+1)+j;faces.append((n,n+1,n+rows+2,n+rows+1))
        border=list(range(rows+1))+[i*(rows+1)+rows for i in range(1,len(sections))]
        border += [(len(sections)-1)*(rows+1)+j for j in range(rows-1,-1,-1)]
        border += [i*(rows+1) for i in range(len(sections)-2,0,-1)]
        faces += [(n,border[(i+1)%len(border)],border[(i+1)%len(border)]+count,n+count) for i,n in enumerate(border)]
        return finish_surface(mesh(name,vertices,faces,material,parent,not jaw))

    def shell(name, points, rear_depth, front_depth):
        # A curved, closed plate with an exact front silhouette and a deep upper surface.
        xs = sorted(set([p[0] for p in points] + list(range(math.ceil(min(p[0] for p in points)), math.floor(max(p[0] for p in points)), 2))))
        sections = []
        for px in xs:
            cuts = []
            for (x0, y0), (x1, y1) in zip(points, points[1:] + points[:1]):
                if x0 != x1 and min(x0, x1) <= px <= max(x0, x1):
                    cuts.append(y0 + (y1 - y0) * (px - x0) / (x1 - x0))
            if cuts:
                x, low = xy(px, max(cuts)); _, high = xy(px, min(cuts))
                sections.append((x, low, high))
        vertices = []; rows = 12; count = len(sections) * (rows + 1)
        for layer in (0, 1):
            for x, low, high in sections:
                for j in range(rows + 1):
                    t = j / rows
                    y = low + (high - low) * t
                    z = front_depth(x) + (rear_depth(x) - front_depth(x)) * math.sin(t * math.pi / 2) - layer * .055
                    vertices.append((x, y, z))
        faces = []
        for layer in (0, 1):
            for i in range(len(sections) - 1):
                for j in range(rows):
                    n = layer * count + i * (rows + 1) + j
                    faces.append((n, n + 1, n + rows + 2, n + rows + 1))
        border = list(range(rows + 1)) + [i * (rows + 1) + rows for i in range(1, len(sections))]
        border += [(len(sections) - 1) * (rows + 1) + j for j in range(rows - 1, -1, -1)]
        border += [i * (rows + 1) for i in range(len(sections) - 2, 0, -1)]
        faces += [(n, border[(i + 1) % len(border)], border[(i + 1) % len(border)] + count, n + count) for i, n in enumerate(border)]
        return finish_surface(mesh(name, vertices, faces, 'Body', turret, True))

    helmet = shell('Front registered helmet', outlines['helmet'],
                   lambda x: -1.1 + 1.15 * (abs(x) / .71) ** 2,
                   lambda x: visor_z(x) + .055)
    solid('Front registered visor hull', outlines['visor'], visor_z, lambda x, y: visor_z(x) - .16, 'Navy')
    a.sculpt('Rounded inner hover hull', (0, -.18, -.35), (.58, .18, .70), 'Navy', turret, .85)
    def jaw_depth(x, y):
        # Two planar cheek plates meet at a forward center ridge. The lower edge
        # used to recede .85m, turning the chest into an inward-sloping apron.
        return 1.53 - .72*abs(x) + .14*(y+.40)
    solid('Front registered V jaw', outlines['jaw'], jaw_depth,
          lambda x, y: jaw_depth(x, y) - .16, 'Body')

    eyes = json.loads(Path(__file__).with_name('laser_eye_trace.json').read_text())['eye_columns']
    for side in (-1, 1):
        columns = eyes[str(side)]; vertices = []; faces = []
        for x, low, high in columns:
            for j in range(5):
                vertices.append((x, low + (high - low) * j / 4, visor_z(x) + .027))
        for i in range(len(columns) - 1):
            for j in range(4):
                n = i * 5 + j; faces.append((n, n + 1, n + 6, n + 5))
        mesh('Reference traced cyan visor eye', vertices, faces, 'Energy', turret)
        shoulder = outlines['left_shoulder'] if side < 0 else mirrored(outlines['left_shoulder'])
        shell('Front registered shoulder', shoulder,
              lambda x: -.70 + .73 * min(1, max(0, (abs(x) - .65) / .92)),
              lambda x: .61 - .30 * min(1, max(0, (abs(x) - .65) / .92)))
        blade = outlines['left_blade'] if side < 0 else mirrored(outlines['left_blade'])

        def blade_z(x, y):
            py = 402 - (y + 1.24 - 1.423) / scale
            return .52 + 2.086 * min(1, max(0, (py - 434) / 125))

        solid('Front registered launch blade', blade, blade_z, lambda x, y: blade_z(x, y) - .10)
        muzzle = outlines['left_muzzle'] if side < 0 else mirrored(outlines['left_muzzle'])
        solid('Front registered muzzle opening', muzzle, lambda x, y: 2.610, lambda x, y: 2.607, 'Navy')
        slot = outlines['left_slot'] if side < 0 else mirrored(outlines['left_slot'])
        solid('Blade inset slot', slot, lambda x, y: blade_z(x, y) + .007, lambda x, y: blade_z(x, y) + .003, 'Navy')

        fin = outlines['left_fin'] if side < 0 else mirrored(outlines['left_fin'])
        def side_h(y):
            return min(1, max(0, (y - xy(0, 371)[1]) / (xy(0, 289)[1] - xy(0, 371)[1])))
        solid('Front registered lateral fin', fin,
              lambda x, y: -.02 - 1.18 * side_h(y) ** .75,
              lambda x, y: -.72 - .485 * side_h(y))

        # The reference's visible front disc, not bounds inferred from a gameplay hitbox.
        pod_x = -.833 if side < 0 else .838
        a.cyl('Suspension neck', (pod_x, .85, .04), .25, .36, 'Metal', root, seg=32)
        a.cyl('Suspension collar', (pod_x, .77, .04), .37, .12, 'Metal', root, seg=32)
        a.cyl('Hover upper armor', (pod_x, .70, .04), .47, .16, 'Metal', root, r2=.40, seg=48)
        a.cyl('Hover lower saucer', (pod_x, .585, .04), .36, .15, 'Metal', root, r2=.47, seg=48)
        lift = a.cyl('Cyan lift disc', (pod_x, .4787, .04), .365, .04, 'Energy', root, seg=48)
        lift.rotation_euler[0] = math.radians(-6)
        a.cyl('Rear exhaust housing', (side * .25, .18, -1.25), .23, .49, 'Metal', turret, 'z', seg=24)
        a.cyl('Rear exhaust rim', (side * .25, .18, -1.53), .205, .08, 'Metal', turret, 'z', seg=24)
        a.cyl('Dark rear exhaust', (side * .25, .18, -1.579), .145, .015, 'Navy', turret, 'z', seg=24)
        # Small, recessed, separate team-color slot on the rear armor.
        a.cyl('Rear team enamel', (side * .25, .18, -1.592), .11, .008, 'Team', turret, 'z', seg=20)

    def center_h(y):
        return min(1, max(0, (y - xy(0, 347)[1]) / (xy(0, 230)[1] - xy(0, 347)[1])))
    central_fin = solid('Front registered central fin', outlines['center_fin'],
          lambda x, y: .04 - 1.86 * center_h(y) ** .85,
          lambda x, y: -1.15 - .675 * center_h(y))
    from mathutils.bvhtree import BVHTree
    bpy.context.view_layer.update()
    crest_trees = [BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get()) for o in (helmet, central_fin)]
    def crest_depth(x, y):
        hits = []
        for tree in crest_trees:
            hit, _, _, _ = tree.ray_cast(a.v((x, y, 5)), a.v((0, 0, -1)))
            if hit is not None: hits.append(-hit.y)
        return max(hits) + .050 if hits else -.25
    solid('Central brow crest', outlines['crest'], crest_depth, lambda x, y: crest_depth(x, y) - .02)

    bpy.context.view_layer.update()
    armor=[o for o in turret.children if o.type=='MESH' and o.data.materials and o.data.materials[0].name=='Body']

    # Surface-conforming UV decals. Keep these separate from Body and Team roles.
    # Front projection establishes UVs only; the armor beneath remains real volume.
    material=bpy.data.materials.new('LaserDecal');material.use_nodes=True
    nodes=material.node_tree.nodes;nodes.clear()
    tex=nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(Path(__file__).resolve().parents[1]/'textures/LaserArmorDecal.png'),check_existing=True)
    tex.image.pack()
    transparent=nodes.new('ShaderNodeBsdfTransparent')
    ink=nodes.new('ShaderNodeEmission');ink.inputs['Strength'].default_value=1
    mix=nodes.new('ShaderNodeMixShader');output=nodes.new('ShaderNodeOutputMaterial')
    links=material.node_tree.links
    links.new(tex.outputs['Color'],ink.inputs[0]);links.new(tex.outputs['Alpha'],mix.inputs[0])
    links.new(transparent.outputs[0],mix.inputs[1]);links.new(ink.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
    a.MATS['LaserDecal']=material
    for source in armor:
        vertices=[];faces=[]
        for face in source.data.polygons:
            if face.normal.y >= -.15: continue
            indices=[]
            for vi in face.vertices:
                v=source.data.vertices[vi].co+face.normal*.002
                indices.append(len(vertices));vertices.append((v.x,v.z,-v.y))
            faces.append(tuple(indices))
        if not faces: continue
        obj=mesh('UV decal '+source.name,vertices,faces,'LaserDecal',turret,False)
        uv=obj.data.uv_layers.new(name='ReferenceFrontUV')
        for loop in obj.data.loops:
            v=obj.data.vertices[loop.vertex_index].co
            px=v.x/scale+295.5;py=402-(v.z-(1.423-1.24))/scale
            uv.data[loop.index].uv=(px/600,1-py/600)
        obj['decalAtlas']='LaserArmorDecal.png'
