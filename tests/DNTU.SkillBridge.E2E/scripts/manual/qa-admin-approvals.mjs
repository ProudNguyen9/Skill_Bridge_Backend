#!/usr/bin/env node
// Isolated GUI QA only. Usage:
// node qa-admin-approvals.mjs seed --base-url http://localhost:5017 [--extras]
// node qa-admin-approvals.mjs verify --base-url http://localhost:5017
// Seed never applies admin decisions or resubmits an existing non-draft project.
import assert from 'node:assert/strict';
import { parseArgs } from 'node:util';

const PREFIX = 'QA ADM-01';
const PASSWORD = 'SkillBridge.Test#2026'; // docs/auth-test-accounts.md
const REQUIRED_TITLES = Array.from({ length: 14 }, (_, i) => `${PREFIX} ${String(i + 1).padStart(2, '0')}`);
const EXTRA_TITLES = [`${PREFIX} EXTRA REJECT`, `${PREFIX} EXTRA REQUEST-CHANGES`];

async function main() {
  const { values, positionals } = parseArgs({
    options: { 'base-url': { type: 'string' }, extras: { type: 'boolean', default: false } },
    allowPositionals: true,
  });
  const [mode] = positionals;
  assert(positionals.length === 1 && ['seed', 'verify'].includes(mode), 'Specify seed or verify.');
  assert(values['base-url'], '--base-url http://localhost:5017 is required.');
  const base = new URL(values['base-url']);
  assert(base.protocol === 'http:' && base.hostname === 'localhost' && base.port === '5017'
    && base.pathname === '/' && !base.username && !base.password && !base.search && !base.hash,
  'Refusing target: only http://localhost:5017 is allowed (isolated admin QA API).');

  async function request(path, { token, method = 'GET', body, expected = 200 } = {}) {
    assert(path.startsWith('/api/v1/') && !path.includes('://'), 'Only local API paths are allowed.');
    const target = new URL(path, base);
    assert.equal(target.origin, base.origin);
    let response;
    try {
      response = await fetch(target, {
        method,
        redirect: 'error', // Never follow redirects or forward credentials to another target.
        signal: AbortSignal.timeout(30000),
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        },
        ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
      });
    } catch {
      throw new Error(`${method} ${path}: local API unavailable, timed out, or redirected.`);
    }
    assert.equal(response.status, expected, `${method} ${path}: expected HTTP ${expected}, got ${response.status}`);
    return response.status === 204 ? undefined : response.json();
  }

  async function login(email) {
    const result = await request('/api/v1/auth/login', { method: 'POST', body: { email, password: PASSWORD } });
    assert(typeof result.data?.accessToken === 'string', 'Login did not return an access token.');
    return result.data.accessToken;
  }

  async function pages(path, token, filters = {}, pageSize = 100) {
    const rows = [];
    let total;
    for (let page = 1; ; page++) {
      const query = new URLSearchParams({ ...filters, page: String(page), pageSize: String(pageSize) });
      const result = await request(`${path}?${query}`, { token });
      assert(Array.isArray(result.data) && result.meta, 'Expected paged response.');
      assert.equal(result.meta.page, page);
      assert.equal(result.meta.pageSize, pageSize);
      assert(Number.isInteger(result.meta.totalItems) && result.meta.totalItems >= 0);
      total ??= result.meta.totalItems;
      assert.equal(result.meta.totalItems, total, 'Queue changed during verification; rerun when GUI actions finish.');
      assert.equal(result.meta.totalPages, Math.ceil(total / pageSize));
      assert.equal(result.data.length, Math.min(pageSize, Math.max(0, total - (page - 1) * pageSize)));
      rows.push(...result.data);
      if (page >= result.meta.totalPages) break;
    }
    assert.equal(rows.length, total);
    return rows;
  }

  const companyToken = await login('company1.test@skillbridge.local');
  const companyPath = '/api/v1/company/projects';
  const queuePath = '/api/v1/admin/project-approvals';
  const before = await pages(companyPath, companyToken);

  if (mode === 'seed') {
    const catalog = await request('/api/v1/catalog/skills');
    const skill = catalog.data.find(item => item.isActive);
    assert(skill, 'An active catalog skill is required.');
    const titles = [...REQUIRED_TITLES, ...(values.extras ? EXTRA_TITLES : [])];
    const output = [];
    for (const title of titles) {
      const matches = before.filter(project => project.title === title);
      assert(matches.length <= 1, `Duplicate QA title already exists: ${title}`);
      let project = matches[0];
      const created = !project;
      const payload = {
        title,
        summary: 'Complete isolated admin approval GUI QA project.',
        problemStatement: 'Validate approval queue search, pagination, detail, and reviewer actions.',
        businessRequirements: 'Persist reviewer decisions and maintain append-only approval history.',
        technicalConstraints: '.NET API and local-only test services; no external integrations.',
        difficulty: 'INTERMEDIATE', workType: 'HYBRID', durationWeeks: 8,
        applicationDeadline: new Date(Date.now() + 30 * 86400000).toISOString(),
        expectedStudentCount: 3, minTeamSize: 2, maxTeamSize: 4,
        allowanceAmount: 3000000, allowanceCurrency: 'VND',
        skills: [{ skillId: skill.id, requirementLevel: 'MUST_HAVE', isRequired: true }],
        deliverables: [{ name: 'QA handover report', description: 'Approval workflow verification results.' }],
      };
      if (!project) {
        project = (await request(companyPath, { token: companyToken, method: 'POST', body: payload, expected: 201 })).data;
        before.push(project);
      } else if (project.status === 'DRAFT') {
        // Repair an interrupted seed, but never reopen/resubmit a GUI-reviewed project.
        project = (await request(`${companyPath}/${project.id}`, { token: companyToken, method: 'PUT', body: payload })).data;
      }
      if (project.status === 'DRAFT') {
        project = (await request(`${companyPath}/${project.id}/submit`, { token: companyToken, method: 'POST' })).data;
        assert.equal(project.status, 'PENDING_APPROVAL');
      }
      output.push({ id: project.id, title: project.title, status: project.status, created });
    }
    console.log(JSON.stringify({ mode, baseUrl: base.origin, projects: output }, null, 2));
    return;
  }

  const adminToken = await login('admin.test@skillbridge.local');
  const companyQa = before.filter(project => project.title.startsWith(PREFIX));
  for (const title of REQUIRED_TITLES) {
    assert.equal(companyQa.filter(project => project.title === title).length, 1, `Missing or duplicate QA project: ${title}`);
  }
  const expectedIds = companyQa.filter(project => project.isActive !== false && project.status !== 'DRAFT')
    .map(project => project.id).sort();
  const queue = await pages(queuePath, adminToken, { search: PREFIX });
  assert.deepEqual(queue.map(project => project.projectId).sort(), expectedIds);
  assert.equal(new Set(queue.map(project => project.projectId)).size, queue.length);
  const paged = await pages(queuePath, adminToken, { search: PREFIX }, 5);
  assert.deepEqual(paged, queue, 'Search page boundaries/order must match the full search result.');
  const mixedCase = await pages(queuePath, adminToken, { search: `  ${PREFIX.toLowerCase()}  ` });
  assert.deepEqual(mixedCase, queue, 'Search must be case-insensitive and trimmed.');
  const baseline = await pages(queuePath, adminToken);
  const whitespace = await pages(queuePath, adminToken, { search: '  \t ' });
  assert.deepEqual(whitespace, baseline, 'Whitespace search must be ignored.');
  assert.deepEqual(await pages(queuePath, adminToken, { search: `${PREFIX} NO-MATCH-9b482a1a` }), []);

  const state = [];
  for (const project of companyQa.sort((a, b) => a.title.localeCompare(b.title))) {
    const detail = (await request(`${queuePath}/${project.id}`, { token: adminToken })).data;
    assert.equal(detail.projectId, project.id);
    assert.equal(detail.title, project.title);
    assert.equal(detail.status, project.status, 'Company and admin persisted status must agree.');
    assert(Array.isArray(detail.history));
    if (detail.status !== 'DRAFT') assert(detail.history.length > 0, 'Submitted QA projects require persisted history.');
    const last = detail.history.at(-1);
    const expectedDecision = {
      PENDING_APPROVAL: 'SUBMITTED', APPROVED: ['APPROVED', 'RESUMED'],
      CHANGES_REQUESTED: 'CHANGES_REQUESTED', REJECTED: 'REJECTED', SUSPENDED: 'SUSPENDED', CANCELLED: 'CANCELLED',
    }[detail.status];
    if (expectedDecision) {
      assert([expectedDecision].flat().includes(last?.decision), `Unexpected last decision for ${detail.title}`);
    }
    const row = queue.find(item => item.projectId === project.id);
    if (row) {
      assert.equal(row.status, detail.status);
      assert.equal(row.lastDecision, last?.decision ?? null);
      assert.equal(row.lastDecidedAt, last?.decidedAt ?? null);
    }
    state.push({
      id: project.id, title: detail.title, status: detail.status,
      submittedAt: detail.submittedAt, approvedAt: detail.approvedAt, publishedAt: detail.publishedAt,
      history: detail.history.map(item => ({ decision: item.decision, note: item.note, decidedAt: item.decidedAt })),
    });
  }
  if (queue.length) {
    const companySearch = await pages(queuePath, adminToken, { search: queue[0].companyName.toUpperCase() });
    for (const project of queue) assert(companySearch.some(item => item.projectId === project.projectId));
  }
  for (const status of new Set(queue.map(project => project.status))) {
    const filtered = await pages(queuePath, adminToken, { search: PREFIX, status }, 5);
    assert.deepEqual(filtered, queue.filter(project => project.status === status));
  }
  console.log(JSON.stringify({
    mode, baseUrl: base.origin, result: 'PASS', queueCount: queue.length,
    checks: ['title/company case-insensitive search', 'trim/whitespace/no-match', 'paging/meta/order', 'status filters', 'persisted status/history'],
    projects: state,
  }, null, 2));
}

main().catch(error => {
  // Do not serialize HTTP payloads/headers or tokens on failures.
  console.error(`Approval QA failed: ${error.message}`);
  process.exitCode = 1;
});
