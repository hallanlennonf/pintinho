# Pintinho — app de pintura (infantil + adulto)

Memória do projeto. Atualize este arquivo sempre que uma decisão importante mudar.

## Objetivo
App de pintar/colorir, em **tela cheia em modo "quiosque"**, com **modos** escolhidos numa tela inicial:
- **Infantil** — para o filho do usuário (ama veículos): botões grandes, 24 veículos.
- **Aconchego** — para adultos (a esposa), no estilo "Bobbie Goods": desenhos fofos e detalhados,
  zoom de pinça, mais ferramentas, 16 desenhos. ⚠️ "Bobbie Goods" é marca registrada: usar só o *estilo*, com
  desenhos e nome próprios. "Aconchego" é nome provisório.
- **Joguinhos** — futuro (aparece como "Em breve").
Ideia de longo prazo: talvez comercializar. Fase atual: **alpha**.

## Hardware alvo (restrição principal)
- **Positivo Duo ZX3040** (2 em 1: notebook + tablet com touch multitoque, gira a tela)
- **Windows 10 32 bits**, CPU Intel Atom, ~2 GB RAM, tela 10" 1280x800
- Tem que ser **leve**: nada de Electron/navegador/runtime pesado.

## Stack (decidida)
- **C# 5 + WinForms + GDI+, .NET Framework 4.x** (já vem no Windows 10).
- Compila com o `csc.exe` do próprio Windows — **sem Visual Studio nem SDK**.
- ⚠️ Esse csc só aceita **C# 5**: NÃO usar `$"..."`, `?.`, `nameof`, `=>` em membros,
  inicializador de auto-property, `out var`, tuplas, `using static`, etc.
- Saída AnyCPU → roda em 32 e 64 bits. Fontes em UTF-8 (`/codepage:65001`).
- Instalador: **Inno Setup 6** (instalado no PC de desenvolvimento via winget, em
  `%LOCALAPPDATA%\Programs\Inno Setup 6`). Instala por usuário (sem admin).
- Os `.bat` precisam de quebra de linha CRLF (ver `.gitattributes`). Neste PC o `cmd` não roda
  programas da pasta atual sem caminho → usar `"%~dp0arquivo.bat"`.

## Design
- Design system "Pintinho": https://claude.ai/artifact/3nDd9RhMxKX8afum9kW95q
  (temas `light`/`dark` = Infantil, `cozy-light`/`cozy-dark` = Aconchego; Segoe UI; raios; tamanhos).
- Telas (canvas): https://claude.ai/artifact/8vZ1DL8P5LHsSEkL7SqgCy
  (Infantil paisagem/retrato, tela inicial, mais cores, carimbos, Aconchego, misturador).
- As cores ficam em `src/Theme.cs` e precisam bater com o design system.
- Layout em pixels do design (1280x800 ou 800x1280) multiplicado por uma escala `sc`.
- O usuário gosta de **ver o design antes de codar**.

## Versão, build e release
- Versão única no arquivo `VERSION` (ex.: `0.2.0`). O `build.bat` gera `src/AppInfo.g.cs`.
- `build.bat` → `dist\Pintinho.exe`. `release.bat` → build + `release\Pintinho-Setup-X.Y.Z.exe`.
- Ícone: `assets\pintinho.ico`, gerado pelo próprio app: `Pintinho.exe --icone assets\pintinho.ico`.
- Testar: `dist\Pintinho.exe --janela` (Esc fecha). Capturas de todas as telas e desenhos:
  `dist\Pintinho.exe --janela --captura captura` (o Claude confere a UI por elas).
- **Publicar versão nova**: subir `VERSION`, `release.bat`, commit, `gh release create vX.Y.Z-alpha
  --prerelease` anexando `release\Pintinho-Setup-X.Y.Z.exe` (e `dist\Pintinho.exe`).
  Repo público: https://github.com/hallanlennonf/pintinho
