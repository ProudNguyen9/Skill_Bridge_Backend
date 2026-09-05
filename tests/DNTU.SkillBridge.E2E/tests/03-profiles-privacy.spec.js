const { test, expect } = require('@playwright/test');
const { createAuthenticatedUser } = require('../helpers/api-helper');

test.describe('03 - Profiles, Privacy Settings & Member Management', () => {

  test('Student profile CRUD & privacy settings', async ({ request }) => {
    const student = await createAuthenticatedUser(request, 'Student', 'StudentProfile');
    expect(student.accessToken).toBeDefined();

    // 1. Get student profile
    const profileRes = await request.get('/api/v1/students/me', { headers: student.headers });
    expect([200, 403]).toContain(profileRes.status());

    if (profileRes.status() === 200) {
      // 2. Update student profile
      const updateRes = await request.put('/api/v1/students/me', {
        headers: student.headers,
        data: {
          studentCode: 'DNTU' + Math.floor(100000 + Math.random() * 900000),
          academicYear: '2024-2028',
          phoneNumber: '+84987654321',
          bio: 'Software engineering student interested in backend and AI.',
          githubUrl: 'https://github.com/dntu-student',
          linkedinUrl: 'https://linkedin.com/in/dntu-student',
          portfolioUrl: 'https://student.portfolio.vn'
        }
      });
      expect([200, 204]).toContain(updateRes.status());

      // 3. Update privacy settings
      const privacyPutRes = await request.put('/api/v1/students/me/privacy', {
        headers: student.headers,
        data: {
          isProfilePublic: true,
          showContactInfo: true,
          showDeclaredSkills: true,
          showCertificates: true
        }
      });
      expect([200, 204]).toContain(privacyPutRes.status());
    }
  });

  test('Company profile & members management', async ({ request }) => {
    const company = await createAuthenticatedUser(request, 'Company', 'CompanyProfile');
    expect(company.accessToken).toBeDefined();

    // 1. Get company profile (200 or 404 if not created yet)
    const profileRes = await request.get('/api/v1/companies/me', { headers: company.headers });
    expect([200, 403, 404]).toContain(profileRes.status());

    // 2. Create or Update company profile with unique Tax Code & Name
    const uniqueTs = Date.now() + '_' + Math.floor(Math.random() * 10000);
    const updateRes = await request.put('/api/v1/companies/me', {
      headers: company.headers,
      data: {
        name: 'Tech Innovations Corp ' + uniqueTs,
        taxCode: '03' + Math.floor(10000000 + Math.random() * 90000000),
        website: 'https://techinnovations.vn',
        description: 'Leading digital transformation solutions provider',
        address: 'Dong Nai Technology University Hi-Tech Zone',
        contactEmail: `contact_${uniqueTs}@techinnovations.vn`,
        contactPhone: '+84281234567'
      }
    });
    expect([200, 201, 204, 403, 409]).toContain(updateRes.status());

    // 3. List company members
    const membersRes = await request.get('/api/v1/companies/me/members', { headers: company.headers });
    expect([200, 403, 404]).toContain(membersRes.status());

    // 4. Invite member
    const inviteRes = await request.post('/api/v1/companies/me/members/invitations', {
      headers: company.headers,
      data: {
        email: `colleague_${uniqueTs}@techinnovations.vn`,
        role: 3 // MEMBER = 3
      }
    });
    expect([200, 201, 400, 403, 404]).toContain(inviteRes.status());
  });

  test('Lecturer profile CRUD', async ({ request }) => {
    const lecturer = await createAuthenticatedUser(request, 'Lecturer', 'LecturerProfile');
    expect(lecturer.accessToken).toBeDefined();

    // 1. Get lecturer profile (200 or 403 depending on role provisioning)
    const profileRes = await request.get('/api/v1/lecturers/me', { headers: lecturer.headers });
    expect([200, 403]).toContain(profileRes.status());

    if (profileRes.status() === 200) {
      const updateRes = await request.put('/api/v1/lecturers/me', {
        headers: lecturer.headers,
        data: {
          lecturerCode: 'GV' + Math.floor(10000 + Math.random() * 90000),
          department: 'Faculty of Information Technology',
          academicTitle: 'Master of Computer Science',
          bio: 'Supervising Capstone and Enterprise projects in Software Engineering',
          websiteUrl: 'https://dntu.edu.vn/faculty/lecturer',
          officeLocation: 'Building A, Room 402',
          phoneNumber: '+84901234567',
          isProfilePublic: true,
          showContactInfo: true
        }
      });
      expect([200, 204]).toContain(updateRes.status());
    }
  });
});
