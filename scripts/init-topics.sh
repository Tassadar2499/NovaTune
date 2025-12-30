#!/bin/bash
# =============================================================================
# Initialize Redpanda Topics
# =============================================================================
# Creates the required Kafka topics in Redpanda for NovaTune.
# Run this script after starting the Redpanda container.
#
# Usage: ./scripts/init-topics.sh [topic_prefix]
#
# Topics created:
#   - ${TOPIC_PREFIX}-audio-events: Audio upload/processing events (7d retention)
#   - ${TOPIC_PREFIX}-track-deletions: Track deletion events (compaction, 30d)
# =============================================================================

set -e

TOPIC_PREFIX=${1:-${TOPIC_PREFIX:-dev}}
REDPANDA_HOST=${REDPANDA_HOST:-localhost}
REDPANDA_PORT=${REDPANDA_PORT:-19092}

echo "Initializing Redpanda topics with prefix: ${TOPIC_PREFIX}"
echo "Connecting to: ${REDPANDA_HOST}:${REDPANDA_PORT}"

# Wait for Redpanda to be ready
echo "Waiting for Redpanda to be ready..."
until rpk cluster info --brokers "${REDPANDA_HOST}:${REDPANDA_PORT}" > /dev/null 2>&1; do
    echo "  Redpanda not ready yet, retrying in 2 seconds..."
    sleep 2
done
echo "Redpanda is ready!"

# Create audio-events topic
# 3 partitions, 7 day retention (604800000 ms)
echo "Creating topic: ${TOPIC_PREFIX}-audio-events"
rpk topic create "${TOPIC_PREFIX}-audio-events" \
    --brokers "${REDPANDA_HOST}:${REDPANDA_PORT}" \
    --partitions 3 \
    --config retention.ms=604800000 \
    || echo "  Topic may already exist, skipping..."

# Create track-deletions topic
# 3 partitions, compaction enabled, 30 day max retention (2592000000 ms)
echo "Creating topic: ${TOPIC_PREFIX}-track-deletions"
rpk topic create "${TOPIC_PREFIX}-track-deletions" \
    --brokers "${REDPANDA_HOST}:${REDPANDA_PORT}" \
    --partitions 3 \
    --config cleanup.policy=compact \
    --config retention.ms=2592000000 \
    || echo "  Topic may already exist, skipping..."

# List all topics
echo ""
echo "Topics created successfully. Current topics:"
rpk topic list --brokers "${REDPANDA_HOST}:${REDPANDA_PORT}"
