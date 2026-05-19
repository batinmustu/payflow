-- Enable extensions PayFlow's schemas rely on.
-- Runs once, on the first container start (when the data volume is empty).
-- See deploy/docker-compose.infra.yml — pgvector/pgvector:pg16 already ships
-- the vector extension; this file just turns it on for the `payflow` database.

CREATE EXTENSION IF NOT EXISTS vector;
