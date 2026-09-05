$ErrorActionPreference = 'Stop'
$B = 'http://localhost:5239'

function Invoke-Status($method, $uri, $headers) {
    try {
        $r = Invoke-WebRequest -Method $method -Uri $uri -Headers $headers -ErrorAction Stop
        return $r.StatusCode
    } catch {
        return [int]$_.Exception.Response.StatusCode
    }
}

$email = "co-$(Get-Random)@test.local"
$reg = [System.Text.Encoding]::UTF8.GetBytes((@{email=$email;displayName='Cong ty Duyet';password='Password#123456';accountType='Company'} | ConvertTo-Json))
Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/register" -ContentType 'application/json; charset=utf-8' -Body $reg | Out-Null
$login = Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/login" -ContentType 'application/json' -Body (@{email=$email;password='Password#123456'} | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.data.accessToken)" }
Invoke-RestMethod -Method Put -Uri "$B/api/v1/companies/me" -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes((@{name="Cong ty Duyet $(Get-Random)"} | ConvertTo-Json))) | Out-Null
$skills = (Invoke-RestMethod "$B/api/v1/catalog/skills").data

$payload = @{
  title = 'Du an duyet smoke test'
  difficulty = 'INTERMEDIATE'
  workType = 'ONSITE'
  durationWeeks = 8
  expectedStudentCount = 4
  minTeamSize = 2
  maxTeamSize = 6
  applicationDeadline = (Get-Date).AddDays(21).ToString('o')
  skills = @(@{ skillId = $skills[0].id; requirementLevel = 'MUST_HAVE'; isRequired = $true })
  deliverables = @(@{ name = 'San pham' })
}
$proj = Invoke-RestMethod -Method Post -Uri "$B/api/v1/company/projects" -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Depth 5)))
$projectId = $proj.data.id
Write-Host "draft created: $($proj.data.status)"
Write-Host "submit (unverified, expect 409): $(Invoke-Status Post "$B/api/v1/company/projects/$projectId/submit" $h)"
Write-Host "cancel draft: $(Invoke-Status Post "$B/api/v1/company/projects/$projectId/cancel" $h)"
Write-Host "reopen: $(Invoke-Status Post "$B/api/v1/company/projects/$projectId/reopen" $h)"
$detail = Invoke-RestMethod "$B/api/v1/company/projects/$projectId" -Headers $h
Write-Host "final status: $($detail.data.status)"
Write-Host "admin queue as company (expect 403): $(Invoke-Status Get "$B/api/v1/admin/project-approvals" $h)"
