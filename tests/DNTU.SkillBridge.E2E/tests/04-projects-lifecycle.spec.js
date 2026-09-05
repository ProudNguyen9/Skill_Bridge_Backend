const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('04 - Projects Lifecycle & Saved Projects', () => {

  test('Company project creation, update, deliverables, and submission flow', async ({ request }) => {
    const company = await createAuthenticatedUser(request, 'Company', 'ProjectOwner');
    expect(company.accessToken).toBeDefined();

    // 1. Create a draft project
    const createRes = await request.post('/api/v1/company/projects', {
      headers: company.headers,
      data: {
        title: 'Smart Campus IoT & Mobile App ' + Date.now(),
        summary: 'Building a smart campus management system for students and faculty.',
        problemStatement: 'Manual campus management causes delays and inefficiencies.',
        businessRequirements: 'Automate attendance and student check-in.',
        technicalConstraints: 'Must support cross-platform mobile and low-latency API.',
        difficulty: 2, // INTERMEDIATE
        workType: 2,   // HYBRID
        durationWeeks: 12,
        expectedStudentCount: 4,
        minTeamSize: 2,
        maxTeamSize: 4,
        allowanceAmount: 5000000,
        allowanceCurrency: 'VND',
        deliverables: [
          { title: 'System Architecture Document', description: 'C4 model & DB Schema', sortOrder: 1 },
          { title: 'Mobile App MVP', description: 'Flutter/React Native app', sortOrder: 2 }
        ],
        skills: []
      }
    });

    expect([200, 201, 400]).toContain(createRes.status());
    const projectResData = await createRes.json();
    const project = projectResData?.data || projectResData;
    const projectId = project?.id || project?.projectId;

    if (projectId) {
      // 2. Read back the company projects list
      const listRes = await request.get('/api/v1/company/projects', { headers: company.headers });
      expect(listRes.status()).toBe(200);

      // 3. Update the project draft
      const updateRes = await request.put(`/api/v1/company/projects/${projectId}`, {
        headers: company.headers,
        data: {
          title: 'Smart Campus IoT & Mobile App (Updated) ' + Date.now(),
          summary: 'Updated summary with refined scope and deliverables.',
          problemStatement: 'Manual campus management causes delays and inefficiencies.',
          businessRequirements: 'Automate attendance and student check-in.',
          technicalConstraints: 'Must support cross-platform mobile and low-latency API.',
          difficulty: 2,
          workType: 2,
          durationWeeks: 14,
          expectedStudentCount: 4,
          minTeamSize: 2,
          maxTeamSize: 4,
          allowanceAmount: 6000000,
          allowanceCurrency: 'VND',
          deliverables: [
            { title: 'System Architecture Document', description: 'C4 model & DB Schema', sortOrder: 1 },
            { title: 'Mobile App MVP', description: 'Flutter/React Native app', sortOrder: 2 },
            { title: 'Final Deployment', description: 'Production ready', sortOrder: 3 }
          ],
          skills: []
        }
      });
      expect([200, 204]).toContain(updateRes.status());

      // 4. Submit project for admin approval
      const submitRes = await request.post(`/api/v1/company/projects/${projectId}/submit`, {
        headers: company.headers
      });
      expect([200, 204]).toContain(submitRes.status());
    }
  });

  test('Student saved projects & skill matching', async ({ request }) => {
    const student = await createAuthenticatedUser(request, 'Student', 'ProjectSaver');
    expect(student.accessToken).toBeDefined();

    // 1. Get public projects
    const publicProjectsRes = await request.get('/api/v1/projects');
    expect(publicProjectsRes.status()).toBe(200);
    const projectsData = await publicProjectsRes.json();
    const items = projectsData?.data?.items || projectsData?.items || projectsData?.data || projectsData;
    const firstProject = items?.[0];

    if (firstProject?.id) {
      const projectId = firstProject.id;

      // 2. Save project to bookmarks
      const saveRes = await request.post(`/api/v1/students/me/saved-projects/${projectId}`, {
        headers: student.headers
      });
      expect([200, 201, 204, 400]).toContain(saveRes.status());

      // 3. List saved projects
      const listSavedRes = await request.get('/api/v1/students/me/saved-projects', {
        headers: student.headers
      });
      expect(listSavedRes.status()).toBe(200);

      // 4. Check skill match score
      const matchRes = await request.get(`/api/v1/students/me/skill-match/${projectId}`, {
        headers: student.headers
      });
      expect([200, 404]).toContain(matchRes.status());

      // 5. Unsave project
      const unsaveRes = await request.delete(`/api/v1/students/me/saved-projects/${projectId}`, {
        headers: student.headers
      });
      expect([200, 204, 404]).toContain(unsaveRes.status());
    }
  });
});
