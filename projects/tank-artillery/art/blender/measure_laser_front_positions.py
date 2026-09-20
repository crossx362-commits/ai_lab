"""Position evidence for the approved front view. Never reports a global match percentage."""
from pathlib import Path
from collections import deque
import json
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/laser-position-fit';REPORT=ROOT/'output/laser-front-authority';REPORT.mkdir(exist_ok=True)
reference=np.array(Image.open(ROOT/'output/laser-overlay-fit/front-reference.png').convert('RGB')).astype(int)
r,g,b=(reference[:,:,j] for j in range(3));cyan=(g-r>30)&(b-r>35)&(g>120)
def components(m):
 seen=np.zeros_like(m);out=[]
 for yy,xx in zip(*np.where(m)):
  if seen[yy,xx]:continue
  todo=deque([(int(yy),int(xx))]);seen[yy,xx]=True;points=[]
  while todo:
   y,x=todo.popleft();points.append((y,x))
   for dy,dx in ((0,1),(0,-1),(1,0),(-1,0)):
    ny,nx=y+dy,x+dx
    if 0<=ny<m.shape[0] and 0<=nx<m.shape[1] and m[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;todo.append((ny,nx))
  if len(points)>20:
   p=np.array(points);cy,cx=p.mean(axis=0);low=p.min(axis=0);high=p.max(axis=0);tip=p[p[:,0]<=low[0]+1][:,1].mean()
   out.append({'pixels':len(points),'center':[float(cx),float(cy)],'bounds':[int(low[1]),int(low[0]),int(high[1]),int(high[0])],'top_tip':[float(tip),int(low[0])]})
 return sorted(out,key=lambda c:c['center'][0])
report={'authority':'Owner-approved FRONT view; fixed registration, no best-fit transform','components':{}}
for name,lo,hi in [('eyes',190,248),('thrusters',315,350)]:
 target=cyan.copy();target[:lo]=False;target[hi:]=False
 actual=np.array(Image.open(OUT/('front-'+('eye' if name=='eyes' else name)+'-mask.png')).convert('RGB'))[:,:,0]>128
 if name=='thrusters':
  def halves(mask):
   result=[]
   for side in (0,1):
    part=mask.copy()
    if side==0:part[:,278:]=False
    else:part[:,:278]=False
    y,x=np.where(part)
    result.append({'pixels':int(part.sum()),'center':[float(x.mean()),float(y.mean())],'bounds':[int(x.min()),int(y.min()),int(x.max()),int(y.max())]})
   return result
  ts=halves(target);ms=halves(actual)
 else:ts=components(target);ms=components(actual)
 pairs=[]
 for t,m in zip(ts,ms):pairs.append({'reference':t,'model':m,'center_error_px':float(np.linalg.norm(np.array(t['center'])-m['center']))})
 report['components'][name]={'reference_count':len(ts),'model_count':len(ms),'pairs':pairs}
polys=json.loads((ROOT/'art/blender/laser_front_shapes.json').read_text())['polygons'];fins=Image.new('1',(555,360));draw=ImageDraw.Draw(fins)
for name in ('left_fin','center_fin'):
 pts=polys[name];draw.polygon([(x-18,y-222) for x,y in pts],fill=1)
 if name=='left_fin':draw.polygon([(591-x-18,y-222) for x,y in pts],fill=1)
actual=np.array(Image.open(OUT/'front-fins-mask.png').convert('RGB'))[:,:,0]>128
refparts=components(np.array(fins));modelparts=components(actual)
report['components']['fins']={'reference_kind':'Manually traced original contours','pairs':[{'reference_tip':t['top_tip'],'model_tip':m['top_tip'],'tip_error_px':float(np.linalg.norm(np.array(t['top_tip'])-m['top_tip']))} for t,m in zip(refparts,modelparts)]}
(REPORT/'position-evidence.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
# Review image retains exact pixel registration and source pixels.
model=Image.open(OUT/'front-fixed.png').convert('RGBA');bg=Image.new('RGBA',model.size,'#f8f5ea');bg.alpha_composite(model);ref=Image.fromarray(reference.astype('uint8')).convert('RGBA')
board=Image.new('RGB',(1665,405),'#f8f5ea');d=ImageDraw.Draw(board)
for i,(label,im) in enumerate([('REFERENCE - FRONT',ref),('BLENDER - FRONT',bg),('FIXED 50% OVERLAY',Image.blend(ref,bg,.5))]):
 d.text((i*555+12,12),label,fill='#23303c');board.paste(im.convert('RGB'),(i*555,35))
board.save(REPORT/'front-review.png')
