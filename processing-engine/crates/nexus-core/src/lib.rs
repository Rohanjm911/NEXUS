use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub enum TaskStatus {
    Queued,
    Processing,
    Paused,
    Completed,
    Failed,
    Cancelled,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ProcessingTask {
    pub id: String,
    pub name: String,
    pub source_path: String,
    pub output_path: String,
    pub source_format: String,
    pub target_format: String,
    pub category: String,
    pub options_json: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ProgressUpdate {
    pub task_id: String,
    pub status: TaskStatus,
    pub progress_percentage: f64,
    pub speed_bytes_per_sec: f64,
    pub processed_bytes: u64,
    pub total_bytes: u64,
    pub message: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IpcRequest {
    pub command: String, // "submit", "cancel", "health", "ping"
    pub task: Option<ProcessingTask>,
    pub task_id: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IpcResponse {
    pub success: bool,
    pub message: String,
    pub task_id: Option<String>,
}

pub type ProgressCallback = Arc<dyn Fn(ProgressUpdate) + Send + Sync>;

pub trait TaskProcessor: Send + Sync {
    fn process(
        &self,
        task: &ProcessingTask,
        on_progress: ProgressCallback,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = anyhow::Result<()>> + Send + '_>>;
}

#[derive(Clone, Default)]
pub struct TaskRegistry {
    active_tasks: Arc<RwLock<HashMap<String, tokio::sync::watch::Sender<bool>>>>,
}

impl TaskRegistry {
    pub fn new() -> Self {
        Self {
            active_tasks: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    pub async fn register(&self, task_id: &str) -> tokio::sync::watch::Receiver<bool> {
        let (tx, rx) = tokio::sync::watch::channel(false);
        let mut map = self.active_tasks.write().await;
        map.insert(task_id.to_string(), tx);
        rx
    }

    pub async fn cancel(&self, task_id: &str) -> bool {
        let map = self.active_tasks.read().await;
        if let Some(tx) = map.get(task_id) {
            let _ = tx.send(true);
            true
        } else {
            false
        }
    }

    pub async fn unregister(&self, task_id: &str) {
        let mut map = self.active_tasks.write().await;
        map.remove(task_id);
    }
}
