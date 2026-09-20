"""Editable technical vector markings for the concept tank family.
No source-image pixels, eyes, silhouettes or pre-painted lighting are baked here.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math
ROOT=Path(__file__).resolve().parents[2]
KINDS=['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','IonAttacker','Poseidon','SecWind']
PALETTES={
 'Catapult':('#573622','#d7aa63'), 'CrossBow':('#15504d','#9ecd99'),
 'Cannon':('#152539','#b0a18a'), 'Carrot':('#a65319','#ffd18a'),
 'Duke':('#354522','#c8d481'), 'MineLander':('#695424','#eed297'),
 'Missile':('#84332d','#ffe6c7'), 'MultiMissile':('#1a555c','#a8e3d2'),
 'SuperTank':('#866024','#ffe7a4'), 'IonAttacker':('#813c66','#ffd1e4'),
 'Poseidon':('#174f86','#b4e0f1'), 'SecWind':('#4c6a31','#dfeab0')}
def make(kind):
 im=Image.new('RGBA',(2048,2048));d=ImageDraw.Draw(im)
 ink,shine=PALETTES[kind]
 def point(tile,p):return (int((tile%4)*512+32+p[0]*448),int((tile//4)*512+32+p[1]*448))
 def line(tile,points,color=ink,width=3):d.line([point(tile,p) for p in points],fill=color,width=width,joint='curve')
 def ellipse(tile,box,color,width=3,fill=None):d.ellipse([point(tile,box[:2]),point(tile,box[2:])],outline=color,width=width,fill=fill)
 def poly(tile,points,color):d.polygon([point(tile,p) for p in points],fill=color)
 # 0: inset armor plate and restrained metal rivets.
 border=[(.07,.17),(.17,.07),(.83,.07),(.93,.17),(.93,.83),(.83,.93),(.17,.93),(.07,.83),(.07,.17)]
 line(0,border,ink,4);line(0,[(x+.009,y+.013) for x,y in border],shine,2)
 for x,y in [( .18,.18),(.82,.18),(.18,.82),(.82,.82)]:ellipse(0,(x-.017,y-.017,x+.017,y+.017),ink,2,shine)
 # 1: irregular stepped panel joints, matching handmade armor language.
 for x in [.26,.73]:line(1,[(x,.02),(x,.29),(x+.03,.33),(x+.03,.58),(x-.025,.63),(x-.025,.98)],ink,3)
 line(1,[(.03,.76),(.26,.76),(.3,.79),(.73,.79),(.79,.76),(.97,.76)],ink,3)
 # 2: broad wood grain and a knot, not a photograph of wood.
 for x in [.14,.34,.68,.85]:
  line(2,[(x+.009*math.sin(j*.6+x*10),j/35) for j in range(36)],ink,2)
 for rx,ry in [(.058,.13),(.035,.083),(.013,.035)]:ellipse(2,(.5-rx,.5-ry,.5+rx,.5+ry),ink,2)
 # 3: leaf midrib with tapered branching veins.
 line(3,[(.5,.04),(.48,.35),(.52,.68),(.5,.97)],shine,4)
 for j in range(1,6):
  y=.13+j*.115;w=.32*math.sin(y*math.pi)
  for side in [-1,1]:line(3,[(.5,y+.06),(.5+side*w*.58,y-.02),(.5+side*w,y-.12)],ink,2)
 # 4: cream skull and crossed bones for the navy captain.
 for side in [-1,1]:
  line(4,[(.5-side*.29,.74),(.5+side*.29,.94)],'#e6dabc',24)
  for x,y in [(.5-side*.29,.74),(.5+side*.29,.94)]:ellipse(4,(x-.037,y-.037,x+.037,y+.037),'#e6dabc',1,'#e6dabc')
 poly(4,[(.25,.31),(.30,.15),(.42,.10),(.60,.10),(.72,.18),(.76,.34),(.71,.51),(.64,.56),(.63,.69),(.38,.69),(.37,.55),(.29,.50)],'#eee1c5')
 for x in [.4,.61]:ellipse(4,(x-.067,.31,x+.067,.46),'#1a2538',1,'#1a2538')
 poly(4,[(.50,.44),(.455,.53),(.55,.53)],'#1a2538')
 for x in [.44,.51,.58]:line(4,[(x,.6),(x,.69)],'#1a2538',5)
 # 5: toxin warning sign and bubbles, retained on the bottle/front armor.
 poly(5,[(.5,.10),(.93,.88),(.07,.88)],'#d8e449');poly(5,[(.5,.18),(.86,.84),(.14,.84)],'#253822')
 ellipse(5,(.46,.59,.54,.67),'#d8e449',1,'#d8e449')
 for angle in [90,210,330]:
  q=math.radians(angle);poly(5,[(.5+math.cos(q-.42)*.12,.63+math.sin(q-.42)*.12),(.5+math.cos(q-.5)*.25,.63+math.sin(q-.5)*.25),(.5+math.cos(q+.5)*.25,.63+math.sin(q+.5)*.25),(.5+math.cos(q+.42)*.12,.63+math.sin(q+.42)*.12)],'#d8e449')
 # 6: turtle shell scutes.
 for row in range(3):
  for col in range(3):
   cx=.18+col*.32+(row%2)*.16;cy=.17+row*.30
   pts=[(cx+.185*math.cos(j*math.pi/3),cy+.175*math.sin(j*math.pi/3)) for j in range(7)]
   line(6,pts,ink,4)
 # 7: royal insignia, reserved for rear/top readable areas.
 poly(7,[(.15,.28),(.32,.48),(.5,.16),(.68,.48),(.86,.28),(.78,.77),(.23,.77)],'#f2cd67')
 line(7,[(.24,.68),(.78,.68)],ink,4)
 ellipse(7,(.445,.46,.555,.60),'#923c34',1,'#a64839')
 # 8: sparse inset inspection hatch.
 line(8,[(.15,.3),(.25,.17),(.8,.17),(.86,.78),(.22,.83),(.15,.3)],ink,4)
 line(8,[(.31,.42),(.66,.42)],shine,3)
 # 9: rocket/root segmented transverse bands.
 for y in [.22,.63,.82]:line(9,[(.04,y),(.28,y+.025),(.5,y+.033),(.72,y+.025),(.96,y)],ink,3)
 for x in [.22,.78]:line(9,[(x,.03),(x,.95)],ink,2)
 # 10: orbit concentric casing trims and tiny fasteners.
 for r in [.32,.41,.46]:ellipse(10,(.5-r,.5-r,.5+r,.5+r),shine if r==.41 else ink,3)
 for j in range(8):
  q=j*math.pi/4;x=.5+.385*math.cos(q);y=.5+.385*math.sin(q)
  ellipse(10,(x-.012,y-.012,x+.012,y+.012),ink,2,shine)
 # 11: feather quill, branches do not replace feather geometry.
 line(11,[(.5,.07),(.48,.43),(.50,.95)],ink,3)
 for j in range(7):
  y=.19+j*.1;w=.29*math.sin(y*math.pi)
  for s in [-1,1]:line(11,[(.5,y),(.5+s*w,y-.1)],shine,2)
 # 12: whale dorsal water crest.
 for y in [.4,.6]:line(12,[(j/40,y+.065*math.sin(j/40*math.pi*4)) for j in range(41)],shine,8)
 poly(12,[(.5,.03),(.40,.18),(.39,.25),(.44,.30),(.52,.31),(.58,.25),(.59,.19)],shine)
 # 13: safety striping confined to an inset narrow lower edge.
 for j in range(7):
  x=j*.15;poly(13,[(x,.32),(x+.06,.32),(x+.16,.68),(x+.10,.68)],'#e0bc65')
 # 14: front tread seams on a wide rubber strip.
 for y in [.14,.32,.50,.68,.86]:
  line(14,[(.03,y),(.29,y),(.34,y+.045),(.66,y+.045),(.71,y),(.97,y)],'#273139',7)
  line(14,[(.05,y+.06),(.25,y+.06)],'#9ba6a1',2)
 # 15: little mechanical panel scratches.
 for path in [[(.15,.25),(.34,.28)],[ (.64,.71),(.83,.66)],[(.21,.77),(.29,.73)]]:line(15,path,shine,2)
 art=ROOT/'art/textures'/f'{kind}ArmorDecal.png';im.save(art)
 target=ROOT/'unity/Assets/_Project/Resources/Art'/art.name;target.write_bytes(art.read_bytes())
 return art
if __name__=='__main__':
 for kind in KINDS:print(make(kind))
