use anyhow::{bail, Result};
use flate2::read::GzDecoder;
use flate2::write::GzEncoder;
use flate2::Compression;
use nexus_core::{ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor, TaskStatus};
use nexus_utils::{sanitize_path, validate_output_file};
use serde::Deserialize;
use std::fs::{self, File};
use std::path::Path;
use std::time::Instant;
use tar::{Archive, Builder};
use walkdir::WalkDir;

#[derive(Debug, Deserialize, Default)]
struct ArchiveOptions {
    files: Option<Vec<String>>,
    operation: Option<String>, // "create" or "extract"
}

pub struct ArchiveProcessor;

impl ArchiveProcessor {
    pub fn new() -> Self {
        Self
    }
}

impl Default for ArchiveProcessor {
    fn default() -> Self {
        Self::new()
    }
}

impl TaskProcessor for ArchiveProcessor {
    fn process(
        &self,
        task: &ProcessingTask,
        on_progress: ProgressCallback,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = Result<()>> + Send + '_>> {
        let task = task.clone();
        Box::pin(async move {
            let start = Instant::now();
            let options: ArchiveOptions = serde_json::from_str(&task.options_json).unwrap_or_default();
            let is_extract = options.operation.as_deref() == Some("extract") || task.source_format == "tar.gz" || task.source_format == "tar";

            let output_path = Path::new(&task.output_path);
            let source_path = Path::new(&task.source_path);

            if is_extract {
                // Extract Tarball
                on_progress(ProgressUpdate {
                    task_id: task.id.clone(),
                    status: TaskStatus::Processing,
                    progress_percentage: 20.0,
                    speed_bytes_per_sec: 0.0,
                    processed_bytes: 0,
                    total_bytes: 0,
                    message: Some("Extracting archive entries safely...".to_string()),
                });

                let tar_gz = File::open(source_path)?;
                let tar = GzDecoder::new(tar_gz);
                let mut archive = Archive::new(tar);

                fs::create_dir_all(output_path)?;

                for entry in archive.entries()? {
                    let mut entry = entry?;
                    let path = entry.path()?;
                    let safe_destination = sanitize_path(output_path, &path)?;

                    if let Some(parent) = safe_destination.parent() {
                        fs::create_dir_all(parent)?;
                    }

                    entry.unpack(&safe_destination)?;
                }

                on_progress(ProgressUpdate {
                    task_id: task.id.clone(),
                    status: TaskStatus::Completed,
                    progress_percentage: 100.0,
                    speed_bytes_per_sec: 0.0,
                    processed_bytes: 0,
                    total_bytes: 0,
                    message: Some("Archive extracted safely.".to_string()),
                });
            } else {
                // Create Archive (Tar.gz / Tar)
                on_progress(ProgressUpdate {
                    task_id: task.id.clone(),
                    status: TaskStatus::Processing,
                    progress_percentage: 10.0,
                    speed_bytes_per_sec: 0.0,
                    processed_bytes: 0,
                    total_bytes: 0,
                    message: Some("Creating compressed archive...".to_string()),
                });

                if let Some(parent) = output_path.parent() {
                    fs::create_dir_all(parent)?;
                }

                let tar_gz = File::create(output_path)?;
                let enc = GzEncoder::new(tar_gz, Compression::default());
                let mut tar = Builder::new(enc);

                if let Some(files) = options.files {
                    for (idx, f) in files.iter().enumerate() {
                        let p = Path::new(f);
                        if p.exists() {
                            let name = p.file_name().unwrap_or_default();
                            if p.is_dir() {
                                tar.append_dir_all(name, p)?;
                            } else {
                                let mut file = File::open(p)?;
                                tar.append_file(name, &mut file)?;
                            }
                        }

                        let pct = 10.0 + ((idx + 1) as f64 / files.len() as f64) * 80.0;
                        on_progress(ProgressUpdate {
                            task_id: task.id.clone(),
                            status: TaskStatus::Processing,
                            progress_percentage: pct,
                            speed_bytes_per_sec: 0.0,
                            processed_bytes: idx as u64,
                            total_bytes: files.len() as u64,
                            message: Some(format!("Archiving {}...", f)),
                        });
                    }
                } else if source_path.exists() {
                    let name = source_path.file_name().unwrap_or_default();
                    if source_path.is_dir() {
                        tar.append_dir_all(name, source_path)?;
                    } else {
                        let mut file = File::open(source_path)?;
                        tar.append_file(name, &mut file)?;
                    }
                } else {
                    bail!("No valid files specified to archive.");
                }

                tar.finish()?;
                let out_size = validate_output_file(output_path, 10)?;
                let elapsed = start.elapsed().as_secs_f64().max(0.001);

                on_progress(ProgressUpdate {
                    task_id: task.id.clone(),
                    status: TaskStatus::Completed,
                    progress_percentage: 100.0,
                    speed_bytes_per_sec: (out_size as f64) / elapsed,
                    processed_bytes: out_size,
                    total_bytes: out_size,
                    message: Some(format!("Archive created successfully ({} bytes).", out_size)),
                });
            }

            Ok(())
        })
    }
}

