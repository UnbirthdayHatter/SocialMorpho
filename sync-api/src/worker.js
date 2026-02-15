const MAX_TITLE_LEN = 64;
const MAX_COLOR_LEN = 32;
const MAX_NAME_LEN = 32;
const MAX_WORLD_LEN = 24;
const MAX_BATCH = 100;
const MAX_LEADERBOARD = 200;
const TTL_SECONDS = 60 * 60 * 24 * 14; // 14 days

export default {
  async fetch(request, env) {
    try {
      if (request.method === "OPTIONS") {
        return withCors(new Response(null, { status: 204 }));
      }

      const url = new URL(request.url);
      const path = url.pathname;

      if (path === "/v1/health" && request.method === "GET") {
        return json(
          {
            ok: true,
            service: "socialmorpho-sync-api",
            nowUtc: new Date().toISOString(),
          },
          200,
        );
      }

      if (!isClientVersionAllowed(request, env)) {
        return json({ ok: false, error: "unsupported_client_version" }, 426);
      }

      if (path === "/v1/title/update" && request.method === "POST") {
        return handleUpdate(request, env);
      }

      if (path === "/v1/title/get" && request.method === "GET") {
        return handleGet(url, env);
      }

      if (path === "/v1/title/batch" && request.method === "POST") {
        return handleBatch(request, env);
      }

      if (path === "/v1/leaderboard/update" && request.method === "POST") {
        return handleLeaderboardUpdate(request, env);
      }

      if (path === "/v1/leaderboard/top" && request.method === "GET") {
        return handleLeaderboardTop(url, env);
      }

      return json({ ok: false, error: "not_found" }, 404);
    } catch (err) {
      return json(
        {
          ok: false,
          error: "server_error",
          message: err instanceof Error ? err.message : String(err),
        },
        500,
      );
    }
  },
};

function isClientVersionAllowed(request, env) {
  const required = (env.REQUIRED_CLIENT_VERSION || "").trim();
  if (!required) {
    return true;
  }

  const sent = (request.headers.get("x-client-version") || "").trim();
  return sent === required;
}

async function handleUpdate(request, env) {
  const body = await request.json();
  const parsed = normalizeUpdatePayload(body);
  if (!parsed.ok) {
    return json({ ok: false, error: "invalid_payload", details: parsed.errors }, 400);
  }

  const data = parsed.value;
  const key = toKey(data.character, data.world);
  const characterOnlyKey = toCharacterOnlyKey(data.character);
  const record = {
    character: data.character,
    world: data.world,
    title: data.title,
    colorPreset: data.colorPreset,
    updatedAtUtc: data.updatedAtUtc || new Date().toISOString(),
  };

  try {
    await env.TITLE_KV.put(key, JSON.stringify(record), { expirationTtl: TTL_SECONDS });
    await env.TITLE_KV.put(characterOnlyKey, JSON.stringify(record), { expirationTtl: TTL_SECONDS });
  } catch (err) {
    return json(
      {
        ok: false,
        error: "kv_unavailable",
        message: err instanceof Error ? err.message : String(err),
      },
      503,
    );
  }
  return json({ ok: true, record }, 200);
}

async function handleGet(url, env) {
  const character = sanitize(url.searchParams.get("character"), MAX_NAME_LEN);
  const world = sanitize(url.searchParams.get("world"), MAX_WORLD_LEN);
  if (!character || !world) {
    return json({ ok: false, error: "character_and_world_required" }, 400);
  }

  const key = toKey(character, world);
  let raw = null;
  try {
    raw = await env.TITLE_KV.get(key);
    if (!raw) {
      raw = await env.TITLE_KV.get(toCharacterOnlyKey(character));
    }
  } catch (err) {
    return json(
      {
        ok: false,
        error: "kv_unavailable",
        message: err instanceof Error ? err.message : String(err),
      },
      503,
    );
  }
  if (!raw) {
    return json({ ok: true, found: false }, 200);
  }

  return json({ ok: true, found: true, record: JSON.parse(raw) }, 200);
}

