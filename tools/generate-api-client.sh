#!/usr/bin/env bash
# Regenera o cliente TypeScript a partir do OpenAPI exportado pelo backend.
# Requer: backend rodando em localhost:5000 (ou BACKEND_URL configurado).
set -euo pipefail

BACKEND_URL="${BACKEND_URL:-http://localhost:5000}"
SPEC_URL="${BACKEND_URL}/api/v1/openapi.json"
OUTPUT_DIR="packages/api-contracts/src/generated"

echo "Fetching OpenAPI spec from ${SPEC_URL}..."
curl -sf "${SPEC_URL}" -o /tmp/honorare-openapi.json

echo "Generating TypeScript client..."
npx @hey-api/openapi-ts \
  --input /tmp/honorare-openapi.json \
  --output "${OUTPUT_DIR}" \
  --client @hey-api/client-fetch

echo "Done. Client written to ${OUTPUT_DIR}"
