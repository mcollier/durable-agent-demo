# Squad Decisions

## Active Decisions

## 2026-08-08 — Feature Requirements: Feedback Blob Storage

Michael requested a planning-first requirements document for persisting customer feedback to Azure Blob Storage. The team decision is to capture implementation requirements in `docs/requirements/feedback-blob-storage.md`, with the current recommendation to reuse the existing storage account, provision a `customer-feedback` container via IaC, and persist one deterministic JSON blob per feedback item using the existing `FeedbackId`-based workflow. See: [`docs/requirements/feedback-blob-storage.md`](../docs/requirements/feedback-blob-storage.md)

### Decision: Feedback blob persistence reuses Function host storage

**Date:** 2026-08-08T21:54:30.921+00:00  
**Author:** Scribe  
**Requested by:** Michael S. Collier

#### Context

The feedback-blob-storage plan was rubber-duck reviewed before implementation, then revised in `docs/requirements/feedback-blob-storage.md` to make the pre-implementation findings explicit. The feature itself was implemented on `feature/feedback-blob-storage` across commits `68e2f06`, `16f9c75`, and `ad6bbe3`, with `53a6daa` capturing the reviewed requirements update that preceded code changes.

#### Decision

- Persist customer feedback plus AI analysis to blob storage immediately after `CustomerServiceAgent` analysis and before the existing `WaitForExternalEvent(HumanReviewCompletedEvent)` wait and customer email path.
- Reuse the Function App's existing host storage account in **every** environment — no separate `CUSTOMER_FEEDBACK_BLOB_SERVICE_URI` app setting or Aspire parameter exists anywhere in the final design (superseding an earlier interim version of this decision, commit `0bff1b6`):
  - In deployed Azure, construct the `BlobServiceClient` from `AzureWebJobsStorage__blobServiceUri` + `DefaultAzureCredential` — the same endpoint/auth model the Functions host already uses.
  - In local `aspire run`, `WithHostStorage(storage)` + `RunAsEmulator(...)` already injects `AzureWebJobsStorage` as a full Azurite connection string; construct the `BlobServiceClient` directly from that connection string (verified by inspecting the live local Functions process environment).
- Preserve the reviewed guardrails: UTC-normalized date-prefix partitioning, bounded durable retry policy on persistence, `Feedback.FeedbackId == Analysis.FeedbackId` validation, conditional-create idempotency preference, and explicit `JsonStringEnumConverter` serialization for enums.

#### Rationale

This keeps configuration minimal and non-duplicative in every environment, reuses the storage account and RBAC/emulator the Function App already depends on, and removes dead Aspire published-mode wiring — true single-storage-account reuse rather than a parallel resource or setting for feedback blobs.
