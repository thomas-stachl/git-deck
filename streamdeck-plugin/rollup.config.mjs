import commonjs from "@rollup/plugin-commonjs";
import { nodeResolve } from "@rollup/plugin-node-resolve";
import terser from "@rollup/plugin-terser";
import typescript from "@rollup/plugin-typescript";
import fs from "node:fs";

const manifest = JSON.parse(fs.readFileSync("./com.gitdeck.plugin.sdPlugin/manifest.json", "utf8"));
const isWatch = !!process.env.ROLLUP_WATCH;

export default {
  input: "src/plugin.ts",
  output: {
    file: `${manifest.UUID}.sdPlugin/bin/plugin.js`,
    format: "es",
    sourcemap: !isWatch,
  },
  // Node built-ins (net, fs, ...) stay external — the plugin runs as a real Node process launched
  // by Stream Deck, not a browser bundle; everything else (including vscode-jsonrpc) gets bundled
  // into the single CodePath entry point the manifest points at.
  plugins: [
    typescript(),
    // exportConditions: vscode-jsonrpc's "./node" entry is gated behind a "node" package.json
    // exports condition, which node-resolve doesn't apply by default.
    nodeResolve({ browser: false, preferBuiltins: true, exportConditions: ["node"] }),
    commonjs(),
    !isWatch && terser(),
  ].filter(Boolean),
};
