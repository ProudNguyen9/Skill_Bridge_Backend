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

test('Swagger UI HTML document loads and contains title', async ({ request }) => {
  const response = await request.get('/swagger/index.html');
  expect(response.status()).toBe(200);
  const html = await response.text();
  expect(html).toContain('DNTU SkillBridge API');
  expect(html).toContain('swagger-ui');
});

test('Swagger v1 JSON documents implemented key system routes', async ({ request }) => {
  const response = await request.get('/swagger/v1/swagger.json');
  expect(response.ok()).toBeTruthy();

  const document = await response.json();
  expect(document.info.version).toBe('v1');
  expect(document.components.securitySchemes.Bearer.scheme).toBe('bearer');

  const rawPaths = Object.keys(document.paths);
  const normalizedPaths = rawPaths.map(p => p.replace('/api/v{version}/', '/api/v1/'));

  for (const route of [
    '/api/v1/auth/login',
    '/api/v1/students/me',
    '/api/v1/companies/me',
    '/api/v1/lecturers/me',
    '/api/v1/company/projects',
    '/api/v1/projects',
    '/api/v1/students/me/saved-projects',
    '/api/v1/projects/{projectId}/applications'
  ]) {
    expect(normalizedPaths).toContain(route);
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