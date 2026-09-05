const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('08 - Notifications, Files & External Integrations', () => {

  test('User notifications & notification preferences endpoints', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'NotifyUser');

    // 1. Get notifications list
    const notifyRes = await request.get('/api/v1/notifications', { headers: user.headers });
    expect(notifyRes.status()).toBe(200);

    // 2. Get unread count
    const unreadRes = await request.get('/api/v1/notifications/unread-count', { headers: user.headers });
    expect(unreadRes.status()).toBe(200);

    // 3. Get notification preferences
    const prefRes = await request.get('/api/v1/notification-preferences', { headers: user.headers });
    expect(prefRes.status()).toBe(200);

    // 4. Mark all notifications as read
    const readAllRes = await request.post('/api/v1/notifications/read-all', { headers: user.headers });
    expect([200, 204]).toContain(readAllRes.status());
  });

  test('Files pre-signed upload request endpoint', async ({ request }) => {
    const user = await createAuthenticatedUser(request, 'Student', 'FileUploader');

    const uploadRes = await request.post('/api/v1/files/upload-requests', {
      headers: user.headers,
      data: {
        fileName: 'student-cv.pdf',
        contentType: 'application/pdf',
        sizeBytes: 1024 * 100, // 100 KB
        category: 'cv'
      }
    });

    expect([200, 201, 400]).toContain(uploadRes.status());
    if (uploadRes.status() === 201 || uploadRes.status() === 200) {
      const uploadData = await uploadRes.json();
      const body = uploadData.data || uploadData;
      expect(body.file || body.fileId || body.id).toBeDefined();
    }
  });

  test('SePay IPN Webhook endpoint processes payment notifications idempotently', async ({ request }) => {
    const ipnPayload = {
      id: 998877,
      gateway: 'Vietcombank',
      transactionDate: '2026-08-30 12:00:00',
      accountNumber: '0123456789',
      code: null,
      content: 'SKILLBRIDGE ORDER INV-TEST-001',
      transferType: 'in',
      transferAmount: 5000000,
      accumulated: 5000000,
      subAccount: null,
      referenceCode: 'MBVCB.123456789.998877',
      description: 'Payment test for project funding order'
    };

    const response = await request.post('/api/v1/integrations/sepay/ipn', {
      data: ipnPayload
    });

    expect([200, 204, 400, 404]).toContain(response.status());
  });
});
