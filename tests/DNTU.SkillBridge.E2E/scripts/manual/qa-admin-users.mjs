import assert from "node:assert/strict";

const args = process.argv.slice(2);
const baseIndex = args.indexOf("--base-url");
const base = baseIndex >= 0 ? args[baseIndex + 1] : undefined;
if (base !== "http://localhost:5017") {
  throw new Error("Pass --base-url http://localhost:5017 for the isolated local QA API. Other targets are refused.");
}

async function request(path, { token, body } = {}) {
  const response = await fetch(`${base}/api/v1${path}`, {
    method: body ? "POST" : "GET",
    redirect: "error",
    headers: {
      ...(body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  return response;
}

async function login(email) {
  const response = await request("/auth/login", { body: { email, password: "SkillBridge.Test#2026" } });
  assert.equal(response.status, 200, "Seeded local test account must be available");
  const result = await response.json();
  assert.equal(typeof result.data?.accessToken, "string");
  return result.data.accessToken;
}

const token = await login("admin.test@skillbridge.local");
async function list(params = {}) {
  const response = await request(`/admin/users?${new URLSearchParams(params)}`, { token });
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.ok(Array.isArray(body.data));
  assert.ok(body.meta);
  for (const user of body.data) {
    assert.deepEqual(Object.keys(user).sort(), ["userId", "email", "displayName", "roles", "isActive", "createdAt"].sort());
    assert.ok(Array.isArray(user.roles));
    assert.equal(typeof user.isActive, "boolean");
  }
  return body;
}

const first = await list({ Page: "1", PageSize: "2" });
const next = await list({ Page: "2", PageSize: "2" });
assert.equal(first.meta.totalItems, next.meta.totalItems);
assert.ok(first.data.every((user) => !next.data.some((other) => other.userId === user.userId)));
const search = await list({ Search: "ADMIN.TEST@SKILLBRIDGE.LOCAL", Role: "ADMIN", IsActive: "true" });
assert.ok(search.data.some((user) => user.email === "admin.test@skillbridge.local"));
assert.ok(search.data.every((user) => user.roles.includes("ADMIN") && user.isActive));
const superAdmins = await list({ Role: "SUPER_ADMIN" });
assert.ok(superAdmins.data.length > 0);
assert.ok(superAdmins.data.every((user) => user.roles.includes("SUPER_ADMIN")));
assert.equal((await list({ Search: "qa-directory-impossible-no-match-71c8" })).meta.totalItems, 0);
assert.equal((await request("/admin/users?PageSize=101", { token })).status, 400);
assert.equal((await request("/admin/users?Role=NOT_A_ROLE", { token })).status, 400);
assert.equal((await request("/admin/users")).status, 401);
const companyToken = await login("company1.test@skillbridge.local");
assert.equal((await request("/admin/users", { token: companyToken })).status, 403);
console.log(JSON.stringify({ result: "PASS", target: base, totalUsers: first.meta.totalItems, checks: ["safe fields", "paging without duplicates", "search+role+active filters", "SUPER_ADMIN membership", "no match", "invalid parameters 400", "unauthenticated 401", "company denied 403"], mutations: "No account-management mutations; only local test logins" }, null, 2));
