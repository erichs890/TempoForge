# TempoForge — Especificação funcional (Fase 1)

Especificação clean-room derivada da análise estática de um app Android de referência
(pacote `com.smp.musicspeed`, versão 13.11.0, versionCode 4001636, minSdk 25, targetSdk 33).
Descreve **o que** o produto faz. Nenhum código, texto, ícone ou layout do app de referência
deve ser reutilizado; os nomes de controles abaixo são descrições nossas.

Legenda de confiança: **[O]** observado no artefato · **[I]** inferido · **[?]** não determinado.

---

## 0. Método e limites da análise

| Fonte | Ferramenta | Resultado |
|---|---|---|
| Assinatura do APK | REA `inspect_apk_signature` | v2+v3, 1 signatário, autoconsistente |
| Símbolos exportados das `.so` | REA `inspect_native_exports` | 1402 símbolos na lib do player, 1950 na lib de separação |
| Inventário do arquivo | REA `open_binary`/`inspect_artifact` | **falhou**: ZIP com CRC32 inválido em 4 entradas |
| Classes/métodos Java/Kotlin | REA CLI (`search-android-classes`, `inspect-android-class`, `inspect-android-method`) com JADX 0.7.1 + JDK 21 | 160 classes do app listadas; modelos de efeitos e de stems lidos (faixas e padrões). Código ofuscado, saída em smali |
| Manifesto, tabela de recursos, XMLs de telas/menus/preferências, pool de strings dos DEX | scripts próprios de leitura de formato (fallback) | completo para manifesto/recursos; DEX só strings |

Consequências:

- O APK é uma **build modificada** (sufixo "-pl (Premium)", CRC inválido em `classes*.dex` e
  `resources.arsc`). A divisão grátis × pago abaixo vem dos recursos originais, mas o
  comportamento real de bloqueio não pôde ser confirmado. Não instale esse arquivo.
- Bandas do EQ: tabela de frequências achada na lib nativa e rótulos/escala na tela do
  equalizador (decompilada).
- Mesmo com decompilação, **não foram determinados**: largura das bandas do EQ, passo dos
  botões +/− de tempo e pitch, algoritmo de detecção de BPM/tom. O código é ofuscado e
  esses valores não estão nos pontos inspecionados. Para esses itens o TempoForge define
  valores próprios (seção 9).
- As faixas dos efeitos (seção 3.6) foram lidas das constantes dos modelos de preferência;
  onde o limite inferior não ficou explícito, está marcado com "≈".
- O app usa proteção de integridade (`libpairipcore.so`), então parte da lógica pode estar
  fora do alcance da análise estática de qualquer forma.

---

## 1. Manifesto [O]

**Telas declaradas**

| Tela | Papel |
|---|---|
| Principal (launcher, instância única) | Biblioteca + editor/player em painel deslizante + fila |
| Receptor de arquivo | Abre mídia vinda de "Abrir com" e "Compartilhar" |
| Configurações | Preferências, com subtelas |
| Timer de desligamento | Para a reprodução após um tempo |
| Editor de tags (faixa) e (álbum) | Edita metadados e capa |
| Sobre / Ajuda | Informações, licenças, links |

**Serviços**: reprodução em segundo plano com sessão de mídia e botões de mídia; exportação de
arquivo; gravador (microfone); separação de faixas (processo isolado); importação de arquivo
(processo isolado); teste de capacidade do dispositivo; mensagens push.

**Permissões**: leitura de áudio e vídeo, armazenamento legado, microfone, notificações,
serviço em primeiro plano, wake lock, internet/estado de rede, compras no app, ID de publicidade.

**Tipos aceitos (abrir/compartilhar)**: `audio/*`, FLAC (`application/flac`, `application/x-flac`),
OGG (`application/ogg`, `application/x-ogg`), `video/mp4`, `video/3gpp` (extrai o áudio de vídeos).
Extensões citadas no código: mp3, m4a, aac, wav/wave, flac, opus, amr, m3u8 (playlist).

---

## 2. Bibliotecas nativas e motor de áudio

