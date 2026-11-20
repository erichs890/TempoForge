# Componentes de terceiros

O TempoForge é distribuído sob a **GNU GPL versão 2 ou posterior** (veja `LICENSE`).
Ele inclui ou usa os componentes abaixo. Os textos completos estão na pasta `licenses/`.

| Componente | Versão | Uso | Licença | Texto |
|---|---|---|---|---|
| [Rubber Band Library](https://breakfastquay.com/rubberband/) — © 2007-2024 Particular Programs Ltd | 4.0.0 | Mudança de velocidade e de tom (`rubberband.dll`, ligada ao app) | GPL-2.0-or-later | `licenses/GPL-2.0.txt` |
| [LAME](https://lame.sourceforge.io/) — © The LAME Project | a que acompanha o NAudio.Lame 2.1.0 | Codificação MP3 (`libmp3lame.64.dll`, ligada dinamicamente) | LGPL-2.0-or-later | `licenses/LGPL-2.0.txt` |
| [NAudio](https://github.com/naudio/NAudio) — © Mark Heath | 2.2.1 | Saída WASAPI e leitura pelo Media Foundation | MIT | `licenses/MIT.txt` |
| [NAudio.Vorbis](https://github.com/naudio/Vorbis) — © Andrew Ward | 1.5.0 | Leitura de OGG Vorbis | MIT | `licenses/MIT.txt` |
| [NVorbis](https://github.com/NVorbis/NVorbis) — © Andrew Ward | 0.10.4 | Decodificador Vorbis | MIT | `licenses/MIT.txt` |
| [NAudio.Lame](https://github.com/Corey-M/NAudio.Lame) — © 2013-2022 Corey Murtagh | 2.1.0 | Ponte .NET para o LAME | MIT | `licenses/MIT.txt` |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — © .NET Foundation and Contributors | 8.2.2 | MVVM | MIT | `licenses/MIT.txt` |
| [.NET 8 / WPF](https://github.com/dotnet) — © .NET Foundation and Contributors | 8.0 | Runtime incluído no instalador | MIT | `licenses/MIT.txt` |

## O que essas licenças pedem

- **GPL (Rubber Band)**: por ligar o Rubber Band sob a GPL, o TempoForge inteiro é GPL.
  Quem distribuir o instalador precisa oferecer o código-fonte correspondente: o deste
  repositório e o do Rubber Band 4.0.0 (https://breakfastquay.com/rubberband/), que é
  compilado sem alterações por `native/build-rubberband.ps1`.
- **LGPL (LAME)**: a biblioteca fica em uma DLL separada, que o usuário pode substituir
  por outra compilação do LAME com o mesmo nome. O fonte está em https://lame.sourceforge.io/.
- **MP3**: as patentes do formato MP3 expiraram em 2017.
- **MIT**: basta manter os avisos de copyright acima e o texto da licença.

O TempoForge não contém código, textos, ícones, imagens nem layouts de nenhum outro
aplicativo: tudo foi escrito do zero.
