# Changelog

All notable changes to **PROJECT NEXUS** ("Any Format. One Destination.") will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

