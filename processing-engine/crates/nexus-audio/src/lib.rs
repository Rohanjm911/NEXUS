use anyhow::{bail, Result};
use nexus_core::{ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor, TaskStatus};
use nexus_utils::validate_output_file;
use serde::Deserialize;
use std::path::Path;
use std::process::Command;
use std::time::Instant;

#[derive(Debug, Deserialize, Default)]
struct AudioOptions {
    bitrate: Option<String>,
    sample_rate: Option<String>,
}

pub struct AudioProcessor;

impl AudioProcessor {
    pub fn new() -> Self {
        Self
    }
}

impl Default for AudioProcessor {
    fn default() -> Self {
        Self::new()
    }
}

impl TaskProcessor for AudioProcessor {
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
                bail!("Source audio file does not exist: {:?}", source_path);
            }

            if let Some(parent) = output_path.parent() {
                std::fs::create_dir_all(parent)?;
            }

            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Processing,
                progress_percentage: 15.0,
                speed_bytes_per_sec: 0.0,
                processed_bytes: 0,
                total_bytes: 0,
                message: Some("Invoking audio processing engine...".to_string()),
            });

            let options: AudioOptions = serde_json::from_str(&task.options_json).unwrap_or_default();
            let bitrate = options.bitrate.unwrap_or_else(|| "320k".to_string());
            let sample_rate = options.sample_rate.unwrap_or_else(|| "44100".to_string());

            // Check ffmpeg path
            let ffmpeg_cmd = if Path::new(r"C:\ffmpeg\ffmpeg.exe").exists() {
                r"C:\ffmpeg\ffmpeg.exe"
            } else {
                "ffmpeg"
            };

            let status = Command::new(ffmpeg_cmd)
                .args([
                    "-i", task.source_path.as_str(),
                    "-b:a", bitrate.as_str(),
                    "-ar", sample_rate.as_str(),
                    "-y", task.output_path.as_str(),
                ])
                .output();

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
                        message: Some(format!("Audio conversion completed ({} bytes).", out_size)),
                    });
                    Ok(())
                }
                Ok(output) => {
                    let stderr = String::from_utf8_lossy(&output.stderr);
                    bail!("Audio processing failed: {}", stderr);
                }
                Err(e) => {
                    bail!("Failed to execute FFmpeg engine: {}", e);
                }
            }
        })
    }
}

