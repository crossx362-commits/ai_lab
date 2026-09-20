"""Deterministic vector panel-line atlas, traced in the approved FRONT coordinates.
Transparent technical markings only: no painted-on eye, shading or silhouette.
"""
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
S=4
im=Image.new('RGBA',(600*S,600*S));d=ImageDraw.Draw(im)
def line(points,width=1,color=(56,28,78,190)):
    d.line([(round(x*S),round(y*S)) for x,y in points],fill=color,width=max(1,round(width*S)),joint='curve')
def pair(points,width=1,color=(56,28,78,190)):
    line(points,width,color);line([(591-x,y) for x,y in points],width,color)
# Continuous machined rims and sharp center fold of the chest.
pair([(183,431),(219,452),(254,474),(295.5,489)],1,(193,143,223,200))
pair([(177,466),(212,490),(253,521),(295.5,549)],1.1,(202,147,229,195))
line([(295,488),(295,550)],1,(51,22,76,210))
line([(297,488),(297,548)],.8,(223,174,246,190))
# Stepped panel breaks follow the source; they terminate at the armor rim.
for path in [[(219,452),(218,458),(222,461),(222,480),(226,487),(227,504)],
             [(251,479),(241,479),(239,482),(239,489),(242,493),(243,514)],
             [(197,458),(201,462),(202,478),(207,481),(208,490)],
             [(266,504),(273,506),(280,505)]]:
    pair(path,.9,(61,30,84,165))
# Helmet inset panel seams; shoulder plates and fin center bevels.
for path in [[(240,354),(247,378),(254,404),(278,409)],
             [(269,345),(269,355)],[(211,370),(219,379),(215,384),(209,377),(211,370)],
             [(174,371),(157,386),(136,412),(118,433),(91,444)],
             [(147,382),(132,391),(119,403)],[(79,446),(92,434),(103,422)],
             [(128,296),(153,312),(173,332),(183,349)],
             [(280,276),(275,311),(270,337)],
             [(155,443),(133,464),(121,466),(100,490),(99,496),(65,531),(65,538),(42,561)]]:
    pair(path,.85)
line([(296,238),(296,323)],.85,(202,155,227,180))
# Small hatch fasteners, matching the original small outlined lozenges.
for x,y in [(160,443),(101,418),(126,394)]:
    pair([(x-3,y),(x,y-2),(x+3,y),(x,y+2),(x-3,y)],.9)
for path in [[(203,465),(214,470)],[(252,489),(261,493)],[(180,447),(187,451)]]:
    pair(path,.7,(198,148,225,100))
path=ROOT/'art/textures/LaserArmorDecal.png';im.save(path)
out=ROOT/'unity/Assets/_Project/Resources/Art/LaserArmorDecal.png';out.write_bytes(path.read_bytes())
print('LASER_DECAL_ATLAS',path)
