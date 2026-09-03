# Demo Planalyzer

Materiali pronti per registrare/mostrare la demo dell'applicazione.

## File

| File | A cosa serve |
|---|---|
| `DEMO-SCRIPT.md` | Storyboard/script del video demo (~5 minuti) con timing, azioni e voice-over. Registrabile con OBS/Loom/QuickTime. |
| `cli-demo.sh` | Script eseguibile che *scriptedly* mostra i comandi CLI con pause di lettura. Usato con `asciinema rec` produce un cast riproducibile. |

## Registrare un video

**Non posso registrarlo io** dall'ambiente in cui giro (nessuno schermo/browser). Fallo con:

### Opzione A — Video screencast completo (web UI + CLI)

Segui `DEMO-SCRIPT.md`. Tools:

- **[OBS Studio](https://obsproject.com/)** — gratis, Windows/Mac/Linux
- **[Loom](https://www.loom.com/)** — browser, ha auto-caption e trim veloce
- **QuickTime → New Screen Recording** — macOS nativo

Setup consigliato:
- 1920×1080 @ 30 fps
- Font terminale: JetBrains Mono / Cascadia Code, 16-18pt
- Zoom browser: 110-125%
- Microfono decente (voice-over registrato separatamente e mixato)

### Opzione B — Solo CLI, formato asciinema (leggerissimo)

```bash
# 1. installa asciinema
sudo apt install asciinema      # oppure: brew install asciinema

# 2. registra
asciinema rec -c "./demo/cli-demo.sh" demo/planalyzer-cli.cast

# 3. riproduci
asciinema play demo/planalyzer-cli.cast

# 4. condividi (upload gratuito su asciinema.org)
asciinema upload demo/planalyzer-cli.cast

# 5. (opzionale) converti in GIF per README
brew install agg     # o cargo install agg
agg demo/planalyzer-cli.cast demo/planalyzer-cli.gif
```

Il `.cast` è ~10-30 KB, riproducibile in qualunque terminale o embeddabile in una pagina web con [asciinema-player](https://github.com/asciinema/asciinema-player).

## Regolare il ritmo

Nel `cli-demo.sh` la variabile `SLEEP` controlla la pausa tra i comandi:

```bash
SLEEP=1 ./demo/cli-demo.sh    # più veloce
SLEEP=3 ./demo/cli-demo.sh    # più lento (default 2)
```

## Note sulla trasparenza

- Il `cli-demo.sh` scrive gli output attesi come *echo* — così il timing di lettura è deterministico e la demo è ripetibile.
- Non sono output "veri" del binario in quel preciso momento: sono simulazioni che rispecchiano il comportamento reale del tool.
- Per una demo con output *dal vivo*, sostituisci le sezioni `cat <<'EOF' ... EOF` con i veri comandi `dotnet run ...` — richiede il DB seed pronto e una connection string valida.

## Storyboard rapido (per fretta)

Sequenza suggerita 5 minuti:

1. **0:00-0:25** intro
2. **0:25-1:00** avvio Codespaces
3. **1:00-1:20** `dotnet run` + apertura URL
4. **1:20-2:15** tab Certifica query, query sbagliata → score basso + finding
5. **2:15-2:40** catalogo regole VR.001..VR.034
6. **2:40-3:20** tab Analizza piano, sample .sqlplan, beginner→expert
7. **3:20-3:55** tab Multi-DB, tre file, sintesi comune vs divergente
8. **3:55-4:35** tab Esegui & history, esecuzione + revisioni + rollback
9. **4:35-4:50** `/swagger` API browsable
10. **4:50-5:00** chiusura + repository URL
