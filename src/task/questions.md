## Clarifications Needed for Redpanda & Garnet Migration

1) Messaging format: Are events staying JSON, or do we need Avro + Schema Registry compatibility in Redpanda?
A: Staying JSON for now.
2) RabbitMQ-only behaviors: Do any existing queues rely on delayed delivery, priorities, or per-message TTL that we must replicate in Redpanda?
A: No, we can use Redpanda's built-in message expiration and TTL features.
3) Topic settings: Confirm desired topic list (e.g., audio-events, track-deletions), partition counts, and retention/compaction policies in Redpanda.
A: Ensure topics are configured with appropriate retention policies and compaction settings for durability and performance.
4) Environment prefixes: Should topics and consumer groups be namespaced by environment (dev/stage/prod) or tenant?
A: Namespaces by environment for now.
5) Security: Will Redpanda require SASL/TLS and does Garnet need auth/SSL, or are we running them plaintext in dev only?
A: Redpanda requires SASL/TLS, Garnet can run plaintext in dev only.
6) Cache durability: Is Garnet acceptable as in-memory only, or should we enable persistence/replication for session and presigned URL caches?
A: Enable persistence/replication for session and presigned URL caches to ensure durability.
7) TTLs & limits: Keep existing cache TTLs and payload size limits, or adjust when moving from NCache to Garnet?
A: Keep existing TTLs and payload size limits.
8) Test strategy: Are integration tests expected to use Testcontainers for Redpanda and Garnet, or may we stub messaging/cache for most cases?
A: Use Testcontainers for Redpanda and Garnet, stub messaging/cache for most cases.
9) Rollout order: Which environment should migrate first, and do we need coexistence (Kafka/RabbitMQ alongside Redpanda) during cutover?
A: Migrate dev first, then stage, followed by prod. Coexistence may be needed during cutover.
