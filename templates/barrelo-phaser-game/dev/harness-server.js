#!/usr/bin/env node
/**
 * Dev-only stand-in for Barrelo: serves the harness control panel and transparently proxies every
 * other request to src/server.ts. Proxying keeps the browser same-origin with this process, so
 * server.ts never needs CORS headers added to it for local testing.
 *
 * Run alongside `npx tsx src/server.ts --port 6100` and `cd ui && npm run dev` (Vite, port 5173) —
 * see the top-level README's "Dev harness" section.
 */

import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function argValue(name, fallback) {
  const flag = "--" + name;
  const index = process.argv.indexOf(flag);
  return index >= 0 && process.argv[index + 1] !== undefined ? process.argv[index + 1] : fallback;
}

const HARNESS_PORT = Number(argValue("port", "8090"));
const GAME_PORT = Number(argValue("game-port", "6100"));
const HARNESS_HTML = fs.readFileSync(path.join(__dirname, "harness.html"));

// Same wedge layout a real board-detection source would report. Duplicated here (rather than
// imported from shared/types.ts) because this file is plain Node with no build step, and the
// wedge order isn't part of the wire contract anyway — it's just how we compute a plausible
// `position` for simulated throws.
const WEDGE_ORDER = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];
const RING_RADIUS_FRACTION = {
  Outer: 0.75,
  Inner: 0.4,
  Triple: 0.605,
  Double: 0.976,
  Miss: 1.05,
};
const RING_MULTIPLIER = {
  Outer: 1,
  Inner: 1,
  Triple: 3,
  Double: 2,
  Miss: 0,
  InnerBull: 1,
  OuterBull: 1,
};

function computePosition(segment, ring) {
  if (ring === "InnerBull" || ring === "OuterBull") {
    return { x: 0, y: ring === "InnerBull" ? 0 : 0.05 };
  }
  const index = WEDGE_ORDER.indexOf(segment);
  const angleRad = ((index >= 0 ? index : 0) * 18 * Math.PI) / 180;
  const fraction = RING_RADIUS_FRACTION[ring] ?? 0.4;
  return {
    x: Math.round(Math.sin(angleRad) * fraction * 1000) / 1000,
    y: Math.round(Math.cos(angleRad) * fraction * 1000) / 1000,
  };
}

function computeScore(segment, ring) {
  if (ring === "InnerBull") return 50;
  if (ring === "OuterBull") return 25;
  return segment * (RING_MULTIPLIER[ring] ?? 1);
}

// Builds a complete DetectedThrow (see ../shared/types.ts) — every field is required by the real
// contract, unlike a hand-rolled {segment, ring, position} shortcut, so throws fired through this
// harness look exactly like what Barrelo itself would send.
function buildDetectedThrow(segment, ring) {
  return {
    throwId: crypto.randomUUID(),
    segment,
    ring,
    score: computeScore(segment, ring),
    rawNotation: `${ring}-${segment}`,
    position: computePosition(segment, ring),
    confidence: null,
    boardId: "harness-board",
    cameraIndex: null,
    detectedAtUtc: new Date().toISOString(),
    source: "Simulator",
  };
}

function callGameServer(method, gamePath, body) {
  return new Promise((resolve, reject) => {
    const payload = body ? Buffer.from(JSON.stringify(body)) : null;
    const req = http.request(
      {
        host: "127.0.0.1",
        port: GAME_PORT,
        path: gamePath,
        method,
        headers: payload ? { "Content-Type": "application/json", "Content-Length": payload.length } : {},
      },
      (res) => {
        let raw = "";
        res.on("data", (chunk) => {
          raw += chunk;
        });
        res.on("end", () => {
          let data = {};
          try {
            data = raw ? JSON.parse(raw) : {};
          } catch {
            // leave as {}
          }
          resolve({ statusCode: res.statusCode, data });
        });
      }
    );
    req.on("error", reject);
    req.end(payload || undefined);
  });
}

function sendSimResult(res, promise) {
  promise
    .then(({ statusCode, data }) => {
      res.writeHead(statusCode, { "Content-Type": "application/json" });
      res.end(JSON.stringify(data));
    })
    .catch((err) => {
      res.writeHead(502, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ message: `Game server unreachable on port ${GAME_PORT}: ${err.message}` }));
    });
}

// GET-friendly helpers so a throw can be simulated with a single curl call instead of a hand-built
// JSON POST body, e.g. GET /sim/throw?segment=20&ring=Double
function handleSim(req, res, url) {
  const params = url.searchParams;

  if (url.pathname === "/sim/create") {
    const playerIds = (params.get("playerIds") || "").split(",").map((s) => s.trim()).filter(Boolean);
    return sendSimResult(res, callGameServer("POST", "/create", { playerIds, options: {}, playerGroups: null }));
  }

  if (url.pathname === "/sim/throw") {
    const segment = Number(params.get("segment"));
    const ring = params.get("ring") || "Outer";
    return sendSimResult(res, callGameServer("POST", "/throw", buildDetectedThrow(segment, ring)));
  }

  if (url.pathname === "/sim/bull") {
    return sendSimResult(res, callGameServer("POST", "/throw", buildDetectedThrow(25, "InnerBull")));
  }

  if (url.pathname === "/sim/random") {
    const segment = WEDGE_ORDER[Math.floor(Math.random() * WEDGE_ORDER.length)];
    const ring = ["Outer", "Inner", "Triple", "Double"][Math.floor(Math.random() * 4)];
    return sendSimResult(res, callGameServer("POST", "/throw", buildDetectedThrow(segment, ring)));
  }

  if (url.pathname === "/sim/end-turn") {
    return sendSimResult(res, callGameServer("POST", "/end-turn"));
  }

  if (url.pathname === "/sim/undo") {
    return sendSimResult(res, callGameServer("POST", "/undo"));
  }

  res.writeHead(404, { "Content-Type": "application/json" });
  res.end(JSON.stringify({ message: `Unknown helper route: ${url.pathname}` }));
}

function proxyToGameServer(req, res) {
  const chunks = [];
  req.on("data", (chunk) => chunks.push(chunk));
  req.on("end", () => {
    const body = Buffer.concat(chunks);
    const headers = { ...req.headers };
    delete headers.host;

    const upstream = http.request(
      { host: "127.0.0.1", port: GAME_PORT, path: req.url, method: req.method, headers },
      (upstreamRes) => {
        res.writeHead(upstreamRes.statusCode || 502, upstreamRes.headers);
        upstreamRes.pipe(res);
      }
    );
    upstream.on("error", (err) => {
      res.writeHead(502, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ message: `Game server unreachable on port ${GAME_PORT}: ${err.message}` }));
    });
    upstream.end(body.length ? body : undefined);
  });
}

const server = http.createServer((req, res) => {
  if (req.method === "GET" && (req.url === "/" || req.url === "/harness.html")) {
    res.writeHead(200, { "Content-Type": "text/html" });
    res.end(HARNESS_HTML);
    return;
  }

  const url = new URL(req.url, `http://127.0.0.1:${HARNESS_PORT}`);
  if (req.method === "GET" && url.pathname.startsWith("/sim/")) {
    return handleSim(req, res, url);
  }

  proxyToGameServer(req, res);
});

server.listen(HARNESS_PORT, "127.0.0.1", () => {
  console.log(`Harness listening on http://127.0.0.1:${HARNESS_PORT} (proxying game calls to 127.0.0.1:${GAME_PORT})`);
});
