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
    """Rasterize the approved C alone on a white app-icon tile."""
    scale = 4
    canvas = Image.new('RGBA', (size*scale, size*scale), '#ffffff')
    mark_height = round(size * scale * 0.60)
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

# Review copies only: keep published product screenshots untouched.
SCREENSHOTS = PUBLIC / 'marketing' / 'aurora'
PREVIEWS = ROOT / 'screenshot-previews'
PREVIEWS.mkdir(exist_ok=True)
ORIGINAL_COPIES = PREVIEWS / 'originals'
ORIGINAL_COPIES.mkdir(exist_ok=True)
for source in sorted(SCREENSHOTS.glob('*.png')):
    original_copy = ORIGINAL_COPIES/source.name
    if not original_copy.exists():
        original_copy.write_bytes(source.read_bytes())
    original = Image.open(source).convert('RGBA')
    background = original.getpixel((10, 10))
    preview = original.copy()
    # Replace only the existing top-left identity in the navy header.
    ImageDraw.Draw(preview).rectangle((12, 12, 320, 116), fill=background)
    mark_height = 72
    mark_width = round(mark_height * 442 / 132)
    mark = Image.open(io.BytesIO(resvg_py.svg_to_bytes(
        svg_path=str(PUBLIC/'comvy-logo-dark.svg'), width=mark_width, height=mark_height
    ))).convert('RGBA')
    preview.alpha_composite(mark, (32, 27))
    preview.save(PREVIEWS/source.name, optimize=True)

(PREVIEWS/'app-icon-512.png').write_bytes((PUBLIC/'comvy-icon-512-v1.png').read_bytes())

gallery = ['<!doctype html><meta charset="utf-8"><title>Comvy — marca implementada</title>',
           '<style>body{font:16px system-ui;margin:24px;background:#f4f6fa;color:#111827}h1{font-size:24px}.icon{width:160px;margin:16px 0 28px}.icon img{width:100%;border-radius:16px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin:16px 0 32px}.pair img{width:100%;border-radius:8px}figure{margin:0}figcaption{margin:6px 0;color:#475569}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>',
           '<h1>Comvy — marca implementada</h1><p>Ícone do app e comparação das capturas antes/depois do cabeçalho.</p><figure class="icon"><figcaption>Ícone: C azul sobre branco</figcaption><img src="app-icon-512.png"></figure>']
for source in sorted(SCREENSHOTS.glob('*.png')):
    preview = PREVIEWS/source.name
    gallery.append(f'<h2>{source.stem}</h2><div class="pair"><figure><figcaption>Antes</figcaption><img src="originals/{source.name}"></figure><figure><figcaption>Implementado</figcaption><img src="{source.name}"></figure></div>')
(PREVIEWS/'index.html').write_text('\n'.join(gallery), encoding='utf-8')
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
