# A REGRA da multilinguagem (definida na M1, nao mexer): o idioma do JOGO decide o perfil.
#   Portuguese_Brazilian -> pt-br / pt / voz 42
#   English              -> en-us / en / voz 3
#   Spanish              -> es    / es / voz 28
#   French               -> fr    / fr / voz 30
#   Italian              -> it    / it / voz 35
#   QUALQUER OUTRO (de, ru, pl, tr, ko, sv, uk, ja, zh...) -> INGLES (en-us / en / voz 3)
#
# Le so a DLL compilada (nao abre o jogo, nao compila nada) e chama CompanionLocale.ByGameLanguage por reflexao.
# Assevera as tres familias da regra: os 5 idiomas com perfil, os idiomas sem perfil (-> ingles) e as respostas do
# "auto" (ResolveKokoro/ResolveWhisper/ResolveVoice fora do Unity, onde nao ha Localization = ingles). Qualquer FAIL
# incrementa o contador e o exit e 1.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\locale-fallback-check.ps1
# Exit: 0 = tudo passou, 1 = alguma regra falhou, 2 = a DLL nao existe (compile antes).
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

function CheckValue([string]$name, $expected, $actual) {
  if ("$expected" -eq "$actual") {
    "ok    {0,-24} {1}" -f $name, $actual
  } else {
    "FAIL  {0,-24} esperado {1}, veio {2}" -f $name, $expected, $actual
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
# Estas tres linhas ja foram Write-Host (nao passavam por Check e nao contavam no $fail): se o "auto" voltasse a
# resolver pt-br/42 o script dizia TODOS OS TESTES PASSARAM. Fora do Unity nao ha Localization, entao o perfil e o
# ingles e estas sao as respostas que a regra da M1 exige.
CheckValue 'ResolveKokoro("auto")' 'en-us' $locale.GetMethod('ResolveKokoro', $BF).Invoke($null, @('auto'))
CheckValue 'ResolveWhisper("auto")' 'en' $locale.GetMethod('ResolveWhisper', $BF).Invoke($null, @('auto'))
CheckValue 'ResolveVoice(42)' 3 $locale.GetMethod('ResolveVoice', $BF).Invoke($null, @(42))
# A outra metade da regra: o valor explicito do jogador sempre ganha do "auto".
CheckValue 'ResolveKokoro("pt-br")' 'pt-br' $locale.GetMethod('ResolveKokoro', $BF).Invoke($null, @('pt-br'))
CheckValue 'ResolveWhisper("pt")' 'pt' $locale.GetMethod('ResolveWhisper', $BF).Invoke($null, @('pt'))

Write-Host ''
Write-Host ($(if ($fail -eq 0) { 'TODOS OS TESTES PASSARAM' } else { "$fail FALHA(S)" }))
exit $(if ($fail -eq 0) { 0 } else { 1 })
