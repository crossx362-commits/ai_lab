"""Concept-led character sculpts. See art/concepts/tankfall-character-sheet.png.
Preserves gameplay rig, animated wheels and muzzle anchors; replaces common hulls.
"""
import bpy, math

def apply_design(k, root, turret, barrel, shape, by, fz, sculpt, ball, box, cyl, torus, beam):
    bl,bw,bh,tw,th,tr,tuh,L,*_=shape
    # Keep the running gear, team marks and rig. Remove the old shared car bodies.
    for o in list(root.children):
        if o.type=='MESH' and not o.name.startswith(('Continuous rubber belt','Wide tread pad','Side team badge','Badge rivet')):
            bpy.data.objects.remove(o,do_unlink=True)
    for o in list(turret.children):
        if o.type=='MESH' and o.name.startswith(('Turret shell','Turret cream cap','Hatch','Crown fin','Satellite stalk','Satellite dish','Dish antenna')):
            bpy.data.objects.remove(o,do_unlink=True)
    # Visible load-bearing necks connect the elevated gameplay pivot to each hull.
    if k not in ('Catapult','CrossBow','Cannon','IonAttacker'):
        neck_mat='Navy' if k=='Missile' else 'Body'
        sculpt('Weapon shoulder',(0,by+bh*.94,-bl*.08),(tr*.69,.46,tr*.56),neck_mat,root,.8)
        sculpt('Weapon cheek',(0,tuh*.27,-.13),(tr*.66,tuh*.52,tr*.54),neck_mat,turret,.86)
    def eye(parent,x,y,z,s=.46,lid=None):
        ball('Character eye socket',(x,y,z),(s*1.13,s*1.22,s*.55),lid or 'Body',parent)
        ball('Character eye white',(x,y,z+s*.34),(s,s*1.1,s*.35),'EyeWhite',parent)
        ball('Character pupil',(x+s*.21,y-.015,z+s*.64),(s*.44,s*.70,s*.13),'Navy',parent)
        ball('Character catchlight',(x+s*.07,y+s*.30,z+s*.76),(s*.15,s*.19,s*.05),'EyeWhite',parent)
    def smile(y,z,width=.38):
        sculpt('Character smile',(0,y,z),(width,.055,.07),'Navy',root,.8)
    def guard(mat='Body'):
        for s in (-1,1): sculpt('Shoulder fender',(s*bw*.59,by+.62,0),(.36,.25,bl*.48),mat,root,.5)
    if k=='Catapult':
        sculpt('Barrel wooden body',(0,by+.65,0),(bw*.50,.84,bl*.46),'WoodLight',root,.85)
        for z in (-bl*.31,bl*.32):
            torus('Barrel iron band',(0,by+.65,z),bw*.48,.075,'Metal',root)
        for x in (-.65,0,.65):
            box('Barrel plank seam',(x,by+.61,bl*.455),(.035,1.17,.025),'Wood',root,.005)
        for s in (-1,1): eye(root,s*bw*.28,by+.84,bl*.43,.45,'WoodLight')
        smile(by+.16,bl*.47)
    elif k=='CrossBow':
        sculpt('Beetle belly',(0,by+.45,0),(bw*.50,.58,bl*.51),'Wood',root)
        for s in (-1,1):
            leaf=sculpt('Beetle leaf shell',(s*bw*.24,by+.83,-bl*.10),(bw*.36,.43,bl*.49),'Teal',root,.92)
            leaf.rotation_euler[1]=math.radians(s*14)
            beam('Leaf ridge',(s*.14,by+1.23,-bl*.48),(s*bw*.46,by+.76,bl*.26),.065,'Gold',root)
            eye(root,s*bw*.28,by+.76,bl*.46,.43,'Teal')
        smile(by+.12,bl*.49)
    elif k=='Cannon':
        sculpt('Pirate carriage',(0,by+.25,0),(bw*.47,.31,bl*.43),'WoodLight',root,.48)
        ball('Round bombard body',(0,-.02,.25),(1.10,1.07,1.13),'Navy',barrel)
        for s in (-1,1): eye(barrel,s*.98,.20,.74,.52,'Navy')
        box('Pirate brow',(0,.99,.24),(1.35,.17,.78),'Navy',barrel,.05)
        cyl('Captain button',(0,1.12,.18),.18,.12,'Gold',barrel)
    elif k=='Carrot':
        sculpt('Carrot heart hull',(0,by+.64,0),(bw*.52,bh*.73,bl*.44),'Body',root,.94)
        guard('Gold')
        ball('Orange turret',(0,tuh*.26,-.15),(tr*.92,tuh*.70,tr*.76),'Body',turret)
        for s in (-1,1):
            eye(turret,s*tr*.68,tuh*.38,tr*.66,.50)
            brow=box('Determined brow',(s*tr*.68,tuh*.38+.49,tr*.73),(.70,.14,.21),'Body',turret)
            brow.rotation_euler[1]=math.radians(s*17)
        for s in (-1,0,1):
            leaf=sculpt('Broad carrot leaf',(s*.34,tuh+ .40,-.38),(.23,.62,.19),'Teal',turret,.95)
            leaf.rotation_euler[1]=math.radians(s*28)
        sculpt('Carrot mouth',(0,tuh*.08,tr*.79),(.20,.045,.055),'Navy',turret)
    elif k=='Duke':
        sculpt('Frog armored belly',(0,by+.68,0),(bw*.62,bh*.68,bl*.48),'Body',root,.9)
        sculpt('Frog broad snout',(0,by+.95,bl*.39),(bw*.53,.42,.46),'Body',root,.78)
        guard()
        for s in (-1,1):
            eye(root,s*bw*.34,by+bh*1.23,bl*.35,.49)
            ball('Frog nostril',(s*.29,by+1.16,bl*.39+.43),(.06,.045,.035),'Navy',root)
        smile(by+.78,bl*.39+.46,.67)
    elif k=='MineLander':
        sculpt('Mole armor',(0,by+.60,-.15),(bw*.55,bh*.72,bl*.48),'Body',root,.61)
        guard('Gold')
        for s in (-1,1):
            cyl('Mole goggle rim',(s*.56,by+bh*1.03,bl*.38),.44,.24,'Metal',root,'z')
            eye(root,s*.56,by+bh*1.03,bl*.38+.13,.31,'Gold')
        box('Broad excavator scoop',(0,by+.06,bl*.55),(bw*1.22,.95,.46),'Metal',root,.12,(-18,0,0))
        for x in (-1.1,-.55,0,.55,1.1): box('Scoop tooth',(x,.21,bl*.65),(.26,.25,.62),'Ivory',root)
    elif k=='Missile':
        for wheel in root.children:
            if wheel.name.startswith('Wheel'):
                wheel.scale*=.72
                wheel.location.x*=.83; wheel.location.y*=.76; wheel.location.z*=.72
        sculpt('Compact rocket chassis',(0,by+.11,-.15),(bw*.43,.32,bl*.37),'Navy',root,.5)
        for side in (-1,1):
            sculpt('Rocket caterpillar belt',(side*bw*.45,.48,-.08),(.34,.40,bl*.42),'Rubber',root,.49)
        for o in list(barrel.children):
            if o.type=='MESH' and o.name.startswith(('Pod armor','Rail guide','Single launch tube')): bpy.data.objects.remove(o,do_unlink=True)
        sculpt('Oversized rocket body',(0,0,fz*.40),(.96,.90,fz*.45),'Rocket',barrel,.95)
        for s in (-1,1):
            eye(barrel,s*.65,.53,fz*.56,.48,'Rocket')
            fin=box('Rocket stabilizer',(s*.90,-.13,.18),(.72,.15,1.12),'Ivory',barrel,.05,(0,s*25,0))
        box('Rocket dorsal fin',(0,.70,.12),(.15,.90,.95),'Ivory',barrel,.05,(-20,0,0))
    elif k=='MultiMissile':
        sculpt('Turtle shell',(0,by+.64,-.15),(bw*.59,.77,bl*.50),'Teal',root,.95)
        sculpt('Turtle cream jaw',(0,by+.51,bl*.43),(bw*.39,.30,.74),'Ivory',root,.92)
        sculpt('Turtle head',(0,by+.85,bl*.44),(bw*.39,.50,.71),'Body',root,.98)
        for s in (-1,1): eye(root,s*bw*.27,by+1.13,bl*.57,.39)
        smile(by+.57,bl*.44+.71,.45)
    elif k=='SuperTank':
        sculpt('Lion citadel',(0,by+.72,0),(bw*.58,bh*.67,bl*.49),'Body',root,.54)
        guard('Gold')
        for s in (-1,1):
            for i in range(3):
                sculpt('Lion mane plate',(s*(bw*.40+.12*i),by+bh*(1.14-.21*i),bl*.29-.12*i),(.38,.47,.55),'Crimson',root,.64)
            eye(turret,s*.76,.56,.69,.45,'Gold')
            sculpt('Lion muzzle cheek',(s*.29,.16,.83),(.38,.26,.26),'Ivory',turret,.9)
        ball('Lion nose',(0,.40,.99),(.21,.14,.14),'Navy',turret)
        sculpt('Lion forehead',(0,.82,.17),(.89,.61,.77),'Gold',turret,.72)
        for x,h in ((-.4,.4),(0,.66),(.4,.4)):
            cyl('Crown point',(x,1.43+h*.5,-.10),.16,h,'Gold',turret,r2=.04)
        box('Crown band',(0,1.43,-.10),(1.18,.18,.55),'Gold',turret)
    elif k=='Laser':
        sculpt('Manta center',(0,by+.45,0),(bw*.52,.53,bl*.55),'Body',root,.9)
        sculpt('Manta mask',(0,by+.54,bl*.48),(bw*.41,.20,.15),'Navy',root,.6)
        sculpt('Cyan slit',(0,by+.54,bl*.48+.12),(bw*.32,.085,.045),'Energy',root,.6)
        for s in (-1,1):
            wing=sculpt('Manta swept wing',(s*bw*.59,by+.21,-.12),(bw*.41,.15,bl*.48),'Plum',root,.75)
            wing.rotation_euler[2]=math.radians(s*24)
            box('Manta dorsal blade',(s*.45,by+1.02,-bl*.32),(.14,.95,1.35),'Plum',root,.04,(-25,0,s*20))
            torus('Hover disc',(s*bw*.43,.50,-.25),.48,.13,'Energy',root,'y')
    elif k=='IonAttacker':
        ball('Alien orb',(0,by+.67,0),(bw*.54,bh*1.10,bl*.43),'Body',root)
        torus('Alien gold orbit',(0,by+.22,0),bw*.75,.13,'Gold',root,'y')
        ball('Cyclops dark socket',(0,by+.77,bl*.39),(.83,.86,.23),'Navy',root)
        ball('Cyclops cyan eye',(0,by+.77,bl*.39+.14),(.65,.69,.16),'Energy',root)
        ball('Cyclops vertical pupil',(.09,by+.77,bl*.39+.28),(.16,.49,.055),'Navy',root)
        ball('Cyclops glint',(-.19,by+1.10,bl*.39+.31),(.12,.15,.04),'EyeWhite',root)
        for s in (-1,1):
            for z in (-bl*.25,bl*.23):
                ball('Orb satellite',(s*bw*.64,by+.9,z),(.30,.30,.30),'Gold',root)
                ball('Satellite light',(s*bw*.64,by+.9,z+.23),(.18,.18,.1),'Energy',root)
    elif k=='Poseidon':
        ball('Whale body',(0,by+.54,0),(bw*.65,bh*.91,bl*.58),'Body',root)
        sculpt('Whale cream belly',(0,by+.03,bl*.24),(bw*.57,.52,bl*.44),'Ivory',root,.95)
        for s in (-1,1):
            eye(root,s*bw*.41,by+.95,bl*.43,.45)
            fin=sculpt('Whale flipper',(s*bw*.64,by+.04,-.12),(.61,.15,.88),'Teal',root,.94)
            fin.rotation_euler[2]=math.radians(s*30)
            tail=sculpt('Whale tail fluke',(s*.56,by+1.48,-bl*.64),(.70,.16,.55),'Body',root,.95)
            tail.rotation_euler[1]=math.radians(s*26)
        beam('Whale tail stem',(0,by+.48,-bl*.44),(0,by+1.47,-bl*.64),.41,'Body',root)
        box('Whale dorsal',(0,by+bh*1.35,-bl*.32),(.16,1.24,.94),'Teal',root,.05,(-23,0,0))
        smile(by+.41,bl*.56,.53)
    else:
        ball('Bird body',(0,by+.54,0),(bw*.52,bh*.90,bl*.48),'Body',root)
        for s in (-1,1):
            eye(root,s*bw*.30,by+.95,bl*.40,.45)
            for i in range(3):
                feather=sculpt('Broad flight feather',(s*(bw*.50+.38*i),by+.50+.19*i,-.30-.22*i),(.45,.14,bl*.48),'Ivory' if i%2 else 'Body',root,.96)
                feather.rotation_euler[2]=math.radians(s*(22+8*i))
        cyl('Cream beak',(0,by+.53,bl*.53),.46,.90,'Ivory',root,'z',.03,seg=8)
        for s in (-1,0,1):
            plume=sculpt('Bird crest',(s*.25,by+bh*1.50,-bl*.23),(.16,.56,.28),'Teal',root,.96)
            plume.rotation_euler[1]=math.radians(s*25)
