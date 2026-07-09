#!/usr/bin/env node
'use strict';

/*
 * Barrelo out-of-process game RPC contract implementation for Killer — see the Darts repo's
 * README.md ("Adding a new game" -> "Out-of-process games") for the full spec.
 *
 * Zero dependencies (Node's built-in http module only). Barrelo spawns one of these processes per
 * match and talks to it exclusively over HTTP; this process never imports or links against any
 * Barrelo/.NET code.
 *
 * State is always recomputed by replaying the full throw/end-turn log (see replay()) rather than
 * mutated in place — the only correct way to make /undo trivial when a stateful flag like isKiller
 * must not un-flip incorrectly depending on when in the log it's undone.
 */

import http from 'node:http';

function argValue(name, fallback) {
  const flag = '--' + name;
  const index = process.argv.indexOf(flag);
  return index >= 0 && process.argv[index + 1] !== undefined ? process.argv[index + 1] : fallback;
}

const PORT = Number(argValue('port', '6200'));
const PROTOCOL_VERSION = 1;
const STARTING_LIVES = 3;
const DARTS_PER_TURN = 3;
const MAX_PLAYERS = 20; // only 20 numbers (1-20) exist to assign
const BULL_SEGMENT = 25;

let playerIds = [];
let numbers = {}; // playerId -> unique number 1-20
let log = []; // { type: 'throw', throw: DetectedThrow } | { type: 'endOfTurn' }

// Assigns each player a unique number 1-20, generalizing the old client-side pickUniqueNumbers to N players.
function assignNumbers(ids) {
  const pool = [];
  for (let n = 1; n <= MAX_PLAYERS; n++) pool.push(n);
  for (let i = pool.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = pool[i];
    pool[i] = pool[j];
    pool[j] = tmp;
  }
  const result = {};
  ids.forEach((id, i) => { result[id] = pool[i]; });
  return result;
}

// Recomputes lives, killer status, turn order, and elimination order from the full log — the only
// place game rules actually live.
function replay() {
  const lives = {};
  const isKiller = {};
  playerIds.forEach((id) => { lives[id] = STARTING_LIVES; isKiller[id] = false; });

  let turnIndex = 0;
  let currentVisitThrows = [];
  const eliminationOrder = [];

  function advanceTurn() {
    const n = playerIds.length;
    for (let step = 1; step <= n; step++) {
      const idx = (turnIndex + step) % n;
      if (lives[playerIds[idx]] > 0) {
        turnIndex = idx;
        return;
      }
    }
  }

  for (const entry of log) {
    if (entry.type === 'endOfTurn') {
      currentVisitThrows = [];
      advanceTurn();
      continue;
    }

    const currentPlayerId = playerIds[turnIndex];
    const detectedThrow = entry.throw;
    const segment = detectedThrow.segment;
    const ring = detectedThrow.ring;

    if (segment !== BULL_SEGMENT) {
      if (segment === numbers[currentPlayerId]) {
        if (ring === 'Double' && !isKiller[currentPlayerId]) {
          isKiller[currentPlayerId] = true;
        }
      } else if (isKiller[currentPlayerId]) {
        const targetId = playerIds.find(
          (id) => id !== currentPlayerId && lives[id] > 0 && numbers[id] === segment
        );
        if (targetId) {
          lives[targetId] -= 1;
          if (lives[targetId] === 0) {
            eliminationOrder.push(targetId);
          }
        }
      }
    }

    currentVisitThrows.push(detectedThrow);
    if (currentVisitThrows.length >= DARTS_PER_TURN) {
      currentVisitThrows = [];
      advanceTurn();
    }
  }

  const alive = playerIds.filter((id) => lives[id] > 0);
  const isComplete = playerIds.length >= 2 && alive.length <= 1;
  const winnerId = isComplete && alive.length === 1 ? alive[0] : null;

  return {
    lives,
    isKiller,
    currentVisitThrows,
    currentPlayerId: isComplete ? null : playerIds[turnIndex],
    isComplete,
    winnerId,
    eliminationOrder,
  };
}

function snapshot() {
  const state = replay();
  const recentThrows = log.filter((e) => e.type === 'throw').slice(-15).map((e) => e.throw);

  return {
    // Barrelo overwrites MatchId on every call regardless of what's returned here, but the field is a
    // non-nullable Guid in the contract, so a real placeholder is required — null fails to deserialize.
    matchId: '00000000-0000-0000-0000-000000000000',
    gameId: 'killer',
    status: state.isComplete ? 'Complete' : 'InProgress',
    currentPlayerId: state.currentPlayerId,
    legNumber: 1,
    setNumber: 1,
    recentThrows,
    isComplete: state.isComplete,
    winnerPlayerIds: state.isComplete ? [state.winnerId] : null,
    payload: {
      numbers,
      lives: state.lives,
      isKiller: state.isKiller,
      dartsThrownThisVisit: state.currentVisitThrows.length,
    },
  };
}

function result() {
  const state = replay();
  const finalStandings = state.winnerId
    ? [state.winnerId, ...state.eliminationOrder.slice().reverse()]
    : [];
  return {
    winnerPlayerIds: state.winnerId ? [state.winnerId] : [],
    finalStandings,
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
        if (!body || !Array.isArray(body.playerIds) || body.playerIds.length < 2) {
          return badRequest(res, 'Killer requires at least 2 players.');
        }
        if (body.playerIds.length > MAX_PLAYERS) {
          return badRequest(res, `Killer supports at most ${MAX_PLAYERS} players.`);
        }
        playerIds = body.playerIds;
        numbers = assignNumbers(playerIds);
        log = [];
        return sendJson(res, 200, {});
      }

      if (req.method === 'POST' && req.url === '/throw') {
        const body = await readJsonBody(req);
        if (!body || typeof body.segment !== 'number' || typeof body.ring !== 'string') {
          return badRequest(res, 'Missing or malformed throw body.');
        }
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
        return sendJson(res, 200, result());
      }

      res.writeHead(404);
      res.end();
    })
    .catch((err) => {
      sendJson(res, 500, { message: String((err && err.message) || err) });
    });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`Killer (Barrelo remote game) listening on http://127.0.0.1:${PORT}`);
});
