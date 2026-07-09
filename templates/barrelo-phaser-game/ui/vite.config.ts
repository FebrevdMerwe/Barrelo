import { defineConfig } from "vite";

export default defineConfig({
  // Critical: the built index.html is served from a nested path (/plugins/{gameId}/ui/), so asset
  // URLs must be relative — Vite's default root-absolute paths (/assets/...) would 404 there even
  // though they work fine under `npm run dev`'s own root-served dev server.
  base: "./",
  build: {
    outDir: "dist",
  },
});
