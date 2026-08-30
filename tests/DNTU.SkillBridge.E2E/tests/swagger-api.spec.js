const { test, expect } = require('@playwright/test');

const publicGetEndpoints = [
  '/health/live',
  '/api/v1/system/contract',
  '/api/v1/system/page-probe',
  '/api/v1/catalog/skills',
  '/api/v1/catalog/industries',
  '/api/v1/catalog/faculties',
  '/api/v1/catalog/majors',
  '/api/v1/catalog/banks',
  '/api/v1/catalog/project-metadata',
  '/api/v1/catalog/task-metadata',
  '/api/v1/skills',
  '/api/v1/companies',
  '/api/v1/projects'
];

test('Swagger UI loads and exposes the v1 document', async ({ page }) => {
  await page.goto('/swagger');
  await expect(page).toHaveTitle('DNTU SkillBridge API');
  await expect(page.locator('.swagger-ui')).toBeVisible();
  await expect(page.getByText('DNTU SkillBridge API v1')).toBeVisible();
  await expect(page.getByText('Authorize')).toBeVisible();
});

test('Swagger v1 JSON documents implemented Task 01-16 routes', async ({ request }) => {
  const response = await request.get('/swagger/v1/swagger.json');
  expect(response.ok()).toBeTruthy();

  const document = await response.json();
  expect(document.info.version).toBe('v1');
  expect(document.components.securitySchemes.Bearer.scheme).toBe('bearer');

  const paths = Object.keys(document.paths);
  for (const route of [
    '/api/v1/auth/login',
    '/api/v1/students/me',
    '/api/v1/companies/me',
    '/api/v1/lecturers/me',
    '/api/v1/company/projects',
    '/api/v1/projects',
    '/api/v1/saved-projects',
    '/api/v1/projects/{projectId}/applications'
  ]) {
    expect(paths).toContain(route);
  }
});

test('public Swagger endpoints respond without server errors', async ({ request }) => {
  for (const endpoint of publicGetEndpoints) {
    const response = await request.get(endpoint);
    expect(response.status(), `${endpoint} returned ${response.status()}`).toBeLessThan(500);
    expect(response.status(), `${endpoint} unexpectedly requires authentication`).not.toBe(401);
    expect(response.status(), `${endpoint} unexpectedly forbids public access`).not.toBe(403);
  }
});