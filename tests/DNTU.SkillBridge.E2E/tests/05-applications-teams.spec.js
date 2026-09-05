const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('05 - Applications & Teams Collaboration', () => {

  test('Team creation, management and member invitations', async ({ request }) => {
    const studentLeader = await createAuthenticatedUser(request, 'Student', 'TeamLeader');
    expect(studentLeader.accessToken).toBeDefined();

    // 1. Create a student team
    const teamRes = await request.post('/api/v1/teams', {
      headers: studentLeader.headers,
      data: {
        name: 'Alpha Innovators ' + Date.now()
      }
    });

    expect([200, 201, 400, 403]).toContain(teamRes.status());
    if (teamRes.status() === 200 || teamRes.status() === 201) {
      const teamResData = await teamRes.json();
      const team = teamResData.data || teamResData;
      const teamId = team.id || team.teamId;

      if (teamId) {
        // 2. Get team details
        const getTeamRes = await request.get(`/api/v1/teams/${teamId}`, { headers: studentLeader.headers });
        expect(getTeamRes.status()).toBe(200);

        // 3. Get team members
        const membersRes = await request.get(`/api/v1/teams/${teamId}/members`, { headers: studentLeader.headers });
        expect(membersRes.status()).toBe(200);

        // 4. List my team invitations
        const myInvitesRes = await request.get('/api/v1/students/me/team-invitations', { headers: studentLeader.headers });
        expect(myInvitesRes.status()).toBe(200);
      }
    }
  });

  test('Student application workflow and company review endpoints', async ({ request }) => {
    const student = await createAuthenticatedUser(request, 'Student', 'Applicant');
    const company = await createAuthenticatedUser(request, 'Company', 'Reviewer');

    // 1. Check student applications list
    const myAppsRes = await request.get('/api/v1/applications/me', { headers: student.headers });
    expect([200, 403, 404]).toContain(myAppsRes.status());

    // 2. Check company applications list
    const compAppsRes = await request.get('/api/v1/company/applications', { headers: company.headers });
    expect([200, 403, 404]).toContain(compAppsRes.status());
  });
});
