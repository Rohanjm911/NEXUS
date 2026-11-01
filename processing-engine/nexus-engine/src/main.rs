use anyhow::Result;
use nexus_archive::ArchiveProcessor;
use nexus_audio::AudioProcessor;
use nexus_core::{
    IpcRequest, IpcResponse, ProcessingTask, ProgressCallback, ProgressUpdate, TaskProcessor,
    TaskRegistry, TaskStatus,
};
use nexus_image::ImageProcessor;
use nexus_pdf::PdfProcessor;
use nexus_video::VideoProcessor;
use std::sync::Arc;
use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader};
use tokio::net::windows::named_pipe::ServerOptions;
use tokio::sync::mpsc;

const PIPE_NAME: &str = r"\\.\pipe\nexus-engine";

#[tokio::main]
async fn main() -> Result<()> {
    println!("NEXUS Processing Engine starting...");
    println!("Platform: Windows Named Pipe Server at {}", PIPE_NAME);

    let registry = TaskRegistry::new();

    let image_proc = Arc::new(ImageProcessor::new());
    let audio_proc = Arc::new(AudioProcessor::new());
    let video_proc = Arc::new(VideoProcessor::new());
    let pdf_proc = Arc::new(PdfProcessor::new());
    let archive_proc = Arc::new(ArchiveProcessor::new());

    // Main Named Pipe accept loop
    let mut server = ServerOptions::new()
        .first_pipe_instance(true)
        .create(PIPE_NAME)?;

    println!("NEXUS Processing Engine listening for incoming pipe connections.");

    loop {
        server.connect().await?;
        let connected_client = server;

        // Construct next pipe instance for future clients
        server = ServerOptions::new().create(PIPE_NAME)?;

        let reg = registry.clone();
        let img = image_proc.clone();
        let aud = audio_proc.clone();
        let vid = video_proc.clone();
        let pdf = pdf_proc.clone();
        let arc = archive_proc.clone();

        tokio::spawn(async move {
            if let Err(e) = handle_client(connected_client, reg, img, aud, vid, pdf, arc).await {
                eprintln!("Error handling IPC client: {:?}", e);
            }
        });
    }
}

async fn handle_client(
    stream: tokio::net::windows::named_pipe::NamedPipeServer,
    registry: TaskRegistry,
    image_proc: Arc<ImageProcessor>,
    audio_proc: Arc<AudioProcessor>,
    video_proc: Arc<VideoProcessor>,
    pdf_proc: Arc<PdfProcessor>,
    archive_proc: Arc<ArchiveProcessor>,
) -> Result<()> {
    let (reader, mut writer) = tokio::io::split(stream);
    let mut lines = BufReader::new(reader).lines();

    let (progress_tx, mut progress_rx) = mpsc::channel::<ProgressUpdate>(100);

    // Background writer loop for streaming progress events
    let write_handle = tokio::spawn(async move {
        while let Some(progress) = progress_rx.recv().await {
            if let Ok(json) = serde_json::to_string(&progress) {
                if writer.write_all(format!("{}\n", json).as_bytes()).await.is_err() {
                    break;
                }
                let _ = writer.flush().await;
            }
        }
    });

    while let Some(line) = lines.next_line().await? {
        if line.trim().is_empty() {
            continue;
        }

        if let Ok(req) = serde_json::from_str::<IpcRequest>(&line) {
            match req.command.as_str() {
                "health" | "ping" => {
                    let resp = IpcResponse {
                        success: true,
                        message: "NEXUS Processing Engine is healthy and responsive.".to_string(),
                        task_id: None,
                    };
                    let json = serde_json::to_string(&resp)?;
                    progress_tx
                        .send(ProgressUpdate {
                            task_id: "health".to_string(),
                            status: TaskStatus::Completed,
                            progress_percentage: 100.0,
                            speed_bytes_per_sec: 0.0,
                            processed_bytes: 0,
                            total_bytes: 0,
                            message: Some(json),
                        })
                        .await?;
                }
                "cancel" => {
                    if let Some(id) = req.task_id {
                        let cancelled = registry.cancel(&id).await;
                        let resp = IpcResponse {
                            success: cancelled,
                            message: if cancelled {
                                "Task cancelled."
                            } else {
                                "Task not found."
                            }
                            .to_string(),
                            task_id: Some(id),
                        };
                        let json = serde_json::to_string(&resp)?;
                        println!("Cancelled task: {}", json);
                    }
                }
                "submit" => {
                    if let Some(task) = req.task {
                        let task_id = task.id.clone();
                        let _cancel_rx = registry.register(&task_id).await;
                        let p_tx = progress_tx.clone();

                        let callback: ProgressCallback = Arc::new(move |update: ProgressUpdate| {
                            let tx = p_tx.clone();
                            tokio::spawn(async move {
                                let _ = tx.send(update).await;
                            });
                        });

                        let category = task.category.to_lowercase();
                        let reg_clone = registry.clone();
                        let t_id = task_id.clone();

                        let img = image_proc.clone();
                        let aud = audio_proc.clone();
                        let vid = video_proc.clone();
                        let pdf = pdf_proc.clone();
                        let arc = archive_proc.clone();

                        tokio::spawn(async move {
                            let result = match category.as_str() {
                                "image" | "1" => img.process(&task, callback.clone()).await,
                                "audio" | "2" => aud.process(&task, callback.clone()).await,
                                "video" | "3" => vid.process(&task, callback.clone()).await,
                                "document" | "4" => pdf.process(&task, callback.clone()).await,
                                "archive" | "5" => arc.process(&task, callback.clone()).await,
                                _ => img.process(&task, callback.clone()).await,
                            };

                            if let Err(e) = result {
                                callback(ProgressUpdate {
                                    task_id: t_id.clone(),
                                    status: TaskStatus::Failed,
                                    progress_percentage: 0.0,
                                    speed_bytes_per_sec: 0.0,
                                    processed_bytes: 0,
                                    total_bytes: 0,
                                    message: Some(format!("Error: {}", e)),
                                });
                            }

                            reg_clone.unregister(&t_id).await;
                        });
                    }
                }
                _ => {}
            }
        }
    }

    let _ = write_handle.await;
    Ok(())
}

