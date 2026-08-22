# Conditional Order Workflow

## Goal

Process each order through one fixed Microsoft Agent Framework graph with conditional routing.
The graph contains four agents and always ends with one customer message.

## Routing

```text
Order Intake
  |-- valid ----------------------> Fulfillment Decision
  `-- invalid ------------------------------------------> Customer Messaging

Fulfillment Decision
  |-- fully fulfillable --------------------------------> Customer Messaging
  `-- shortfall ------------------> Substitution --------> Customer Messaging
```

- Order Intake validates the order and returns `OrderIntakeResult`.
- Fulfillment Decision checks inventory and returns `FulfillmentDecisionResult`.
- Substitution recommends an in-stock replacement when possible and always generates one 25%
  coupon for a shortfall, including when no substitute is available.
- Customer Messaging sends exactly one email and is the only terminal output.

The workflow does not use handoff orchestration, autonomous turn limits, nested workflows, or a
workflow wrapped with `AsAIAgent()`.

## Agent Turn Routing

The published Workflows 1.18 package uses a chat protocol for agent executors. An agent turn emits
the generated message list followed by a `TurnToken`. A switch attached directly to an agent can
also observe protocol messages that are not routing results.

The DurableTask 1.16 adapter additionally serializes routed activity input as an array of
`{"input": ..., "state": ...}` envelopes. Turn adapters normalize that durable wire shape and the
in-process `ChatMessage` shape before starting the next agent.

`OrderWorkflowFactory` therefore starts with a deterministic input adapter that converts the raw
order JSON into a user message and turn token. It binds each agent with incoming-message forwarding
disabled and uses deterministic turn-forwarder executors around conditional branches. Each
forwarder:

1. receives only a completed agent response;
2. forwards that response to the selected next agent;
3. sends the `TurnToken` that starts the next agent.

The adapters contain no AI or business decisions. The graph still has exactly four AI agents, and
the two switches make decisions only from validated structured output. The terminal executor
returns the final customer message as its typed result so the durable workflow result is populated.

## Durable Registration

The public workflow is registered directly as `order-processing-workflow`. The inbound order
trigger starts it by posting the raw serialized order to:

```text
POST /api/workflows/order-processing-workflow/run
```

Agent names and IDs are stable. Durable registrations also include private aliases matching each
graph executor key (`{Name}_{Id}`), which prevents durable entity lookup failures without exposing
additional HTTP or MCP endpoints.

## Failure Behavior

Malformed or missing structured output fails the workflow with an explicit error. It is never
treated as a negative routing decision. Empty protocol message batches and turn tokens are ignored
by the switches.

## Validation Scenarios

1. Invalid order skips inventory and substitution, then sends one corrective message.
2. Fully fulfillable order skips substitution, then sends one confirmation.
3. Shortfall with an available substitute recommends it, issues one 25% coupon, and sends one
   message.
4. Shortfall without a substitute still issues one 25% coupon and sends one apologetic message.
5. Malformed routing output fails visibly.

Reference:
<https://learn.microsoft.com/en-us/azure/durable-task/sdks/durable-agents-microsoft-agent-framework?tabs=csharp&pivots=azure-functions#graph-based-workflows-with-microsoft-agent-framework>
