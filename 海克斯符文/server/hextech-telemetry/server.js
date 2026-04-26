const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");

const HOST = process.env.HOST || "127.0.0.1";
const PORT = Number.parseInt(process.env.PORT || "3000", 10);
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, "data");
const PUBLIC_DIR = path.join(__dirname, "public");
const DERIVED_DIR = path.join(DATA_DIR, "derived");
const RESULTS_FILE = path.join(DATA_DIR, "run_results.jsonl");
const MAX_BODY_BYTES = 256 * 1024;
const MIN_RUN_TIME_FOR_DEFAULT_STATS = 60;

fs.mkdirSync(DATA_DIR, { recursive: true });
fs.mkdirSync(DERIVED_DIR, { recursive: true });

const knownRunIds = loadKnownRunIds();

function loadKnownRunIds() {
  const ids = new Set();
  for (const record of readRecordSet().records) {
    const runId = record?.payload?.run?.runId;
    if (typeof runId === "string" && runId.length > 0) {
      ids.add(runId);
    }
  }
  return ids;
}

function sendJson(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body)
  });
  res.end(body);
}

function sendText(res, status, value) {
  res.writeHead(status, {
    "content-type": "text/plain; charset=utf-8",
    "cache-control": "no-store"
  });
  res.end(value);
}

