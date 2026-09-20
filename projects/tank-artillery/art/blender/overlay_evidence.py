"""Composite untouched concept pixels with true alpha renders for headless review."""
from pathlib import Path
from PIL import Image,ImageDraw
import json
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/orthographic-review'
crops=json.loads((ROOT/'art/concepts/orthographic-v1/reference-crops.json').read_text())
for kind,row in crops.items():
 art=Image.open(ROOT/'art/concepts/orthographic-v1'/f'{kind}.png').convert('RGBA')
 board=Image.new('RGB',(1800,650),'#f8f5ea');draw=ImageDraw.Draw(board)
 for j,view in enumerate(('front','side','top')):
  x,y,w,h=row['boxes'][j];ref=art.crop((x,y,x+w,y+h));model=Image.open(OUT/kind/f'{view}.png').convert('RGBA');bb=model.getchannel('A').getbbox();model=model.crop(bb)
  # Independent uniform normalisation removes camera framing, NEVER changes aspect ratios.
  layers=[]
  for source in (ref,model):
   source.thumbnail((530,530),Image.Resampling.LANCZOS);canvas=Image.new('RGBA',(600,600),(248,245,234,255));canvas.alpha_composite(source,((600-source.width)//2,(600-source.height)//2));layers.append(canvas)
  composite=Image.blend(*layers,.5);board.paste(composite,(j*600,50));draw.text((j*600+20,16),kind+' / '+view+' / 50% model',fill='#243447')
 board.save(OUT/kind/'overlay.png')
print('OVERLAY_EVIDENCE_SAVED 13')
