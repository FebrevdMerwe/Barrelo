#!/usr/bin/env node
/**
 * One-command dev harness launcher: starts the rules server, the Vite dev server, and the harness
 * control panel together, and stops all three on Ctrl+C. Equivalent to running the three commands
 * documented in the README's "Getting started" section by hand, in three separate terminals.
 *
 * Invokes `node` directly against each tool's own CLI entry point (rather than `npx tsx`/`npm run
 * dev`) for the same reason plugin.json does: on Windows those resolve to .cmd wrappers, which
 * can't be spawned without a shell — see the README's "Where to put your game" section.
 */

import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const UI_DIR = path.join(ROOT, "ui");

const GAME_PORT = "6100";
const VITE_PORT = "5173";
const HARNESS_PORT = "8090";

const processes = [];
let shuttingDown = false;

function startChild(label, color, command, args, cwd) {
  const child = spawn(command, args, { cwd, stdio: ["ignore", "pipe", "pipe"] });
  const prefix = `\x1b[${color}m[${label}]\x1b[0m `;

  const forward = (stream) => (chunk) => {
    for (const line of chunk.toString().split(/\r?\n/)) {
      if (line.length > 0) stream.write(prefix + line + "\n");
    }
  };
  child.stdout.on("data", forward(process.stdout));
  child.stderr.on("data", forward(process.stderr));

  child.on("exit", (code) => {
    if (shuttingDown) return;
    console.error(`${prefix}exited unexpectedly (code ${code}) — stopping the other processes.`);
    shutdown(1);
  });

  processes.push(child);
  return child;
}

function shutdown(exitCode) {
  if (shuttingDown) return;
  shuttingDown = true;
  for (const child of processes) child.kill();
  process.exitCode = exitCode ?? 0;
}

process.on("SIGINT", () => shutdown(0));
process.on("SIGTERM", () => shutdown(0));

startChild(
  "rules server",
  "36" /* cyan */,
  process.execPath,
  [path.join(ROOT, "node_modules/tsx/dist/cli.mjs"), "src/server.ts", "--port", GAME_PORT],
  ROOT
);

startChild(
  "vite",
  "35" /* magenta */,
  process.execPath,
  [path.join(ROOT, "node_modules/vite/bin/vite.js"), "--port", VITE_PORT],
  UI_DIR
);

startChild(
  "harness",
  "33" /* yellow */,
  process.execPath,
  [path.join(ROOT, "dev/harness-server.js"), "--port", HARNESS_PORT, "--game-port", GAME_PORT],
  ROOT
);

console.log(`
Starting the rules server (:${GAME_PORT}), Vite (:${VITE_PORT}), and the dev harness (:${HARNESS_PORT})...
Once all three are ready, open http://localhost:${HARNESS_PORT} to create a match and play it.
Press Ctrl+C to stop everything.
`);
