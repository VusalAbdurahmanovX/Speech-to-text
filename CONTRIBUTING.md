# Contributing to Speech to Text

Contributions are welcome: bug fixes, interface improvements, documentation, model handling, and performance work. This project is MIT licensed. Please write issues, pull requests, and documentation in English.

## Start a contribution

1. Check existing issues and pull requests. For a larger feature, open an issue or discussion first so we can agree on the scope.
2. Fork this repository using GitHub's **Fork** button.
3. Clone your fork and create a branch, for example `git switch -c fix/model-selection`.
4. Install a .NET SDK that targets .NET 8 and build on Windows:

   ```powershell
   .\build.ps1 -Test
   .\run.ps1
   ```

5. Make a focused change. Follow the existing C#/WPF style and use platform features before adding dependencies.
6. Run the relevant checks. For interface changes, also run `dotnet run --project tests/CoreChecks.csproj -c Release -- --ui` and inspect the generated previews in `artifacts/`.
7. Push your branch to your fork and open a pull request against this repository's `main` branch. Explain the problem, the change, and how you tested it. Include before/after screenshots for visual changes.

## Project boundaries

- Keep transcription local through the official `whisper-cli.exe` process. Do not add Python, PyTorch, openai-whisper, or a C# Whisper reimplementation.
- Inspect the installed CLI's help before adding flags. CUDA mode must never silently fall back to CPU.
- Preserve original media and existing user edits. Keep cancellation and error handling working.
- Do not commit recordings, transcripts, models, native tool binaries, personal settings, credentials, build output, or generated logs.
- When reporting speed improvements, include the model/quantization, media duration, processing duration, hardware, backend, and timing method. Do not present one recording's speed as a universal guarantee.

Use **Issues** for reproducible bugs and feature requests, and **Discussions** for questions or ideas. Pull requests from forks are welcome; they are reviewed before merging. By submitting code, you agree that your contribution is distributed under the repository's MIT license.
