const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawn, spawnSync } = require("node:child_process");
const test = require("node:test");

const SERVER_FILE = path.join(__dirname, "..", "server.js");

function makePayload(runId, isVictory = true) {
  return {
    schemaVersion: 1,
    modId: "HextechRunes",
    modVersion: "0.9.1",
    gameVersion: "0.111.0",
    uploadedAtUtc: new Date().toISOString(),
    run: {
      runId,
      seedHash: `seed-${runId}`,
      isVictory,
      runTime: 120,
      netMode: "Singleplayer",
      playerCount: 1
    },
    players: [{ slot: 0, character: "Ironclad", hextechRunes: ["TestRune"] }],
    runeChoices: [{ actIndex: 1, playerSlot: 0, rarity: "Silver", rerollCount: 0, options: ["TestRune"], selected: "TestRune" }],
    monsterHexes: [{ actIndex: 1, rarity: "Silver", hex: "TestHex" }]
  };
}

async function waitForServer(port, child) {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (child.exitCode != null) {
      throw new Error(`server exited early with code ${child.exitCode}`);
    }
    try {
      const response = await fetch(`http://127.0.0.1:${port}/health`);
      if (response.ok) {
        return;
      }
    } catch {
      // 服务进程尚未开始监听。
    }
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
  throw new Error("server did not become ready");
}

async function startServer(dataDir, port) {
  const publicDir = path.join(dataDir, "public");
  fs.mkdirSync(publicDir, { recursive: true });
  for (const fileName of ["index.html", "latest-version.json"]) {
    fs.copyFileSync(path.join(__dirname, "..", "public", fileName), path.join(publicDir, fileName));
  }
  const child = spawn(process.execPath, [SERVER_FILE], {
    env: {
      ...process.env,
      HOST: "127.0.0.1",
      PORT: String(port),
      DATA_DIR: dataDir,
      PUBLIC_DIR: publicDir,
      SUMMARY_FLUSH_INTERVAL_MS: "1000",
      RECENT_RUN_ID_LIMIT: "1000",
      RECENT_RUN_ID_TAIL_BYTES: String(1024 * 1024)
    },
    stdio: ["ignore", "pipe", "pipe"]
  });
  let output = "";
  child.stdout.on("data", (chunk) => { output += chunk; });
  child.stderr.on("data", (chunk) => { output += chunk; });
  try {
    await waitForServer(port, child);
  } catch (error) {
    child.kill("SIGKILL");
    throw new Error(`${error.message}\n${output}`);
  }
  return { child, getOutput: () => output };
}

async function stopServer(server) {
  const exited = new Promise((resolve) => server.child.once("exit", resolve));
  server.child.kill("SIGTERM");
  await exited;
}

async function postRun(port, payload) {
  const response = await fetch(`http://127.0.0.1:${port}/api/hextech-runes/run-result`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(payload)
  });
  assert.equal(response.status, 202);
  return response.json();
}

test("增量快照写入、尾部回放和近期去重不触发全量重建", async () => {
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), "hextech-telemetry-"));
  const port = 32000 + Math.floor(Math.random() * 10000);
  let server = await startServer(dataDir, port);
  try {
    const firstPayload = makePayload("run-0000000000000001");
    assert.deepEqual(await postRun(port, firstPayload), {
      ok: true,
      duplicate: false,
      runId: firstPayload.run.runId
    });
    const duplicate = await postRun(port, firstPayload);
    assert.equal(duplicate.duplicate, true);
    await new Promise((resolve) => setTimeout(resolve, 1200));

    const summary = await fetch(`http://127.0.0.1:${port}/api/hextech-runes/summary`).then((response) => response.json());
    assert.equal(summary.runCount, 1);
    assert.equal(summary.raw.totalUniqueRuns, 1);
    assert.equal(summary.versionSummaries, undefined);
  } finally {
    await stopServer(server);
  }

  const summaryPath = path.join(dataDir, "derived", "summary.json");
  const legacySummary = JSON.parse(fs.readFileSync(summaryPath, "utf8"));
  delete legacySummary._incremental;
  fs.writeFileSync(summaryPath, `${JSON.stringify(legacySummary, null, 2)}\n`, "utf8");

  const tailReceivedAtUtc = new Date(Date.now() + 1000).toISOString();
  const tailRecords = [];
  let lastPayload = null;
  for (let index = 1; index <= 10000; index += 1) {
    lastPayload = makePayload(`run-tail-${String(index).padStart(16, "0")}`, index % 2 === 0);
    tailRecords.push(JSON.stringify({
      receivedAtUtc: tailReceivedAtUtc,
      payloadHash: `simulated-crash-tail-${index}`,
      payload: lastPayload
    }));
  }
  fs.appendFileSync(path.join(dataDir, "run_results.jsonl"), `${tailRecords.join("\n")}\n`, "utf8");

  const bootstrap = spawnSync(process.execPath, [SERVER_FILE, "--bootstrap-incremental"], {
    env: { ...process.env, DATA_DIR: dataDir },
    encoding: "utf8"
  });
  assert.equal(bootstrap.status, 0, bootstrap.stderr || bootstrap.stdout);

  server = await startServer(dataDir, port);
  try {
    const summary = await fetch(`http://127.0.0.1:${port}/api/hextech-runes/summary`).then((response) => response.json());
    assert.equal(summary.runCount, 10001);
    assert.equal(summary.winCount, 5001);
    assert.equal(summary.raw.totalUniqueRuns, 10001);

    const duplicate = await postRun(port, lastPayload);
    assert.equal(duplicate.duplicate, true);
    const health = await fetch(`http://127.0.0.1:${port}/health`).then((response) => response.json());
    assert.equal(health.runs, 10001);
    assert.equal(health.derived.rebuilding, false);
    assert.equal(health.derived.checkpointOffset, health.derived.resultsSize);
  } finally {
    await stopServer(server);
  }
});
