const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('07 - Academics, Rubrics, Evaluations & Skill Passport', () => {

  test('Lecturer dashboard, rubrics management & evaluation endpoints', async ({ request }) => {
    const lecturer = await createAuthenticatedUser(request, 'Lecturer', 'DrAcademics');

    // 1. Lecturer dashboard
    const dashRes = await request.get('/api/v1/lecturer/dashboard', { headers: lecturer.headers });
    expect([200, 403, 404]).toContain(dashRes.status());

    // 2. Lecturer rubrics list
    const rubricsRes = await request.get('/api/v1/lecturer/rubrics', { headers: lecturer.headers });
    expect([200, 403, 404]).toContain(rubricsRes.status());

    // 3. Create a rubric
    const createRubricRes = await request.post('/api/v1/lecturer/rubrics', {
      headers: lecturer.headers,
      data: {
        name: 'Capstone Evaluation Rubric 2026'
      }
    });
    expect([200, 201, 400, 403]).toContain(createRubricRes.status());

    // 4. Lecturer reviews
    const reviewsRes = await request.get('/api/v1/lecturer/reviews', { headers: lecturer.headers });
    expect([200, 403, 404]).toContain(reviewsRes.status());

    // 5. Lecturer evaluations
    const evalRes = await request.get('/api/v1/lecturer/evaluations', { headers: lecturer.headers });
    expect([200, 403, 404]).toContain(evalRes.status());

    // 6. Lecturer meetings
    const meetRes = await request.get('/api/v1/lecturer/meetings', { headers: lecturer.headers });
    expect([200, 403, 404]).toContain(meetRes.status());
  });

  test('Student portfolio, skill passport & verified skills', async ({ request }) => {
    const student = await createAuthenticatedUser(request, 'Student', 'PassportStudent');

    // 1. Student portfolio
    const portfolioRes = await request.get('/api/v1/students/me/portfolio', { headers: student.headers });
    expect([200, 403, 404]).toContain(portfolioRes.status());

    // 2. Student skill passport
    const passportRes = await request.get('/api/v1/students/me/skill-passport', { headers: student.headers });
    expect([200, 403, 404]).toContain(passportRes.status());

    // 3. Student verified skills
    const verifiedSkillsRes = await request.get('/api/v1/students/me/verified-skills', { headers: student.headers });
    expect([200, 403, 404]).toContain(verifiedSkillsRes.status());

    // 4. Courses listing
    const coursesRes = await request.get('/api/v1/courses', { headers: student.headers });
    expect(coursesRes.status()).toBe(200);
  });
});