- **Atualização no app**: Área dos pais → "Procurar atualização". O `Updater` lê
  `api.github.com/repos/hallanlennonf/pintinho/releases`, pega a mais nova que tem um asset
  `*Setup*.exe`, compara com `AppInfo.Version`, baixa e roda `/VERYSILENT`. O app fecha; o
  instalador espera o mutex `PintinhoAppMutex` sumir e reabre o app. Também verifica sozinho 4s
  depois de abrir (mostra aviso na tela inicial).

## Arquitetura (src/)
- `Program.cs` — entrada, argumentos, mutex, log (`%LOCALAPPDATA%\Pintinho\erros.log`).
- `Config.cs` — `%LOCALAPPDATA%\Pintinho\config.txt`: tema, "minhas cores".
- `Theme.cs` — 4 temas (Infantil claro/escuro, Aconchego claro/escuro) + paletas. Aconchego tem 4
  paletas de 24 cores (Básica = padrão, Pastel, Terra, Pele e cabelo); a escolhida fica no config.
- `MainForm.cs` — núcleo: telas (`Screen`), sobreposições (`Overlay`), layout, pintura, cadeado,
  avisos, salvar. Partes em arquivos `partial`:
  - `HomeScreen.cs` — tela inicial, galeria (por modo), Área dos pais (tema, trocar modo,
    atualizar, sair).
  - `KidsScreen.cs` — Infantil (folha 10:7 no tamanho da tela; vários dedos pintam juntos;
    "+" mais cores; painel de carimbos).
  - `CozyScreen.cs` — Aconchego (folha 1200x1200 com zoom/pan; pincel, lápis, marcador, spray,
    balde, borracha, carimbo, conta-gotas; tamanho, opacidade; misturador HSV; carimbos por
    categoria; pinça com dois dedos).
  - `Touch.cs` — multitoque via `WM_POINTER` (cada dedo = um `Contact` com papel: pintar, pinça,
    arrastar, cadeado). Mouse entra como id -1.
  - `Capture.cs` — modo `--captura`.
- `Surface.cs` — tinta + contorno + máscara do balde; desfazer/refazer; `StrokePath` (traço
  transparente sem acumular tinta); balde com opacidade; conta-gotas.
- `Drawings.cs` / `DrawingsCozy.cs` — desenhos numa mini-linguagem (um comando por linha; `S*` =
  forma sólida que apaga o que está atrás). Veículos em 200x140; Aconchego em 200x200.
- `SvgPath.cs` — `d` de SVG → GraphicsPath (M L H V C Q T A Z).
- `Stamps.cs` — 12 carimbos (formas, natureza, veículos). `Icons.cs` — ícones (grade 10x10).
- `Gallery.cs` — itens por modo (pastas `desenhos\` e `desenhos-aconchego\` aceitam PNG/JPG extras).
- `Updater.cs`, `AppIcon.cs`, `KioskGuard.cs` (bloqueia Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4).
- `installer/Pintinho.iss` — script do Inno Setup.

## Área dos pais / saída do quiosque
- **Segurar o cadeado** 3 s (existe em todas as telas) → Área dos pais.
- **Ctrl+Shift+Q** sai direto. Ctrl+Alt+Del não dá para bloquear.

## Desenhos da internet (decisão)
- Por enquanto os desenhos são feitos em código (leves, sem problema de licença, ficam nítidos no zoom).
- Fontes seguras se quiser importar: **OpenClipart** (domínio público, CC0) e **Wikimedia Commons**
  (filtrar por domínio público/CC0). Evitar Pinterest/Google Imagens e páginas "para colorir"
  comuns: têm direitos autorais e travariam uma venda futura. O app já aceita PNG/JPG nas pastas
  `desenhos\` e `desenhos-aconchego\`.

## Ideias / próximos passos
- Testar no tablet de verdade: multitoque, pinça, desempenho do balde e do zoom no Atom, rotação,
  instalador e atualização automática.
- Mais desenhos (Aconchego e veículos); pacote de desenhos baixável sem nova versão do app.
- Joguinhos (labirinto, ligue os pontos, memória).
- Sons; abrir desenhos salvos para continuar; Aconchego com folha retrato.
- Bloquear gestos de borda do Windows 10 no modo tablet.

## Preferências do usuário
- Fala português (BR), informal. Gosta de ver o design antes de codar.
