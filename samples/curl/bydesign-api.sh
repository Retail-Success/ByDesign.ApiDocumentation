#!/usr/bin/env bash
# ByDesign API v2 - curl sample
#
# Requires: bash, curl, jq
# Set these environment variables first (values come from the ByDesign back office):
#   BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
#   BYDESIGN_CLIENT_ID
#   BYDESIGN_CLIENT_SECRET
#   BYDESIGN_API_KEY
set -euo pipefail

: "${BYDESIGN_API_URL:?Set BYDESIGN_API_URL}"
: "${BYDESIGN_CLIENT_ID:?Set BYDESIGN_CLIENT_ID}"
: "${BYDESIGN_CLIENT_SECRET:?Set BYDESIGN_CLIENT_SECRET}"
: "${BYDESIGN_API_KEY:?Set BYDESIGN_API_KEY}"

# 1. Request an access token (valid for one hour - reuse it, don't request one per call)
ACCESS_TOKEN=$(curl --silent --show-error --fail \
  -X POST "$BYDESIGN_API_URL/oauth2/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=client_credentials" \
  --data-urlencode "client_id=$BYDESIGN_CLIENT_ID" \
  --data-urlencode "client_secret=$BYDESIGN_CLIENT_SECRET" \
  --data-urlencode "scope=api/Read api/Write" \
  | jq -r '.access_token')

# 2. GET request: list inventory categories (requires api/Read)
curl --silent --show-error --fail \
  "$BYDESIGN_API_URL/api/v2/inventory/categorydetail" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "x-api-key: $BYDESIGN_API_KEY" \
  | jq .

# 3. POST request: look up customers by email (requires api/Write)
curl --silent --show-error --fail \
  -X POST "$BYDESIGN_API_URL/api/v2/customer/lookup" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "x-api-key: $BYDESIGN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"Email": "jane.doe@example.com"}' \
  | jq .
