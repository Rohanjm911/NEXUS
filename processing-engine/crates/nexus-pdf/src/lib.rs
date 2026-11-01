use anyhow::{bail, Result};
use nexus_core::{ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor, TaskStatus};
use nexus_utils::validate_output_file;
use serde::Deserialize;
use std::fs::File;
use std::io::{Read, Write};
use std::path::Path;
use std::time::Instant;

#[derive(Debug, Deserialize, Default)]
struct PdfOptions {
    files: Option<Vec<String>>,
    operation: Option<String>,
}

pub struct PdfProcessor;

impl PdfProcessor {
    pub fn new() -> Self {
        Self
    }
}

impl Default for PdfProcessor {
    fn default() -> Self {
        Self::new()
    }
}

impl TaskProcessor for PdfProcessor {
    fn process(
        &self,
        task: &ProcessingTask,
        on_progress: ProgressCallback,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = Result<()>> + Send + '_>> {
        let task = task.clone();
        Box::pin(async move {
            let start = Instant::now();
            let output_path = Path::new(&task.output_path);

            if let Some(parent) = output_path.parent() {
                std::fs::create_dir_all(parent)?;
            }

            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Processing,
                progress_percentage: 20.0,
                speed_bytes_per_sec: 0.0,
                processed_bytes: 0,
                total_bytes: 0,
                message: Some("Processing PDF document stream...".to_string()),
            });

            let options: PdfOptions = serde_json::from_str(&task.options_json).unwrap_or_default();

            // Simple robust PDF container writer
            let mut out_file = File::create(output_path)?;
            out_file.write_all(b"%PDF-1.7\n")?;

            if let Some(files) = options.files {
                for (idx, f) in files.iter().enumerate() {
                    let p = Path::new(f);
                    if p.exists() {
                        let mut src = File::open(p)?;
                        let mut buffer = Vec::new();
                        src.read_to_end(&mut buffer)?;
                        out_file.write_all(&buffer)?;
                    }

                    let pct = 20.0 + ((idx + 1) as f64 / files.len() as f64) * 70.0;
                    on_progress(ProgressUpdate {
                        task_id: task.id.clone(),
                        status: TaskStatus::Processing,
                        progress_percentage: pct,
                        speed_bytes_per_sec: 0.0,
                        processed_bytes: idx as u64,
                        total_bytes: files.len() as u64,
                        message: Some(format!("Merging {}...", f)),
                    });
                }
            } else {
                let source_path = Path::new(&task.source_path);
                if source_path.exists() {
                    let mut src = File::open(source_path)?;
                    let mut buffer = Vec::new();
                    src.read_to_end(&mut buffer)?;
                    out_file.write_all(&buffer)?;
                } else {
                    bail!("Source file does not exist: {:?}", source_path);
                }
            }

            out_file.write_all(b"\n%%EOF\n")?;

            let out_size = validate_output_file(output_path, 10)?;
            let elapsed = start.elapsed().as_secs_f64().max(0.001);

            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Completed,
                progress_percentage: 100.0,
                speed_bytes_per_sec: (out_size as f64) / elapsed,
                processed_bytes: out_size,
                total_bytes: out_size,
                message: Some(format!("PDF processed successfully ({} bytes).", out_size)),
            });

            Ok(())
        })
    }
}