function sendFile(res, filePath, contentType) {
  if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    return sendText(res, 404, "not found");
  }

  res.writeHead(200, {
    "content-type": contentType,
    "cache-control": "no-store"
  });
  fs.createReadStream(filePath).pipe(res);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    req.on("data", (chunk) => {
      size += chunk.length;
      if (size > MAX_BODY_BYTES) {
        reject(Object.assign(new Error("payload too large"), { statusCode: 413 }));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

function validatePayload(payload) {
  if (!payload || typeof payload !== "object") {
    return "payload must be an object";
  }
  if (payload.schemaVersion !== 1) {
    return "unsupported schemaVersion";
  }
  if (payload.modId !== "HextechRunes") {
    return "modId must be HextechRunes";
  }
  if (!payload.run || typeof payload.run !== "object") {
    return "run is required";
  }
  if (typeof payload.run.runId !== "string" || payload.run.runId.length < 16) {
    return "run.runId is required";
  }
  if (typeof payload.run.seedHash !== "string" || payload.run.seedHash.length < 16) {
    return "run.seedHash is required";
  }
  if (typeof payload.run.isVictory !== "boolean") {
    return "run.isVictory must be boolean";
  }
  if (!Array.isArray(payload.players) || !Array.isArray(payload.runeChoices) || !Array.isArray(payload.monsterHexes)) {
    return "players, runeChoices, and monsterHexes must be arrays";
  }
  return null;
}

async function handleIngest(req, res) {
  let payload;
  try {
    payload = JSON.parse(await readBody(req));
  } catch (error) {
    return sendJson(res, error.statusCode || 400, { ok: false, error: error.message || "invalid json" });
  }

  const validationError = validatePayload(payload);
  if (validationError) {
    return sendJson(res, 400, { ok: false, error: validationError });
  }

  const runId = payload.run.runId;
  if (knownRunIds.has(runId)) {
    return sendJson(res, 202, { ok: true, duplicate: true, runId });
  }

  const record = {
    receivedAtUtc: new Date().toISOString(),
    payloadHash: sha256(JSON.stringify(payload)),
    payload
  };
  fs.appendFileSync(RESULTS_FILE, `${JSON.stringify(record)}\n`, "utf8");
  knownRunIds.add(runId);
  writeDerivedTables();
  return sendJson(res, 202, { ok: true, duplicate: false, runId });
}

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

function readRecordSet() {
  if (!fs.existsSync(RESULTS_FILE)) {
    return { records: [], physicalLines: 0, duplicateLines: 0, malformedLines: 0 };
  }

  const lines = fs.readFileSync(RESULTS_FILE, "utf8").split(/\r?\n/).filter((line) => line.trim());
  const byRunId = new Map();
  let malformedLines = 0;
  for (const line of lines) {
    try {
      const record = JSON.parse(line);
      const payload = record?.payload;
      const runId = payload?.run?.runId;
      if (typeof runId === "string" && runId.length > 0) {
        byRunId.set(runId, record);
      }
    } catch {
      malformedLines += 1;
    }
  }

  return {
    records: [...byRunId.values()],
    physicalLines: lines.length,
    duplicateLines: Math.max(0, lines.length - byRunId.size - malformedLines),
    malformedLines
  };
}

function getRun(record) {
  return record?.payload?.run || {};
}

function getRunTime(record) {
  const runTime = Number(getRun(record).runTime);
  return Number.isFinite(runTime) ? runTime : 0;
}

function getExcludeReasons(record) {
  const reasons = [];
  if (getRunTime(record) < MIN_RUN_TIME_FOR_DEFAULT_STATS) {
    reasons.push("short_run");
  }
  return reasons;
}

function isDefaultEligible(record) {
  return getExcludeReasons(record).length === 0;
}

function addCounter(map, key, isVictory) {
  if (!key) {
    return;
  }
  if (!map[key]) {
    map[key] = { runs: 0, wins: 0 };
  }
  map[key].runs += 1;
  if (isVictory) {
    map[key].wins += 1;
  }
}

function addMonsterCounter(map, key, isPlayerVictory) {
  if (!key) {
    return;
  }
  if (!map[key]) {
    map[key] = { runs: 0, wins: 0, playerWins: 0, monsterWins: 0 };
  }
  map[key].runs += 1;
  if (isPlayerVictory) {
    map[key].wins += 1;
    map[key].playerWins += 1;
  } else {
    map[key].monsterWins += 1;
  }
}

function addChoiceCounter(map, key, field, isVictory) {
  if (!key) {
    return;
  }
  if (!map[key]) {
    map[key] = { offered: 0, selected: 0, selectedWins: 0 };
  }
  map[key][field] += 1;
  if (field === "selected" && isVictory) {
    map[key].selectedWins += 1;
  }
}

function addSimpleCounter(map, key) {
  if (!key) {
    return;
  }
  map[key] = (map[key] || 0) + 1;
}

function buildDerivedData() {
  const recordSet = readRecordSet();
  const records = recordSet.records;
  const eligibleRecords = records.filter(isDefaultEligible);
  const excludedShortRuns = records.length - eligibleRecords.length;
  const summary = {
    generatedAtUtc: new Date().toISOString(),
    filters: {
      minRunTimeForDefaultStats: MIN_RUN_TIME_FOR_DEFAULT_STATS,
      defaultExcludes: ["short_run"]
    },
    raw: {
      physicalLines: recordSet.physicalLines,
      uniqueRuns: records.length,
      duplicateLines: recordSet.duplicateLines,
      malformedLines: recordSet.malformedLines
    },
    runCount: eligibleRecords.length,
    winCount: 0,
    winRate: 0,
    excludedShortRuns,
    playerRuneRuns: {},
    playerRuneChoices: {},
    monsterHexRuns: {},
    versions: {},
    netModes: {},
    characters: {},
    tables: {
      playerRuneRuns: [],
      playerRuneChoices: [],
      monsterHexRuns: [],
      versions: [],
      netModes: [],
      characters: []
    }
  };

  const runsRows = [];
  const playerRuneRows = [];
  const runeChoiceRows = [];
  const monsterHexRows = [];

  for (const record of records) {
    const payload = record.payload || {};
    const run = payload.run || {};
    const isVictory = run.isVictory === true;
    const runId = run.runId || "";
    const excludeReasons = getExcludeReasons(record);
    const eligible = excludeReasons.length === 0;
    const runCommon = {
      receivedAtUtc: record.receivedAtUtc || "",
      uploadedAtUtc: payload.uploadedAtUtc || "",
      runId,
      seedHash: run.seedHash || "",
      modVersion: payload.modVersion || "",
      gameVersion: payload.gameVersion || "",
      netMode: run.netMode || "",
      playerCount: run.playerCount || 0,
      ascension: run.ascension || 0,
      currentActIndex: run.currentActIndex || 0,
      totalFloor: run.totalFloor || 0,
      runTime: getRunTime(record),
      isVictory: isVictory ? 1 : 0,
      eligibleDefaultStats: eligible ? 1 : 0,
      excludeReasons: excludeReasons.join("|")
    };

    runsRows.push(runCommon);

    if (eligible) {
      if (isVictory) {
        summary.winCount += 1;
      }
      addSimpleCounter(summary.versions, payload.modVersion || "(unknown)");
      addSimpleCounter(summary.netModes, run.netMode || "(unknown)");
    }

    for (const player of payload.players || []) {
      const character = player.character || "";
      if (eligible) {
        addSimpleCounter(summary.characters, character || "(unknown)");
      }
      const hextechRunes = Array.isArray(player.hextechRunes) ? player.hextechRunes : [];
      for (const rune of hextechRunes) {
        playerRuneRows.push({
          ...runCommon,
          playerSlot: player.slot ?? "",
          character,
          rune
        });
        if (eligible) {
          addCounter(summary.playerRuneRuns, rune, isVictory);
        }
      }
    }

    for (const choice of payload.runeChoices || []) {
      const options = Array.isArray(choice.options) ? choice.options : [];
      const selected = typeof choice.selected === "string" ? choice.selected : "";
      for (const option of options) {
        const isSelected = option === selected;
        runeChoiceRows.push({
          ...runCommon,
          actIndex: choice.actIndex ?? "",
          playerSlot: choice.playerSlot ?? "",
          rarity: choice.rarity || "",
          rerollCount: choice.rerollCount ?? 0,
          option,
          selectedRune: selected,
          isSelected: isSelected ? 1 : 0
        });
        if (eligible) {
          addChoiceCounter(summary.playerRuneChoices, option, "offered", isVictory);
          if (isSelected) {
            addChoiceCounter(summary.playerRuneChoices, option, "selected", isVictory);
          }
        }
      }
      if (eligible && selected && !options.includes(selected)) {
        addChoiceCounter(summary.playerRuneChoices, selected, "offered", isVictory);
        addChoiceCounter(summary.playerRuneChoices, selected, "selected", isVictory);
      }
    }

    for (const monsterHex of payload.monsterHexes || []) {
      monsterHexRows.push({
        ...runCommon,
        actIndex: monsterHex.actIndex ?? "",
        rarity: monsterHex.rarity || "",
        hex: monsterHex.hex || ""
      });
      if (eligible) {
        addMonsterCounter(summary.monsterHexRuns, monsterHex.hex, isVictory);
      }
    }
  }

  summary.winRate = pctNumber(summary.winCount, summary.runCount);
  summary.tables.playerRuneRuns = buildRateRows(summary.playerRuneRuns, "player");
  summary.tables.playerRuneChoices = buildChoiceRows(summary.playerRuneChoices);
  summary.tables.monsterHexRuns = buildMonsterRows(summary.monsterHexRuns);
  summary.tables.versions = buildCountRows(summary.versions);
  summary.tables.netModes = buildCountRows(summary.netModes);
  summary.tables.characters = buildCountRows(summary.characters);

  return {
    summary,
    tables: {
      runs: runsRows,
      playerRunes: playerRuneRows,
      runeChoices: runeChoiceRows,
      monsterHexes: monsterHexRows
    }
  };
}

function pctNumber(part, total) {
  return total > 0 ? Number(((part / total) * 100).toFixed(1)) : 0;
}

function buildRateRows(map) {
  return Object.entries(map)
    .map(([id, stat]) => ({
      id,
      runs: stat.runs,
      wins: stat.wins,
      winRate: pctNumber(stat.wins, stat.runs)
    }))
    .sort((a, b) => b.runs - a.runs || b.winRate - a.winRate || a.id.localeCompare(b.id));
}

function buildChoiceRows(map) {
  return Object.entries(map)
    .map(([id, stat]) => ({
      id,
      offered: stat.offered,
      selected: stat.selected,
      pickRate: pctNumber(stat.selected, stat.offered),
      selectedWins: stat.selectedWins,
      selectedWinRate: pctNumber(stat.selectedWins, stat.selected)
    }))
    .sort((a, b) => b.selected - a.selected || b.pickRate - a.pickRate || b.offered - a.offered || a.id.localeCompare(b.id));
}

function buildMonsterRows(map) {
  return Object.entries(map)
    .map(([id, stat]) => ({
      id,
      runs: stat.runs,
      playerWins: stat.playerWins,
      playerWinRate: pctNumber(stat.playerWins, stat.runs),
      monsterWins: stat.monsterWins,
      monsterWinRate: pctNumber(stat.monsterWins, stat.runs)
    }))
    .sort((a, b) => b.runs - a.runs || b.monsterWinRate - a.monsterWinRate || a.id.localeCompare(b.id));
}

function buildCountRows(map) {
  return Object.entries(map)
    .map(([id, count]) => ({ id, count }))
    .sort((a, b) => b.count - a.count || a.id.localeCompare(b.id));
}

function writeDerivedTables() {
  const derived = buildDerivedData();
  writeFileAtomic(path.join(DERIVED_DIR, "summary.json"), `${JSON.stringify(derived.summary, null, 2)}\n`);
  writeCsv("runs.csv", derived.tables.runs, [
    "receivedAtUtc",
    "uploadedAtUtc",
    "runId",
    "seedHash",
    "modVersion",
    "gameVersion",
    "netMode",
    "playerCount",
    "ascension",
    "currentActIndex",
    "totalFloor",
    "runTime",
    "isVictory",
    "eligibleDefaultStats",
    "excludeReasons"
  ]);
  writeCsv("player_runes.csv", derived.tables.playerRunes, [
    "receivedAtUtc",
    "runId",
    "modVersion",
    "netMode",
    "playerCount",
    "ascension",
    "totalFloor",
    "runTime",
    "isVictory",
    "eligibleDefaultStats",
    "playerSlot",
    "character",
    "rune"
  ]);
  writeCsv("rune_choices.csv", derived.tables.runeChoices, [
    "receivedAtUtc",
    "runId",
    "modVersion",
    "netMode",
    "playerCount",
    "ascension",
    "totalFloor",
    "runTime",
    "isVictory",
    "eligibleDefaultStats",
    "actIndex",
    "playerSlot",
    "rarity",
    "rerollCount",
    "option",
    "selectedRune",
    "isSelected"
  ]);
  writeCsv("monster_hexes.csv", derived.tables.monsterHexes, [
    "receivedAtUtc",
    "runId",
    "modVersion",
    "netMode",
    "playerCount",
    "ascension",
    "totalFloor",
    "runTime",
    "isVictory",
    "eligibleDefaultStats",
    "actIndex",
    "rarity",
    "hex"
  ]);
  return derived.summary;
}

function writeCsv(fileName, rows, headers) {
  const lines = [headers.join(",")];
  for (const row of rows) {
    lines.push(headers.map((header) => csvCell(row[header])).join(","));
  }
  writeFileAtomic(path.join(DERIVED_DIR, fileName), `${lines.join("\n")}\n`);
}

function csvCell(value) {
  const raw = value == null ? "" : String(value);
  if (/[",\r\n]/.test(raw)) {
    return `"${raw.replaceAll('"', '""')}"`;
  }
  return raw;
}

function writeFileAtomic(filePath, body) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const tmpPath = `${filePath}.${process.pid}.tmp`;
  fs.writeFileSync(tmpPath, body, "utf8");
  fs.renameSync(tmpPath, filePath);
}

function serveDerived(req, res, pathname) {
  const fileName = path.basename(pathname);
  const allowed = new Set(["summary.json", "runs.csv", "player_runes.csv", "rune_choices.csv", "monster_hexes.csv"]);
  if (!allowed.has(fileName)) {
    return sendText(res, 404, "not found");
  }
  writeDerivedTables();
  const contentType = fileName.endsWith(".json") ? "application/json; charset=utf-8" : "text/csv; charset=utf-8";
  return sendFile(res, path.join(DERIVED_DIR, fileName), contentType);
}

function serveStatic(req, res) {
  const url = new URL(req.url, "http://localhost");
  let pathname = decodeURIComponent(url.pathname);
  if (pathname === "/") {
    pathname = "/index.html";
  }

  const filePath = path.normalize(path.join(PUBLIC_DIR, pathname));
  const relativePath = path.relative(PUBLIC_DIR, filePath);
  if (relativePath.startsWith("..") || path.isAbsolute(relativePath)) {
    return sendText(res, 403, "forbidden");
  }

  if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    return sendText(res, 404, "not found");
  }

  const ext = path.extname(filePath);
  const contentType = {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "application/javascript; charset=utf-8",
    ".json": "application/json; charset=utf-8",
    ".svg": "image/svg+xml"
  }[ext] || "application/octet-stream";

  res.writeHead(200, {
    "content-type": contentType,
    "cache-control": ext === ".html" ? "no-cache" : "public, max-age=3600"
  });
  fs.createReadStream(filePath).pipe(res);
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  if (req.method === "GET" && url.pathname === "/health") {
    return sendJson(res, 200, { ok: true, service: "hextech-runes-telemetry", runs: knownRunIds.size });
  }
  if (req.method === "GET" && url.pathname === "/api/hextech-runes/summary") {
    return sendJson(res, 200, writeDerivedTables());
  }
  if (req.method === "GET" && url.pathname.startsWith("/api/hextech-runes/derived/")) {
    return serveDerived(req, res, url.pathname);
  }
  if (req.method === "POST" && url.pathname === "/api/hextech-runes/run-result") {
    return handleIngest(req, res);
  }
  if (req.method === "GET" || req.method === "HEAD") {
    return serveStatic(req, res);
  }
  return sendText(res, 405, "method not allowed");
});

writeDerivedTables();

server.listen(PORT, HOST, () => {
  console.log(`hextech-runes-telemetry listening on ${HOST}:${PORT}`);
});
