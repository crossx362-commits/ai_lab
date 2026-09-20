"""Original three-view pixels vs actual Blender renders; no aspect-ratio distortion."""
from pathlib import Path
import json,sys,hashlib
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
kind=sys.argv[1]
folder=ROOT/'output/orthographic-review'/kind
reference=ROOT/'art/concepts/orthographic-v1'/f'{kind}.png'
art=Image.open(reference).convert('RGBA')
boxes=json.loads((reference.parent/'reference-crops.json').read_text())[kind]['boxes']
W,H=500,440
board=Image.new('RGB',(W*3,H*3+60),'#f8f5ea');draw=ImageDraw.Draw(board)
records=[]
for i,view in enumerate(('front','side','top')):
 x,y,w,h=boxes[i];ref=art.crop((x,y,x+w,y+h))
 model_path=folder/f'{view}.png';model=Image.open(model_path).convert('RGBA');bbox=model.getchannel('A').getbbox();model=model.crop(bbox)
 layers=[];transforms=[]
 for source in (ref,model):
  scale=min((W-32)/source.width,(H-60)/source.height)
  size=(round(source.width*scale),round(source.height*scale))
  source=source.resize(size,Image.Resampling.LANCZOS)
  canvas=Image.new('RGBA',(W,H-36),'#f8f5ea');offset=((W-size[0])//2,(H-36-size[1])//2)
  canvas.alpha_composite(source,offset);layers.append(canvas);transforms.append({'uniform_scale':scale,'offset':offset})
 for row,layer in enumerate((layers[0],layers[1],Image.blend(layers[0],layers[1],.5))):
  label=('REFERENCE','BLENDER MODEL','50% OVERLAY')[row]
  draw.text((i*W+16,row*H+16),f'{view.upper()} / {label}',fill='#172633')
  board.paste(layer.convert('RGB'),(i*W,row*H+36))
 records.append({'view':view,'reference_crop':boxes[i],'model_alpha_crop':bbox,'transforms':transforms,'render_sha256':hashlib.sha256(model_path.read_bytes()).hexdigest()})
draw.text((16,H*3+12),'Uniform fit only. Source pixels unchanged. No shape-match percentage asserted.',fill='#172633')
board.save(folder/'three-view-comparison.png')
(folder/'three-view-comparison.json').write_text(json.dumps({'reference':str(reference),'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'views':records},indent=2))
print(folder/'three-view-comparison.png')
