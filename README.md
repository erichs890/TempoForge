# TempoForge

Aplicativo nativo para Windows que muda a velocidade e o tom de músicas de forma
independente, pensado para estudar e ensaiar. Escrito do zero em C# / WPF (.NET 8),
sem Electron nem webview.

## Recursos

- Abre MP3, M4A/AAC, WAV, FLAC, OGG, WMA e AIFF; waveform com clique para buscar.
- Velocidade de 0,25× a 4× sem alterar o tom.
- Tom de ±12 semitons com ajuste fino de ±50 cents, sem alterar a velocidade.
- Loop A-B com alças arrastáveis e marcadores.
- Equalizador de 8 bandas com pré-amplificador, predefinições e limitador.
- Redução de voz (cancelamento do centro, preservando graves e agudos).
- Exportação em WAV ou MP3 com todos os ajustes, do arquivo inteiro ou só do trecho A-B.
- Biblioteca de pastas, arquivos recentes e arrastar-e-soltar.
- Ajustes salvos por música e restaurados ao reabrir.
- Tema escuro próprio.

Atalhos principais (F1 mostra a lista completa):

| Tecla | Ação |
|---|---|
| Espaço | Tocar / pausar |
| ← / → | Voltar / avançar |
| `[` / `]` | Marcar o ponto A / B do loop |
| `+` / `-` | Aumentar / diminuir a velocidade |
| Ctrl+O / Ctrl+E | Abrir arquivo / exportar |

## Instalação

Rode `TempoForge-<versão>-setup.exe`. O instalador não pede administrador (instala para
o usuário atual), cria o atalho no menu Iniciar e já inclui o runtime do .NET.
Requer Windows 10 ou 11 de 64 bits.

Os ajustes ficam em `%AppData%\TempoForge`.

## Atualização automática

Ao abrir, a cópia instalada consulta a última release em
`github.com/erichs890/TempoForge` (espera no máximo 4 s). Se houver um instalador com
versão maior, ela o baixa, confere o tamanho e o SHA-256 informados pelo GitHub, roda o
instalador em modo silencioso e reabre o app já atualizado. Sem internet, ou se algo
falhar, o app abre normalmente. O botão "Abrir sem atualizar" pula a verificação, e a
variável de ambiente `TEMPOFORGE_NO_UPDATE` a desliga.

Para publicar uma versão nova:

1. Aumente `<Version>` em `Directory.Build.props`.
2. Rode `.\tools\build-release.ps1`.
3. Crie uma release no GitHub e anexe `installer\Output\TempoForge-<versão>-setup.exe`,
   sem renomear: a versão é lida do nome do arquivo. Releases marcadas como rascunho ou
   pré-lançamento são ignoradas.

## Como compilar

Dependências:

| Ferramenta | Para quê | Obrigatória |
|---|---|---|
| [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) | Compilar e testar | Sim |
| [Inno Setup 6](https://jrsoftware.org/isinfo.php) | Gerar o instalador | Só para o instalador |
| [Zig](https://ziglang.org/download/) e o fonte do [Rubber Band 4.0.0](https://breakfastquay.com/rubberband/) | Recompilar `rubberband.dll` | Não: a DLL já vem no repositório |

Os pacotes NuGet são baixados automaticamente.

Compilar e rodar em modo de desenvolvimento:

```powershell
dotnet build TempoForge.sln
```

```powershell
dotnet run --project src\TempoForge.App
```

Rodar os testes:

```powershell
dotnet test TempoForge.sln
```

Gerar a versão Release em `publish\` e o instalador em `installer\Output\`:

```powershell
.\tools\build-release.ps1
```

Recompilar o Rubber Band (opcional):

```powershell
.\native\build-rubberband.ps1 -Source <pasta do rubberband-4.0.0> -Zig <caminho do zig.exe>
```

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/TempoForge.Core` | Motor de áudio, DSP, decodificação, exportação, persistência |
| `src/TempoForge.Native` | Ligação com `rubberband.dll` |
| `src/TempoForge.App` | Interface WPF |
| `tests/TempoForge.Tests` | Testes automatizados |
| `native/`, `tools/`, `installer/` | Scripts de build e do instalador |

A arquitetura está descrita em [ARCHITECTURE.md](ARCHITECTURE.md) e a especificação
funcional em [SPEC.md](SPEC.md).

## Testes

Os 65 testes automatizados cobrem, entre outros pontos:

- duração do áudio após mudar a velocidade (erro abaixo de 2 %);
- tom medido por FFT após mudar a altura (erro abaixo de 5 cents);
- ganho de cada banda do equalizador e cancelamento do centro na redução de voz;
- ausência de cliques ao variar velocidade, bandas do EQ e a redução de voz;
- loop A-B, exportação WAV/MP3, persistência e atalhos.

Teste com arquivos de 10 s, 5 min e 1 h (ruído rosa + tom, gerados com ffmpeg), em cada
formato, medindo abertura, decodificação, varredura da waveform, seek e reprodução em
tempo real a 0,8× e +2 semitons:

| Formato | Abrir (1 h) | Waveform (1 h) | Erro de seek (pior caso) | Falhas de buffer |
|---|---|---|---|---|
| WAV | 60 ms | 8 s | 0 ms | 0 |
| OGG | 124 ms | 12 s | 0 ms | 0 |
| M4A | 13 ms | 13 s | 23 ms | 0 |
| FLAC | 39 ms | 6 s | 104 ms | 0 |
| MP3 (VBR) | 6 ms | 8 s | 219 ms | 0 |

Todos os 15 arquivos abriram com a duração correta e tocaram sem falhas de buffer. A
waveform é varrida em segundo plano, guardada em cache e não bloqueia a reprodução. O uso
de CPU tocando fica em torno de 18 % de um núcleo.

## Limitações conhecidas

- **Seek em MP3 e FLAC**: o decodificador do Windows (Media Foundation) posiciona com erro
  de até cerca de 0,2 s em MP3 de taxa variável e 0,1 s em FLAC. Para loops A-B com
  precisão de amostra, use WAV ou OGG.
- **Reverb** não foi implementado.
- Só Windows de 64 bits.

## Licença

O TempoForge é software livre sob a **GNU GPL versão 2 ou posterior** — veja
[LICENSE](LICENSE). A escolha decorre do uso da Rubber Band Library, que é GPL.

Componentes de terceiros e suas licenças (Rubber Band — GPL; LAME — LGPL; NAudio,
NVorbis, NAudio.Lame, CommunityToolkit e .NET — MIT) estão em
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), com os textos completos em
[licenses/](licenses). Quem redistribuir o instalador precisa oferecer também o
código-fonte correspondente.

O projeto é uma implementação independente: não contém código, textos, ícones, imagens
nem layouts de outros aplicativos.
"# TempoForge" 
