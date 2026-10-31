# A REGRA da multilinguagem (definida na M1, nao mexer): o idioma do JOGO decide o perfil.
#   Portuguese_Brazilian -> pt-br / pt / voz 42
#   English              -> en-us / en / voz 3
#   Spanish              -> es    / es / voz 28
#   French               -> fr    / fr / voz 30
#   Italian              -> it    / it / voz 35
#   QUALQUER OUTRO (de, ru, pl, tr, ko, sv, uk, ja, zh...) -> INGLES (en-us / en / voz 3)
#
# Le so a DLL compilada (nao abre o jogo, nao compila nada) e chama CompanionLocale.ByGameLanguage por reflexao.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\locale-fallback-check.ps1
$ErrorActionPreference = 'Stop'

$game = $env:VALHEIM_DIR
if ([string]::IsNullOrWhiteSpace($game)) { $game = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim' }
$dll = Join-Path $game 'BepInEx\plugins\AI-Companions\Companions.dll'
if (-not (Test-Path $dll)) { Write-Host "FALHA: $dll nao existe (compile o mod antes)"; exit 2 }

$dirs = @((Join-Path $game 'BepInEx\core'), (Join-Path $game 'valheim_Data\Managed'), (Join-Path $game 'BepInEx\plugins'))
$null = [System.AppDomain]::CurrentDomain.add_AssemblyResolve({
  param($s, $e)
  $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name
  foreach ($d in $dirs) { $p = Join-Path $d ($name + '.dll'); if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFrom($p) } }
  return $null
})
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$locale = $asm.GetType('Companions.CompanionLocale', $true)
$BF = [System.Reflection.BindingFlags] 'Static, NonPublic, Public'
$IF = [System.Reflection.BindingFlags] 'Instance, NonPublic, Public'
$byGame = $locale.GetMethod('ByGameLanguage', $BF)
$english = $locale.GetField('English', $BF).GetValue($null)

function Field($obj, [string]$name) { return $obj.GetType().GetField($name, $IF).GetValue($obj) }

function Check([string]$gameLanguage, [string]$kokoro, [string]$whisper, [int]$voice) {
  $profile = $byGame.Invoke($null, @($gameLanguage))
  if ($null -eq $profile) { $profile = $english }
  $ok = (Field $profile 'KokoroCode') -eq $kokoro -and (Field $profile 'WhisperCode') -eq $whisper -and (Field $profile 'DefaultVoice') -eq $voice
  if ($ok) {
    "ok    {0,-24} kokoro={1} whisper={2} voz={3}" -f $gameLanguage, (Field $profile 'KokoroCode'), (Field $profile 'WhisperCode'), (Field $profile 'DefaultVoice')
  } else {
    "FAIL  {0,-24} esperado {1}/{2}/{3}, veio {4}/{5}/{6}" -f $gameLanguage, $kokoro, $whisper, $voice,
      (Field $profile 'KokoroCode'), (Field $profile 'WhisperCode'), (Field $profile 'DefaultVoice')
    $script:fail++
  }
}

$fail = 0
Write-Host '=== idiomas do jogo com perfil proprio ==='
Check 'Portuguese_Brazilian' 'pt-br' 'pt' 42
Check 'English' 'en-us' 'en' 3
Check 'Spanish' 'es' 'es' 28
Check 'French' 'fr' 'fr' 30
Check 'Italian' 'it' 'it' 35

Write-Host ''
Write-Host '=== idiomas do jogo SEM perfil: a regra e o INGLES ==='
foreach ($l in 'German','Russian','Polish','Turkish','Korean','Swedish','Ukrainian','Japanese','Chinese','Hindi',
               'Czech','Dutch','Finnish','Greek','Hungarian','Romanian','Thai','Vietnamese') {
  Check $l 'en-us' 'en' 3
}

Write-Host ''
Write-Host '=== [AI] TtsLanguage / VoiceLanguage = auto ==='
foreach ($m in 'ResolveKokoro','ResolveWhisper') {
  $v = $locale.GetMethod($m, $BF).Invoke($null, @('auto'))
  "auto -> $m = $v (fora do Unity o perfil e o ingles)"
}
$rv = $locale.GetMethod('ResolveVoice', $BF).Invoke($null, @(42))
Write-Host "ResolveVoice(42) = $rv (voz de outro idioma cai na padrao do idioma ativo)"

Write-Host ''
Write-Host ($(if ($fail -eq 0) { 'TODOS OS TESTES PASSARAM' } else { "$fail FALHA(S)" }))
exit $(if ($fail -eq 0) { 0 } else { 1 })
