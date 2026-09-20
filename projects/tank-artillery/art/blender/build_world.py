"""Six themed environment props. Uses the tank pipeline's Blender mesh exporter."""
from pathlib import Path
source=Path(__file__).with_name('build_models.py')
exec(compile(source.read_text().split('models=[]; stats=[]; libraries=[]')[0],str(source),'exec'))
CUTE=True
names=['Meadow','Desert','Snow','Valley','Autumn','Volcano']
for kind in names:
    r=empty('Decor_'+kind)
    if kind=='Meadow':
        sculpt('Cottage',(0,1.05,0),(1.4,1.1,1.15),'Ivory',r,.55)
        cyl('Mushroom roof',(0,2.3,0),1.95,1.10,'Crimson',r,r2=.20,seg=24)
        sculpt('Door',(0,.67,1.15),(.38,.68,.07),'Wood',r,.55)
        for side in (-1,1):
            cyl('Round window',(side*.87,1.25,1.11),.28,.12,'Gold',r,'z')
            cyl('Blue glass',(side*.87,1.25,1.19),.20,.05,'Glass',r,'z')
        for x,z in ((-.7,-.3),(.8,.1),(.2,-.7)):
            ball('Roof spot',(x,2.60,z),(.23,.06,.23),'Ivory',r)
        box('Chimney',(-.75,2.75,-.4),(.36,.9,.40),'Wood',r)
        for side in (-1,1): ball('Garden shrub',(side*1.45,.38,.60),(.52,.45,.58),'Teal',r)
    elif kind=='Desert':
        for i in range(3): box('Temple step',(0,.14+i*.24,0),(3.3-i*.5,.28,3.3-i*.5),'WoodLight',r)
        cyl('Obelisk',(0,2.2,0),.75,3.2,'Ivory',r,r2=.50,seg=4)
        cyl('Golden pyramid',(0,4.02,0),.74,.72,'Gold',r,r2=0,seg=4)
        for y in (1.3,1.85,2.4,2.95):
            box('Engraved rune',(0,y,.53),(.22,.22,.045),'Teal',r,.02,(0,0,45))
        for side in (-1,1):
            cyl('Broken column',(side*1.62,.75,-.7),.35,1.5,'Ivory',r,seg=8)
            cyl('Column capital',(side*1.62,1.5,-.7),.48,.22,'Gold',r,seg=8)
    elif kind=='Snow':
        sculpt('Polar station',(0,1,0),(1.65,1,1.2),'Navy',r,.55)
        sculpt('Snow roof',(0,2,0),(1.88,.36,1.4),'Snow',r,.55)
        box('Station door',(0,.66,1.22),(.72,1.28,.09),'Teal',r)
        for side in (-1,1): box('Warm window',(side*1.03,1.23,1.22),(.55,.48,.10),'Gold',r)
        cyl('Antenna pole',(-1,2.8,-.5),.065,1.3,'Metal',r)
        ball('Antenna dish',(-1,3.48,-.5),(.5,.15,.45),'Ivory',r)
        for side in (-1,1):
            box('Supply crate',(side*1.8,.42,.5),(.68,.78,.75),'Teal',r)
            box('Snow cap',(side*1.8,.85,.5),(.72,.10,.80),'Snow',r)
    elif kind=='Valley':
        for side in (-1,1):
            for i in range(4): sculpt('Ancient pillar',(side*1.25,.42+i*.73,0),(.51,.40,.55),'Edge',r,.48)
            for i in range(4): ball('Moss',(side*(1.18+.11*math.sin(i)),.7+i*.67,.50),(.36,.29,.20),'Teal',r)
        sculpt('Lintel',(0,3.33,0),(1.94,.38,.68),'Edge',r,.50)
        for x in (-.6,0,.6): cyl('River crystal',(x,.58,.4),.22,1.15,'Energy',r,r2=0,seg=5)
        for x in (-1.7,1.8): ball('Mossy boulder',(x,.3,.75),(.62,.37,.49),'Teal',r)
    elif kind=='Autumn':
        cyl('Mill tower',(0,1.5,0),1.02,3,'Ivory',r,r2=.75,seg=12)
        cyl('Terracotta cap',(0,3.4,0),1.30,1.1,'Crimson',r,r2=0,seg=12)
        cyl('Sail hub',(0,2.45,1.12),.26,.35,'Gold',r,'z')
        for a in (25,115,205,295):
            q=math.radians(a)
            beam('Windmill arm',(0,2.45,1.2),(math.cos(q)*2,2.45+math.sin(q)*2,1.2),.15,'Wood',r)
            p=(math.cos(q)*1.3,2.45+math.sin(q)*1.3,1.24)
            box('Canvas sail',p,(1.35,.42,.10),'Ivory',r,.02,(0,0,a))
        for x in (-1.4,1.4):
            ball('Pumpkin',(x,.35,.5),(.47,.38,.43),'Gold',r)
            cyl('Pumpkin stalk',(x,.76,.5),.08,.17,'Wood',r)
    else:
        box('Mining platform',(0,.3,0),(3.5,.6,2.8),'Metal',r)
        for side in (-1,1):
            beam('Derrick leg',(side*1.1,.6,0),(side*.6,3.7,0),.32,'Crimson',r)
            for y in (1.1,1.8,2.5): box('Hazard stripe',(side*.9,y,.18),(.4,.16,.06),'Gold',r,.02)
        beam('Derrick crossbar',(-.72,3.6,0),(.72,3.6,0),.32,'Metal',r)
        beam('Suspension',(0,3.5,0),(0,2,0),.06,'Edge',r)
        cyl('Mining drill',(0,1.2,0),.63,1.7,'Metal',r,r2=0,seg=8)
        for side in (-1,1):
            cyl('Basalt crystal',(side*1.5,.75,.7),.35,1.5,'Hot',r,r2=0,seg=5)
        box('Ore bin',(0,.85,-.95),(1.6,.65,.60),'Navy',r)
    print('WORLD_PROP',kind,bake(r),flush=True)
    export_fbx(r)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'world_props.blend'))
print('WORLD_PROPS_PASS 6',flush=True)
