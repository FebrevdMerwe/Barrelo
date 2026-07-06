#!/usr/bin/env node
'use strict';

/*
 * Reference implementation of Barrelo's out-of-process game RPC contract — see
 * ../../README.md ("Adding a new game" -> "Out-of-process games") for the full spec.
 *
 * Deliberately zero dependencies (Node's built-in http module only, no npm install needed) so it's
 * copy-pasteable as a starting point regardless of what package manager/framework you'd rather use for
 * a real game. Barrelo spawns one of these processes per match and talks to it exclusively over HTTP;
 * it never imports or links against any Barrelo/.NET code.
 *
 * The game itself — "Round the Clock" — is intentionally simple and structurally different from the
 * built-in X01/Cricket/Kickoff games (target progression, not score countdown), to prove the contract
 * isn't secretly shaped around X01's rules.
 */

const http = require('http');

function argValue(name, fallback) {
  const flag = '--' + name;
  const index = process.argv.indexOf(flag);
  return index >= 0 && process.argv[index + 1] !== undefined ? process.argv[index + 1] : fallback;
}

const PORT = Number(argValue('port', '6100'));
const PROTOCOL_VERSION = 1;
const TARGETS_TO_WIN = 20;

let playerIds = [];
// Every /throw and /end-turn call, in order — state is always recomputed by replaying this log (same
// "replay from history" approach X01Game uses internally), which makes /undo trivial: just pop the log.
let log = [];

function initialState(ids) {
  const targets = {};
  ids.forEach((id) => { targets[id] = 1; });
  return { targets };
}

// Recomputes turn order, per-player progress, and the current visit's throws from the full log — the only
// place game rules actually live.
function replay() {
  const { targets } = initialState(playerIds);
  let turnIndex = 0;
  let currentVisitThrows = [];

  for (const entry of log) {
    if (entry.type === 'endOfTurn') {
      turnIndex = (turnIndex + 1) % playerIds.length;
      currentVisitThrows = [];
      continue;
    }

    const playerId = playerIds[turnIndex];
    const detectedThrow = entry.throw;

    if (targets[playerId] <= TARGETS_TO_WIN && detectedThrow.segment === targets[playerId]) {
      targets[playerId] += 1;
    }

    currentVisitThrows.push(detectedThrow);
    if (currentVisitThrows.length >= 3) {
      turnIndex = (turnIndex + 1) % playerIds.length;
      currentVisitThrows = [];
    }
  }

  const winnerIds = playerIds.filter((id) => targets[id] > TARGETS_TO_WIN);
  return {
    targets,
    currentPlayerId: winnerIds.length > 0 ? null : playerIds[turnIndex],
    currentVisitThrows,
    winnerIds,
  };
}

function snapshot() {
  const state = replay();
  const recentThrows = log.filter((e) => e.type === 'throw').slice(-15).map((e) => e.throw);

  return {
    // Barrelo overwrites MatchId on every call regardless of what's returned here, but the field is a
    // non-nullable Guid in the contract, so a real placeholder is required — null fails to deserialize.
    matchId: '00000000-0000-0000-0000-000000000000',
    gameId: 'round-the-clock',
    status: state.winnerIds.length > 0 ? 'Complete' : 'InProgress',
    currentPlayerId: state.currentPlayerId,
    legNumber: 1,
    setNumber: 1,
    recentThrows,
    isComplete: state.winnerIds.length > 0,
    winnerPlayerIds: state.winnerIds.length > 0 ? state.winnerIds : null,
    payload: {
      targetsToWin: TARGETS_TO_WIN,
      targets: state.targets,
      currentVisitThrows: state.currentVisitThrows,
    },
  };
}

function sendJson(res, statusCode, body) {
  const json = JSON.stringify(body);
  res.writeHead(statusCode, {
    'Content-Type': 'application/json',
    'Content-Length': Buffer.byteLength(json),
  });
  res.end(json);
}

function badRequest(res, message) {
  // Barrelo maps a 400 here straight back onto GameRuleViolationException on the host side.
  sendJson(res, 400, { message });
}

function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', (chunk) => { raw += chunk; });
    req.on('end', () => {
      if (!raw) return resolve(null);
      try {
        resolve(JSON.parse(raw));
      } catch (err) {
        reject(err);
      }
    });
    req.on('error', reject);
  });
}

const server = http.createServer((req, res) => {
  Promise.resolve()
    .then(async () => {
      if (req.method === 'GET' && req.url === '/health') {
        return sendJson(res, 200, { status: 'ok', protocolVersion: PROTOCOL_VERSION });
      }

      if (req.method === 'POST' && req.url === '/create') {
        const body = await readJsonBody(req);
        if (!body || !Array.isArray(body.playerIds) || body.playerIds.length === 0) {
          return badRequest(res, 'Round the Clock requires at least one player.');
        }
        playerIds = body.playerIds;
        log = [];
        return sendJson(res, 200, {});
      }

      if (req.method === 'POST' && req.url === '/throw') {
        const body = await readJsonBody(req);
        if (!body) return badRequest(res, 'Missing throw body.');
        log.push({ type: 'throw', throw: body });
        return sendJson(res, 200, {});
      }

      if (req.method === 'POST' && req.url === '/end-turn') {
        log.push({ type: 'endOfTurn' });
        return sendJson(res, 200, {});
      }

      if (req.method === 'POST' && req.url === '/undo') {
        if (log.length === 0) return badRequest(res, 'There is nothing to undo.');
        log.pop();
        return sendJson(res, 200, {});
      }

      if (req.method === 'GET' && req.url === '/state') {
        return sendJson(res, 200, snapshot());
      }

      if (req.method === 'GET' && req.url === '/result') {
        const state = replay();
        return sendJson(res, 200, {
          winnerPlayerIds: state.winnerIds,
          finalStandings: playerIds,
        });
      }

      res.writeHead(404);
      res.end();
    })
    .catch((err) => {
      sendJson(res, 500, { message: String((err && err.message) || err) });
    });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`Round the Clock (reference remote game) listening on http://127.0.0.1:${PORT}`);
});
