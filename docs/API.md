# Planalyzer.Web — API REST

Servizio ASP.NET Core minimal API, pensato per essere ospitato su localhost o
LAN. Default `http://localhost:5057`.

## Endpoint

### `POST /api/validate`

Valida il SQL contro le convenzioni interne (vedi CERTIFICATION.md).

Request:
```json
{
  "sql": "SELECT * FROM Orders",
  "options": {
    "maxOutputColumns": 30,
    "maxJoinedTables": 7,
    "maxDerivedColumns": 8,
    "disabledRules": ["VR.020"]
  }
}
```

Response: `ValidationReport` (vedi CERTIFICATION.md).

### `POST /api/analyze`

```json
{ "planXml": "<ShowPlanXML.../>", "level": "beginner" }
```
Response:
```json
{
  "text": "...",
  "statements": [ { ... } ]
}
```

### `POST /api/compare`

```json
{ "estimatedXml": "...", "actualXml": "..." }
```

### `POST /api/multidb`

```json
{ "variants": [{ "label": "prod", "planXml": "..." }, ...] }
```

### `POST /api/run`

```json
{
  "connectionString": "...",
  "slug": "orders-by-date",
  "sql": "...",
  "note": "v3",
  "mode": "actual",
  "historyDbPath": "planalyzer.db"
}
```

Response include validation report + plan analysis + revision saved.

### `GET /api/history/{slug}?historyDbPath=planalyzer.db`

Lista revisioni.

### `POST /api/rollback`

```json
{ "slug": "...", "to": 3, "historyDbPath": "planalyzer.db" }
```

## Errori

`400` con `{ "error": "..." }` su input invalido, `500` su errore interno.

## CORS

Abilitato per `localhost:*` e `127.0.0.1:*` di default (rete locale).

## UI statica

`/` serve l'app a singola pagina da `wwwroot/index.html`.

## Hosting

```bash
dotnet run --project src/Planalyzer.Web
# poi http://localhost:5057
```
