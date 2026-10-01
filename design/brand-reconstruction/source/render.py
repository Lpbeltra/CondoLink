"""Render a review board and validate the five SVG deliverables."""
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import io
import resvg_py
import xml.etree.ElementTree as ET
import math

ROOT = Path(__file__).resolve().parent.parent
PUBLIC = ROOT.parents[1] / 'frontend' / 'public'

for asset in [
    'comvy-symbol.svg', 'comvy-logo.svg', 'comvy-logo-dark.svg',
    'comvy-logo-mono-dark.svg', 'comvy-logo-mono-light.svg',
]:
    (PUBLIC/asset).write_bytes((ROOT/asset).read_bytes())
(PUBLIC/'icon.svg').write_bytes((ROOT/'comvy-symbol.svg').read_bytes())
(PUBLIC/'icon-maskable.svg').write_bytes((ROOT/'comvy-symbol.svg').read_bytes())

def render_app_icon(size, filename, maskable=False):
    """Rasterize approved C over existing blue-and-white app-icon palette."""
    scale = 4
    canvas = Image.new('RGBA', (size*scale, size*scale), '#6682f4' if maskable else (0,0,0,0))
    draw = ImageDraw.Draw(canvas)
    if not maskable:
        draw.ellipse((0,0,size*scale-1,size*scale-1), fill='#6682f4')
    inset = size * scale * (0.125 if not maskable else 0.12)
    draw.ellipse((inset,inset,size*scale-inset,size*scale-inset), fill='#ffffff')
    mark_height = round(size * scale * 0.5625)
    mark_width = round(mark_height * 116/132)
    mark = Image.open(io.BytesIO(resvg_py.svg_to_bytes(
        svg_path=str(PUBLIC/'comvy-symbol.svg'), width=mark_width, height=mark_height
    ))).convert('RGBA')
    canvas.alpha_composite(mark, ((canvas.width-mark.width)//2, (canvas.height-mark.height)//2))
    canvas.resize((size,size), Image.Resampling.LANCZOS).save(PUBLIC/filename, optimize=True)

for size, filename, maskable in [
    (192,'comvy-icon-192-v1.png',False),
    (512,'comvy-icon-512-v1.png',False),
    (512,'comvy-maskable-512-v1.png',True),
    (180,'apple-touch-icon-180-v1.png',False),
]:
    render_app_icon(size, filename, maskable)
FONT_DIR = Path('C:/Windows/Fonts')
font = ImageFont.truetype(str(FONT_DIR / 'arial.ttf'), 22)
small = ImageFont.truetype(str(FONT_DIR / 'arial.ttf'), 17)
board = Image.new('RGB', (1100, 1190), '#f8fafc')
draw = ImageDraw.Draw(board)
def label(x, y, text):
    draw.text((x, y), text, font=font, fill='#0f172a')
def paste(name, x, y, width=None, height=None):
    im = Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_path=str(ROOT/name), width=width, height=height)))
    board.paste(im, (x,y), im)
label(40,22,'COMVY | Reconstrução vetorial — Etapa A')
label(40,72,'Referência: LOGO PRINCIPAL')
board.paste(Image.open(ROOT/'source/reference-lockup.png').resize((884,264)), (40,110))
label(40,396,'Vetor: azul #2563EB + navy #0F172A')
paste('comvy-logo.svg',40,433,width=884)
draw.rectangle((30,725,1070,890),fill='#0f172a')
paste('comvy-logo-dark.svg',70,752,width=420)
paste('comvy-logo-mono-light.svg',580,752,width=420)
label(40,915,'Símbolo — altura real, mesma geometria')
x=40
for size in [180,48,32,24,16]:
    paste('comvy-symbol.svg',x,950,height=size)
    draw.text((x,1145),f'{size} px',font=small,fill='#475569')
    x += 250 if size==180 else 165
board.save(ROOT/'comparison.png')
for path in ROOT.glob('*.svg'):
    root=ET.parse(path).getroot()
    assert root.attrib.get('viewBox') and 'width' not in root.attrib and 'height' not in root.attrib
    assert all(n.tag.split('}')[-1] in ('svg','path') for n in root.iter())
    assert all('fill' in n.attrib for n in list(root))
    im=Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_path=str(path),width=884)))
    assert im.mode=='RGBA' and im.getpixel((0,0))[3]==0
    assert im.getbbox() is not None
    print(path.name, path.stat().st_size, 'bytes; valid; transparent; paths only')
