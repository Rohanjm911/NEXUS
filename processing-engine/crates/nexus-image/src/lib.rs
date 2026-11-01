use anyhow::{bail, Context, Result};
use image::imageops::FilterType;
use image::{DynamicImage, ImageFormat};
use nexus_core::{ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor, TaskStatus};
use nexus_utils::validate_output_file;
use serde::Deserialize;
use std::fs::File;
use std::path::Path;
use std::time::Instant;

#[derive(Debug, Deserialize, Default)]
struct ImageOptions {
    width: Option<u32>,
    height: Option<u32>,
    quality: Option<u8>,
    strip_metadata: Option<bool>,
    scale: Option<f32>,
    algorithm: Option<String>,
    sharpen: Option<f32>,
    denoise: Option<bool>,
}

pub struct ImageProcessor;

impl ImageProcessor {
    pub fn new() -> Self {
        Self
    }

    fn resolve_image_format(ext: &str) -> Result<ImageFormat> {
        match ext.to_lowercase().as_str() {
            "png" => Ok(ImageFormat::Png),
            "jpg" | "jpeg" => Ok(ImageFormat::Jpeg),
            "webp" => Ok(ImageFormat::WebP),
            "avif" => Ok(ImageFormat::Avif),
            "bmp" => Ok(ImageFormat::Bmp),
            "tiff" | "tif" => Ok(ImageFormat::Tiff),
            "gif" => Ok(ImageFormat::Gif),
            _ => bail!("Unsupported image format: {}", ext),
        }
    }
}

impl Default for ImageProcessor {
    fn default() -> Self {
        Self::new()
    }
}

impl TaskProcessor for ImageProcessor {
    fn process(
        &self,
        task: &ProcessingTask,
        on_progress: ProgressCallback,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = Result<()>> + Send + '_>> {
        let task = task.clone();
        Box::pin(async move {
            let start_time = Instant::now();
            let source_path = Path::new(&task.source_path);
            let output_path = Path::new(&task.output_path);

            if !source_path.exists() {
                bail!("Source file does not exist: {:?}", source_path);
            }

            let source_metadata = std::fs::metadata(source_path)?;
            let total_bytes = source_metadata.len();

            // Progress: 10% - Reading
            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Processing,
                progress_percentage: 10.0,
                speed_bytes_per_sec: 0.0,
                processed_bytes: 0,
                total_bytes,
                message: Some("Opening and decoding source image...".to_string()),
            });

            // Decode image
            let mut img: DynamicImage = image::open(source_path)
                .with_context(|| format!("Failed to decode image from {:?}", source_path))?;

            // Parse options
            let options: ImageOptions = serde_json::from_str(&task.options_json).unwrap_or_default();

            // Calculate target dimensions
            let (target_w, target_h) = if let Some(s) = options.scale {
                if s > 0.0 {
                    ((img.width() as f32 * s).round() as u32, (img.height() as f32 * s).round() as u32)
                } else {
                    (options.width.unwrap_or(img.width()), options.height.unwrap_or(img.height()))
                }
            } else if let (Some(w), Some(h)) = (options.width, options.height) {
                if w > 0 && h > 0 {
                    (w, h)
                } else {
                    (img.width(), img.height())
                }
            } else {
                (img.width(), img.height())
            };

            let filter = match options.algorithm.as_deref().unwrap_or("lanczos3") {
                "catmullrom" | "bicubic" => FilterType::CatmullRom,
                "nearest" | "integer" | "pixel" => FilterType::Nearest,
                "triangle" | "bilinear" => FilterType::Triangle,
                "gaussian" => FilterType::Gaussian,
                _ => FilterType::Lanczos3,
            };

            // Progress: 40% - Resizing / Super-sampling if requested
            if target_w != img.width() || target_h != img.height() {
                on_progress(ProgressUpdate {
                    task_id: task.id.clone(),
                    status: TaskStatus::Processing,
                    progress_percentage: 40.0,
                    speed_bytes_per_sec: 0.0,
                    processed_bytes: total_bytes / 2,
                    total_bytes,
                    message: Some(format!("Super-sampling to {}x{} using {:?} filter...", target_w, target_h, filter)),
                });

                img = img.resize_exact(target_w, target_h, filter);
            }

            // Progress: 60% - Edge sharpening & detail recovery
            if let Some(sigma) = options.sharpen {
                if sigma > 0.0 {
                    on_progress(ProgressUpdate {
                        task_id: task.id.clone(),
                        status: TaskStatus::Processing,
                        progress_percentage: 60.0,
                        speed_bytes_per_sec: 0.0,
                        processed_bytes: (total_bytes * 6) / 10,
                        total_bytes,
                        message: Some("Applying adaptive edge sharpening & detail recovery...".to_string()),
                    });
                    img = img.unsharpen(sigma, 10);
                }
            }

            // Progress: 70% - Encoding to target format
            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Processing,
                progress_percentage: 70.0,
                speed_bytes_per_sec: 0.0,
                processed_bytes: (total_bytes * 7) / 10,
                total_bytes,
                message: Some(format!("Encoding to {}...", task.target_format)),
            });

            if let Some(parent) = output_path.parent() {
                std::fs::create_dir_all(parent)?;
            }

            let format = Self::resolve_image_format(&task.target_format)?;
            let mut out_file = File::create(output_path)?;
            img.write_to(&mut out_file, format)?;

            // Validate output
            let output_size = validate_output_file(output_path, 10)?;
            let elapsed = start_time.elapsed().as_secs_f64().max(0.001);
            let speed = (total_bytes as f64) / elapsed;

            // Progress: 100% - Completed
            on_progress(ProgressUpdate {
                task_id: task.id.clone(),
                status: TaskStatus::Completed,
                progress_percentage: 100.0,
                speed_bytes_per_sec: speed,
                processed_bytes: output_size,
                total_bytes,
                message: Some(format!("Conversion completed successfully ({} bytes).", output_size)),
            });

            Ok(())
        })
    }
}

