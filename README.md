<div align="center">

# TsOverlay

**A lightweight, high-performance voice overlay for TeamSpeak 6**

Fork of [beka2nt/TS6-SpeakerOverlay](https://github.com/beka2nt/TS6-SpeakerOverlay)

[![Download EXE](https://img.shields.io/github/v/release/ignery1/TsOverlay?style=for-the-badge&label=Download%20EXE&color=4FCD8E)](https://github.com/ignery1/TsOverlay/releases/latest)
[![GitHub release](https://img.shields.io/github/v/release/ignery1/TsOverlay?style=for-the-badge)](https://github.com/ignery1/TsOverlay/releases)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)](#)
[![License](https://img.shields.io/github/license/ignery1/TsOverlay?style=for-the-badge)](LICENSE)

**🇧🇷 Português** | [🇬🇧 English](#-english)

</div>

---

## 🚀 Novidades na v1.5.0

- **Wizard de primeira execução**: escolha idioma, posição (com preview arrastável em miniatura), processo alvo e modo de exibição sem precisar caçar tudo nas Configurações.
- **Detecção de app mais robusta**: agora casa tanto pelo nome do processo quanto pelo **título da janela** — funciona mesmo com jogos que rodam sob anti-cheat e disfarçam o nome do próprio processo.
- **Bloqueio de arrasto automático**: destrava sozinho ao abrir Configurações, trava sozinho ao fechar — sem risco de mexer no overlay sem querer durante o jogo.
- **Tela de erro amigável**: se algo quebrar, agora aparece uma tela com o log salvo em disco e opção de copiar, em vez do app simplesmente não abrir.
- **Tela de carregamento persistente**: fica visível durante toda a abertura, verificação de atualização e espera pela permissão do TeamSpeak 6.
- **Atualizador com progresso real**: pergunta antes de baixar, mostra a porcentagem do download, e tem checagem manual pelo ícone da bandeja.
- **Ícones redesenhados**: mic/áudio/ausente agora usam formas geométricas exatas em vez de traços feitos à mão.
- **Traduções completadas**: EN/FR/RU estavam com chaves faltando — corrigido; seletor de idioma também disponível no wizard.

## ✨ Principais recursos

- **Clique-através do mouse**: usa a API do Windows pra deixar os cliques passarem direto pro jogo.
- **Integração com a bandeja do sistema**: minimiza pra bandeja com menu completo de controle.
- **Feedback visual**: ícones vetoriais nítidos para mudo, sem áudio e ausente, além de notificações de entrada/saída no canal.
- **Arquitetura nativa**: .NET 10, publicado como single-file self-contained — um único `.exe`, sem instalador.

## 📦 Como usar

1. **Baixe**: clique no badge "Download EXE" acima.
2. **Execute**: rode o `TsOverlay.exe`. Na primeira vez, o wizard de configuração abre sozinho.
3. **Autorize**: clique em "Permitir" no seu cliente TeamSpeak 6 quando solicitado.
4. **Ajuste quando quiser**: clique com o botão direito no ícone da bandeja → Configurações.

## Mudanças em relação ao original (beka2nt v1.4.4)

| Recurso | Original | TsOverlay |
|---|---|---|
| Nome do processo/exe | TS6-SpeakerOverlay.exe | TsOverlay.exe |
| Publicação | Multi-arquivo | Single-file self-contained |
| Primeira execução | Nenhuma | Wizard guiado: idioma, posição, processo alvo, exibição |
| Detecção do app alvo | Só nome do processo | Nome do processo **e** título da janela |
| Vários processos/jogos por overlay | ❌ | ✅ Lista com múltiplos nomes/títulos |
| Posicionamento | Arrastar manualmente | Preview em miniatura arrastável + atalhos de canto com destaque |
| Bloquear/desbloquear arrasto | Atalho global (Ctrl+L) + bandeja | Automático (Configurações aberta/fechada) |
| Crash na inicialização | App simplesmente não abria | Tela de erro amigável com log salvo |
| Tela de carregamento | Nenhuma | Splash durante toda a inicialização |
| Atualização | Download silencioso | Confirmação + progresso real + checagem manual |
| Ícones de status | Path vetorial único | Formas geométricas combinadas |
| Idiomas | EN / ZH / FR / RU (traduções incompletas) | Traduções revisadas e completas |

## Créditos

- Projeto original: [beka2nt/TS6-SpeakerOverlay](https://github.com/beka2nt/TS6-SpeakerOverlay) (MIT)
- Este fork: TsOverlay — robustez, setup guiado e experiência de atualização

## Licença

MIT — mesma do projeto original. Veja [LICENSE](LICENSE).

---

## 🇬🇧 English

## 🚀 What's New in v1.5.0

- **First-run setup wizard**: pick language, position (with a draggable mini-preview), target process, and display mode without hunting through Settings.
- **More robust app detection**: now matches by process name **and** window title — works even with anti-cheat-protected games that disguise their own process name.
- **Automatic drag lock**: unlocks itself when Settings opens, locks itself when it closes — no risk of accidentally moving the overlay mid-game.
- **Friendly crash screen**: if something breaks, you now get a screen with the saved log and a copy option, instead of the app silently failing to open.
- **Persistent loading screen**: stays visible through the entire startup, update check, and wait for TeamSpeak 6 permission.
- **Updater with real progress**: asks before downloading, shows actual download percentage, and can be checked manually from the tray icon.
- **Redesigned icons**: mic/audio/away now use exact geometric shapes instead of hand-drawn paths.
- **Completed translations**: EN/FR/RU were missing keys — fixed; language picker also available in the setup wizard.

## ✨ Key Features

- **Mouse Click-Through**: uses the Windows API to let mouse events pass straight through to the game.
- **System Tray Integration**: minimizes to the tray with a full context menu.
- **Visual Feedback**: crisp vector icons for mute, deafened, and away, plus toast notifications for channel events.
- **Native Architecture**: .NET 10, published as a single-file self-contained `.exe` — no installer needed.

## 📦 How to Use

1. **Download**: click the "Download EXE" badge above.
2. **Launch**: run `TsOverlay.exe`. On first run, the setup wizard opens automatically.
3. **Authorize**: click "Allow" in your TeamSpeak 6 client when prompted.
4. **Configure anytime**: right-click the tray icon → Settings.

## Changes vs original (beka2nt v1.4.4)

| Feature | Original | TsOverlay |
|---|---|---|
| Process/exe name | TS6-SpeakerOverlay.exe | TsOverlay.exe |
| Publishing | Multi-file | Single-file self-contained |
| First run | None | Guided wizard: language, position, target app, display |
| Target app detection | Process name only | Process name **and** window title |
| Multiple processes/games per target | ❌ | ✅ List of multiple names/titles |
| Positioning | Manual drag only | Draggable mini-preview + corner shortcuts with highlight |
| Lock/unlock dragging | Global hotkey (Ctrl+L) + tray | Automatic (tied to Settings open/close) |
| Startup crash | App silently failed to open | Friendly crash screen with saved log |
| Loading screen | None | Splash through the whole startup |
| Updates | Silent download | Confirmation + real progress + manual check |
| Status icons | Single hand-drawn vector path | Combined geometric shapes |
| Languages | EN / ZH / FR / RU (incomplete) | Reviewed, complete translations |

## Credits

- Original project: [beka2nt/TS6-SpeakerOverlay](https://github.com/beka2nt/TS6-SpeakerOverlay) (MIT)
- This fork: TsOverlay — robustness, guided setup, and update experience

## License

MIT — same as upstream. See [LICENSE](LICENSE).
