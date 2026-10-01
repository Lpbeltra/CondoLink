# Comvy — reconstrução vetorial, Etapa A

Status: aprovada e integrada na Etapa B.

## Entrega

- `comvy-symbol.svg`: C azul, transparente (476 bytes).
- `comvy-logo.svg`: C azul + wordmark navy.
- `comvy-logo-dark.svg`: C azul + wordmark branco, para fundo escuro.
- `comvy-logo-mono-dark.svg`: assinatura navy.
- `comvy-logo-mono-light.svg`: assinatura branca.
- `preview.html`: comparação no navegador, referências e tamanhos reais.
- `comparison.png`: prancha de revisão renderizada dos vetores.
- `source/`: fontes licenciadas, licenças, recortes de referência, comparação tipográfica e scripts de reprodução.

Copies in `frontend/public/` are byte-identical. `source/render.py` exports them
and rasterizes the 192, 512, 180, and maskable app icons from the approved C.

Os quatro lockups têm aproximadamente 2,3 KB cada. Todos os SVGs usam somente paths preenchidos, com viewBox, transparência e sem dimensões obrigatórias. Não contêm texto dependente de fontes, imagens, máscaras, filtros ou metadados de editor. O slogan da prancha não integra os arquivos solicitados.

## Geometria

O símbolo foi desenhado manualmente com um único contorno fechado: 14 curvas cúbicas e quatro segmentos retos. A cauda pertence ao contorno inferior esquerdo; não existe triângulo sobreposto. Foram preservados o corpo dominante do C, a abertura, os terminais arredondados, a espessura e a assimetria óptica da assinatura principal. Não houve auto-trace ou geração de imagem.

Referência: `Imagem do Codex 24 de set. de 2026, 15_28_51.png`, 1536 × 1024. A assinatura principal governa as proporções; 04.1 confirma a leitura do símbolo. As alternativas 04.2–04.5 não foram usadas.

A verificação auxiliar de silhueta em 116 × 132 px obteve interseção/união aproximada de 97,7%. Método: recorte da assinatura em x=343, y=147; azul do raster com R<110, G<170, B>150; alfa do vetor >127. Esse número depende do limiar e da resolução e não mede fidelidade tipográfica nem substitui avaliação visual.

## Wordmark e licença

O projeto declara Inter com fallback de sistema, sem arquivo de fonte localizado. Foram comparadas Inter e Outfit em pesos 650, 700 e 750, nas mesmas caixas ópticas. Inter apresenta diferenças evidentes nos terminais do c e na descendente do y. A base adotada foi Outfit 650, convertida em paths, com dimensões e posicionamentos por letra medidos na assinatura principal. O c recebeu terminais diagonais mais rasos e curvas contínuas; o v recebeu base mais larga e contraforma mais profunda. Não foi criada uma família tipográfica.

Fonte oficial: https://github.com/Outfitio/Outfit-Fonts
Distribuição utilizada: https://github.com/google/fonts/tree/main/ofl/outfit
Comparação Inter: https://github.com/google/fonts/tree/main/ofl/inter
As fontes e respectivas licenças SIL OFL estão em `source/`. A fonte modificada não é distribuída como uma nova família: somente os cinco glifos da assinatura estão nos SVGs.

## Diferenças e limites

A versão sólida usa #2563EB e #0F172A, conforme solicitado. Reflexos, gradientes, volume e antialiasing do raster não foram reproduzidos. A fonte exata da imagem não é identificável com certeza; Outfit é uma base ajustada, não uma identificação da fonte original. Persistem pequenas diferenças nos ombros do m, curvas do o e junção do y. Não se declara equivalência pixel a pixel.

A comparação no navegador incluiu assinatura ampliada, símbolo 04.1, fundos branco/navy, monocromáticas e alturas de 180, 48, 32, 24 e 16 px. O C permanece legível; a cauda fica menos definida em 16 px. Nenhuma geometria alternativa de favicon foi criada. A prancha PNG também permite revisão independente do navegador.

## Auditoria de consumo

- `Brand.tsx` agora serve os lockups aprovados em landing, cabeçalho desktop, menu compacto, login, menus laterais e carregamento.
- Tema escuro seleciona logo navy; landing fixa versão reversa na barra navy. Login preserva composição e órbitas, com novo C aprovado no espaço gráfico já existente.
- `index.html` aponta favicon SVG aprovado. PWA mantém nomes e dimensões PNG existentes (192, 512, maskable 512, Apple 180); notificação push preserva URL do ícone atualizado.
- Ícones raster usam C aprovado sobre campo branco, com o fundo azul #6682F4 já usado pela identidade de app. Maskable usa fundo azul quadrado; demais mantêm transparência fora do disco circular.
- `icon.svg` e `icon-maskable.svg` continuam disponíveis como aliases do mesmo SVG aprovado.

## Verificação

- Etapa A: 20 testes aprovados. Etapa B: 23 testes focais aprovados (Brand (3), AuthShell (1), manifest (7), PublicLandingPage (12)).
- Build de produção do frontend aprovado.
- ESLint focal dos arquivos alterados aprovado. ESLint completo encontra 11 erros preexistentes em outros arquivos e 17 avisos.
- SVGs analisados como XML e renderizados; PNGs mantêm dimensões e aliases vetoriais conferidos.
- Browser: landing desktop, marca reversa navy, login, C ampliado, SVGs e PNGs validados. Mobile testado a 390 × 844; manifestação da UI também conferida por teste e build PWA.
- `git diff --check` aprovado.
- Backend, banco, APIs, autenticação e migrations não alterados; nenhum teste backend necessário.
- Avisos preexistentes: React Router future flags, anotações PURE do SignalR e tamanho do bundle de UI.
- A primeira execução dos testes foi impedida pela restrição de acesso do ambiente; a execução autorizada seguinte passou.

Nenhum commit ou push. Nenhuma migration, mudança de API, screenshot de produto ou backend.

## Reprodução

Em ambiente Python com `fonttools==4.66.1`, `Pillow==12.3.0` e `resvg-py==0.5.0`, execute `python source/build.py` e `python source/render.py` a partir desta pasta. O renderizador da prancha usa Arial instalada no Windows apenas para rótulos; os SVGs não dependem dela. Abra `preview.html` diretamente, ou sirva esta pasta localmente. Os recortes são evidência da referência, não assets de produção.
