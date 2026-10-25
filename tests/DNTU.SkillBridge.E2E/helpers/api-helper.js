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
  accountType = 'Student',
  studentCode = '22' + Math.floor(100000 + Math.random() * 900000)
}) {
  const payload = {
    email,
    password,
    displayName,
    accountType,
    ...(accountType === 'Student' ? { studentCode } : {})
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
  const typeLower = (accountType || 'student').toLowerCase();

  if (typeLower.includes('lecturer')) {
    const email = 'lecturer.test@skillbridge.local';
    const password = 'SkillBridge.Test#2026';
    const loginResult = await loginUser(request, email, password);
    return {
      email,
      password,
      displayName: `${namePrefix} Lecturer`,
      accountType: 'Lecturer',
      ...loginResult
    };
  }

  if (typeLower.includes('admin')) {
    const email = 'admin.test@skillbridge.local';
    const password = 'SkillBridge.Test#2026';
    const loginResult = await loginUser(request, email, password);
    return {
      email,
      password,
      displayName: `${namePrefix} Admin`,
      accountType: 'Admin',
      ...loginResult
    };
  }

  const email = generateTestEmail(typeLower);
  const password = 'TestUser@2026!Strong';
  const displayName = `${namePrefix} ${accountType}`;
  const studentCode = '22' + Math.floor(100000 + Math.random() * 900000);

  await registerUser(request, {
    email,
    password,
    displayName,
    accountType: typeLower.includes('company') ? 'Company' : 'Student',
    studentCode
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
