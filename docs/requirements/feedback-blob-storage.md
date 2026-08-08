# Feedback Blob Storage Requirements

## Overview / Goal

Add durable storage for Froyo Foundry customer feedback by writing each submitted feedback item to Azure Blob Storage as a JSON file in the `customer-feedback` container. The goal is to preserve an auditable copy of each feedback submission, grouped by submission date for easy support and operations review, while staying aligned with the repo's existing zero-secrets, managed-identity-first Azure Functions architecture.

## Functional Requirements

### 1. Storage location

- Persist feedback JSON blobs to the existing Azure Storage account already provisioned in `infra/main.bicep`.
- Use blob container name: `customer-feedback`.
- Use blob naming pattern: `{yyyy-MM-dd}/{feedbackId}.json`.
  - Example: `2028-08-07/fbk-10021.json`
  - The date prefix must come from `FeedbackMessage.SubmittedAt` in UTC.
  - Implementation must normalize with `SubmittedAt.ToUniversalTime()` before formatting `yyyy-MM-dd` (or use an equivalent UTC-guaranteed approach), because `SubmittedAt` may carry a non-UTC offset.
  - The filename must use the existing `FeedbackMessage.FeedbackId`, not a new GUID, so retries do not create duplicates.

### 2. JSON document shape

Persist a single envelope document per feedback item so the blob contains both the original submission and the AI analysis already produced by the orchestration pipeline.

Recommended top-level shape:

```json
{
  "feedback": {
    "feedbackId": "fbk-10021",
    "submittedAt": "2028-08-07T18:22:11.0000000+00:00",
    "storeId": "store-014",
    "orderId": "ord-77812",
    "customer": {
      "preferredName": "Aidan",
      "firstName": "Aidan",
      "lastName": "Smith",
      "email": "aidan@example.com",
      "phoneNumber": "555-0100",
      "preferredContactMethod": "email"
    },
    "channel": "kiosk",
    "rating": 5,
    "comment": "Mint Condition is unreal. Best froyo I've had.",
    "flavorId": "flavor-001"
  },
  "analysis": {
    "feedbackId": "fbk-10021",
    "sentiment": "positive",
    "risk": {
      "isHealthOrSafety": false,
      "isFoodQualityIssue": false,
      "keywords": []
    },
    "action": "THANK_YOU",
    "coupon": null,
    "followUp": {
      "requiresHuman": false,
      "caseId": null
    },
    "confidence": 0.98
  }
}
```

Required field sources:

- `feedback`: the existing `DurableAgent.Core.Models.FeedbackMessage`
- `analysis`: the existing `DurableAgent.Core.Models.FeedbackResult`

Serialization requirements:

