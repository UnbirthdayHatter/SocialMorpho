# SocialMorpho Sync API (Cloudflare Workers)

This is a minimal phase-1 backend so SocialMorpho users can share custom titles.

## What it does
- Stores one latest title per `character + world`.
- Supports:
  - `POST /v1/title/update`
  - `GET /v1/title/get?character=...&world=...`
  - `POST /v1/title/batch`
  - `GET /v1/health`
- Uses Cloudflare KV for storage.
- No API key required for plugin users.
- Includes basic per-IP write rate limiting on `/v1/title/update`.

## 1) Prerequisites
- Node.js 20+
- Cloudflare account (free tier is fine)
- Wrangler CLI

```bash
npm install -g wrangler
wrangler login
```

## 2) Create KV namespace
Run in `sync-api/`:

```bash
wrangler kv namespace create TITLE_KV
wrangler kv namespace create TITLE_KV --preview
```

Copy both IDs into `wrangler.toml`:
- `id = "..."`
- `preview_id = "..."`

## 3) Configure vars
Optional client pinning:
- Set `REQUIRED_CLIENT_VERSION` in `wrangler.toml`, or leave empty.

## 4) Deploy
From `sync-api/`:

```bash
wrangler deploy
```

Wrangler prints your Worker URL, for example:
`https://socialmorpho-sync-api.<account>.workers.dev`

## 5) API contract

### POST `/v1/title/update`
Headers:
- `content-type: application/json`

Body:
```json
{
  "character": "Unbirthday Hatter",
  "world": "Balmung",
  "title": "Butterfly Kisses",
  "colorPreset": "Gold Glow",
  "updatedAtUtc": "2026-02-13T23:15:00Z"
}
```

### GET `/v1/title/get`
Query params:
- `character`
- `world`

Response:
```json
{
  "ok": true,
  "found": true,
  "record": {
    "character": "Unbirthday Hatter",
    "world": "Balmung",
    "title": "Butterfly Kisses",
    "colorPreset": "Gold Glow",
    "updatedAtUtc": "2026-02-13T23:15:00Z"
  }
}
```

### POST `/v1/title/batch`
Body:
```json
{
  "players": [
    { "character": "Unbirthday Hatter", "world": "Balmung" },
    { "character": "Another Player", "world": "Balmung" }
  ]
}
```

Response:
```json
{
  "ok": true,
  "records": {
    "title:unbirthday hatter@balmung": {
      "character": "Unbirthday Hatter",
      "world": "Balmung",
      "title": "Butterfly Kisses",
      "colorPreset": "Gold Glow",
      "updatedAtUtc": "2026-02-13T23:15:00Z"
    }
  }
}
```

## Plugin integration notes (phase 1)
- Push local title on:
  - login
  - title unlock change
  - color preset change
- Pull batch records for nearby characters on a timer (3-10s), cache locally.
- Add settings toggles:
  - `Share my title`
  - `Show synced titles`
  - `Sync API URL`
