# Pintinho — app de pintura infantil

Memória do projeto. Atualize este arquivo sempre que uma decisão importante mudar.

## Objetivo
App de pintar/colorir para o filho do usuário (estilo Tux Paint, só que moderno), rodando em
**tela cheia em modo "quiosque"** para a criança não sair do app. Tema atual: **veículos**
(ele adora). Fase: **alpha**.

## Hardware alvo (restrição principal)
- **Positivo Duo ZX3040** (2 em 1: notebook + tablet com touch, gira a tela)
- **Windows 10 32 bits**, CPU Intel Atom, ~2 GB RAM, tela 10" 1280x800
- Tem que ser **leve**: nada de Electron/navegador/runtime pesado.

## Stack (decidida)
- **C# 5 + WinForms + GDI+, .NET Framework 4.x** (já vem no Windows 10).
- Compila com o `csc.exe` do próprio Windows
  (`%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`) — **sem Visual Studio nem SDK**.
- ⚠️ Esse csc só aceita **C# 5**: NÃO usar `$"..."`, `?.`, `nameof`, `=>` em membros,
  inicializador de auto-property, `out var`, tuplas, `using static`, etc.
- Saída AnyCPU → roda em 32 e 64 bits. Fontes em UTF-8 (`/codepage:65001`).

## Design
- Design system "Pintinho": https://claude.ai/artifact/3nDd9RhMxKX8afum9kW95q
  (cores dos temas claro/escuro, tipografia Segoe UI, raios, tamanhos).
- Telas (canvas): https://claude.ai/artifact/8vZ1DL8P5LHsSEkL7SqgCy
  (tela principal + galeria, claro/escuro, paisagem 1280x800 / retrato 800x1280).
- As cores ficam em `src/Theme.cs` e precisam bater com o design system.
- Layout é definido em pixels do design (1280x800 ou 800x1280) e multiplicado por uma escala.
- Ícones e miniaturas dos veículos vieram dos SVGs do canvas (grade 10x10 e 200x140).

## Build e execução
- `build.bat` → gera `dist\Pintinho.exe` (+ pasta `dist\desenhos`).
- `dist\Pintinho.exe` → modo quiosque (tela cheia, bloqueia teclas).
- `dist\Pintinho.exe --janela` → janela 1280x800 para testar no PC (Esc fecha).
- `dist\Pintinho.exe --janela --captura <pasta>` → salva PNGs das telas (claro/escuro,
  paisagem/retrato, galeria, área dos pais) e de cada desenho, e fecha. Serve para o Claude
  conferir a UI sem ver a tela.
- Instalar no tablet: copiar a pasta `dist` inteira.

## Arquitetura (src/)
- `Program.cs` — entrada, argumentos, DPI aware, log de erros (`%LOCALAPPDATA%\Pintinho\erros.log`).
- `Config.cs` — preferências dos pais (`%LOCALAPPDATA%\Pintinho\config.txt`, ex.: `tema=escuro`).
- `Theme.cs` — cores dos temas claro e escuro + paleta de 16 cores de pintar.
- `MainForm.cs` — UI inteira desenhada à mão num só controle. Modos: Pintar, Galeria, Pais.
  Layout paisagem/retrato recalculado no `OnResize` (o tablet gira).
- `Surface.cs` — folha de pintura (proporção fixa 10:7): camada de tinta + camada de contorno
  (overlay transparente, sempre por cima) + máscara das linhas para o balde. Desfazer = pilha
  de cópias (máx. 12). Ao girar a tela a folha é reescalada.
- `Drawings.cs` — veículos descritos numa mini-linguagem (espaço 200x140, um comando por linha;
  `S`/`SR`/`SC`/`SG`/`SE` = forma "sólida" que apaga o que está atrás) + importação de imagens da
  pasta `desenhos\` (linhas escuras viram contorno).
- `SvgPath.cs` — parser de `d` de SVG (M L H V C Q T Z) para GraphicsPath.
- `Shapes.cs` — estrela, coração, flor, círculo, retângulo arredondado, utilidades de cor.
- `Icons.cs` — ícones dos botões (grade 10x10, iguais ao design).
- `Gallery.cs` — itens da galeria (folha em branco + veículos + arquivos) e miniaturas.
- `KioskGuard.cs` — hook de teclado: bloqueia Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4.

## Área dos pais / saída do quiosque
- **Segurar o cadeado** 3 segundos → abre a "Área dos pais": trocar tema, sair, voltar.
- **Ctrl+Shift+Q** sai direto.
- Ctrl+Alt+Del não dá para bloquear (é do Windows).

## Funcionalidades (alpha)
Pincel, spray, arco-íris, balde (respeita contornos), carimbos (estrela/coração/bolinha/flor —
tocar de novo troca), borracha, 3 tamanhos, 16 cores, desfazer, limpar, salvar PNG em
`Imagens\Desenhos do Pintinho`, galeria com 14 veículos + desenhos extras da pasta `desenhos\`,
temas claro/escuro, paisagem/retrato.

## Ideias / próximos passos
- Testar no tablet de verdade (desempenho do balde, toque, rotação).
- Sons (clique, balde, salvar) com WAV curtos via `SoundPlayer`.
- Abrir desenhos salvos para continuar pintando.
- Mais veículos e cenários (estrada, garagem); carimbos de veículos.
- Iniciar com o Windows (atalho em `shell:startup`).
- Bloquear gestos de borda do Windows 10 no modo tablet.

## Preferências do usuário
- Fala português (BR), informal. Gosta de ver o design antes de codar.
