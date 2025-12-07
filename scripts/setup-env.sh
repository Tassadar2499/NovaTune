#!/bin/bash
# =============================================================================
# NovaTune Environment Setup Script
# =============================================================================
# Creates a .env file from .env.example for local development.
# =============================================================================

set -e

ENV_FILE=".env"
EXAMPLE_FILE=".env.example"

# Change to repository root
cd "$(dirname "$0")/.."

if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "ERROR: $EXAMPLE_FILE not found"
    exit 1
fi

if [ -f "$ENV_FILE" ]; then
    echo "Warning: $ENV_FILE already exists."
    read -p "Overwrite? (y/N): " confirm
    if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
        echo "Aborted."
        exit 0
    fi
fi

cp "$EXAMPLE_FILE" "$ENV_FILE"
echo "Created $ENV_FILE from $EXAMPLE_FILE"
echo ""
echo "Next steps:"
echo "1. Review and update values in $ENV_FILE"
echo "2. Generate JWT signing key: ./scripts/generate-keys.sh"
echo "3. Start infrastructure: docker compose up -d"
