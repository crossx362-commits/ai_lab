from pathlib import Path
from PIL import Image,ImageDraw
import json,html
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/roster-approved-method';reg=json.loads((OUT/'fixed-registration.json').read_text());rows=[]
for kind,views in reg.items():
 folder=OUT/'fixed-review'/kind;source=Image.open(ROOT/'art/concepts/orthographic-v1'/f'{kind}.png').convert('RGBA')
 board=Image.new('RGB',(1920,2040),(248,245,235));d=ImageDraw.Draw(board)
 for index,view in enumerate(['front','side','top']):
  rec=views[view];x,y,w,h=rec['reference_crop'];crop=source.crop((x,y,x+w,y+h));scale=640*rec['units_per_reference_pixel']/rec['ortho_scale'];size=(round(w*scale),round(h*scale));crop=crop.resize(size,Image.Resampling.LANCZOS)
  ref=Image.new('RGBA',(640,640),(248,245,235,255));ref.alpha_composite(crop,((640-size[0])//2,(640-size[1])//2));ref.save(folder/(view+'-reference.png'))
  model=Image.new('RGBA',(640,640),(248,245,235,255));model.alpha_composite(Image.open(folder/(view+'.png')).convert('RGBA'));model.save(folder/(view+'-model.png'))
  overlay=Image.blend(ref,model,.5);overlay.save(folder/(view+'-overlay.png'))
  for col,(label,im) in enumerate([('REFERENCE',ref),('BLENDER',model),('50% OVERLAY',overlay)]):
   d.text((col*640+12,index*680+12),kind+' / '+view.upper()+' / '+label,fill=(20,30,40));board.paste(im.convert('RGB'),(col*640,index*680+36))
 board.save(folder/'comparison.png')
 rows.append(kind)
# Fixed-size roster contact board, same panel size for all twelve candidates.
board=Image.new('RGB',(1600,1240),(248,245,235));d=ImageDraw.Draw(board)
for i,kind in enumerate(rows):
 x=(i%4)*400;y=(i//4)*410;d.text((x+10,y+10),kind+'  REF / MODEL',fill=(20,30,40))
 for j,mode in enumerate(['reference','model']):
  im=Image.open(OUT/'fixed-review'/kind/('front-'+mode+'.png'));im.thumbnail((200,360));board.paste(im,(x+j*200,y+45+(360-im.height)//2))
board.save(OUT/'roster-front-comparison.png')
options=''.join(f'<option>{html.escape(k)}</option>' for k in rows)
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>탱크 원화 · 모델 검토</title><style>
body{margin:0;background:#202332;color:#f7f0de;font:16px system-ui}header{padding:20px 28px;position:sticky;top:0;background:#202332ee}h1{font-size:23px;margin:0 0 12px}select,button{font:inherit;padding:8px 14px;border:0;border-radius:9px;background:#eee5d4;color:#262337;margin-right:8px}small{color:#c7c4d1}main{padding:12px;display:grid;grid-template-columns:repeat(3,1fr);gap:12px}figure{margin:0}figure img{width:100%;background:#f8f5eb}figcaption{text-align:center;padding:8px}input{vertical-align:middle} .overlay{position:relative}.overlay img+img{position:absolute;top:0;left:0}a{color:#d7c5ff}footer{padding:20px}
</style><header><h1>원화와 입체 모델 비교</h1><select id="kind">OPTIONS</select><select id="view"><option value="front">정면</option><option value="side">측면</option><option value="top">상면</option></select><label>겹침 <input id="alpha" type="range" min="0" max="100" value="50"></label><p><small>모델 13종 · 코드 0종 | 레이저 형태: 오너 합격 | 나머지 12종: 적용 후 검토용<br>카메라는 수정 전 모델에서 고정했습니다. 수정한 메시 외곽에 맞춰 다시 확대·축소하지 않습니다.</small></p></header><main><figure><figcaption>원화</figcaption><img id="ref"></figure><figure><figcaption>Blender 모델</figcaption><img id="model"></figure><figure><figcaption>겹쳐 보기</figcaption><div class="overlay"><img id="under"><img id="over"></div></figure></main><footer><a href="roster-front-comparison.png">12종 정면 비교판</a> · <a href="../../art/blender/roster_reference_workbench.blend">Blender 작업 파일</a></footer><script>
const k=document.querySelector('#kind'),v=document.querySelector('#view'),a=document.querySelector('#alpha');function update(){const base='fixed-review/'+k.value+'/'+v.value;ref.src=under.src=base+'-reference.png';model.src=over.src=base+'-model.png';over.style.opacity=a.value/100;history.replaceState(null,'','#'+k.value)}k.value=decodeURIComponent(location.hash.slice(1))||k.options[0].value;k.onchange=v.onchange=a.oninput=update;update();</script></html>'''.replace('OPTIONS',options)
(OUT/'index.html').write_text(page)
print('ROSTER_COMPARISON_BOARDS',len(rows))
