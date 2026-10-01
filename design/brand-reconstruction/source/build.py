"""Generate review-only brand assets. Requires fonttools and Pillow.

The symbol is deliberately drawn with cubic curves, never auto-traced.
Only the five required glyphs are converted from the licensed Outfit source.
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.transformPen import TransformPen

ROOT = Path(__file__).resolve().parent.parent
# Coordinates follow the main composition, translated from its raster origin.
# The lower-left tail belongs to this same closed contour.
C = ('M110 17 C97 6 81 0 64 0 C29 0 0 29 0 65 '
     'C0 82 6.5 97 17.5 109 L15.5 124.5 '
     'C14.4 132 18 133.5 24 130.5 L38.5 124 '
     'C46.5 127.5 55 129 64 129 C83 129 100 121 112 107 '
     'C117.5 100.5 117 93.5 111 89.5 L98 81 '
     'C93 77.5 89 79 85.5 83 '
     'C80 89 72.5 92 64 92 C48.5 92 36.5 80 36.5 65 '
     'C36.5 49.5 48.5 37 64 37 C72.5 37 80 40.5 85.5 45.5 '
     'C90 49.5 94 49.5 98 46 L110 36 '
     'C117 30.5 116 22.5 110 17 Z')

def wordmark(family='Outfit', weight=650):
    font = TTFont(ROOT / 'source' / f'{family}.ttf')
    axes = {'wght': weight}
    if family == 'Inter':
        axes['opsz'] = 32
    font = instantiateVariableFont(font, axes, inplace=True)
    glyphs = font.getGlyphSet()
    cmap = font.getBestCmap()
    # Measured ink boxes from LOGO PRINCIPAL. Optical spacing is explicit.
    boxes = [(143,40,48,53), (193,40,54,53), (251,40,77,52),
             (329,41,55,51), (385,41,57,71)]
    paths = []
    for letter, (x,y,w,h) in zip('comvy', boxes):
        glyph = glyphs[cmap[ord(letter)]]
        bounds = BoundsPen(glyphs)
        glyph.draw(bounds)
        x0,y0,x1,y1 = bounds.bounds
        sx,sy = w/(x1-x0), h/(y1-y0)
        pen = SVGPathPen(glyphs, ntos=lambda v: f'{v:.2f}'.rstrip('0').rstrip('.') if v else '0')
        glyph.draw(TransformPen(pen, (sx,0,0,-sy,x-x0*sx,y+y1*sy)))
        path = pen.getCommands()
        if family == 'Outfit' and weight == 650:
            if letter == 'c':
                # Match the shallower diagonal terminals visible in the raster.
                path = ('M170 40 C178 40 185 43.5 191 51.5 L179.5 58.5 '
                        'C177.2 55.2 174 53.5 170 53.5 C162.8 53.5 157.8 59 157.8 66.5 '
                        'C157.8 74 163 79.5 170 79.5 C174 79.5 177.2 77.8 179.5 74.5 '
                        'L191 81.5 C185.5 89 178.5 93 170 93 C154.5 93 143 82 143 66.5 '
                        'C143 51 154.5 40 170 40Z')
            elif letter == 'v':
                # Broader bottom and deeper counter than the unmodified font.
                path = 'M329 41H345L356.5 74L368 41H384L365 92H348Z'
        paths.append(path)
    return paths

def svg(symbol, word=None, family='Outfit', weight=650):
    width = 442 if word else 116
    body = f'<path fill="{symbol}" d="{C}"/>'
    if word:
        body += ''.join(f'<path fill="{word}" d="{p}"/>' for p in wordmark(family, weight))
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} 132">{body}</svg>\n'

for name, symbol, word in [
    ('comvy-symbol','#2563EB',None),
    ('comvy-logo','#2563EB','#0F172A'),
    ('comvy-logo-dark','#2563EB','#FFFFFF'),
    ('comvy-logo-mono-dark','#0F172A','#0F172A'),
    ('comvy-logo-mono-light','#FFFFFF','#FFFFFF'),
]:
    (ROOT / f'{name}.svg').write_text(svg(symbol, word), encoding='utf-8')
for family in ['Inter','Outfit']:
    for weight in [650,700,750]:
        (ROOT/'source'/f'comparison-{family}-{weight}.svg').write_text(svg('#2563EB','#0F172A',family,weight), encoding='utf-8')
