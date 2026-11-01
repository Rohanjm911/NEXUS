use anyhow::{bail, Context, Result};
use sha2::{Digest, Sha256, Sha512};
use std::fs::{self, File};
use std::io::{Read, Write};
use std::path::{Path, PathBuf};

pub fn sanitize_path(base_dir: &Path, user_path: &Path) -> Result<PathBuf> {
    let mut resolved = base_dir.to_path_buf();
    for component in user_path.components() {
        match component {
            std::path::Component::Normal(p) => resolved.push(p),
            std::path::Component::CurDir => {}
            std::path::Component::ParentDir => {
                bail!("Path traversal attempt detected with parent directory access: {:?}", user_path);
            }
            std::path::Component::RootDir | std::path::Component::Prefix(_) => {
                // If it's absolute, check that it starts with base_dir
                let candidate = PathBuf::from(component.as_os_str());
                if !candidate.starts_with(base_dir) {
                    bail!("Path traversal attempt detected outside target base: {:?}", user_path);
                }
            }
        }
    }

    Ok(resolved)
}

pub fn create_temp_file(prefix: &str, extension: &str) -> Result<PathBuf> {
    let temp_dir = std::env::temp_dir().join("nexus");
    fs::create_dir_all(&temp_dir)?;

    let filename = format!("{}_{}_{}.{}", prefix, std::process::id(), uuid::Uuid::new_v4().simple(), extension.trim_start_matches('.'));
    Ok(temp_dir.join(filename))
}

pub fn sha256_file(path: &Path) -> Result<String> {
    let mut file = File::open(path).with_context(|| format!("Failed to open file for hashing: {:?}", path))?;
    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 64 * 1024];

    loop {
        let n = file.read(&mut buffer)?;
        if n == 0 {
            break;
        }
        hasher.update(&buffer[..n]);
    }

    Ok(hex::encode(hasher.finalize()))
}

pub fn sha512_file(path: &Path) -> Result<String> {
    let mut file = File::open(path)?;
    let mut hasher = Sha512::new();
    let mut buffer = [0u8; 64 * 1024];

    loop {
        let n = file.read(&mut buffer)?;
        if n == 0 {
            break;
        }
        hasher.update(&buffer[..n]);
    }

    Ok(hex::encode(hasher.finalize()))
}

pub fn validate_output_file(path: &Path, min_bytes: u64) -> Result<u64> {
    if !path.exists() {
        bail!("Validation failed: Output file does not exist: {:?}", path);
    }

    let meta = fs::metadata(path).with_context(|| format!("Cannot read metadata for {:?}", path))?;
    let size = meta.len();

    if size < min_bytes {
        bail!("Validation failed: Output file size is too small ({} bytes, minimum expected {})", size, min_bytes);
    }

    Ok(size)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_sanitize_path_valid() {
        let base = Path::new("C:/temp/base");
        let safe = Path::new("subfolder/file.txt");
        let res = sanitize_path(base, safe).unwrap();
        assert!(res.starts_with(base));
    }

    #[test]
    fn test_sanitize_path_traversal_rejected() {
        let base = Path::new("C:/temp/base");
        let unsafe_path = Path::new("../../windows/system32/cmd.exe");
        let res = sanitize_path(base, unsafe_path);
        assert!(res.is_err());
    }

    #[test]
    fn test_create_temp_file() {
        let temp = create_temp_file("test", "txt").unwrap();
        assert!(temp.to_string_lossy().contains("test"));
        assert!(temp.to_string_lossy().ends_with(".txt"));
    }
}
