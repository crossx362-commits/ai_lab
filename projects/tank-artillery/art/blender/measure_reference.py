"""Measure reference silhouettes without changing artwork or distorting proportions."""
from pathlib import Path
from PIL import Image,ImageFilter
import json,collections
ROOT=Path(__file__).resolve().parents[2]
result={}
for path in sorted((ROOT/'art/concepts/orthographic-v1').glob('*.png')):
 im=Image.open(path).convert('RGB');w,h=im.size
 # Detect drawn column separators. All panels retain their original image pixels.
 small=im.resize((w//3,h//3));sw,sh=small.size;pix=small.load();cuts=[0]
 for target in (1/3,2/3):
  scores=[(sum(max(pix[x,y])<150 for y in range(4,sh-4)),x) for x in range(int(sw*(target-.10)),int(sw*(target+.10)))]
  score,x=max(scores);cuts.append(x*3 if score>sh*.6 else int(w*target))
 cuts.append(w);boxes=[]
 for n in range(3):
  left=cuts[n]+(12 if n==0 else 8);right=cuts[n+1]-(20 if n==2 else 8)
  panel=im.crop((left,12,right,h-12));p=panel.resize((panel.width//3,panel.height//3));pw,ph=p.size
  mask=Image.new('L',p.size);mask.putdata([255 if min(c)<175 else 0 for c in p.getdata()]);mask=mask.filter(ImageFilter.MaxFilter(5));raw=bytearray(mask.tobytes());best=[]
  for i in range(len(raw)):
   if not raw[i]:continue
   raw[i]=0;queue=[i];component=[]
   while queue:
    j=queue.pop();component.append(j);x=j%pw;y=j//pw
    for k in ([j-1] if x else [])+([j+1] if x<pw-1 else [])+([j-pw] if y else [])+([j+pw] if y<ph-1 else []):
     if raw[k]:raw[k]=0;queue.append(k)
   if len(component)>len(best):best=component
  x0=min(v%pw for v in best);x1=max(v%pw for v in best);y0=min(v//pw for v in best);y1=max(v//pw for v in best)
  boxes.append([left+x0*3,12+y0*3,(x1-x0+1)*3,(y1-y0+1)*3])
 f,s,t=boxes;rf=f[2]/f[3];rs=s[2]/s[3];rt=t[2]/t[3]
 result[path.stem]={'boxes':boxes,'front_width_height':rf,'side_length_height':rs,'top_width_length':rt,'projection_closure_ratio':rt/(rf/rs),'note':'Silhouette bounds only; not a similarity score. A consistent orthographic sheet has closure ratio near 1, except pose/occlusion differences.'}
(ROOT/'art/concepts/orthographic-v1/reference-crops.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:round(v['projection_closure_ratio'],3) for k,v in result.items()},indent=2))
