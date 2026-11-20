# TempoForge — Arquitetura (Fase 2)

Aplicativo desktop nativo para Windows 10/11 x64. Base funcional: [SPEC.md](SPEC.md).

## 1. Decisões de stack

| Área | Escolha | Motivo | Licença |
|---|---|---|---|
| Linguagem/UI | C# 12, .NET 8, **WPF** | Nativo Windows, controles custom fáceis (waveform, sliders), sem webview | MIT |
| MVVM | CommunityToolkit.Mvvm | Geradores de código, sem framework pesado | MIT |
| Saída de áudio | NAudio `WasapiOut` (modo compartilhado, event-driven) | Latência ~20–40 ms sem driver especial; ASIO fica como opção futura | MIT |
| Decodificação | Media Foundation via NAudio (`MediaFoundationReader`) + **NVorbis** para OGG | MF já decodifica MP3, AAC/M4A, WAV e FLAC no Windows 10+; evita distribuir FFmpeg | Windows / MIT |
| Time-stretch + pitch | **Rubber Band** (motor R3, DLL nativa, P/Invoke pela API C) atrás de `ITimeStretcher` | Melhor qualidade em 0,25×–0,5× e 4×; modo tempo real com troca de razão sem clique | **GPL-2.0-or-later** |
| EQ, redutor de voz, reverb, limiter | Código próprio em C# | Biquads e Freeverb-style são pequenos; zero dependência | — |
| Exportação MP3 | LAME (`libmp3lame.dll`) via NAudio.Lame | Qualidade e controle de bitrate | LGPL-2.0 |
| Exportação WAV | NAudio `WaveFileWriter` | — | MIT |
| Persistência | JSON (`System.Text.Json`) em `%AppData%\TempoForge` | Poucos dados, legível, sem banco | MIT |
| Testes | xUnit + FFT própria para medir pitch | — | Apache-2.0 |
| Instalador | Inno Setup 6 | Pedido no briefing | Livre |

### WPF ou Avalonia?
WPF. O alvo é só Windows, o WPF tem melhor integração com o shell (jump list de recentes,
drag-and-drop, taskbar) e não há ganho em pagar o custo de portabilidade do Avalonia.

### C#/.NET ou JUCE/C++?
Fico com C#. JUCE seria "claramente melhor" só se precisássemos de plugins VST, de várias
plataformas ou de latência abaixo de ~10 ms; nenhum é requisito. O risco real do .NET em áudio
é o coletor de lixo pausar a thread de áudio, e ele é controlado por regra de projeto: nenhuma
alocação no caminho de áudio, buffers pré-alocados, `GCSettings.LatencyMode = SustainedLowLatency`
e o trabalho pesado de DSP em código nativo (SoundTouch). JUCE também traria licença própria
(GPL ou paga) e um ciclo de desenvolvimento mais lento para a UI.

### SoundTouch ou Rubber Band?
**Decidido: Rubber Band.** Consequência: o TempoForge é distribuído sob **GPL** (código-fonte
aberto junto com o binário). A interface `ITimeStretcher` continua existindo para testes e para
uma eventual troca de motor.

| | SoundTouch | Rubber Band |
|---|---|---|
| Licença | LGPL-2.1 (app pode ser fechado) | **GPL** (app inteiro vira GPL) ou licença comercial paga |
| Qualidade 0,75×–1,5× | Boa | Muito boa |
| Qualidade em 0,25× e 4× | Audível "granulado" em material polifônico | Bem superior (motor R3) |
| CPU | Baixa | Média/alta |

A DLL é compilada do código-fonte oficial pelo arquivo único `single/RubberBandSingle.cpp`
(FFT e reamostrador embutidos, sem dependências externas).

## 2. Módulos

```
TempoForge.App        WPF: janelas, controles, ViewModels, tema, atalhos
TempoForge.Core       Sem dependência de UI: motor, DSP, exportação, persistência
TempoForge.Native     DLLs nativas (SoundTouch, LAME) + declarações P/Invoke
TempoForge.Tests      xUnit: DSP, loop, exportação, persistência
```

