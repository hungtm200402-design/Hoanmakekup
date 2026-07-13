import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import test from "node:test";
import vm from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("../node_modules/typescript");
const source = readFileSync(new URL("../src/lib/adminOrders.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
});
const module = { exports: {} };
vm.runInNewContext(outputText, { module, exports: module.exports, require });
const orders = module.exports;

test("returns only valid order actions for each order state", () => {
  assert.deepEqual(plain(orders.availableOrderStatusActions("Pending").map((action) => action.status)), ["Paid", "Cancelled"]);
  assert.deepEqual(plain(orders.availableOrderStatusActions("Paid").map((action) => action.status)), ["Shipping", "Cancelled"]);
  assert.deepEqual(plain(orders.availableOrderStatusActions("Shipping").map((action) => action.status)), ["Completed"]);
  assert.deepEqual(plain(orders.availableOrderStatusActions("Completed")), []);
});

function plain(value) {
  return JSON.parse(JSON.stringify(value));
}
