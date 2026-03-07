#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# deploy.sh — Deploy Durable Agent Serverless infrastructure
# ──────────────────────────────────────────────────────────────────────────────
# Usage:
#   ./deploy.sh                       # deploy with defaults from main.bicepparam
#   ./deploy.sh -l westus2            # override location
#   ./deploy.sh -n my-deployment      # custom deployment name
#   ./deploy.sh -w                    # what-if preview (no changes applied)
#   ./deploy.sh -d                    # delete the deployment stack
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_FILE="${SCRIPT_DIR}/main.bicep"
PARAMS_FILE="${SCRIPT_DIR}/main.bicepparam"
DEPLOYMENT_NAME="durable-agent-$(date +%Y%m%d-%H%M%S)"
LOCATION=""
WHAT_IF=false
DELETE=false

usage() {
  cat <<EOF
Usage: $(basename "$0") [OPTIONS]

Options:
  -l, --location LOCATION   Azure region (overrides param file value)
  -n, --name NAME           Deployment name (default: durable-agent-<timestamp>)
  -w, --what-if             Run what-if preview without deploying
  -d, --delete              Delete the resource group and deployment
  -h, --help                Show this help message
EOF
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -l|--location)   LOCATION="$2"; shift 2 ;;
    -n|--name)       DEPLOYMENT_NAME="$2"; shift 2 ;;
    -w|--what-if)    WHAT_IF=true; shift ;;
    -d|--delete)     DELETE=true; shift ;;
    -h|--help)       usage ;;
    *) echo "Unknown option: $1"; usage ;;
  esac
done

# ─── Pre-flight checks ──────────────────────────────────────────────────────

if ! command -v az &>/dev/null; then
  echo "Error: Azure CLI (az) is not installed." >&2
  exit 1
fi

if ! az account show &>/dev/null; then
  echo "Error: Not logged in. Run 'az login' first." >&2
  exit 1
fi

SUBSCRIPTION=$(az account show --query "{name:name, id:id}" -o tsv)
echo "Active subscription: ${SUBSCRIPTION}"
echo ""

# Resolve location: CLI flag > extract from .bicepparam > fail
if [[ -z "${LOCATION}" ]]; then
  LOCATION=$(grep -oP "param location\s*=\s*'\K[^']+" "${PARAMS_FILE}" 2>/dev/null || true)
fi

if [[ -z "${LOCATION}" ]]; then
  echo "Error: Location not specified. Use -l flag or set it in main.bicepparam." >&2
  exit 1
fi

echo "Deployment name : ${DEPLOYMENT_NAME}"
echo "Location        : ${LOCATION}"
echo "Template        : ${TEMPLATE_FILE}"
echo "Parameters      : ${PARAMS_FILE}"
echo ""

# ─── Delete mode ─────────────────────────────────────────────────────────────

if [[ "${DELETE}" == true ]]; then
  RG_NAME=$(grep -oP "param resourceGroupName\s*=\s*'\K[^']+" "${PARAMS_FILE}" 2>/dev/null || true)
  if [[ -z "${RG_NAME}" ]]; then
    RG_NAME=$(grep -oP "param baseName\s*=\s*'\K[^']+" "${PARAMS_FILE}" 2>/dev/null || true)
    RG_NAME="rg-${RG_NAME}"
  fi
  echo "Deleting resource group: ${RG_NAME}"
  read -rp "Are you sure? (y/N): " confirm
  if [[ "${confirm}" =~ ^[Yy]$ ]]; then
    az group delete --name "${RG_NAME}" --yes --no-wait
    echo "Resource group deletion initiated."
  else
    echo "Cancelled."
  fi
  exit 0
fi

# ─── What-If mode ────────────────────────────────────────────────────────────

if [[ "${WHAT_IF}" == true ]]; then
  echo "Running what-if analysis..."
  echo ""
  az deployment sub what-if \
    --name "${DEPLOYMENT_NAME}" \
    --location "${LOCATION}" \
    --template-file "${TEMPLATE_FILE}" \
    --parameters "${PARAMS_FILE}" \
    ${LOCATION:+--parameters location="${LOCATION}"}
  exit 0
fi

# ─── Deploy ──────────────────────────────────────────────────────────────────

echo "Starting deployment..."
echo ""

az deployment sub create \
  --name "${DEPLOYMENT_NAME}" \
  --location "${LOCATION}" \
  --template-file "${TEMPLATE_FILE}" \
  --parameters "${PARAMS_FILE}" \
  ${LOCATION:+--parameters location="${LOCATION}"} \
  --output table

echo ""
echo "Deployment complete. Fetching outputs..."
echo ""

az deployment sub show \
  --name "${DEPLOYMENT_NAME}" \
  --query "properties.outputs" \
  --output json