| Módulo (namespace em Core) | Responsabilidade |
|---|---|
| `Decoding` | `IAudioDecoder` (abrir, ler float 32 estéreo, seek por amostra, duração, tags). Implementações: Media Foundation, Vorbis. Reamostra tudo para a taxa do motor |
| `Engine` | `AudioEngine`: ciclo de vida, transporte, thread de decodificação, callback de saída, posição |
| `Dsp` | `ITimeStretcher`, `Equalizer8`, `VocalReducer`, `Reverb`, `Limiter`, `SmoothedValue`, `Biquad` |
| `Looping` | Pontos A/B, marcadores, salto por compasso |
| `Waveform` | Varredura em segundo plano gerando picos mín/máx por bloco; cache em disco |
| `Export` | Render offline pelo mesmo grafo de DSP → WAV ou MP3; progresso e cancelamento |
| `Library` | Pastas monitoradas, recentes, leitura de tags |
| `Persistence` | Preferências, estado por faixa, presets |

Regra de dependência: `App → Core → Native`. `Core` não conhece WPF, então o motor inteiro é
testável sem interface.

## 3. Pipeline de áudio

```
 arquivo
   │
   ▼
[Decoder] ──► [Resampler p/ 48 kHz] ──► (ring buffer SPSC ~2 s, float estéreo)
  thread de decodificação, ciente do loop A-B                     │
                                                                  ▼
                              ┌───────────── callback WASAPI ─────────────┐
                              │ [TimeStretcher: tempo + pitch + rate]     │
                              │ [Preamp] → [EQ 8 bandas]                  │
                              │ [Redutor de voz] → [Reverb]               │
                              │ [Balanço] → [Limiter] → [Volume/fade]     │
                              └────────────────────┬──────────────────────┘
                                                   ▼
                                              WasapiOut
```

- **Formato interno**: float 32, estéreo intercalado, 48 kHz fixos (mono é duplicado; mais de
  2 canais é reduzido para estéreo). Taxa fixa simplifica EQ e reverb.
- **Loop A-B na origem**: a thread de decodificação lê até B e faz seek para A, em amostras.
  O stretcher recebe um fluxo contínuo, então a emenda é exata e sem buraco; um crossfade de
  5 ms na emenda evita clique.
- **Seek e mudança de loop**: geram um novo "número de geração". O callback descarta blocos de
  gerações antigas, limpa o stretcher e faz fade-in de 10 ms.
- **Rate** (velocidade e tom juntos): o Rubber Band só tem razão de tempo e escala de pitch,
  então o rate `r` é convertido: tempo efetivo = tempo × r, pitch efetivo = pitch × r.
- **Posição de reprodução**: amostras de origem consumidas pelo stretcher menos a latência dele
  e a do dispositivo; publicada com `Volatile.Write` e lida pela UI a 30 Hz.
- **Exportação**: mesmo grafo, mesma ordem, puxado em laço offline (mais rápido que tempo real),
  sem ring buffer e sem WASAPI. Garante que o arquivo soa igual ao que se ouviu.

### Suavização de parâmetros (sem cliques)
| Parâmetro | Técnica |
|---|---|
| Tempo, pitch, rate | Alvo atômico; o callback aproxima o valor atual do alvo em rampa de ~30 ms, em passos por bloco |
| Ganhos (preamp, volume, balanço, wet) | `SmoothedValue` linear por amostra, 20 ms |
| Bandas do EQ | Ganho suavizado; coeficientes do biquad recalculados a cada 64 amostras durante a rampa |
| Liga/desliga de efeito | Crossfade seco/processado de 15 ms em vez de bypass seco |
| Play/pause/seek | Fade de 10 ms |

## 4. Modelo de threads

| Thread | Faz | Nunca faz |
|---|---|---|
| **UI** (WPF dispatcher) | Entrada do usuário, desenho, escreve alvos de parâmetros | DSP, E/S de arquivo |
| **Decodificação** (1, prioridade acima do normal) | Lê do decoder, reamostra, aplica loop, enche o ring buffer; dorme quando cheio | Tocar em UI |
| **Callback de áudio** (WASAPI, MMCSS "Pro Audio") | Lê do ring buffer, roda o DSP, entrega ao dispositivo | Bloquear, alocar, travar lock, E/S, logar |
| **Waveform** (pool, baixa prioridade) | Segunda instância do decoder só para picos | — |
| **Exportação** (pool) | Render offline com decoder e grafo próprios | Compartilhar estado com a reprodução |

Regras do callback de áudio:
1. Sem `lock`, sem `await`, sem alocação gerenciada, sem exceções no caminho normal.
2. Comunicação UI → áudio por campos atômicos (`Volatile`/`Interlocked`) e uma fila SPSC de
   comandos de tamanho fixo (seek, novo loop, flush).
