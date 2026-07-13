import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import test from "node:test";
import vm from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("../node_modules/typescript");
const source = readFileSync(new URL("../src/lib/bookingSubmission.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
});
const module = { exports: {} };
vm.runInNewContext(outputText, { module, exports: module.exports, require });
const booking = module.exports;

test("uses the backend origin for local appointment requests", () => {
  assert.equal(booking.resolveBookingApiUrl("/api/appointments", "localhost"), "http://127.0.0.1:5000/api/appointments");
  assert.equal(booking.resolveBookingApiUrl("/api/appointments", "hoanmakeup.vn"), "/api/appointments");
});

test("runs only one appointment submission while the first request is pending", async () => {
  const gate = booking.createBookingSubmissionGate();
  let calls = 0;
  let release;
  const first = gate.run(() => new Promise((resolve) => {
    calls += 1;
    release = resolve;
  }));
  const second = await gate.run(async () => { calls += 1; });

  assert.equal(second, undefined);
  assert.equal(calls, 1);
  release();
  await first;
  await gate.run(async () => { calls += 1; });
  assert.equal(calls, 2);
});