| Lib | Função | Evidência |
|---|---|---|
| lib do player (≈2,4 MB) | Player, DSP, exportação, análise | ver abaixo |
| libavcodec/avformat/avutil/swresample/avfilter | Decodificação universal (FFmpeg) | nomes das libs; classe decodificadora com abrir/decodificar/seek/metadados |
| libmp3lame | Codificação MP3 | nome da lib; rotina de init do LAME no exportador |
| liboboe | Saída de áudio de baixa latência (AAudio/OpenSL) | nome da lib; callbacks de stream |
| lib de separação (≈0,8 MB) | Separação de stems por IA (família Spleeter) | nome da lib e das classes |
| libc++_shared, libpairipcore | Runtime C++; proteção de integridade | — |

**Conclusão sobre o motor [O/I]**

- Time-stretch/pitch-shift: **vocoder de fase próprio** — símbolos de um módulo de stretch com
  `setTempo`, `setPitchSemiTones`, `setTimeRatio`, `setFrequencyRatio`, análise/síntese,
  estimativa de fase e overlap-add. O texto "Tecnologia" do app confirma algoritmo interno
  baseado em pesquisa recente de phase vocoder. **[O]**
- O nome "Elastique" nas classes é legado: o histórico de versões diz que o motor comercial
  élastique foi adotado na v6 e substituído pelo algoritmo próprio na v10. **[I]**
- **Não** há SoundTouch nem Rubber Band nos símbolos (SoundTouch aparece só em um texto de
  licença antigo). **[O]**
- Superpowered SDK: FFT, EQ de N bandas, compressor, limiter, eco, flanger, reverb, filtros,
  analisador/waveform. **[O]**
- libsamplerate: reamostragem (usada para "rate" e conversões). **[O]**
- libFLAC: codificação FLAC na exportação. **[O]**
- Detecção de BPM e grade de batidas: módulos próprios. **[O]**

**Cadeia de processamento por bloco [O, ordem I]**: decodificar → fila → stretch/pitch →
rate (reamostragem) → preamp → EQ → redutor de voz → compressor → eco → flanger → reverb →
mono → balanço → limiter → volume com rampa em play/pause → saída. Laços de decodificação e de
processamento rodam em threads separadas do callback de áudio.

---

## 3. Funcionalidades

### 3.1 Tempo, pitch e rate

| Controle | Faixa | Padrão | Observações |
|---|---|---|---|
| Tempo (velocidade sem alterar tom) | mín. configurável 15/20/25/30/40/50 %; máx. configurável 200/250/300/400/500 % | 100 %; limites padrão 25 %–250 % | escala interna de 4000 passos, centro em 2000 **[O]** |
| Pitch (tom sem alterar velocidade) | extensão total 1, 2 ou 4 oitavas (±6, ±12, ±24 semitons) | 0; extensão padrão 2 oitavas (±12) | resolução em cents (escala 0–2400, centro 1200) **[O]** |
| Rate (velocidade e tom juntos, tipo vinil) | mesma escala do tempo | 100 % | aparece quando pitch e tempo são "unidos" **[O]** |

Comportamentos **[O]**:
- Valor central clicável abre entrada numérica pelo teclado.
- Botão de reset por controle; chave liga/desliga por cartão.
- Botões +/− opcionais para ajuste fino (passo **[?]**).
- "Snap" opcional: pitch em semitons inteiros; velocidade em múltiplos de 5 %.
- Botão opcional para unir/desunir pitch e tempo.
- Ao trocar de faixa: manter ajustes · resetar · salvar e restaurar por faixa (inclui loop).
- Qualidade do stretch: Alta / Média (padrão) / Baixa / Eficiente.
- Atenuação padrão de −3 dB para evitar clipping em pitch negativo.

### 3.2 BPM e tom musical **[O]**
- Detecta BPM e tonalidade originais da faixa e mostra o valor resultante após os ajustes.
- Usuário pode corrigir o BPM detectado (×2, ÷2, digitar, reset) e a tonalidade (nota + menor).

