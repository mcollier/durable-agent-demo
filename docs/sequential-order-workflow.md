# Sequential Order Workflow

## Goal

Process every order through one fixed Microsoft Agent Framework graph:

```text
Order Intake -> Fulfillment -> Customer Messaging
```

- Order Intake validates the order and returns `OrderIntakeResult`.
- Fulfillment returns `OrderFulfillmentResult`. For a valid order it checks inventory, selects an
  in-stock substitute when needed, and chooses a policy-approved coupon tier. For an invalid order
  it preserves the validation failure without calling tools.
- Customer Messaging sends exactly one email and is the only terminal output.

The workflow does not use conditional routing, handoff orchestration, autonomous turn limits,
nested workflows, or a workflow wrapped with `AsAIAgent()`.

## Non-Determinism Boundary

Fulfillment is the demo's meaningful non-deterministic decision point:

- `CheckInventory` and `GetAvailableInventory` ground the decision in current inventory facts.
- The model chooses a suitable substitute only from confirmed in-stock candidates.
- The model chooses a coupon tier of 10%, 15%, 20%, or 25% according to shortfall severity.
- `GenerateCouponCode` enforces those tiers in deterministic code and generates the coupon.

Order validation is constrained by explicit business rules. Customer Messaging varies the wording
but does not decide the business outcome.

Durable execution checkpoints completed agent turns. If the orchestration replays, it reuses the
recorded Fulfillment output instead of asking the model to choose a substitute and coupon again.
The workflow path is deterministic while the bounded Fulfillment judgment is durably recorded.

## Durable Agent Turns

The published Workflows 1.18 package uses a chat protocol for agent executors. An agent turn emits
the generated message list followed by a `TurnToken`.

The DurableTask 1.16 adapter additionally serializes routed activity input as an array of
`{"input": ..., "state": ...}` envelopes. Turn adapters normalize that durable wire shape and the
in-process `ChatMessage` shape before starting the next agent.

`OrderWorkflowFactory` therefore starts with a deterministic input adapter that converts the raw
order JSON into a user message and turn token. It binds each agent with incoming-message forwarding
disabled and uses deterministic turn-forwarder executors between agents. Each
forwarder:

1. receives only a completed agent response;
2. forwards that response to the fixed next agent;
3. sends the `TurnToken` that starts the next agent.

The adapters contain no AI or business decisions. The terminal executor returns the final customer
message as its typed result so the durable workflow result is populated.

The invalid-order no-op is enforced by the Fulfillment agent's instructions. The current agent
hosting API exposes one static tool set for every invocation, so it does not provide a hard
input-dependent tool gate. Add a deterministic pre-agent guard if invalid-order tool suppression
becomes a security or billing boundary rather than a demo behavior.

## Durable Registration

The public workflow is registered directly as `order-processing-workflow`. The inbound order
trigger starts it by posting the raw serialized order to:

```text
POST /api/workflows/order-processing-workflow/run
```

Agent names and IDs are stable. Durable registrations also include private aliases matching each
graph executor key (`{Name}_{Id}`), which prevents durable entity lookup failures without exposing
additional HTTP or MCP endpoints.

This topology replaces executor identities used by the earlier four-agent graph. Drain or terminate
in-flight `order-processing-workflow` instances before deploying the change; existing checkpoints
from the old topology are not compatible with the new executor set.

## Failure Behavior

Malformed or missing structured output fails the workflow explicitly. The agents must not fabricate
inventory, substitution, or coupon data.

## Validation Scenarios

1. Invalid order traverses Fulfillment without tool calls, then sends one corrective message.
2. Fully fulfillable order has no substitution or coupon, then sends one confirmation.
3. A shortfall with an available substitute recommends it, issues one approved coupon, and sends
   one message.
4. A shortfall without a substitute issues one approved coupon and sends one apologetic message.

Reference:
<https://learn.microsoft.com/en-us/azure/durable-task/sdks/durable-agents-microsoft-agent-framework?tabs=csharp&pivots=azure-functions#graph-based-workflows-with-microsoft-agent-framework>
