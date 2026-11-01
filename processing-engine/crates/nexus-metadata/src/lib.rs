use anyhow::Result;
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::Path;

#[derive(Debug, Serialize, Deserialize)]
pub struct FileMetadataInfo {
    pub file_name: String,
    pub file_size_bytes: u64,
    pub is_read_only: bool,
    pub created_at: Option<String>,
    pub modified_at: Option<String>,
}

pub fn extract_metadata(path: &Path) -> Result<FileMetadataInfo> {
    let meta = fs::metadata(path)?;
    let file_name = path
        .file_name()
        .map(|s| s.to_string_lossy().to_string())
        .unwrap_or_default();

    let created_at = meta
        .created()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| format!("Timestamp: {}", d.as_secs()));

    let modified_at = meta
        .modified()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| format!("Timestamp: {}", d.as_secs()));

    Ok(FileMetadataInfo {
        file_name,
        file_size_bytes: meta.len(),
        is_read_only: meta.permissions().readonly(),
        created_at,
        modified_at,
    })
}

