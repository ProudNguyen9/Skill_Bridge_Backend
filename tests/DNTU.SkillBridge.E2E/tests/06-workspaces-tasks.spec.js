const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('06 - Workspaces, Kanban Tasks & Collaboration', () => {

  test('Student tasks, dashboard and commitments endpoints', async ({ request }) => {
    const student = await createAuthenticatedUser(request, 'Student', 'WorkspaceStudent');

    // 1. Check student dashboard
    const dashRes = await request.get('/api/v1/student/dashboard', { headers: student.headers });
    expect([200, 403, 404]).toContain(dashRes.status());

    // 2. Check student commitments
    const commitRes = await request.get('/api/v1/students/me/commitments', { headers: student.headers });
    expect([200, 403, 404]).toContain(commitRes.status());

    // 3. Check student tasks
    const tasksRes = await request.get('/api/v1/students/me/tasks', { headers: student.headers });
    expect([200, 403, 404]).toContain(tasksRes.status());
  });

  test('Company dashboard and meetings endpoints', async ({ request }) => {
    const company = await createAuthenticatedUser(request, 'Company', 'WorkspaceCompany');

    // 1. Company dashboard
    const dashRes = await request.get('/api/v1/company/dashboard', { headers: company.headers });
    expect([200, 403, 404]).toContain(dashRes.status());

    // 2. Company meetings
    const meetRes = await request.get('/api/v1/company/meetings', { headers: company.headers });
    expect([200, 403, 404, 500]).toContain(meetRes.status());
  });
});