- Write JSON as `application/json`.
- Use camelCase property names.
- Serialize with a shared static `JsonSerializerOptions` instance named `ProcessedFeedbackRecordJsonSerializerOptions.Default`.
- `ProcessedFeedbackRecordJsonSerializerOptions.Default` must set `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and include `new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` so enum values serialize as camelCase strings (for example `email` / `phone`), not integers.
- Do not rely on current default `System.Text.Json` behavior or existing enum annotations alone; the persisted blob serializer must explicitly guarantee the enum-string output above.

### 3. Pipeline location

`ProcessFeedbackActivity` is the correct implementation surface because it is already the placeholder/extensibility point for post-analysis business logic. However, the orchestrator should pass both the original `FeedbackMessage` and the `FeedbackResult` into that activity.

Recommended orchestration change:

- Keep blob persistence inside `ProcessFeedbackActivity`
- Change the activity input from `FeedbackMessage` to a new envelope model containing:
  - `FeedbackMessage Feedback`
  - `FeedbackResult Analysis`
  - optional deterministic metadata such as orchestration instance ID if the team wants it

Recommended call order in `FeedbackOrchestrator`:

1. Run `CustomerServiceAgent`
2. Construct the `ProcessedFeedbackRecord` envelope from the original `FeedbackMessage` plus the `FeedbackResult`
3. Call `ProcessFeedbackActivity` immediately after obtaining `FeedbackResult`, with a bounded Durable Task retry policy on that persistence call
4. Only after persistence succeeds, evaluate the `RequiresHuman` branch and, if needed, wait on `HumanReviewCompleted`
5. Compose the follow-up email with `EmailAgent`
6. Send the follow-up email

This ordering ensures the audit record exists before any potentially indefinite human-review wait, and still ensures feedback is stored even if downstream email sending fails.

## Non-Functional Requirements

### Authentication / security

- Must use managed identity only.
- Must not use connection strings, account keys, or SAS tokens.
- Must follow the repo's existing pattern in `Program.cs`, `AIServiceExtensions.cs`, and `EmailServiceExtensions.cs`:
  - development: Azure CLI credential
  - deployed Azure: managed-identity-capable credential (`DefaultAzureCredential` is already the current project pattern)

### Idempotency and retries

- Durable activity executions must be safe under retry/at-least-once execution semantics.
- Blob naming must be deterministic: `{submittedAtUtc:yyyy-MM-dd}/{feedbackId}.json`.
- The write operation must not generate a second file on retry.
- The new persistence activity call in `FeedbackOrchestrator` must use `TaskOptions` with a bounded `RetryPolicy` so transient blob failures do not permanently fail the orchestration on first attempt.
  - Default requirement: `maxNumberOfAttempts = 4`, `firstRetryInterval = 5 seconds`, `backoffCoefficient = 2.0`, `maxRetryInterval = 1 minute`.
  - If implementation details require slightly different concrete values, keep them bounded and in the same range.
- Preferred behavior: use conditional-create semantics (`If-None-Match: *`, or the Azure Storage SDK equivalent) so a concurrent duplicate orchestration instance cannot silently overwrite an existing blob with different analysis content.
- If the blob already exists, treat that as success only when the existing content is equivalent to the would-be payload; otherwise, log and fail/handle it distinctly as a `FeedbackId` collision.
- Straight overwrite to the same blob name is an accepted fallback only if conditional-create semantics prove disproportionately complex with the Azure Storage SDK. If that fallback is chosen, the implementation must document the tradeoff explicitly in code comments or PR notes rather than leaving overwrite behavior implicit.
- Do not stamp the JSON with `DateTimeOffset.UtcNow` inside the activity, because that would make retries produce different payloads. If processed-time metadata is needed, it should be supplied deterministically by the orchestrator.

### Validation

- Validate the full persistence envelope before upload.
- `Feedback.FeedbackId` and `Analysis.FeedbackId` must both be present and must match exactly; mismatches must fail fast and be logged/rejected rather than uploaded.
- Validate `FeedbackId` before constructing the blob name:
  - required / non-empty
  - must not contain blob-path-unsafe separators such as `/`
- Fail fast on other required-field omissions needed to build the blob path or payload.

### Error handling

- Fail the activity if blob upload fails; do not silently swallow storage exceptions.
- Log the `feedbackId`, container name, and blob name on success/failure.
- Surface failures back to the orchestration so normal Durable Functions retry/error behavior applies.

### Container provisioning

- The `customer-feedback` container must be created through infrastructure-as-code.
- Production code must not depend on runtime container creation.
- Local development should also treat a missing `customer-feedback` container as a deployment/configuration problem; do not rely on runtime container creation for this feature.
- Do not plan around Azurite for this blob-persistence path, because Azurite does not support Entra ID / managed-identity-style authentication.

## Infrastructure Changes Needed

### Recommended approach: reuse the existing storage account

Use the storage account already provisioned by `infra/main.bicep`.

Rationale:

- the repo already provisions one `StorageV2` account with blob service enabled
- the Function App already uses that account with managed identity
- `modules/rbac.bicep` already grants the Function App `Storage Blob Data Contributor` at the storage-account scope
- adding one container is simpler than introducing a second account, new settings, and extra RBAC

### Bicep changes

Update `infra/main.bicep`:

- add `{ name: 'customer-feedback' }` to the `blobServices.containers` array of the existing `storageAccount` AVM module

Example intent:

```bicep
blobServices: {
  containers: [
    { name: deploymentStorageContainerName }
    { name: 'customer-feedback' }
  ]
}
```

### RBAC changes

- **No new RBAC assignment is required if the existing storage account is reused**, because `modules/rbac.bicep` already assigns `Storage Blob Data Contributor` to the Function App managed identity at the storage account scope.
- If the team later chooses a dedicated storage account instead, `modules/rbac.bicep` would need an additional assignment for that new account.

### Configuration changes

One explicit blob endpoint setting should be added for application code clarity, even though it points at the same storage account already used by the Functions host:

- Function App app setting in `infra/main.bicep`: `CUSTOMER_FEEDBACK_BLOB_SERVICE_URI`
- Value: `storageAccount.outputs.primaryBlobEndpoint`
- In local/Aspire development, do **not** point this setting at Azurite. Instead, align with the repo's existing local pattern for Azure-backed dependencies (`Program.cs`, `AIServiceExtensions.cs`, `EmailServiceExtensions.cs`): local runs use `AzureCliCredential` against a real Azure resource endpoint, while deployed Azure uses `DefaultAzureCredential`.
- In `source/DurableAgent.AppHost/AppHost.cs`, expose the setting as a real-Azure parameter (mirroring how Service Bus, Azure OpenAI, and email settings are already passed through), then forward it into the Functions project as `CUSTOMER_FEEDBACK_BLOB_SERVICE_URI`.
- Keep Aspire host storage (`WithHostStorage(storage)`) separate from this feature's blob endpoint. The Functions host may continue using Azurite for runtime storage in run mode, but feedback-blob persistence itself must target a real Azure Storage account endpoint for local development.

## Code Changes Needed

### New files

- `source/DurableAgent.Functions/Services/IFeedbackBlobStorageService.cs`
  - abstraction for persisting feedback blobs
- `source/DurableAgent.Functions/Services/AzureBlobFeedbackStorageService.cs`
  - Azure Blob Storage implementation
- `source/DurableAgent.Functions/Models/ProcessedFeedbackRecord.cs`
  - `sealed record` envelope containing the persisted `FeedbackMessage` + `FeedbackResult`
- `source/DurableAgent.Functions/Models/ProcessedFeedbackRecordJsonSerializerOptions.cs`
  - shared static JSON options for persisted feedback blobs (`ProcessedFeedbackRecordJsonSerializerOptions.Default`)
- `source/DurableAgent.Functions/Extensions/BlobStorageExtensions.cs`
  - registers the blob client/service using managed identity and the configured blob endpoint

### Existing files to change

- `source/DurableAgent.Functions/DurableAgent.Functions.csproj`
  - add `Azure.Storage.Blobs`
- `source/DurableAgent.Functions/Program.cs`
  - call the new blob-storage registration extension
- `source/DurableAgent.Functions/Orchestrations/FeedbackOrchestrator.cs`
  - construct `ProcessedFeedbackRecord`
  - call `ProcessFeedbackActivity` with the new record instead of raw `FeedbackMessage`
  - invoke persistence immediately after `CustomerServiceAgent` returns, before any `RequiresHuman` wait branch and before `SendCustomerEmailActivity`
  - use `TaskOptions` + bounded `RetryPolicy` on the persistence activity call
- `source/DurableAgent.Functions/Activities/ProcessFeedbackActivity.cs`
  - make the activity async
  - resolve `IFeedbackBlobStorageService` from `FunctionContext.InstanceServices`, matching the existing `SendCustomerEmailActivity` pattern
  - validate input, including `Feedback.FeedbackId == Analysis.FeedbackId` and blob-name-safe `FeedbackId`
  - serialize and upload the JSON blob
  - return a message that includes the blob path or feedback ID
- `source/DurableAgent.AppHost/AppHost.cs`
  - pass through `CUSTOMER_FEEDBACK_BLOB_SERVICE_URI` for local orchestration using a real Azure Storage endpoint parameter, not Azurite blob emulation

### Implementation notes aligned to repo conventions

- Keep Azure SDK usage in `DurableAgent.Functions`, not `DurableAgent.Core`, because Core is intentionally cloud-SDK-free.
- Keep the activity as a static function, consistent with the repo's activity convention.
- Use `FunctionContext.InstanceServices` to resolve the storage service, consistent with `SendCustomerEmailActivity`.
- Use a `sealed record` for the persisted envelope model, consistent with existing DTO conventions.

## Testing Considerations

- Update `source/DurableAgent.Functions.Tests/Activities/ProcessFeedbackActivityTests.cs`
  - inject a fake `IFeedbackBlobStorageService` via `FunctionContext.InstanceServices`
  - verify the activity calls the storage service once for valid input
  - verify validation/guard-clause behavior remains intact
  - verify `Feedback.FeedbackId` / `Analysis.FeedbackId` mismatch is rejected
  - verify upload failures are propagated, not swallowed
- Add `source/DurableAgent.Functions.Tests/Services/AzureBlobFeedbackStorageServiceTests.cs`
  - verify container name is `customer-feedback`
  - verify blob name uses the exact `{yyyy-MM-dd}/{feedbackId}.json` path derived from `SubmittedAt.ToUniversalTime()`
  - include a UTC day-boundary case where a non-UTC offset crosses into a different UTC date
  - verify the exact blob path, container name, and `application/json` content type used for upload
  - verify the serialized payload contains both `feedback` and `analysis`
  - verify enum values serialize as camelCase strings, not numbers
- Add/adjust orchestration tests to verify the persistence activity is scheduled immediately after `CustomerServiceAgent`, before any `RequiresHuman` wait and before email composition/send, and that the persistence call carries the configured retry policy.
- If orchestration ordering is changed, add/update orchestration-focused tests to verify persistence happens before email dispatch.

Use the existing test stack and patterns already present in the repo:

- xUnit
- FakeItEasy
- fake `FunctionContext` + DI service collection

## Decisions (resolved 2026-08-08 by Michael S. Collier)

1. **PII:** No redaction required. Store raw feedback as submitted — `RedactPiiTool` is out of scope for this feature.
2. **Filename:** `{feedbackId}.json` confirmed as the blob filename.
3. **Envelope:** `feedback + analysis` envelope confirmed as acceptable.
4. **Retention:** No retention/lifecycle policy needed at this time. Blobs are retained indefinitely.

## Review Notes (resolved 2026-08-08)

- UTC blob-date derivation now explicitly requires `SubmittedAt.ToUniversalTime()` (or equivalent UTC normalization) before formatting `yyyy-MM-dd`.
- Persistence ordering now requires the blob write immediately after `CustomerServiceAgent` returns and before any human-review wait or email work.
- Persistence activity retries now require a bounded Durable Task `RetryPolicy` on the orchestrator call.
- Idempotency now defaults to conditional-create semantics, with explicit collision handling if an existing blob differs.
- Envelope validation now requires matching `Feedback.FeedbackId` / `Analysis.FeedbackId`, required fields, and blob-name-safe `FeedbackId` input.
- Local development now targets a real Azure Storage endpoint with `AzureCliCredential`; Azurite is explicitly not the auth model for this feature.
- Blob serialization now requires shared explicit `JsonSerializerOptions` with camelCase property names and camelCase enum-string output.
- Testing guidance now includes UTC day-boundary, exact blob path/content type, enum-string serialization, exception propagation, and ID-mismatch coverage.

## Out of Scope

This requirements document does **not** cover:

- building a UI or dashboard to browse stored feedback
- querying or reprocessing stored blobs
- lifecycle management/automatic deletion beyond noting it as an open question
- search indexing, analytics pipelines, or data warehousing
- changes to how feedback is submitted over HTTP or Service Bus
