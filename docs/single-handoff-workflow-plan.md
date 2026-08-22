# Single Order Handoff Workflow

## Goal

Use one Microsoft Agent Framework handoff workflow for the complete order lifecycle. This avoids
wrapping a nested workflow with `AsAIAgent()`, which caused workflow/agent registry identity
mismatches at runtime.

## Participants

- Order Intake validates the order.
- Fulfillment Decision checks inventory.
- Substitution finds in-stock replacements.
- Promotion issues one policy-compliant coupon when appropriate.
- Escalation opens one customer-service case when human review is required.
- Customer Messaging sends the final email and produces the workflow output.

The former Order Resolution coordinator is not needed because handoff participants receive
synchronized user and agent messages.

## Routing

```text
OrderIntake
  |-- valid ----------------------> FulfillmentDecision
  `-- invalid ------------------------------------------> CustomerMessaging

FulfillmentDecision
  |-- fully fulfilled ----------------------------------> CustomerMessaging
  |-- substitution useful -------> Substitution
  |-- compensation only ---------> Promotion
  `-- human review required -----> Escalation

Substitution
  |-- further compensation ------> Promotion
  |-- human review required -----> Escalation
  `-- resolution complete ------------------------------> CustomerMessaging

Promotion
  |-- human review required -----> Escalation
  `-- resolution complete ------------------------------> CustomerMessaging

Escalation ---------------------------------------------> CustomerMessaging
CustomerMessaging --------------------------------------> terminal output
```

This progression supports multiple specialists without cycles, duplicate coupons, or duplicate
case creation.

## Execution Controls

- Autonomous mode is enabled because no human input is expected between agents.
- Each agent is limited to four autonomous turns.
- The workflow terminates only when an assistant message deserializes to a complete
  `CustomerMessageResult`.
- Customer Messaging is the only terminal output source.
- Participant names and IDs remain stable for durable checkpoint reconstruction.

## Package Baseline

- `Microsoft.Agents.AI.Workflows`: 1.18.0
- `Microsoft.Agents.AI.Hosting`: 1.18 preview line
- `Microsoft.Agents.AI.OpenAI`: 1.18.0
- `Microsoft.Agents.AI.DurableTask`: 1.16.0 preview line
- `Microsoft.Agents.AI.Hosting.AzureFunctions`: 1.16.0 preview line

The durable packages have no 1.18 release on NuGet at the time of implementation.

## Durable Hosting Boundary

The 1.16 durable workflow runner cannot directly start a 1.18 handoff workflow. It converts the
entire run request to one string, while `HandoffStart` deliberately buffers that message until it
receives a separate typed `TurnToken`. Direct registration therefore runs only `HandoffStart` and
then completes with no active executor.

Until the durable adapter supports the handoff chat protocol, the complete handoff workflow is
registered once as the named durable agent `order-processing-workflow`. `WorkflowHostAgent`
supplies the message and turn token expected by the handoff workflow, and the Azure Functions
durable-agent entity provides reliable fire-and-forget invocation. The inbound trigger calls:

```text
POST /api/agents/order-processing-workflow/run?wait=false
```

The request body contains a `message` string whose value is the serialized order. The stable
workflow-agent name and ID prevent the anonymous GUID entity lookup failures seen with the former
nested workflow.

## Validation

Tests cover workflow composition, stable reconstructed executor identities, prompt routing
contracts, and terminal detection. Runtime validation must confirm:

- only the named `order-processing-workflow` durable agent is registered for order processing;
- no anonymous workflow-as-agent GUID entity is created;
- no `Agent '...' not found` error occurs;
- Customer Messaging runs last and sends one email.

Reference:
<https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/handoff?pivots=programming-language-csharp>
