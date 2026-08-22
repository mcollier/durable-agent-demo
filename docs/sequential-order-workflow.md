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

## Sequential Durable Workflow

The three agents are the workflow nodes:

```csharp
new WorkflowBuilder(orderIntakeAgent)
    .WithName("order-processing-workflow")
    .AddEdge(orderIntakeAgent, fulfillmentAgent)
    .AddEdge(fulfillmentAgent, customerMessagingAgent)
    .Build();
```

The workflow is registered with `options.Workflows.AddWorkflow(...)`. The Azure Functions hosting
extension maps the graph to a durable orchestration, maps each agent executor to a durable activity,
and generates the HTTP endpoint. No custom start, forwarding, routing, or output executors are part
of the graph.

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

Agent names and IDs are stable so the generated durable activity names remain stable. Each workflow
agent is also registered under its graph executor ID (`{Name}_{Id}`), because the durable activity
uses that ID when resolving the backing durable agent entity. These private aliases do not expose
standalone HTTP or MCP endpoints.

This topology replaces executor identities used by earlier graph versions. Drain or terminate
in-flight `order-processing-workflow` instances before deploying the change; their checkpoints are
not compatible with the new executor set.

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
<https://devblogs.microsoft.com/dotnet/durable-workflows-in-microsoft-agent-framework/>
