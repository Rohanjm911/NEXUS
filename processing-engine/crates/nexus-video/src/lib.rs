use anyhow::{bail, Result};
use nexus_core::{ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor, TaskStatus};
use nexus_utils::validate_output_file;
use serde::Deserialize;
use std::path::Path;
use std::process::Command;
use std::time::Instant;

#[derive(Debug, Deserialize, Default)]
struct VideoOptions {
    resolution: Option<String>,
    extract_audio: Option<bool>,
}

pub struct VideoProcessor;

impl VideoProcessor {
    pub fn new() -> Self {
        Self
    }
}

impl Default for VideoProcessor {
    fn default() -> Self {
        Self::new()
    }
}

impl TaskProcessor for VideoProcessor {
    fn process(
        &self,
        task: &ProcessingTask,
        on_progress: ProgressCallback,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = Result<()>> + Send + '_>> {
        let task = task.clone();
        Box::pin(async move {
            let start = Instant::now();
            let source_path = Path::new(&task.source_path);
            let output_path = Path::new(&task.output_path);

            if !source_path.exists() {
                bail!("Source video file does not exist: {:?}", source_path);
            }

            if let Some(parent) = output_path.parent() {
                std::fs::create_dir_all(parent)?;
            }

            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Processing,
                progress_percentage: 10.0,
                speed_bytes_per_sec: 0.0,
                processed_bytes: 0,
                total_bytes: 0,
                message: Some("Initializing video encoding pipeline...".to_string()),
            });

            let options: VideoOptions = serde_json::from_str(&task.options_json).unwrap_or_default();

            let ffmpeg_cmd = if Path::new(r"C:\ffmpeg\ffmpeg.exe").exists() {
                r"C:\ffmpeg\ffmpeg.exe"
            } else {
                "ffmpeg"
            };

            let mut args = vec!["-i", task.source_path.as_str()];

            if options.extract_audio == Some(true) {
                args.extend_from_slice(&["-vn", "-acodec", "copy"]);
            } else if let Some(res) = &options.resolution {
                if !res.is_empty() {
                    args.extend_from_slice(&["-s", res.as_str()]);
                }
            }

            args.extend_from_slice(&["-y", task.output_path.as_str()]);

            let status = Command::new(ffmpeg_cmd).args(&args).output();

            match status {
                Ok(output) if output.status.success() => {
                    let out_size = validate_output_file(output_path, 10)?;
                    let elapsed = start.elapsed().as_secs_f64().max(0.001);

                    on_progress(ProgressUpdate {
                        task_id: task.id.clone(),
                        status: TaskStatus::Completed,
                        progress_percentage: 100.0,
                        speed_bytes_per_sec: (out_size as f64) / elapsed,
                        processed_bytes: out_size,
                        total_bytes: out_size,
                        message: Some(format!("Video processed successfully ({} bytes).", out_size)),
                    });
                    Ok(())
                }
                Ok(output) => {
                    let stderr = String::from_utf8_lossy(&output.stderr);
                    bail!("Video processing error: {}", stderr);
                }
                Err(e) => {
                    bail!("Failed to execute video engine: {}", e);
                }
            }
        })
    }
}
