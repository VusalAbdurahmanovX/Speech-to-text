# Third-party software

The application source is MIT licensed. This does not replace upstream licenses.

- **.NET 8 / WPF**: self-contained packages include Microsoft .NET runtime components, primarily under MIT. Source, license, and notices: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT, https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT, https://github.com/dotnet/wpf/blob/main/LICENSE.TXT, https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT. Runtime DLLs retain their embedded notices.
- **whisper.cpp**: MIT; downloaded directly from ggml-org's official b5454 release, with upstream package contents preserved. Source/license: https://github.com/ggml-org/whisper.cpp/tree/b5454 and https://github.com/ggml-org/whisper.cpp/blob/b5454/LICENSE. The CUDA archive includes NVIDIA runtime libraries governed by NVIDIA's terms: https://docs.nvidia.com/cuda/eula/.
- **FFmpeg**: the app downloads BtbN's `win64-lgpl` build directly from upstream; FFmpeg binaries are not bundled in the application installer. Build/source instructions: https://github.com/BtbN/FFmpeg-Builds. FFmpeg source: https://ffmpeg.org/download.html. License and legal information: https://ffmpeg.org/legal.html. Preserve the downloaded archive's license information when copying or redistributing tools.
- **Whisper model weights**: downloaded directly from https://huggingface.co/ggerganov/whisper.cpp; see that repository's model card/license and https://github.com/openai/whisper/blob/main/LICENSE. Weights are not bundled in the installer or source repository.
- **Inno Setup**: a build-time dependency downloaded from https://github.com/jrsoftware/issrc/releases. It is not part of the application runtime. Licensing: https://jrsoftware.org/isinfo.php.

When repackaging third-party binaries yourself, comply with their respective redistribution and source-disclosure requirements. The supplied installer distributes the application and its .NET runtime; first-time setup obtains the transcription tools and models from their upstream providers.