### 3.3 Loop A-B **[O]**
- Botões: marcar A, marcar B, limpar (pede segundo toque para confirmar), ajustar.
- Diálogo de ajuste: A e B em minutos : segundos . milissegundos, com mini-waveform, duração
  do trecho e botões "compasso anterior/próximo" (desloca o loop pelo tamanho dele).
- Trecho do loop destacado na barra de posição.
- Opção: encaixar pontos na batida mais próxima.
- Modo de latência mínima melhora a precisão dos botões de captura.
- Ponto inválido é recusado com aviso.

### 3.4 Marcadores **[O]**
- Adicionar na posição atual; ir para anterior/próximo; lista com posição, tempo e nome.
- Por marcador: editar (tempo e nome), usar como ponto A, usar como ponto B, remover; limpar todos.
- Persistidos por arquivo. Encaixe na batida opcional.

### 3.5 Equalizador **[O]**
- 8 bandas gráficas em 60, 150, 400, 1k, 2k, 4k, 8k e 16k Hz; liga/desliga, reset, valor
  digitável por banda. A mesma tabela é usada na reprodução e na exportação.
- Ganho por banda de −15 a +15 dB, resolução 0,01 dB, padrão 0; botões +/− em passos de 0,2 dB.
- Preamp (padrão −3 dB **[I]**, mesma escala das bandas) e Balanço (L/R) como controles separados.
- Presets nomeados: salvar, carregar, remover.

### 3.6 Efeitos (cada um: liga/desliga, reset, presets, expandir/recolher) **[O]**

| Efeito | Parâmetro | Faixa | Padrão |
|---|---|---|---|
| Redutor de voz | corte de graves | 20–400 Hz, passo 10 | 120 Hz |
| | corte de agudos | ≈400–20 000 Hz | 9 000 Hz |
| Compressor | wet | 0–1, passo 0,02 | 1,0 |
| | ganho de entrada / saída | −24 a +24 dB | 0 dB |
| | attack | 0,0001–1 s | 0,003 s |
| | release | 0,1–4 s | 0,3 s |
| | ratio | valores fixos 1,5 · 2 · 3 · 5 · 10 | 3 |
| | threshold | −40 a 0 dB | 0 dB |
| | passa-altas | 1–10 000 Hz | 1 Hz |
| Limiter | teto, threshold | −40 a 0 dB | 0 dB |
| | release | 0,05–1,6 s | 0,05 s |
| Eco | dry / wet | 0–1 | 0,7 / 0,6 |
| | tempo | 40–250 BPM | 128 |
| | atraso | 1/32 a 2 batidas (valores fixos) | 0,5 |
| | decay | 0–0,99 | 0,75 |
| Flanger | wet | 0–1 | 0,7 |
| | profundidade | 0–1 | 0,16 |
| | ciclo | 0,25–128 batidas (valores fixos) | 16 |
| | tempo | 40–250 BPM | 128 |
| | limiar do clipper | −100 a 0 dB | −3 dB |
| | máximo do clipper | −48 a +48 dB | 6 dB |
| Reverb | wet | 0–1 | 0,4 |
| | largura estéreo | 0–1 | 1,0 |
| | amortecimento de agudos | 0–1 | 0,5 |
| | tamanho da sala | 0–1 | 0,8 |
| | pré-delay | 0–500 ms, passo 10 | ≈0 ms |
| | corte de graves | ≈0–1 000 Hz | ≈20 Hz |
| Mono | ganho esquerdo / direito | 0–1 | 0,5 |

- Efeitos baseados em BPM podem sincronizar o tempo com o BPM detectado da faixa.
- Associação padrão↔parâmetro é **[I]** (ordem das constantes); faixas são **[O]**.

### 3.7 Separação de faixas (stems) **[O]**
- Separa em 2 (voz/resto), 4 (voz, bateria, baixo, outros) ou 5 (+ piano) stems; qualidade
  padrão ou alta.
- Fila de processamento em segundo plano; processamento local (com download único dos modelos)
  e também via servidor.
- No editor: volume independente por stem (0–150 %, padrão 100 %), com presets.
- Saída em MP3, WAV ou FLAC; compartilhar stems selecionados.

