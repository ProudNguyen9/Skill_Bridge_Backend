$ErrorActionPreference = 'Continue'
$B = 'http://localhost:5239'
$prefix = "dbg$(Get-Random)"

$email = "co-$prefix@test.local"
$reg = [System.Text.Encoding]::UTF8.GetBytes((@{email=$email;displayName='Co';password='Password#123456';accountType='Company'} | ConvertTo-Json))
Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/register" -ContentType 'application/json; charset=utf-8' -Body $reg | Out-Null
$login = Invoke-RestMethod -Method Post -Uri "$B/api/v1/auth/login" -ContentType 'application/json' -Body (@{email=$email;password='Password#123456'} | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.data.accessToken)" }
$compBody = [System.Text.Encoding]::UTF8.GetBytes((@{name="Cong ty $prefix"} | ConvertTo-Json))
$comp = Invoke-WebRequest -Method Put -Uri "$B/api/v1/companies/me" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $compBody
Write-Host "company put: $($comp.StatusCode)"
$skills = (Invoke-RestMethod "$B/api/v1/catalog/skills").data
$sid = $skills[0].id

$createBody = [System.Text.Encoding]::UTF8.GetBytes((@{
  title = "$prefix Guard"
  difficulty = 'BEGINNER'; workType = 'ONSITE'; durationWeeks = 4
  expectedStudentCount = 3; minTeamSize = 2; maxTeamSize = 4
  skills = @(@{ skillId = $sid; requirementLevel = 'MUST_HAVE'; isRequired = $true })
  deliverables = @(@{ name = 'Báo cáo bàn giao' })
} | ConvertTo-Json -Depth 5))
$created = Invoke-RestMethod -Method Post -Uri "$B/api/v1/company/projects" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $createBody
$projectId = $created.data.id
Write-Host "created: $($created.data.status) id=$projectId"

$updBody = [System.Text.Encoding]::UTF8.GetBytes((@{
  title = "$prefix Guard"
  summary = "Tóm tắt $prefix Guard"
  problemStatement = 'Vấn đề thực tế cần giải quyết.'
  businessRequirements = 'Yêu cầu nghiệp vụ rõ ràng.'
  technicalConstraints = 'Ràng buộc kỹ thuật .NET.'
  difficulty = 'BEGINNER'; workType = 'ONSITE'; durationWeeks = 4
  applicationDeadline = (Get-Date).AddDays(14).ToString('o')
  expectedStudentCount = 3; minTeamSize = 2; maxTeamSize = 4
  allowanceAmount = 2000000; allowanceCurrency = 'VND'
  skills = @(@{ skillId = $sid; requirementLevel = 'MUST_HAVE'; isRequired = $true })
  deliverables = @(@{ name = 'Báo cáo bàn giao'; description = $null })
} | ConvertTo-Json -Depth 5))
try {
  $upd = Invoke-WebRequest -Method Put -Uri "$B/api/v1/company/projects/$projectId" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $updBody -ErrorAction Stop
  Write-Host "update: $($upd.StatusCode)"
} catch {
  $resp = $_.Exception.Response
  Write-Host "update FAILED: $([int]$resp.StatusCode)"
  $stream = $resp.GetResponseStream()
  $reader = New-Object System.IO.StreamReader($stream)
  Write-Host $reader.ReadToEnd()
}
