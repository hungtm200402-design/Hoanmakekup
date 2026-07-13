import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import test from "node:test";
import vm from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("../node_modules/typescript");
const source = readFileSync(new URL("../src/lib/cart.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    esModuleInterop: true
  }
});

const module = { exports: {} };
vm.runInNewContext(outputText, {
  module,
  exports: module.exports,
  require,
  window: { dispatchEvent() {} },
  Event
});

const cart = module.exports;

const lipstick = {
  id: "11111111-1111-1111-1111-111111111111",
  slug: "son-black-rouge-air-fit",
  name: "Son Black Rouge Air Fit",
  price: 280000,
  salePrice: 250000,
  stock: 3,
  imagePath: "/images/products/black-rouge.png"
};

const foundation = {
  id: "22222222-2222-2222-2222-222222222222",
  slug: "perfect-diary",
  name: "Kem nền Perfect Diary",
  price: 350000,
  salePrice: null,
  stock: 5,
  imagePath: "/images/products/perfect-diary.png"
};

test("adds backend products and clamps quantity to stock", () => {
  const items = cart.addCartItem([], lipstick, 2);
  const next = cart.addCartItem(items, lipstick, 5);

  assert.equal(next.length, 1);
  assert.equal(next[0].id, lipstick.id);
  assert.equal(next[0].quantity, 3);
});

test("increases, decreases, removes items, and calculates totals", () => {
  let items = cart.addCartItem([], lipstick, 1);
  items = cart.addCartItem(items, foundation, 2);
  items = cart.increaseCartItem(items, lipstick.id);
  items = cart.decreaseCartItem(items, foundation.id);

  assert.deepEqual(plain(cart.getCartSummary(items)), {
    totalQuantity: 3,
    totalPrice: 850000
  });

  items = cart.removeCartItem(items, lipstick.id);
  assert.equal(items.length, 1);
  assert.equal(items[0].id, foundation.id);
});

test("serializes, parses, and ignores malformed storage values", () => {
  const items = cart.addCartItem([], lipstick, 2);
  const stored = cart.serializeCartItems(items);

  assert.deepEqual(plain(cart.parseCartItems(stored)), plain(items));
  assert.deepEqual(plain(cart.parseCartItems("not-json")), []);
  assert.deepEqual(plain(cart.parseCartItems(JSON.stringify([{ id: lipstick.id }]))), []);
});

test("reconciles stored cart items with current backend product data", () => {
  const stored = cart.addCartItem([], { ...lipstick, price: 280000, salePrice: null, stock: 10 }, 8);
  const reconciled = cart.reconcileCartItems(stored, [{ ...lipstick, price: 300000, salePrice: 240000, stock: 4 }]);

  assert.equal(reconciled.length, 1);
  assert.equal(reconciled[0].price, 300000);
  assert.equal(reconciled[0].salePrice, 240000);
  assert.equal(reconciled[0].quantity, 4);
});


function plain(value) {
  return JSON.parse(JSON.stringify(value));
}
