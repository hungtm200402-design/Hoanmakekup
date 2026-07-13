import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import test from "node:test";
import vm from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("../node_modules/typescript");
const source = readFileSync(new URL("../src/lib/adminAppointments.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    esModuleInterop: true
  }
});

const module = { exports: {} };
vm.runInNewContext(outputText, { module, exports: module.exports, require });
const appointments = module.exports;

test("returns Vietnamese labels for appointment statuses", () => {
  assert.equal(appointments.appointmentStatusText("Pending"), "Chờ xác nhận");
  assert.equal(appointments.appointmentStatusText("Confirmed"), "Đã xác nhận");
  assert.equal(appointments.appointmentStatusText("Rejected"), "Đã từ chối");
  assert.equal(appointments.appointmentStatusText("Completed"), "Đã hoàn thành");
  assert.equal(appointments.appointmentStatusText("Cancelled"), "Đã hủy");
});

test("returns allowed admin actions by current appointment status", () => {
  assert.deepEqual(plain(appointments.availableAppointmentStatusActions("Pending").map((action) => action.status)), [
    "Confirmed",
    "Rejected",
    "Cancelled"
  ]);
  assert.deepEqual(plain(appointments.availableAppointmentStatusActions("Confirmed").map((action) => action.status)), [
    "Completed",
    "Cancelled"
  ]);
  assert.deepEqual(plain(appointments.availableAppointmentStatusActions("Completed")), []);
});

test("normalizes appointment filters into stable backend query parameters", () => {
  assert.deepEqual(plain(appointments.normalizeAppointmentFilters({
    customerName: "  Hoa  ",
    phone: " 0909000111 ",
    status: "Pending",
    service: "  Co dau ",
    fromDate: "2026-08-10",
    toDate: "2026-08-11"
  })), {
    customerName: "Hoa",
    phone: "0909000111",
    status: "Pending",
    service: "Co dau",
    fromDate: "2026-08-10",
    toDate: "2026-08-11"
  });
});

function plain(value) {
  return JSON.parse(JSON.stringify(value));
}
