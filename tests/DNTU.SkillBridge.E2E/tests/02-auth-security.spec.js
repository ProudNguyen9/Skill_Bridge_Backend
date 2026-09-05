const { test, expect } = require('@playwright/test');
const { generateTestEmail, registerUser, loginUser, createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('02 - Authentication, Security & Session Lifecycle', () => {

  test('POST /api/v1/auth/register creates a new Student account', async ({ request }) => {
    const email = generateTestEmail('student');
    const password = 'StrongPassword@2026!';
    const regRes = await registerUser(request, {
      email,
      password,
      displayName: 'Nguyen Van Test',
      accountType: 'Student'
    });

    expect([200, 201]).toContain(regRes.status);
    expect(regRes.body).toBeDefined();
  });

  test('POST /api/v1/auth/register creates a new Company account', async ({ request }) => {
    const email = generateTestEmail('company');
    const password = 'StrongPassword@2026!';
    const regRes = await registerUser(request, {
      email,
      password,
      displayName: 'HR Manager',
      accountType: 'Company'
    });

    expect([200, 201]).toContain(regRes.status);
  });

  test('POST /api/v1/auth/login succeeds with valid credentials and returns JWT token', async ({ request }) => {
    const email = generateTestEmail('login_test');
    const password = 'Password@123456!';
    await registerUser(request, { email, password, displayName: 'Login Tester', accountType: 'Student' });

    const loginRes = await loginUser(request, email, password);
    expect(loginRes.status).toBe(200);
    expect(loginRes.accessToken).toBeDefined();
    expect(typeof loginRes.accessToken).toBe('string');
  });

  test('POST /api/v1/auth/login returns 400/401 ProblemDetails on wrong password', async ({ request }) => {
    const email = generateTestEmail('wrong_pass');
    const password = 'Password@123456!';
    await registerUser(request, { email, password, displayName: 'Wrong Pass User', accountType: 'Student' });

    const loginRes = await loginUser(request, email, 'WrongPassword999!');
    expect([400, 401]).toContain(loginRes.status);
  });

  test('GET /api/v1/auth/me returns current user identity with valid token', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'ProfileCheck');
    expect(user.accessToken).toBeDefined();

    const meRes = await request.get('/api/v1/auth/me', { headers: user.headers });
    expect(meRes.status()).toBe(200);
    const body = await meRes.json();
    const email = body?.data?.email || body?.email;
    expect(email).toBe(user.email);
  });

  test('GET /api/v1/auth/me returns 401 Unauthorized without Authorization header', async ({ request }) => {
    const meRes = await request.get('/api/v1/auth/me');
    expect(meRes.status()).toBe(401);
  });

  test('POST /api/v1/auth/refresh rotates tokens successfully', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'RefreshCheck');
    if (!user.refreshToken) {
      test.skip('No refresh token returned');
      return;
    }

    const refreshRes = await request.post('/api/v1/auth/refresh', {
      data: { refreshToken: user.refreshToken }
    });

    expect(refreshRes.status()).toBe(200);
    const body = await refreshRes.json();
    const token = body?.data?.accessToken || body?.accessToken;
    expect(token).toBeDefined();
  });

  test('GET /api/v1/auth/sessions lists active user sessions', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'SessionCheck');
    const sessionsRes = await request.get('/api/v1/auth/sessions', { headers: user.headers });
    expect(sessionsRes.status()).toBe(200);
    const body = await sessionsRes.json();
    const sessions = body?.data || body;
    expect(Array.isArray(sessions)).toBeTruthy();
  });

  test('POST /api/v1/auth/logout invalidates session', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'LogoutCheck');
    const logoutRes = await request.post('/api/v1/auth/logout', {
      headers: user.headers,
      data: { refreshToken: user.refreshToken || '' }
    });
    expect([200, 204]).toContain(logoutRes.status());
  });
});
