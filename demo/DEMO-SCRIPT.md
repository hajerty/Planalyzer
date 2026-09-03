# Demo Planalyzer — Storyboard & Script

Video ~5 minuti, un unico take, thin narration in italiano.
Registrabile con **OBS Studio** (gratis) o **Loom** (browser). Risoluzione 1920×1080, 30 fps.

Ogni scena ha:
- **[VISIVO]** cosa deve vedere lo spettatore
- **[VOICE]** narrazione voice-over (leggibile in <secondi indicati>)
- **[AZIONE]** cosa deve fare il presentatore in schermo

Durata totale: 5:00. Timestamp cumulativi.

---

## SCENA 1 — Introduzione (0:00 → 0:25)

**[VISIVO]** Terminal + editor aperti in split-screen. Logo/titolo "Planalyzer" a schermo intero per 3 secondi, poi fade.

**[VOICE]** (25s)
> "Planalyzer è uno strumento .NET per analizzare le performance delle query SQL Server. Fa tre cose: **certifica** le query contro convenzioni interne ispirate a Hugo Kornelis e Pinal Dave, **analizza i piani di esecuzione** con dettaglio non troncato come fa SSMS, e **traccia la storia** delle modifiche live contro un DB reale."

**[AZIONE]** Nessuna, solo cambio inquadratura verso il terminale.

---

## SCENA 2 — Avvio in Codespaces (0:25 → 1:00)

**[VISIVO]** Browser aperto su GitHub → repository `hajerty/Planalyzer` → menu Code → Codespaces → "Create codespace on claude/sql-plan-analyzer-tool-GneRj".

**[VOICE]** (35s)
> "L'installazione più rapida è via GitHub Codespaces. Un click sul bottone verde, e in tre minuti hai un ambiente completo: .NET 8, SQL Server 2022 come sidecar per i test, Postgres per lo storico. Il devcontainer fa tutto lui — restore, seed di un database demo con 50.000 ordini, e installa gli strumenti."

**[AZIONE]** Mostra il progress bar del Codespace che si costruisce. Salta col tempo se serve: cut a 30 secondi dopo con il terminale pronto.

---

## SCENA 3 — Avvio del servizio (1:00 → 1:20)

**[VISIVO]** Terminale nel Codespace.

**[AZIONE]** Digita:
```bash
dotnet run --project src/Planalyzer.Web
```
Attende ~10 secondi. Appare la riga:
```
Planalyzer Web in ascolto su http://0.0.0.0:5057
```

**[VOICE]** (20s)
> "Un solo comando avvia il servizio web. Codespaces auto-forwarda la porta 5057. Tab `Ports` in basso — click sull'icona globo. La UI si apre nel browser."

---

## SCENA 4 — Tab Certifica query (1:20 → 2:15)

**[VISIVO]** Browser con Planalyzer, tab **Certifica query** attivo.

**[AZIONE]** Incolla nel textarea:
```sql
SELECT *
FROM Orders o
JOIN Customers c ON YEAR(o.OrderDate) = 2025
                AND c.CustomerId = o.CustomerId
WHERE c.Email LIKE '%example.com'
  AND c.Name = NULL;
```

Click su **Valida**.

**[VOICE]** (55s)
> "Prima cosa: certifichiamo una query. Questa è volutamente sbagliata in cinque modi: SELECT stella, tabella senza schema, funzione sulla colonna dentro il JOIN che rompe la sargability, LIKE con wildcard iniziale, e confronto uguale-NULL che è sempre falso. Il validator lo dice tutto con un click."

**[VISIVO]** Compare il risultato: score basso (esempio 20/100), badge rosso "NON CERTIFICATA". Sotto, tabella dei finding: VR.001 SELECT *, VR.002 schema prefix, VR.006 funzione su colonna, VR.007 leading wildcard, VR.013 uguale-NULL.

**[AZIONE]** Passa il mouse su uno dei finding — si espande con snippet + fixHint + riferimento a Hugo Kornelis / Pinal Dave.

**[VOICE]** (continuo)
> "Ogni finding cita la fonte — HK per Hugo Kornelis, PD per Pinal Dave. Le regole Critical, come UPDATE senza WHERE, non si possono giustificare — bloccano l'esecuzione a monte. Le High si possono disinnescare con un commento `-- justify: VR.NNN motivo` sopra lo statement."

---

## SCENA 5 — Il catalogo regole (2:15 → 2:40)

**[VISIVO]** Nel tab Certify, click su "Catalogo regole" — si apre l'elenco delle 34 regole VR.001..VR.034.

**[VOICE]** (25s)
> "Il team può vedere l'elenco completo delle 34 regole — con severity, fonte e titolo — e disabilitare quelle non pertinenti al progetto. È il contratto interno: cosa significa 'query certificata' per noi."

**[AZIONE]** Scroll veloce sul catalogo. Filtra "sargability" per mostrare il filtro.

---

## SCENA 6 — Tab Analizza piano (2:40 → 3:20)

**[VISIVO]** Cambio tab su **Analizza piano**.

**[AZIONE]** Carica `samples/sample-estimated.sqlplan`. Livello "Beginner". Click Analizza.

