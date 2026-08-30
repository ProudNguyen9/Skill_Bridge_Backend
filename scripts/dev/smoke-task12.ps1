$ErrorActionPreference = 'Stop'
$B = 'http://localhost:5239'
$email = "co-$(Get-Random)@test.local"
$reg = [System.Text.Encoding]::UTF8.GetBytes((@{email=$email;displayName='Cong ty Du an';password='Password#123456';accountType='Company'} | ConvertTo-Json))
Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/register" -ContentType 'application/json; charset=utf-8' -Body $reg | Out-Null
$login = Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/login" -ContentType 'application/json' -Body (@{email=$email;password='Password#123456'} | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.data.accessToken)" }
Invoke-RestMethod -Method Put -Uri "$B/api/v1/companies/me" -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes((@{name="Cong ty Du an $(Get-Random)"} | ConvertTo-Json))) | Out-Null
$skills = (Invoke-RestMethod "$B/api/v1/catalog/skills").data

$payload = @{
  title = 'Xay dung ung dung quan ly thu vien'
  difficulty = 'INTERMEDIATE'
  workType = 'HYBRID'
  durationWeeks = 12
  expectedStudentCount = 4
  minTeamSize = 2
  maxTeamSize = 6
  allowanceAmount = 5000000
  allowanceCurrency = 'VND'
  applicationDeadline = (Get-Date).AddDays(30).ToString('o')
  skills = @(@{ skillId = $skills[0].id; requirementLevel = 'MUST_HAVE'; isRequired = $true })
  deliverables = @(@{ name = 'Source code'; description = 'Ma nguon day du' })
}
$proj = Invoke-RestMethod -Method Post -Uri "$B/api/v1/company/projects" -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Depth 5)))
Write-Host "created: $($proj.data.code) slug=$($proj.data.slug) status=$($proj.data.status)"
$projectId = $proj.data.id

$upd = @{
  title = 'Ung dung quan ly thu vien online'
  difficulty = 'ADVANCED'
  workType = 'HYBRID'
  durationWeeks = 10
  expectedStudentCount = 5
  minTeamSize = 2
  maxTeamSize = 6
  skills = @(@{ skillId = $skills[1].id; requirementLevel = 'IMPORTANT'; isRequired = $true })
  deliverables = @(@{ name = 'Bao cao tong ket' })
}
$updRes = Invoke-RestMethod -Method Put -Uri "$B/api/v1/company/projects/$projectId" -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes(($upd | ConvertTo-Json -Depth 5)))
Write-Host "updated title: $($updRes.data.title) skills=$($updRes.data.skills.Count) deliverables=$($updRes.data.deliverables.Count)"

$prog = (Invoke-RestMethod "$B/api/v1/company/projects/$projectId/progress" -Headers $h).data
Write-Host "progress: status=$($prog.status) skills=$($prog.skillCount) deliverables=$($prog.deliverableCount) isDraft=$($prog.isDraft)"
$list = Invoke-RestMethod "$B/api/v1/company/projects?sort=title" -Headers $h
Write-Host "list total=$($list.meta.totalItems)"
Write-Host "team members: $((Invoke-RestMethod "$B/api/v1/company/projects/$projectId/team" -Headers $h).data.Count)"
$del = Invoke-WebRequest -Method Delete -Uri "$B/api/v1/company/projects/$projectId" -Headers $h
Write-Host "delete draft: $($del.StatusCode)"
$gone = Invoke-WebRequest -Uri "$B/api/v1/company/projects/$projectId" -Headers $h -SkipHttpErrorCheck
Write-Host "get after delete: $($gone.StatusCode)"
