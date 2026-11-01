# Changelog

All notable changes to **PROJECT NEXUS** ("Any Format. One Destination.") will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2026-11-02

### Added
- **Emerald Edition Design Identity**:
  - Replaced legacy electric blue with Deep Forest (`#10251D`), Graphite (`#18231F`), Dark Slate (`#24332C`), Emerald Accent (`#10B981`), and Mint (`#6EE7B7`).
  - Dynamic dual-theme engine: real-time switching between Emerald Obsidian Dark and Emerald Clean Light modes via header toggle and `Ctrl+T`.
  - Distraction-Free Focus Mode (`F10`) collapsing sidebars and headers into an immersive processing workspace.
  - Global Command Palette (`Ctrl+Shift+P` / `Ctrl+K`) for fuzzy navigation, studio switching, and direct tool execution.
- **AI Image Upscaler Studio**:
  - Dedicated super-resolution studio in WORKSPACE with `Ctrl+U` global hotkey.
  - 2X, 4X, and 8X multipliers with 4K UHD (3840×2160) and 8K Master (7680×4320) direct presets.
  - Multiple reconstruction algorithms: Lanczos3 Sinc-Windowed, Catmull-Rom Bicubic Spline, Nearest Neighbor Integer (Pixel Art), and Adaptive Edge-Enhanced.
  - Interactive dual-viewport stage featuring live before/after side-by-side comparison.
  - Dual-engine execution: Rust SIMD IPC named pipe daemon with automated high-fidelity C# Fant sampling fallback.
- **Dedicated Media Studio UIs**:
  - Image Studio: Digital Darkroom with Lanczos3 SIMD, quick resolution chips, aspect lock, and EXIF stripping.
  - Audio Studio: Sound Engineer Suite with audio waveform visualizer stage, bitrate pills (320k to 64k), and sample rate matrix.
  - Video Studio: Cinema Lab with 16:9 cinema viewport, resolution chips, and device profiles (YouTube 4K, Discord, Mobile).
  - PDF Studio: Document Publisher with staging board, visual page reordering (▲ Up / ▼ Down), and output naming.
  - Archive Manager: Compression Vault with dual Create/Extract modes, SIMD compression levels, and Zip-Slip vulnerability shield.
- **Quick Actions Dock**:
  - Added 1-click launchers for all 7 studios directly on the Command Center Dashboard.

### Fixed
- Resolved TextBox vertical text clipping on numeric inputs across all studios.
- Added 2-second auto-dismissal timer and duplicate prevention for Toast notifications.
- Fixed missing `Icon.Download` resource in `Themes/Icons.xaml` ensuring Archive Manager opens reliably.
- Cleaned ComboBox popup backgrounds and item contrast across both light and dark themes.

---

## [1.0.0] - 2026-11-01

### Added
- **Obsidian Dark Design System**:
  - Precision dark theme (`#0D1117` base, `#161B22` cards, `#388BFD` electric blue accents).
  - High-DPI vector iconography, borderless window chrome with draggable title bar.
  - Interactive Command Palette overlay (`Ctrl+K` and search trigger).
  - Real-time CPU, RAM (Win32 P/Invoke), and processing speed telemetry cards.
- **Universal File Conversion Studio**:
  - Multi-category conversion supporting Images, Audio, Video, Documents, and Archives.
  - Automatic format detection via magic bytes and extensions.
  - Batch queue management with drag-and-drop ingestion.
- **Image Studio**:
  - Real format conversion across PNG, JPG, WEBP, AVIF, BMP, TIFF, GIF via Rust `image-rs`.
  - High-performance Lanczos3 resampling and quality adjustments.
  - Privacy-preserving EXIF and camera metadata stripping.
- **Audio Studio**:
  - High-fidelity audio transcoding across MP3, WAV, FLAC, AAC, OGG, M4A, WMA via FFmpeg pipeline.
  - Bitrate and sample rate adjustments.
- **Video Studio**:
  - Video transcoding and compression across MP4, MKV, MOV, AVI, WEBM.
  - Resolution scaling and audio extraction pipeline.
- **PDF Studio**:
  - Multi-document PDF merging and stream stitching.
- **Archive Manager**:
  - Safe TAR.GZ and TAR archive creation and extraction.
  - Built-in Zip-Slip and path traversal mitigation.
- **Developer Toolkit**:
  - Offline JSON formatting and strict schema validation.
  - Base64 encoding and decoding.
  - Cryptographic hashing: MD5, SHA-256, and SHA-512.
  - Comprehensive File Checksum Verification reports.
  - Hex dump viewer (offset, hex bytes, and ASCII inspection).
- **Processing Center**:
  - Real-time task queue management with priority badges.
  - Interactive retry for failed tasks and cancellation for active tasks.
  - Bidirectional named pipe streaming progress updates (`\\.\pipe\nexus-engine`).
- **Automation Center**:
  - Background directory monitoring via `FileSystemWatcher`.
  - Conditional rule-based format detection and automated conversions.
  - Interactive toggle and folder management.
- **Storage Analyzer**:
  - Recursive directory scanner categorizing storage breakdown across file types.
- **Local SQLite Database**:
  - WAL mode, foreign keys, transaction handling, and schema migrations.
  - Full task history, conversion presets, watch folders, settings, and activity logs.
- **Security & Privacy**:
  - 100% local-first architecture with zero telemetry, zero mandatory accounts, and offline processing.
  - Path canonicalization preventing arbitrary write access outside destination roots.

