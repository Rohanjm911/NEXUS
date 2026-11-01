# NEXUS Security & Privacy Specification

## 1. Local-First Privacy Model
NEXUS is engineered as a strictly local, privacy-first Windows desktop platform:
- **Zero Cloud Mandates:** All conversion, compression, metadata analysis, and developer utilities execute entirely locally.
- **Zero Involuntary Telemetry:** The platform does not transmit telemetry, analytics, or user documents over the network.
- **Offline Reliability:** Core functionality remains fully operable without an active network connection.

---

## 2. Threat Modeling & Protections

### 2.1 Untrusted File Handling
All imported files are treated as untrusted input:
- The presentation layer never parses or opens complex media containers directly.
- All file decoders and encoders run within the isolated Rust processing daemon.

### 2.2 Path Traversal & Zip-Slip Protection
Archive extraction routines are vulnerable to Zip-Slip attacks where malicious archive headers contain `../` sequences to overwrite system binaries or user startup scripts.
- In `nexus-utils::sanitize_path`, every component of an archive entry is checked.
- Any presence of `ParentDir` (`..`) or absolute path traversal beyond the target directory throws an immediate error and aborts extraction.

```rust
pub fn sanitize_path(base_dir: &Path, user_path: &Path) -> Result<PathBuf> {
    let mut resolved = base_dir.to_path_buf();
    for component in user_path.components() {
        match component {
            std::path::Component::Normal(p) => resolved.push(p),
            std::path::Component::CurDir => {}
            std::path::Component::ParentDir => {
                bail!("Path traversal attempt detected: {:?}", user_path);
            }
            std::path::Component::RootDir | std::path::Component::Prefix(_) => {
                let candidate = PathBuf::from(component.as_os_str());
                if !candidate.starts_with(base_dir) {
                    bail!("Path traversal attempt detected outside target base: {:?}", user_path);
                }
            }
        }
    }
    Ok(resolved)
}
```

### 2.3 Metadata Stripping & Privacy
Files frequently contain sensitive metadata:
- Camera models, serial numbers, GPS coordinates, timestamps, author names, and software fingerprints.
- The `ImageStudio` and `ConversionStudio` provide an automated metadata stripping pipeline that re-encodes pixel streams cleanly without retaining EXIF, IPTC, or XMP metadata blocks.

### 2.4 Temporary File Lifecycle & Secure Deletion
- Temporary conversion files are generated in dedicated per-process directories under `%TEMP%\nexus\`.
- All temporary artifacts are cleaned up upon task completion and process exit.

### 2.5 Plugin Isolation
- External plugins are loaded into collectible `PluginLoadContext` instances.
- Plugins are prohibited from executing code directly in the default load context and can be unloaded dynamically to free resources.

