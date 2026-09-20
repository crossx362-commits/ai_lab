"""회전하지 않는 형제 메시를 부모·재질별로 묶는다. 회전축·바퀴는 따로 유지."""
import json
from pathlib import Path

def pack(model):
    src=model['nodes']; nodes=[]; indices={}; groups={}
    parents={n['parent'] for n in src}
    for i,n in enumerate(src):
        if not n['vertices']:
            indices[i]=len(nodes)
            node=dict(n); node['parent']=-1 if n['parent']<0 else indices[n['parent']]; nodes.append(node)
        else:
            assert i not in parents, 'Mesh parent cannot be flattened'
    for n in src:
        if not n['vertices']: continue
        parent=indices[n['parent']]; key=(parent,n['material'])
        if key not in groups:
            node={'name':n['material']+'Mesh','parent':parent,'position':[0,0,0], 'vertices':[], 'normals':[], 'triangles':[], 'material':n['material']}
            groups[key]=node; nodes.append(node)
        dst=groups[key]; offset=len(dst['vertices'])//3
        if 'uv' in n or 'uv' in dst:
            if 'uv' not in dst: dst['uv']=[0.0]*(offset*2)
            dst['uv'].extend(n.get('uv',[0.0]*(len(n['vertices'])//3*2)))
        dst['vertices'].extend(x+n['position'][i%3] for i,x in enumerate(n['vertices']))
        dst['normals'].extend(n['normals']); dst['triangles'].extend(x+offset for x in n['triangles'])
    model['nodes']=nodes
    return model

if __name__=='__main__':
    root=Path(__file__).resolve().parents[2]/'unity/Assets/_Project/Resources/BlenderModels'
    for p in root.glob('*.json'):
        model=json.loads(p.read_text()); before=len(model['nodes']); pack(model)
        p.write_text(json.dumps(model,separators=(',',':')))
        print(p.stem,before,'->',len(model['nodes']))
