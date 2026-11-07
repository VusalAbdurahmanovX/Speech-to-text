# Speech to Text

1. Open Speech to Text from the Start menu (or SubtitleStudio.exe in the portable folder).
2. Open **Settings > Tools & paths** and click **Install transcription tools**. This downloads the official CUDA-enabled whisper.cpp build and an LGPL FFmpeg build directly from their upstream GitHub releases. Downloads are SHA-256 checked.
3. Select **large-v3-turbo**, then **Download selected model**. The initial model is approximately 548 MiB. You can also choose tiny (31 MiB), medium, large-v1, large-v2, or large-v3. Large-v1 downloads the full 2.9 GiB model and needs more memory. Tools require approximately 821 MiB of downloads and more disk space after extraction. Allow several GB of free space.
4. Choose the spoken language. Turkish (`tr`) is the default. Click **Save changes**.
5. Select your preferred model in the main **Models** panel. Installed models show their size; other models can be downloaded using **Download selected model**.
6. Browse for or drop your media file, then click **Transcribe**.
7. The app automatically saves TXT and SRT. Edit the text and choose **Save TXT** to save a copy anywhere.

Requirements: Windows 10 22H2 / Windows 11 x64 and a recent NVIDIA driver for CUDA. The app includes .NET; no SDK, Python, or CUDA toolkit is needed. Install Microsoft's Visual C++ 2015-2022 x64 Redistributable if whisper.cpp reports a missing VC runtime DLL. CPU use is available only by explicitly selecting CPU in Settings > General.

Models, downloaded tools, settings, and automatic results are stored in `%LOCALAPPDATA%\SubtitleStudio`. They are kept during upgrades and uninstall. The app needs internet for initial tool/model downloads. Media stays on your PC during transcription.


The installer is currently unsigned. A signing certificate and publisher name can be added before a public release. Do not bypass your organization's installation policy.

Downloaded backend tools retain their upstream licenses. See THIRD_PARTY_NOTICES.md for source and license links. The plain-text editor does not edit the timestamps in the automatically generated SRT.
