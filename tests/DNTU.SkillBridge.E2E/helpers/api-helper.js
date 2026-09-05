/**
 * API Test Automation Helpers
 */

function generateTestEmail(role = 'user') {
  const ts = Date.now();
  const rand = Math.floor(Math.random() * 10000);
  return `test_${role}_${ts}_${rand}@skillbridge.local`;
}

async function registerUser(request, {
  email,
  password = 'TestPassword@2026!',
  displayName = 'Automated Test User',
  accountType = 'Student' // 'Student' | 'Company'
}) {
  const payload = {
    email,
    password,
    displayName,
    accountType
  };

  const response = await request.post('/api/v1/auth/register', {
    data: payload
  });

  return {
    status: response.status(),
    response,
    body: await response.json().catch(() => null),
    credentials: { email, password }
  };
}

async function loginUser(request, email, password = 'TestPassword@2026!') {
  const response = await request.post('/api/v1/auth/login', {
    data: { email, password }
  });

  const status = response.status();
  const body = await response.json().catch(() => null);

  const accessToken = body?.data?.accessToken || body?.accessToken || body?.token;
  const refreshToken = body?.data?.refreshToken || body?.refreshToken;

  return {
    status,
    body,
    accessToken,
    refreshToken,
    headers: accessToken ? { 'Authorization': `Bearer ${accessToken}`, 'Content-Type': 'application/json' } : {}
  };
}

async function createAuthenticatedUser(request, accountType = 'Student', namePrefix = 'Tester') {
  const email = generateTestEmail(accountType.toLowerCase());
  const password = 'TestUser@2026!Strong';
  const displayName = `${namePrefix} ${accountType}`;

  await registerUser(request, {
    email,
    password,
    displayName,
    accountType: accountType === 'Company' ? 'Company' : 'Student'
  });

  const loginResult = await loginUser(request, email, password);
  return {
    email,
    password,
    displayName,
    accountType,
    ...loginResult
  };
}

module.exports = {
  generateTestEmail,
  registerUser,
  loginUser,
  createAuthenticatedUser
};
