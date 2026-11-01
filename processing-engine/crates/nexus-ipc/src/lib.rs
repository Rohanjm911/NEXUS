use nexus_core::TaskRegistry;

pub const PIPE_NAME: &str = r"\\.\pipe\nexus-engine";

pub struct IpcServer {
    pub pipe_name: String,
    pub registry: TaskRegistry,
}

impl IpcServer {
    pub fn new(pipe_name: Option<&str>, registry: TaskRegistry) -> Self {
        Self {
            pipe_name: pipe_name.unwrap_or(PIPE_NAME).to_string(),
            registry,
        }
    }

    pub fn registry(&self) -> &TaskRegistry {
        &self.registry
    }
}
