"""Original SVG selection artwork, authored for Let me sleep. No external assets."""
from pathlib import Path

ROOT = Path(__file__).resolve().parent
INK, SKIN, MAIN, ACCENT = "#232532", "#efb28e", "#78bbc2", "#e0b966"
PALETTE = ["#ef9479", "#78bbc2", "#b3bb79", "#be9ccc", "#e0b966", "#8ca6d0"]

def path(d, fill="none", stroke=INK, width=4):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>'

def ellipse(x,y,rx,ry,fill,stroke=INK,width=4):
    return f'<ellipse cx="{x}" cy="{y}" rx="{rx}" ry="{ry}" fill="{fill}" stroke="{stroke}" stroke-width="{width}"/>'

def face(role, variant=0):
    if role == "human":
        art = path("M49 26 Q78 9 109 26 L110 66 Q108 91 81 99 Q51 93 48 67 Z",SKIN)
        art += path("M54 41 Q61 36 68 40 M88 39 Q98 35 104 42",width=5)
        if variant == 1:
            art += path("M55 53 Q62 57 69 53 M88 53 Q96 57 104 53")
        else:
            art += ellipse(63,54,3,5,INK,width=0)+ellipse(96,54,3,5,INK,width=0)
        if variant == 2:
            art += path("M54 37 L70 42 M87 42 L104 37",width=5)
        art += path("M79 54 L76 69 L83 69 M67 80 Q79 85 91 80")
    else:
        art = ellipse(80,65,32,28,MAIN)
        art += ellipse(63,52,19,22,"#fff6df")+ellipse(97,52,19,22,"#fff6df")
        art += ellipse(65,54,5,8,INK,width=0)+ellipse(95,54,5,8,INK,width=0)
        if variant == 1: art += path("M46 29 L65 34 M95 34 L113 27",width=5)
        if variant == 2: art += path("M46 48 L80 48 M80 48 L113 48",width=5)
        art += path("M80 76 L78 105 L86 79",ACCENT,width=3)
        art += path("M70 87 Q76 91 82 87",width=2)
    return art

def hair(role, variant):
    if role == "human":
        art=face(role,1)
        choices=["M47 45 L47 24 Q68 6 106 22 L113 42 L97 29 L82 31 L65 26 L54 45 Z",
                 "M48 41 L48 24 Q68 12 89 24 Q77 11 79 4 Q105 6 109 28 L111 43 L92 31 L70 29 Z",
                 "M45 39 Q32 20 49 19 Q45 2 66 10 Q81 -1 89 12 Q110 2 111 22 Q129 27 113 44 Q98 31 86 35 Q70 24 57 42 Z"]
        return art+path(choices[variant],"#645064")
    art=face(role,0)
    choices=["M64 30 L57 8 M96 30 L103 8", "M63 30 Q37 8 55 7 M97 30 Q123 8 105 7", "M63 30 L50 8 M97 30 L110 8 M57 20 L43 19 M55 15 L55 5 M104 18 L118 17 M107 13 L105 4"]
    return art+path(choices[variant],width=4)

def outfit(role,variant):
    if role == "human":
        art=path("M60 20 L41 26 L19 59 L38 73 L47 56 L44 100 L116 100 L113 56 L123 73 L141 59 L119 26 L100 20 L80 36 Z",MAIN)
        art+=path("M61 21 L80 37 L68 49 L57 31 M99 21 L80 37 L92 49 L103 31 M80 39 L80 99",width=3)
        if variant == 1:
            for y in [41,54,68]: art+=path(f"M30 {y} L43 {y+8} M118 {y+8} L131 {y}","none","#fff6df",6)
        if variant == 2:
            art+=path("M61 21 L79 48 L100 21 L111 31 L108 100 L52 100 L49 32 Z",ACCENT)
            art+=path("M80 50 L80 100",width=3)
        art+=path("M91 63 L106 63 L105 79 L92 79 Z",width=2)
        for y in [54,70,87]: art+=ellipse(80,y,2,2,INK,width=0)
        return art
    art=ellipse(80,64,29,43,MAIN)
    if variant in [0,2]:
        for y in ([42,58,74,90] if variant==0 else [49,79]): art+=path(f"M56 {y} Q80 {y+12} 104 {y}",stroke=INK,width=5 if variant==0 else 10)
    else:
        for x,y in [(68,40),(90,53),(69,71),(87,89)]: art+=ellipse(x,y,5,6,ACCENT,width=0)
    art+=path("M49 46 Q17 19 12 46 Q19 68 48 63 M111 46 Q143 19 148 46 Q141 68 112 63","#d4e9e7",width=3)
    return art