3. Comunicação decodificação → áudio só pelo ring buffer SPSC lock-free.
4. Se o ring buffer esvaziar (disco lento), o callback entrega silêncio com fade e sinaliza
   underrun; nunca espera.
5. Todos os buffers são alocados ao abrir o arquivo, dimensionados para o pior caso (0,25×).

Arquivo de 1 h: memória constante (ring buffer de 2 s ≈ 770 KB + cache de picos ≈ 1–2 MB).

## 5. Persistência

```
%AppData%\TempoForge\
  settings.json          preferências, janela, dispositivo de saída, última sessão
  recents.json           até 30 arquivos recentes
  library.json           pastas adicionadas
  presets\eq\*.json      presets de equalizador
  tracks\<hash>.json     por faixa: tempo, pitch, loop A/B, marcadores
  cache\peaks\<hash>.pk  picos de waveform
```

`<hash>` = SHA-1 de caminho + tamanho + data de modificação. Gravação com debounce de 500 ms e
escrita atômica (arquivo temporário + rename).

## 6. Estrutura de pastas

```
TempoForge/
├─ TempoForge.sln
├─ src/
│  ├─ TempoForge.App/
│  │  ├─ Views/            MainWindow, ExportDialog, SettingsDialog, LoopEditDialog
│  │  ├─ ViewModels/
│  │  ├─ Controls/         WaveformView, ParamSlider, EqBandSlider, TransportBar
│  │  ├─ Themes/           Dark.xaml, cores, tipografia
│  │  ├─ Input/            mapa de atalhos
│  │  └─ Assets/           ícone próprio
│  ├─ TempoForge.Core/
│  │  ├─ Decoding/  Engine/  Dsp/  Looping/  Waveform/
│  │  ├─ Export/    Library/ Persistence/  Util/ (RingBuffer, SpscQueue)
│  └─ TempoForge.Native/
│     ├─ RubberBandInterop.cs, LameInterop.cs
│     └─ runtimes/win-x64/native/   rubberband.dll, libmp3lame.dll
├─ tests/TempoForge.Tests/
├─ installer/TempoForge.iss
├─ licenses/                LGPL, textos de terceiros
├─ SPEC.md  ARCHITECTURE.md  README.md
```

## 7. Conformidade de licenças
- O TempoForge inteiro é licenciado sob **GPL-2.0-or-later**, exigência do Rubber Band. O
  repositório tem `LICENSE` (GPL) e o instalador aponta para o código-fonte completo.
- Rubber Band e LAME ficam como DLLs separadas; LAME (LGPL) e as bibliotecas MIT são
  compatíveis com a GPL.
- O instalador inclui os textos GPL/LGPL e um `THIRD-PARTY-NOTICES.txt` com origem e versão de
  cada DLL; o README explica como obter e recompilar o código-fonte delas.
- Patentes de MP3 expiraram (2017); não há restrição de codificação.
- Nada do app de referência entra no repositório: nem código, nem textos, nem imagens.

## 8. Riscos e mitigação
| Risco | Mitigação |
|---|---|
| Custo de CPU do Rubber Band R3 no callback | Medir; se passar de ~30 % de um núcleo, mover o stretch para uma thread própria com segundo ring buffer, ou oferecer o motor R2 ("rápido") como opção |
| Latência inicial do Rubber Band (atraso de partida) | Descartar as amostras de `getStartDelay` após cada seek e compensar na posição exibida |
| Pausas do GC no callback | Regras da seção 4; teste de estresse medindo underruns |
| Seek impreciso em MP3 VBR pelo Media Foundation | Seek, depois descarte até a amostra exata; teste de loop com tom de referência |
| FLAC/OGG em Windows antigos | NVorbis cobre OGG; FLAC exige Windows 10+, que já é o mínimo |
| Dispositivo de saída removido | Reabrir no dispositivo padrão e manter a posição |

## 9. Pré-requisitos para a Fase 3
1. **.NET 8 SDK** — não está instalado nesta máquina (só o runtime).
2. **Git** — a pasta ainda não é um repositório; "um commit por feature" exige `git init`.
3. `rubberband.dll` x64, compilada do código-fonte oficial (exige um compilador C++; não há
   nenhum nesta máquina) e `libmp3lame.dll` (vem no pacote NAudio.Lame).
4. Inno Setup 6 (só na Fase 4).
