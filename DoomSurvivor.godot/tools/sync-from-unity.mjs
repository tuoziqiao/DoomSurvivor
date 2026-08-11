#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const projectRoot = resolve(import.meta.dirname, "..");
const pairs = [
  ["config", join(projectRoot, "..", "DoomSurvivor.unity", "Assets", "StreamingAssets", "GameConfig"), join(projectRoot, "resources", "config")],
  ["art", join(projectRoot, "..", "DoomSurvivor.unity", "Assets", "DoomSurvivor", "Presentation", "Resources", "Art"), join(projectRoot, "resources", "art")],
  ["models", join(projectRoot, "..", "DoomSurvivor.unity", "Assets", "DoomSurvivor", "Presentation", "Resources", "Models"), join(projectRoot, "resources", "models")]
];

const ignored = new Set([".meta", ".import", ".uid"]);

function filesUnder(root) {
  if (!existsSync(root)) return new Map();
  const result = new Map();
  const visit = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const fullPath = join(directory, entry.name);
      if (entry.isDirectory()) {
        visit(fullPath);
        continue;
      }
      const extension = entry.name.includes(".") ? entry.name.slice(entry.name.lastIndexOf(".")).toLowerCase() : "";
      if (ignored.has(extension)) continue;
      result.set(relative(root, fullPath).replaceAll("\\", "/"), fullPath);
    }
  };
  visit(root);
  return result;
}

function hash(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

let failures = 0;
for (const [name, unityRoot, godotRoot] of pairs) {
  const unityFiles = filesUnder(unityRoot);
  const godotFiles = filesUnder(godotRoot);
  const keys = new Set([...unityFiles.keys(), ...godotFiles.keys()]);
  let missing = 0;
  let extra = 0;
  let mismatched = 0;
  for (const key of [...keys].sort()) {
    if (!unityFiles.has(key)) {
      extra++;
      console.log(`[${name}] Godot-only: ${key}`);
      continue;
    }
    if (!godotFiles.has(key)) {
      missing++;
      console.log(`[${name}] Missing in Godot: ${key}`);
      continue;
    }
    if (hash(unityFiles.get(key)) !== hash(godotFiles.get(key))) {
      mismatched++;
      console.log(`[${name}] Hash mismatch: ${key}`);
    }
  }
  const summary = `${name}: unity=${unityFiles.size}, godot=${godotFiles.size}, missing=${missing}, extra=${extra}, mismatched=${mismatched}`;
  console.log(summary);
  failures += missing + mismatched;
}

if (failures > 0) {
  console.error(`Resource check failed with ${failures} blocking difference(s).`);
  process.exitCode = 1;
} else {
  console.log("Resource check passed.");
}