### 3.8 Exportação ("salvar faixa editada") **[O]**
- Formatos: MP3 (padrão), WAV, FLAC.
- Bitrate MP3: 128 / 192 / 256 / 320 kbps (padrão 320); opção "qualidade máxima" (mais lenta).
- Aplica todos os ajustes ativos; opção de salvar só o trecho do loop.
- Usuário define nome do arquivo e título; artista/título são copiados para as tags.
- Nome sugerido inclui os ajustes (pitch/tempo/rate).
- Pasta de destino configurável; progresso em notificação; uma exportação por vez.

### 3.9 Outras ferramentas **[O]**
- Reverter áudio (gera versão de trás para frente).
- Gravador de áudio (m4a ou wav; 128–320 kbps, padrão 192).
- Editor de tags (título, álbum, artista, gênero, ano, nº da faixa, capa).
- Timer de desligamento (horas/minutos, +2 min, cancelar).
- Atraso de início da reprodução (contagem regressiva de 5 s).
- Compartilhar faixa original; detalhes do arquivo (caminho, tamanho, data, duração, bitrate).

### 3.10 Biblioteca, fila e playlists **[O]**
- Biblioteca em abas: músicas, álbuns, artistas, outros áudios, playlists; busca; ordenação por
  nome ou data de modificação (asc/desc); embaralhar tudo.
- Navegador de pastas com trilha de navegação, pasta inicial configurável e locais de armazenamento.
- Seletor de arquivos do sistema.
- Fila de reprodução: tocar, tocar a seguir, adicionar, remover, reordenar, limpar, salvar como
  playlist, ir para a faixa atual.
- Playlists: criar, renomear, excluir (inclusive várias), reordenar, exportar como m3u8.
- "Minhas criações": faixas editadas, gravações e faixas separadas.
- Seleção múltipla com ações em lote.
- Modos de reprodução: parar ao fim da faixa · seguir até o fim da fila · repetir tudo ·
  repetir uma; embaralhar.

### 3.11 Transporte e atalhos **[O]**
- Play/pause, faixa anterior/próxima, avançar/retroceder (1/2/3/4/5/10/20/30 s; padrão 3 s),
  barra de posição, tempo atual e duração.
- Waveform rolável (preencher espaço · fixa pequena · fixa grande · desligada).
- Controle por botões de mídia externos (fone, carro) e pela notificação/tela de bloqueio.
- Não há atalhos de teclado no app de referência (é mobile); os do TempoForge são desenho nosso.

### 3.12 Grátis × pago
- **[O]** Único item pago encontrado nos recursos: **remover anúncios** (compra única), com
  alternativa de assistir a um anúncio para ficar ~30 min sem anúncios.
- **[O]** Nenhum texto indica recurso de edição bloqueado por pagamento.
- **[?]** Limites ocultos (ex.: cota de separação via servidor) não puderam ser verificados.
- **TempoForge**: sem anúncios, sem compras; tudo liberado.

---

## 4. Telas e navegação [O]

```
Menu lateral
├─ Biblioteca (abas) ──► álbum / artista / playlist ──► faixa
├─ Pastas
├─ Seletor de áudio do sistema
├─ Gravador
├─ Separador de faixas (fila ► concluídas)
├─ Minhas criações (editadas | gravações | separadas)
└─ Sobre · Configurações · Ajuda

Painel inferior deslizante = Editor/Player
├─ Barra: título, play/pause, seletor de componentes, menu
├─ Área de componentes (um conjunto por vez):
│    Pitch e tempo · Tom e BPM · Isolamento de faixas · Marcadores ·
│    Equalizador · Efeitos · Capa · Personalizado (escolhe quais aparecem)
├─ Loop A-B
├─ Waveform
├─ Transporte
└─ Fila de reprodução (painel)

Menu do editor: salvar editada · adicionar à playlist · enviar ao separador ·
ir para álbum/artista · compartilhar · reverter · tags · detalhes ·
unir pitch/tempo · componentes · atraso de início · timer
```

