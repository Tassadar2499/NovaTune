# 3. MinIO Integration

## Bucket Configuration

- Bucket name: `{env}-audio-uploads` (e.g., `dev-audio-uploads`)
- Enable versioning (for NF-6.5 DR requirements)
- Configure lifecycle policy to abort incomplete multipart uploads after 24 hours

## Bucket Notification → Redpanda

Configure MinIO to publish `s3:ObjectCreated:*` events to Redpanda:

```bash
mc admin config set myminio notify_kafka:novatune \
  brokers="redpanda:9092" \
  topic="{env}-minio-events" \
  queue_dir="/tmp/minio/events"

mc event add myminio/{env}-audio-uploads arn:minio:sqs::novatune:kafka \
  --event put --prefix "audio/"
```

## Event Payload (MinIO → Redpanda)

```json
{
  "EventName": "s3:ObjectCreated:Put",
  "Key": "audio/01HXK.../01HXK.../a1B2c3D4e5F6g7H8",
  "Records": [{
    "s3": {
      "bucket": { "name": "dev-audio-uploads" },
      "object": {
        "key": "audio/...",
        "size": 15728640,
        "contentType": "audio/mpeg",
        "eTag": "abc123..."
      }
    }
  }]
}
```