async function handleBatch(request, env) {
  const body = await request.json();
  const players = Array.isArray(body?.players) ? body.players : null;
  if (!players) {
    return json({ ok: false, error: "players_array_required" }, 400);
  }

  if (players.length > MAX_BATCH) {
    return json({ ok: false, error: `max_batch_${MAX_BATCH}` }, 400);
  }

  const keys = [];
  const characterOnlyKeys = [];
  for (const p of players) {
    const character = sanitize(p?.character, MAX_NAME_LEN);
    const world = sanitize(p?.world, MAX_WORLD_LEN);
    if (!character) {
      continue;
    }

    if (world) {
      keys.push(toKey(character, world));
    }
    characterOnlyKeys.push(toCharacterOnlyKey(character));
  }

  const uniqueKeys = [...new Set(keys)];
  const uniqueCharacterOnlyKeys = [...new Set(characterOnlyKeys)];

  let rows = {};
  let fallbackRows = {};
  try {
    rows = uniqueKeys.length ? await env.TITLE_KV.get(uniqueKeys, "json") : {};
    fallbackRows = uniqueCharacterOnlyKeys.length
      ? await env.TITLE_KV.get(uniqueCharacterOnlyKeys, "json")
      : {};
  } catch (err) {
    // Degrade gracefully instead of throwing Cloudflare 1101.
    return json(
      {
        ok: true,
        degraded: true,
        records: {},
        warning: err instanceof Error ? err.message : String(err),
      },
      200,
    );
  }

  const merged = {};
  for (const [k, v] of Object.entries(rows || {})) {
    if (v) {
      merged[k] = v;
    }
  }

  for (const [k, v] of Object.entries(fallbackRows || {})) {
    if (v) {
      merged[k] = v;
    }
  }

  return json({ ok: true, records: merged }, 200);
}

async function handleLeaderboardUpdate(request, env) {
  const body = await request.json();
  const parsed = normalizeLeaderboardPayload(body);
  if (!parsed.ok) {
    return json({ ok: false, error: "invalid_payload", details: parsed.errors }, 400);
  }

  const data = parsed.value;
  const leaderboardKey = "leaderboard:all";
  const entryKey = toKey(data.character, data.world || "unknown");

  let rows = [];
  try {
    const raw = await env.TITLE_KV.get(leaderboardKey);
    if (raw) {
      const parsedRows = JSON.parse(raw);
      rows = Array.isArray(parsedRows) ? parsedRows : [];
    }
  } catch {
    rows = [];
  }

  const nextRow = {
    key: entryKey,
    displayName: data.displayName,
    title: data.title,
    colorPreset: data.colorPreset,
    totalCompletions: data.totalCompletions,
    reputationXp: data.reputationXp,
    weeklyCompletions: data.weeklyCompletions,
    updatedAtUtc: data.updatedAtUtc || new Date().toISOString(),
  };

  const filtered = rows.filter((r) => r && r.key !== entryKey);
  filtered.push(nextRow);
  filtered.sort((a, b) => {
    const byCompletions = (b.totalCompletions || 0) - (a.totalCompletions || 0);
    if (byCompletions !== 0) return byCompletions;
    const byRep = (b.reputationXp || 0) - (a.reputationXp || 0);
    if (byRep !== 0) return byRep;
    return Date.parse(b.updatedAtUtc || 0) - Date.parse(a.updatedAtUtc || 0);
  });

  const compact = filtered.slice(0, MAX_LEADERBOARD).map((r) => ({
    key: sanitize(r.key || "", 96),
    displayName: sanitize(r.displayName || "", MAX_NAME_LEN),
    title: sanitize(r.title || "", MAX_TITLE_LEN),
    colorPreset: sanitize(r.colorPreset || "Gold", MAX_COLOR_LEN),
    totalCompletions: Math.max(0, Number(r.totalCompletions || 0)),
    reputationXp: Math.max(0, Number(r.reputationXp || 0)),
    weeklyCompletions: Math.max(0, Number(r.weeklyCompletions || 0)),
    updatedAtUtc: sanitize(r.updatedAtUtc || "", 40) || new Date().toISOString(),
  }));

  try {
    await env.TITLE_KV.put(leaderboardKey, JSON.stringify(compact), { expirationTtl: TTL_SECONDS });
  } catch (err) {
    return json(
      {
        ok: false,
        error: "kv_unavailable",
        message: err instanceof Error ? err.message : String(err),
      },
      503,
    );
  }

  return json({ ok: true }, 200);
}

