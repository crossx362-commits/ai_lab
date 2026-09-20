"""Measure actual visible eye silhouettes at the fixed front registration, not overall similarity."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/laser-position-fit'
ref=np.array(Image.open(ROOT/'output/laser-overlay-fit/front-reference.png').convert('RGB')).astype(int)
r,g,b=(ref[:,:,j] for j in range(3));target=(g-r>30)&(b-r>35)&(g>120);target[:190]=False;target[248:]=False
actual=np.array(Image.open(OUT/'front-eye-mask.png').convert('RGB'))[:,:,0]>128
def stats(mask):
 y,x=np.where(mask)
 return {'pixels':int(mask.sum()),'centroid':[float(x.mean()),float(y.mean())],'bounds':[int(x.min()),int(y.min()),int(x.max()),int(y.max())]}
result={'scope':'Only front-view visible eye silhouette, not whole-model acceptance','reference':stats(target),'model':stats(actual),'intersection_over_union':float(np.logical_and(actual,target).sum()/np.logical_or(actual,target).sum())}
result['centroid_distance_pixels']=float(np.linalg.norm(np.array(result['reference']['centroid'])-result['model']['centroid']))
(OUT/'eye-alignment.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
