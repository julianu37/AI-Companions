# Gate unico de release do AI Companions (1.9.5, achado C6 do backlog).
#
# Roda os checks de release em sequencia, imprime PASS/FAIL por item e termina com exit 1 se qualquer item falhar.
# Nada aqui altera o repositorio: o build grava em bin/ e copia para o DeployDir do Environment.props, como sempre.
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version 1.9.5
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -SkipGame
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -SkipIntents
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -SkipGame -SkipIntents
#
# Parametros:
#   -Version <x.y.z>  versao que esta sendo publicada. Padrao: a PluginVersion lida do fonte. Se for informada e nao
#                     bater com o fonte, o item "versao" falha (o zip esperado e AI_Companions-<versao>.zip).
#   -SkipGame         pula o locale-fallback (ele le a DLL publicada em BepInEx\plugins\AI-Companions do Valheim).
#   -SkipIntents      pula o ai-intents-check (roda em net472 e precisa das assemblies do jogo; e o item mais lento).
#
# Itens (na ordem):
#   1. dotnet build Companions.sln -c Release                                 0 erros
#   2. PluginVersion do CompanionsPlugin.cs x -Version                        tem que bater
#   3. python tools/audit_translations.py --lang Spanish                      0 lacunas
#   4. python tools/audit_translations.py --lang French                       0 lacunas
#   5. python tools/audit_translations.py --lang Italian                      0 lacunas
#   6. powershell -File tools/locale-fallback-check.ps1                       exit 0
#   7. python tools/gen_config_doc.py --check src/Companions/Core/ModConfig.cs package/CONFIGURATION.md
#      (SKIP enquanto o modo --check nao existir no gen_config_doc.py - a deteccao le o proprio script, entao o
#       item passa a valer sozinho quando o modo entrar)
#   8. python tools/mcp-devbridge/test_mcp_devbridge.py                       0 FAIL
#   9. dotnet run --project tools/ai-intents-check -c Release                 0 FAIL
#
# Exit: 0 = release liberada, 1 = algum item falhou. As ultimas linhas do comando que falhou saem indentadas.
# O que este gate NAO cobre (fica com o agente de docs/release): CHANGELOG, release notes, tag, staging e zip.
param(
  [string]$Version = '',
  [switch]$SkipGame,
  [switch]$SkipIntents
)

$root = Split-Path -Parent $PSScriptRoot
$total = 10
$script:index = 0
$script:failures = New-Object System.Collections.Generic.List[string]
$script:summary = New-Object System.Collections.Generic.List[object]

# Roda um executavel, junta stdout+stderr e devolve o exit code (sem depender de $ErrorActionPreference: um
# "command not found" vira exit -1 com a mensagem na lista, nunca uma excecao que derruba o gate).
function Invoke-Tool {
  param([string]$Exe, [string[]]$ArgList)
  $lines = New-Object System.Collections.Generic.List[string]
  $code = 0
  try {
    & $Exe @ArgList 2>&1 | ForEach-Object { $lines.Add([string]$_) }
    $code = $LASTEXITCODE
    if ($null -eq $code) { $code = 0 }
  } catch {
    $code = -1
    $lines.Add($_.Exception.Message)
  }
  [pscustomobject]@{ ExitCode = $code; Lines = $lines.ToArray() }
}

# As ultimas linhas do comando que falhou - e o que o agente precisa ver para corrigir.
function Show-Tail {
  param([string[]]$Lines, [int]$Count = 15)
  if (-not $Lines -or $Lines.Count -eq 0) { return }
  $start = [Math]::Max(0, $Lines.Count - $Count)
  for ($i = $start; $i -lt $Lines.Count; $i++) {
    Write-Host ("      | " + $Lines[$i]) -ForegroundColor DarkGray
  }
}

function Report {
  param([string]$Item, [bool]$Ok, [string]$Detail, [string[]]$Lines)
  $script:index++
  $status = 'FAIL'
  $color = 'Red'
  if ($Ok) { $status = 'PASS'; $color = 'Green' }
  $head = "[{0}/{1}] {2}  {3}" -f $script:index, $total, $status, $Item
  if ($Detail) { $head = $head + "  -  " + $Detail }
  Write-Host $head -ForegroundColor $color
  if (-not $Ok) {
    $script:failures.Add($Item)
    Show-Tail $Lines
  }
  $script:summary.Add([pscustomobject]@{ Item = $Item; Status = $status; Detail = $Detail })
}

function Skip {
  param([string]$Item, [string]$Why)
  $script:index++
  Write-Host ("[{0}/{1}] SKIP  {2}  -  {3}" -f $script:index, $total, $Item, $Why) -ForegroundColor Yellow
  $script:summary.Add([pscustomobject]@{ Item = $Item; Status = 'SKIP'; Detail = $Why })
}

