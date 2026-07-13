import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import test from "node:test";
import vm from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("../node_modules/typescript");
const source = readFileSync(new URL("../src/lib/adminAuth.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    esModuleInterop: true
  }
});

const module = { exports: {} };
vm.runInNewContext(outputText, { module, exports: module.exports, require });
const auth = module.exports;

test("stores and reads a valid admin session", () => {
  const store = memoryStorage();
  auth.saveAdminSession({ token: "abc", expiresAt: "2030-01-01T00:00:00Z", role: "Admin" }, store);

  assert.deepEqual(plain(auth.readAdminSession(store, new Date("2026-01-01T00:00:00Z"))), {
    token: "abc",
    expiresAt: "2030-01-01T00:00:00Z",
    role: "Admin"
  });
  assert.deepEqual(plain(auth.getAdminAuthHeaders(auth.readAdminSession(store, new Date("2026-01-01T00:00:00Z")))), {
    Authorization: "Bearer abc"
  });
});

test("ignores expired or malformed admin sessions", () => {
  const store = memoryStorage();
  auth.saveAdminSession({ token: "abc", expiresAt: "2020-01-01T00:00:00Z", role: "Admin" }, store);
  assert.equal(auth.readAdminSession(store, new Date("2026-01-01T00:00:00Z")), null);

  store.setItem(auth.adminAuthStorageKey, "{");
  assert.equal(auth.readAdminSession(store), null);
});

test("clears admin session", () => {
  const store = memoryStorage();
  auth.saveAdminSession({ token: "abc", expiresAt: "2030-01-01T00:00:00Z", role: "Admin" }, store);
  auth.clearAdminSession(store);

  assert.equal(auth.readAdminSession(store), null);
});

function memoryStorage() {
  const values = new Map();
  return {
    getItem(key) {
      return values.get(key) ?? null;
    },
    setItem(key, value) {
      values.set(key, value);
    },
    removeItem(key) {
      values.delete(key);
    }
  };
}

function plain(value) {
  return JSON.parse(JSON.stringify(value));
}
