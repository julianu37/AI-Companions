# tts-language-check — o Kokoro fala os 5 idiomas do mod?

Teste **fora do jogo** que carrega o **modelo e os nativos que o mod empacota** e sintetiza uma frase em cada idioma,
com as vozes que o painel oferece. É o teste que faltou antes da 2.0.0: em 30/10/2026 ele mostrou que
**`en-gb` e `fr-fr` carregam mas quebram no `Generate`** (e não que o idioma fosse só "cosmético"), e que
**o idioma muda mesmo o áudio** — o mesmo texto em `pt-br` gera 80.062 amostras e em `en-us` 76.495.

## Como rodar

```powershell
# 1. o build copia o modelo para a pasta do jogo; o harness usa ESSA pasta (ou passe a sua)
$env:AI_COMPANIONS_DIR = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\AI-Companions"

# 2. as nativas têm de estar ao lado do .exe (o DllImport resolve por nome)
cd tools\tts-language-check
dotnet build -c Release
Copy-Item "$env:AI_COMPANIONS_DIR\runtimes\win-x64\native\*.dll" "bin\Release\net472" -Force

# 3. roda
cd bin\Release\net472
.\TtsLanguageCheck.exe
```

Sai `TODOS OS TESTES PASSARAM` (exit 0) quando os 5 idiomas carregam, todas as vozes do `CompanionLocale` existem no
modelo e o mesmo texto muda de áudio entre `pt-br` e `en-us`.

## O que ele verifica

1. `Lang` = `pt-br`, `en-us`, `es`, `fr`, `it` **carrega** e **sintetiza** com a voz padrão de cada um (imprime
   amostras, duração e **RTF**; referência nesta máquina: 0,76–1,08 com 4 threads).
2. **Todas** as vozes de `src/Companions/Core/CompanionLocale.cs` (42/43/44, 3/2/6/14/16/18, 28/29, 30, 35/36) são
   `sid` válidos no `voices.bin` empacotado (`NumSpeakers` = 54).
3. O **mesmo texto em português** com `pt-br` e com `en-us` gera áudio **diferente** — é a prova de que o `Lang`
   escolhe o fonemizador (e, portanto, de que o modelo precisa ser recarregado quando o idioma muda).
4. Os códigos que o F8 **não** oferece: `en-gb`, `fr-fr` (quebram no `Generate`), `pt` (português de Portugal, funciona).

## Quando rodar

- Depois de mexer em `CompanionTts`, `CompanionLocale` ou no catálogo de vozes.
- Depois de trocar o pacote do sherpa-onnx ou os arquivos de `tts\kokoro`.
- Antes de publicar uma release que mexa em voz/idioma.