# Le o resumo "=== N PASS / M FAIL ===" que os harnesses imprimem. Devolve "N PASS / M FAIL" ou ''.
function Get-HarnessSummary {
  param([string[]]$Lines)
  $text = ''
  foreach ($l in $Lines) {
    if ($l -match '===\s*(\d+)\s+PASS\s*/\s*(\d+)\s+FAIL\s*===') { $text = "$($Matches[1]) PASS / $($Matches[2]) FAIL" }
  }
  $text
}

Push-Location $root
$exitCode = 1
try {
  Write-Host "AI Companions - gate de release" -ForegroundColor Cyan
  Write-Host "raiz do repo: $root"
  if ($SkipGame) { Write-Host "(-SkipGame: locale-fallback pulado)" }
  if ($SkipIntents) { Write-Host "(-SkipIntents: ai-intents-check pulado)" }

  # ------------------------------------------------------------------ 1. build
  Write-Host ''
  Write-Host '== 1. build' -ForegroundColor Cyan
  $build = Invoke-Tool 'dotnet' @('build', 'Companions.sln', '-c', 'Release')
  $buildErrors = $null
  foreach ($l in $build.Lines) {
    if ($l -match '^\s*(\d+)\s+(Erro|Error)\(s\)') { $buildErrors = [int]$Matches[1] }
  }
  $buildOk = ($build.ExitCode -eq 0) -and ($null -eq $buildErrors -or $buildErrors -eq 0)
  $buildDetail = "exit $($build.ExitCode)"
  if ($null -ne $buildErrors) { $buildDetail = "$buildErrors erro(s), $buildDetail" }
  Report 'build: dotnet build Companions.sln -c Release' $buildOk $buildDetail $build.Lines

  # ------------------------------------------------------------------ 2. versao x zip
  Write-Host ''
  Write-Host '== 2. versao' -ForegroundColor Cyan
  $pluginCs = Join-Path $root 'src\Companions\Core\CompanionsPlugin.cs'
  $sourceVersion = ''
  if (Test-Path $pluginCs) {
    $hit = Select-String -Path $pluginCs -Pattern 'PluginVersion\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"' | Select-Object -First 1
    if ($hit) { $sourceVersion = $hit.Matches[0].Groups[1].Value }
  }
  $informed = '(nao informado)'
  $wanted = $sourceVersion
  if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $informed = $Version.Trim()
    $wanted = $informed
  }
  $zip = "AI_Companions-$wanted.zip"
  $versionOk = ($sourceVersion -ne '') -and ($wanted -eq $sourceVersion)
  $versionDetail = "PluginVersion=$sourceVersion, -Version=$informed, zip esperado $zip"
  if ($sourceVersion -eq '') { $versionDetail = "PluginVersion nao encontrada em $pluginCs, zip esperado $zip" }
  Report 'versao: PluginVersion x -Version (nome do zip)' $versionOk $versionDetail

  # ------------------------------------------------------------------ 3-5. traducoes
  Write-Host ''
  Write-Host '== 3-5. traducoes (0 lacunas)' -ForegroundColor Cyan
  foreach ($lang in 'Spanish', 'French', 'Italian') {
    $audit = Invoke-Tool 'python' @('tools\audit_translations.py', '--lang', $lang)
    $gaps = $null
    foreach ($l in $audit.Lines) {
      if ($l -match 'lacunas:\s*(\d+)') { $gaps = [int]$Matches[1] }
    }
    $auditOk = ($audit.ExitCode -eq 0) -and ($null -ne $gaps) -and ($gaps -eq 0)
    $auditDetail = "exit $($audit.ExitCode)"
    if ($null -ne $gaps) { $auditDetail = "$gaps lacuna(s), $auditDetail" }
    else { $auditDetail = "$auditDetail, sem a linha 'lacunas:' na saida" }
    Report "traducoes: $lang" $auditOk $auditDetail $audit.Lines
  }

  # ------------------------------------------------------------------ 6. locale
  Write-Host ''
  Write-Host '== 6. locale' -ForegroundColor Cyan
  if ($SkipGame) {
    Skip 'locale-fallback: CompanionLocale (auto/idioma sem perfil)' 'pulado por -SkipGame (le a DLL publicada no Valheim)'
  } else {
    $localeScript = Join-Path $root 'tools\locale-fallback-check.ps1'
    $loc = Invoke-Tool 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $localeScript)
    $last = ''
    if ($loc.Lines.Count -gt 0) { $last = $loc.Lines[$loc.Lines.Count - 1] }
    Report 'locale-fallback: CompanionLocale (auto/idioma sem perfil)' ($loc.ExitCode -eq 0) "exit $($loc.ExitCode), $last" $loc.Lines
  }

  # ------------------------------------------------------------------ 7. config doc
  Write-Host ''
  Write-Host '== 7. config doc' -ForegroundColor Cyan
  $genDoc = Join-Path $root 'tools\gen_config_doc.py'
  $hasCheck = $false
  if (Test-Path $genDoc) {
    $hasCheck = [bool](Select-String -Path $genDoc -Pattern '--check' -SimpleMatch -Quiet)
  }
  if (-not $hasCheck) {
    Skip 'config-doc: package/CONFIGURATION.md em dia' 'SKIP (modo --check pendente no gen_config_doc.py)'
  } else {
    $doc = Invoke-Tool 'python' @('tools\gen_config_doc.py', '--check', 'src\Companions\Core\ModConfig.cs', 'package\CONFIGURATION.md')
    Report 'config-doc: package/CONFIGURATION.md em dia' ($doc.ExitCode -eq 0) "exit $($doc.ExitCode)" $doc.Lines
  }

  # ------------------------------------------------------------------ 8. devbridge
  Write-Host ''
  Write-Host '== 8. MCP DevBridge' -ForegroundColor Cyan
  $bridge = Invoke-Tool 'python' @('tools\mcp-devbridge\test_mcp_devbridge.py')
  $bridgeSummary = Get-HarnessSummary $bridge.Lines
  $bridgeOk = ($bridge.ExitCode -eq 0) -and ($bridgeSummary -ne '') -and ($bridgeSummary -match '/ 0 FAIL$')
  $bridgeDetail = "exit $($bridge.ExitCode)"
  if ($bridgeSummary -ne '') { $bridgeDetail = "$bridgeSummary, $bridgeDetail" }
  else { $bridgeDetail = "$bridgeDetail, sem a linha de resumo" }
  Report 'mcp-devbridge: test_mcp_devbridge.py' $bridgeOk $bridgeDetail $bridge.Lines

  # ------------------------------------------------------------------ 9. ai intents
  Write-Host ''
  Write-Host '== 9. redes por palavras' -ForegroundColor Cyan
  if ($SkipIntents) {
    Skip 'ai-intents-check (redes por palavras)' 'pulado por -SkipIntents (net472 + assemblies do jogo)'
  } else {
    $intents = Invoke-Tool 'dotnet' @('run', '--project', 'tools\ai-intents-check', '-c', 'Release')
    $intentsSummary = Get-HarnessSummary $intents.Lines
    $intentsOk = ($intents.ExitCode -eq 0) -and ($intentsSummary -ne '') -and ($intentsSummary -match '/ 0 FAIL$')
    $intentsDetail = "exit $($intents.ExitCode)"
    if ($intentsSummary -ne '') { $intentsDetail = "$intentsSummary, $intentsDetail" }
    else { $intentsDetail = "$intentsDetail, sem a linha de resumo" }
    Report 'ai-intents-check: redes por palavras' $intentsOk $intentsDetail $intents.Lines
  }

  # ------------------------------------------------------------------ 10. datas no futuro
  # 05/11/2026 (REGRA 14): o agente escreveu uma data futura em 619 linhas de 48 arquivos. Este check fecha a
  # classe: qualquer data dd/mm/aaaa ou aaaa-mm-dd posterior a HOJE (local, nunca UTC) em arquivo rastreado.
  Write-Host ''
  Write-Host '== 10. datas no futuro' -ForegroundColor Cyan
  $dates = Invoke-Tool 'python' @('tools\check_dates.py', '--quiet')
  $datesOk = ($dates.ExitCode -eq 0)
  $datesDetail = "exit $($dates.ExitCode)"
  $datesLine = ($dates.Lines | Where-Object { $_ -match '\[ok\]|\[!\]' } | Select-Object -First 1)
  if ($datesLine) { $datesDetail = "$($datesLine.Trim()), $datesDetail" }
  Report 'datas no futuro (REGRA 14)' $datesOk $datesDetail $dates.Lines

  # ------------------------------------------------------------------ resumo
  Write-Host ''
  Write-Host '=== resumo ===' -ForegroundColor Cyan
  foreach ($r in $script:summary) {
    $color = 'Green'
    if ($r.Status -eq 'FAIL') { $color = 'Red' }
    elseif ($r.Status -eq 'SKIP') { $color = 'Yellow' }
    Write-Host ("{0,-5} {1}" -f $r.Status, $r.Item) -ForegroundColor $color
  }
  Write-Host ''
  if ($script:failures.Count -eq 0) {
    Write-Host 'RELEASE OK - todos os itens passaram' -ForegroundColor Green
    $exitCode = 0
  } else {
    Write-Host ("$($script:failures.Count) FALHA(S) - release BLOQUEADA: " + ($script:failures -join ' | ')) -ForegroundColor Red
    $exitCode = 1
  }
} finally {
  Pop-Location
}
exit $exitCode
