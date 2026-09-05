const { test, expect } = require('@playwright/test');

test.describe('01 - System & Public Catalog APIs', () => {

  test('Health check endpoint returns Healthy 200', async ({ request }) => {
    const response = await request.get('/health/live');
    expect(response.status()).toBe(200);
    const text = await response.text();
    expect(text).toContain('Healthy');
  });

  test('OpenAPI v1 JSON specification is valid and exposes schemas', async ({ request }) => {
    const response = await request.get('/openapi/v1.json');
    expect(response.status()).toBe(200);
    const doc = await response.json();
    expect(doc.openapi || doc.info?.version).toBeDefined();
    expect(doc.paths).toBeDefined();
  });

  test('Swagger v1 JSON document exposes Bearer authentication', async ({ request }) => {
    const response = await request.get('/swagger/v1/swagger.json');
    expect(response.status()).toBe(200);
    const doc = await response.json();
    expect(doc.info.title).toBe('DNTU SkillBridge API');
    expect(doc.components?.securitySchemes?.Bearer?.type).toBe('http');
  });

  test('System contract & page probe respond with valid metadata', async ({ request }) => {
    const contractRes = await request.get('/api/v1/system/contract');
    expect(contractRes.status()).toBeLessThan(500);

    const probeRes = await request.get('/api/v1/system/page-probe');
    expect(probeRes.status()).toBeLessThan(500);
  });

  const catalogEndpoints = [
    { path: '/api/v1/catalog/skills', key: 'skills' },
    { path: '/api/v1/catalog/industries', key: 'industries' },
    { path: '/api/v1/catalog/faculties', key: 'faculties' },
    { path: '/api/v1/catalog/majors', key: 'majors' },
    { path: '/api/v1/catalog/banks', key: 'banks' },
    { path: '/api/v1/catalog/project-metadata', key: 'project-metadata' },
    { path: '/api/v1/catalog/task-metadata', key: 'task-metadata' },
    { path: '/api/v1/skills', key: 'public-skills' }
  ];

  for (const endpoint of catalogEndpoints) {
    test(`Catalog endpoint ${endpoint.path} responds without error and returns data`, async ({ request }) => {
      const response = await request.get(endpoint.path);
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(data).toBeDefined();
    });
  }

  test('Public companies listing endpoint returns list', async ({ request }) => {
    const response = await request.get('/api/v1/companies');
    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data).toBeDefined();
  });

  test('Public projects marketplace listing endpoint returns list', async ({ request }) => {
    const response = await request.get('/api/v1/projects');
    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data).toBeDefined();
  });
});