Configurações: Controles do editor (pitch/tempo, componentes personalizados, botões +/−,
incremento de seek, waveform) · Interação com o dispositivo (tela ligada, foco de áudio,
retomada automática) · Cores (tema claro/escuro/sistema, preto puro, cor de destaque por tema) ·
Batidas (encaixe de loop/marcadores, efeitos por BPM) · Exportação · Separador · Gravador ·
Avançado (qualidade do stretch, tamanho do buffer 4096–20480 com padrão 12288, API de áudio,
latência mínima) · Restaurar avisos.

Layout "Personalizado" padrão: Pitch e tempo + Loop ligados; demais desligados.

---

## 5. Persistência [O]
- Preferências do usuário.
- Por faixa: ajustes de pitch/tempo e loop (se a opção estiver ativa), marcadores, BPM/tom,
  início da batida, cache de waveform.
- Presets de EQ/efeitos/stems; playlists internas; fila de reprodução e ordem de embaralhamento;
  fila do separador.

---

## 6. Escopo do TempoForge (desktop)

| Do app de referência | TempoForge v1 |
|---|---|
| Tempo, pitch, rate, snap, união, reset, entrada numérica | Sim |
| Loop A-B com ajuste fino, marcadores nomeados | Sim |
| EQ 8 bandas + preamp + balanço + presets | Sim |
| Redutor de voz | Sim |
| Reverb | Sim (pedido no briefing) |
| Compressor, limiter, eco, flanger, mono | Pós-v1 |
| Exportar WAV/MP3 (+ só o loop) | Sim; FLAC pós-v1 |
| Biblioteca por pastas, recentes, arrastar-e-soltar | Sim (sem scanner de álbuns/artistas na v1) |
| Playlists, fila | Fila simples na v1; playlists pós-v1 |
| BPM/tom | Pós-v1 |
| Separação por IA, gravador, tags, reverter, timer | Fora da v1 |
| Anúncios, compras, telemetria, push | Nunca |

---

## 7. Requisitos não funcionais (nossos)
- Thread de áudio nunca bloqueia; sem alocação nem locks no callback.
- Mudanças de parâmetro suavizadas (sem cliques).
- Latência de saída alvo ≤ 50 ms (WASAPI).
- Arquivos de 1 h abrem em streaming, sem carregar tudo na memória.
- Estado e preferências restaurados entre sessões.

---

## 8. Atalhos do TempoForge (desenho nosso)
Espaço play/pause · ←/→ seek · Shift+←/→ seek maior · `[` marca A · `]` marca B ·
`\` limpa loop · `+`/`−` velocidade · ↑/↓ pitch (semitom) · Ctrl+↑/↓ pitch (cents) ·
`M` marcador · `,`/`.` marcador anterior/próximo · Ctrl+O abrir · Ctrl+E exportar · `R` reset.

---

## 9. Valores definidos por nós (onde a referência é [?])
- Velocidade: 0,25×–4,00×, passo 0,01 (botões 0,05).
- Pitch: ±12 semitons (opção ±24), cents ±50, passo 1 cent.
- EQ: adotamos o layout observado (60, 150, 400, 1k, 2k, 4k, 8k, 16k Hz; ±15 dB; passo fino
  0,2 dB). São valores de engenharia comuns, não expressão protegida; preamp ±15 dB, padrão 0.
- Redutor de voz: cancelamento do centro com banda preservada abaixo de 120 Hz e acima de 8 kHz
  (ajustáveis).
- Reverb: sala 0–100 %, wet 0–100 %, amortecimento 0–100 %, pré-delay 0–200 ms.

---

## 10. Pendências ainda abertas após a decompilação
1. Largura (Q) das bandas do EQ e faixa exata do preamp.
2. Passo dos botões +/− de tempo e pitch (o do EQ é 0,2 dB).
3. Regras reais de bloqueio grátis × pago (só "remover anúncios" foi observado).
4. Parâmetros de cada nível de qualidade do stretch.

Nenhuma delas bloqueia a Fase 2: são cobertas pelos valores da seção 9.