def accessory(role,variant):
    art=face(role,1 if role=="human" else 0)
    if variant==0:
        art+=ellipse(126,20,13,13,"#fff6df",width=3)+path("M118 20 L134 20",width=3)
    elif role=="human":
        if variant==1: art+=path("M44 39 Q43 4 86 7 Q110 13 110 39 L128 45 L51 45 Z",ACCENT)
        if variant==2: art+=path("M50 46 L73 46 L72 63 L52 63 Z M87 46 L110 46 L108 63 L88 63 Z M73 51 L87 51",fill="none",width=5)
        if variant==3:
            art+=path("M44 34 Q50 5 79 7 Q113 8 122 28 L137 42 Q111 29 99 30 L111 42 Z",MAIN)
            art+=path("M44 33 Q76 24 111 35 L111 46 Q78 37 44 46 Z","#fff6df")
            art+=ellipse(139,44,8,8,"#fff6df",width=3)
    elif variant==1:
        art+=path("M74 24 Q39 -2 48 26 Q45 42 75 29 L85 29 Q114 43 112 22 Q116 -3 84 24 Z",ACCENT)
        art+=ellipse(79,26,7,6,ACCENT,width=3)
    else:
        art+=ellipse(63,53,20,21,"#badbe3",width=5)+ellipse(97,53,20,21,"#badbe3",width=5)
        art+=path("M47 46 L57 39 M85 46 L95 39",stroke="#fff6df",width=4)
    return art

def footwear(role,variant):
    if role=="human":
        art=""
        for x in [5,78]:
            art+=path(f"M{x+21} 47 L{x+47} 47 L{x+49} 62 Q{x+74} 66 {x+69} 87 Q{x+58} 101 {x+12} 91 L{x+12} 66 Z",ACCENT)
            art+=path(f"M{x+13} 88 Q{x+46} 98 {x+69} 86",stroke=INK,width=5)
            if variant==1: art+=path(f"M{x+19} 64 L{x+57} 85 M{x+49} 63 L{x+20} 85",stroke="#fff6df",width=8)
            if variant==2:
                art+=path(f"M{x+12} 64 Q{x+30} 53 {x+50} 65",stroke="#fff6df",width=12)
                art+=path(f"M{x+21} 78 L{x+53} 78",stroke="#fff6df",width=3)
        return art
    art=ellipse(80,39,14,20,MAIN)
    for side in [-1,1]:
        for y in [30,45,60]:
            end=80+side*(44+(60-y)//2)
            art+=path(f"M{80+side*10} {y} L{end} {y+16} L{end+side*7} {y+34}",stroke=INK,width=5)
            if variant==1: art+=path(f"M{end-side*3} {y+12} L{end+side*3} {y+20}",stroke=ACCENT,width=7)
            if variant==2: art+=ellipse(end+side*4,y+28,8,7,ACCENT,width=2)
    return art

def swatch(index):
    c=PALETTE[index]
    return ellipse(80,59,45,43,c)+path("M48 47 Q55 27 76 27",stroke="#fff6df",width=7)+path("M66 91 Q86 99 104 85",stroke=INK,width=3)

for role in ["human","mosquito"]:
    for key,fn in [("face",face),("hair",hair),("outfit",outfit),("accessory",accessory),("footwear",footwear)]:
        for index in range(4 if role=="human" and key=="accessory" else 3):
            content=fn(role,index)
            (ROOT/f"{role}_{key}_{index}.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="160" height="112" viewBox="0 0 160 112">{content}</svg>\n',encoding="utf-8")
for index in range(6):
    (ROOT/f"swatch_{index}.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="160" height="112" viewBox="0 0 160 112">{swatch(index)}</svg>\n',encoding="utf-8")
print("Generated 37 original SVG thumbnails.")