async function handleLeaderboardTop(url, env) {
  const limitRaw = Number(url.searchParams.get("limit") || "25");
  const limit = Number.isFinite(limitRaw) ? Math.max(1, Math.min(50, Math.floor(limitRaw))) : 25;
  const leaderboardKey = "leaderboard:all";

  let rows = [];
  try {
    const raw = await env.TITLE_KV.get(leaderboardKey);
    if (raw) {
      const parsedRows = JSON.parse(raw);
      rows = Array.isArray(parsedRows) ? parsedRows : [];
    }
  } catch {
    rows = [];
  }

  const entries = rows
    .filter((r) => r && r.displayName)
    .sort((a, b) => {
      const byCompletions = (b.totalCompletions || 0) - (a.totalCompletions || 0);
      if (byCompletions !== 0) return byCompletions;
      const byRep = (b.reputationXp || 0) - (a.reputationXp || 0);
      if (byRep !== 0) return byRep;
      return Date.parse(b.updatedAtUtc || 0) - Date.parse(a.updatedAtUtc || 0);
    })
    .slice(0, limit)
    .map((r) => ({
      displayName: sanitize(r.displayName, MAX_NAME_LEN),
      title: sanitize(r.title, MAX_TITLE_LEN),
      colorPreset: sanitize(r.colorPreset, MAX_COLOR_LEN),
      totalCompletions: Math.max(0, Number(r.totalCompletions || 0)),
      reputationXp: Math.max(0, Number(r.reputationXp || 0)),
      weeklyCompletions: Math.max(0, Number(r.weeklyCompletions || 0)),
      updatedAtUtc: sanitize(r.updatedAtUtc, 40),
    }));

  return json({ ok: true, entries }, 200);
}

function normalizeUpdatePayload(body) {
  const errors = [];
  const character = sanitize(body?.character, MAX_NAME_LEN);
  const world = sanitize(body?.world, MAX_WORLD_LEN);
  const title = sanitize(body?.title, MAX_TITLE_LEN);
  const colorPreset = sanitize(body?.colorPreset, MAX_COLOR_LEN);
  const updatedAtUtc = sanitize(body?.updatedAtUtc, 40);

  if (!character) errors.push("character_required");
  if (!world) errors.push("world_required");
  if (!title) errors.push("title_required");
  if (!colorPreset) errors.push("colorPreset_required");

  if (updatedAtUtc && Number.isNaN(Date.parse(updatedAtUtc))) {
    errors.push("updatedAtUtc_invalid_iso8601");
  }

  if (errors.length) {
    return { ok: false, errors };
  }

  return {
    ok: true,
    value: { character, world, title, colorPreset, updatedAtUtc },
  };
}

function normalizeLeaderboardPayload(body) {
  const errors = [];
  const character = sanitize(body?.character, MAX_NAME_LEN);
  const world = sanitize(body?.world, MAX_WORLD_LEN);
  const displayName = sanitize(body?.displayName, MAX_NAME_LEN);
  const title = sanitize(body?.title, MAX_TITLE_LEN);
  const colorPreset = sanitize(body?.colorPreset, MAX_COLOR_LEN);
  const updatedAtUtc = sanitize(body?.updatedAtUtc, 40);
  const totalCompletions = toSafeInt(body?.totalCompletions);
  const reputationXp = toSafeInt(body?.reputationXp);
  const weeklyCompletions = toSafeInt(body?.weeklyCompletions);

  if (!character) errors.push("character_required");
  if (!world) errors.push("world_required");
  if (!displayName) errors.push("displayName_required");
  if (!title) errors.push("title_required");
  if (!colorPreset) errors.push("colorPreset_required");
  if (totalCompletions < 0) errors.push("totalCompletions_invalid");
  if (reputationXp < 0) errors.push("reputationXp_invalid");
  if (weeklyCompletions < 0) errors.push("weeklyCompletions_invalid");

  if (updatedAtUtc && Number.isNaN(Date.parse(updatedAtUtc))) {
    errors.push("updatedAtUtc_invalid_iso8601");
  }

  if (errors.length) {
    return { ok: false, errors };
  }

  return {
    ok: true,
    value: {
      character,
      world,
      displayName,
      title,
      colorPreset,
      totalCompletions,
      reputationXp,
      weeklyCompletions,
      updatedAtUtc,
    },
  };
}

function sanitize(value, maxLen) {
  if (typeof value !== "string") {
    return "";
  }
  const trimmed = value.trim();
  if (!trimmed) {
    return "";
  }
  return trimmed.substring(0, maxLen);
}

function toSafeInt(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) {
    return -1;
  }

  return Math.max(0, Math.floor(n));
}

function toKey(character, world) {
  return `title:${character.toLowerCase()}@${world.toLowerCase()}`;
}

function toCharacterOnlyKey(character) {
  return `title:${character.toLowerCase()}`;
}

function json(payload, status) {
  return withCors(
    new Response(JSON.stringify(payload), {
      status,
      headers: { "content-type": "application/json; charset=utf-8" },
    }),
  );
}

function withCors(response) {
  response.headers.set("access-control-allow-origin", "*");
  response.headers.set("access-control-allow-methods", "GET,POST,OPTIONS");
  response.headers.set("access-control-allow-headers", "content-type,x-client-version");
  return response;
}
