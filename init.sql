CREATE TABLE IF NOT EXISTS imm_data (
    id SERIAL PRIMARY KEY,
    machine_id VARCHAR(10),
    sensor_id VARCHAR(50),
    metric VARCHAR(50),
    value DOUBLE PRECISION,
    timestamp TIMESTAMP,
    status VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_machine_timestamp ON imm_data (machine_id, timestamp);
