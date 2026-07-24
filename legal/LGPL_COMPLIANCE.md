# LGPL Compliance & Library Replacement Instructions

**Limbus Split Pro** complies strictly with the GNU Lesser General Public License (LGPL v2.1 / v3.0) for dynamically linked audio decoder libraries (e.g. `ffmpeg.dll`, `libsndfile.dll`).

---

## Architecture & Dynamic Linking

1. All LGPL libraries are packaged exclusively as separate, dynamically linked dynamic-link libraries (`.dll` files) inside the application runtime directory.
2. The core executable (`LimbusSplitPro.exe`) and Python engine processes bind dynamically to these libraries at runtime using standard C/C++ or .NET P/Invoke interfaces without static compilation.
3. No static linking of LGPL code is performed in any build artifact.

---

## User Instructions for Library Replacement

End users have the right to modify, replace, or upgrade any LGPL component packaged with Limbus Split Pro.

To replace an LGPL library with a custom build:

1. Close **Limbus Split Pro**.
2. Navigate to the application installation folder (default: `C:\Program Files\Limbus Split Pro\` or `%LOCALAPPDATA%\Programs\Limbus Split Pro\`).
3. Locate the dynamic library you wish to replace (e.g., `ffmpeg.dll` or `libsndfile-1.dll`).
4. Replace the `.dll` file with your custom-compiled, binary-compatible dynamic library.
5. Launch **Limbus Split Pro**. The application will dynamically load your replaced library binary.

---

## Source Code Access

The exact source code for all LGPL-licensed dynamic libraries included in Limbus Split Pro is available upon request or can be downloaded from official repositories:
- **FFmpeg**: [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)
- **libsndfile**: [https://github.com/libsndfile/libsndfile](https://github.com/libsndfile/libsndfile)