**[VOICE]** (40s)
> "Passiamo all'analisi del piano d'esecuzione. Prendiamo un file `.sqlplan` — quello che SSMS salva. Modalità Beginner: sintesi narrativa. Vediamo che tipo di operatori usa, il costo, e sopratutto i punti di attenzione: qui, un Clustered Index Scan con `EstimatedRowsRead` a un milione, ma solo 12mila righe di output — significa filtro non sargable applicato dopo la lettura. Il validator lo aveva già detto."

**[AZIONE]** Passa a **Expert level**. Riclicca Analizza. Compare l'albero completo con tutte le properties.

**[VOICE]** (continuo)
> "Modalità Expert: ogni proprietà di ogni operatore. Predicate, ResidualPredicate, HashKeysBuild, RunTimeInformation per thread. Zero truncation — è il motivo principale per cui l'ho scritto: SSMS ti nasconde metà dei dati nei tooltip."

---

## SCENA 7 — Tab Multi-DB (3:20 → 3:55)

**[VISIVO]** Tab **Multi-DB**.

**[AZIONE]** Aggiungi 3 file .sqlplan (della stessa query da tre ambienti diversi). Label: `prod`, `staging`, `dev`. Click Confronta.

**[VOICE]** (35s)
> "Quando la stessa query gira su ambienti diversi — prod, staging, dev — spesso ha piani diversi. Multi-DB ti dà una sintesi: cosa migliora la query in modo trasversale, applicabile ovunque. Le raccomandazioni comuni vanno risolte una volta sola per tutti; quelle divergenti sono specifiche di un DB e vanno affrontate localmente."

---

## SCENA 8 — Tab Esegui & history (3:55 → 4:35)

**[VISIVO]** Tab **Esegui & history**.

**[AZIONE]** Nella connection string incolla:
```
Server=db,1433;Database=PlanalyzerDemo;User Id=sa;Password=Planalyzer_Dev_2026!;TrustServerCertificate=True;Encrypt=False
```
Slug: `orders-by-date`. SQL:
```sql
SELECT o.OrderId, o.Total, o.Status
FROM dbo.Orders AS o
WHERE o.OrderDate >= DATEADD(DAY, -30, SYSUTCDATETIME())
ORDER BY o.Total DESC;
```
Click "Esegui + valida + analizza".

**[VISIVO]** Loading, poi risultato: revision #1 creata, durata, CPU, logical reads, plan analysis.

**[VOICE]** (40s)
> "Ecco la parte live. Colleghiamo un DB, mettiamo un nome logico alla query — lo *slug*. Ogni volta che clicchiamo Esegui, viene creata una **revisione** immutabile: il SQL, il piano reale, le statistiche di quella esecuzione. Modifichiamo la query, la rieseguiamo — nuova revision. La storia resta. Se dopo tre rewrite la nuova versione è peggio, click su una revisione precedente, bottone Rollback: crea una nuova revisione con il vecchio SQL. Niente si cancella mai."

**[AZIONE]** Modifica la query aggiungendo `TOP 1000`. Riclicca. Vedi revision #2 apparire. Click su #1 nella history — si apre side panel con SQL + stats. Click Rollback.

---

## SCENA 9 — API browsable (4:35 → 4:50)

**[VISIVO]** Cambio URL a `/swagger`.

**[VOICE]** (15s)
> "Tutte le funzioni sono anche API REST — vedi Swagger. Utile per integrarle in una pipeline CI: `curl POST /api/validate` con il SQL, verifica lo score, bocci il commit se sotto una soglia."

**[AZIONE]** Espande un endpoint, clicca "Try it out", esegue.

---

## SCENA 10 — Chiusura (4:50 → 5:00)

**[VISIVO]** Slide finale con: "Planalyzer — github.com/hajerty/Planalyzer" + i tre siti citati (sqlserverfast.com, sqlauthority.com).

**[VOICE]** (10s)
> "Repository su GitHub, branch `claude/sql-plan-analyzer-tool-GneRj`. Codespaces per provarlo in tre minuti. Attribuzione doverosa a Hugo Kornelis e Pinal Dave — sono le fonti delle regole."

FINE.

---

## Consigli tecnici per la registrazione

- **Risoluzione consigliata**: 1920×1080 @ 30fps.
- **Font terminale**: JetBrains Mono / Cascadia Code, size 16-18pt.
- **Zoom UI browser**: 110-125% per leggibilità.
- **Tema browser**: chiaro per contrasto in registrazione.
- **Audio**: microfono decente (Yeti, ATR2100 o simili). Registra separatamente e monta se serve.
- **Editing minimo**: taglia i tempi morti dei loading (`dotnet restore` etc) con jumpcut.
- **Tools**:
  - Gratis: **OBS Studio** (desktop, tutte le piattaforme)
  - Facilità: **Loom** (browser, ha auto-caption)
  - macOS nativo: **QuickTime > New Screen Recording**
  - Post-processing gratis: **Shotcut** o **DaVinci Resolve**

## Alternativa: solo CLI in asciinema

Se preferisci una demo puramente testuale (CLI), vedi `demo/planalyzer-cli.cast` — è un asciinema cast riproducibile in terminale o con `asciinema-player` in una pagina web. Zero installazione lato spettatore.
