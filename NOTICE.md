# Disclaimer

This repository is a **personal study project** about modding for **Valheim**.

- **It is not affiliated with, endorsed by or associated with** Iron Gate Studio, Coffee Stain Publishing or
  ego.ai.
- The **"Thrall"** mod (ego.ai) is mentioned only as a **conceptual reference** for study. **No binary, file or
  source code from Thrall is redistributed** — neither in this repository nor in the release package; the
  reference material stays outside version control (see `.gitignore`).
- **Third-party libraries and models (under permissive licenses) ARE redistributed in the release package** —
  the published `.zip` ships the binaries and the models the mod loads at runtime:
  **Whisper.net** and **whisper.cpp** (MIT), **sherpa-onnx** (Apache-2.0), **onnxruntime** (MIT), the
  **Kokoro-82M** model (Apache-2.0) and the **Whisper weights** (MIT, from OpenAI via whisper.cpp), plus
  the **espeak-ng data** (GPL-3.0, used only as phonemization data). The license texts travel **along with**
  the binaries, in the `LICENSES\` folder (see [`LICENSES/LEIA-ME.txt`](LICENSES/LEIA-ME.txt)
  and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)). They are **not** versioned in this
  repository (the models stay outside git).
- "Valheim" and other trademarks belong to their respective owners.

The content of the agents and skills in `.opencode/` and the documentation of this project are original to this
repository.
