# Option B Implementation Plan: Sub-Workflow via Native Durable Dispatch

**Date:** 2026-08-21  
**Status:** Planning  
**Goal:** Fix the `Agent 'order_resolution_workflow' not found` runtime error by replacing the
`AsAIAgent()` sub-workflow nesting approach with the framework's native sub-workflow dispatch path.

---

## Root Cause Analysis

The current approach chains two different MAF entity types incorrectly:

| Attempt | What happens | Error |
|---|---|---|
| `AsAIAgent()` no args | GUID-keyed agent entity, never registered | `Agent '176672…' not found` |
| `AsAIAgent(id=…, name=…)` | Doubled key `{name}_{id}` | `Agent 'order_resolution_workflow_order_resolution_workflow' not found` |
| `AsAIAgent(id=null, name=…)` + `AddWorkflow` | Workflow entity registered but agent registry has no entry under that name | `Agent 'order_resolution_workflow' not found` |

The fundamental mismatch: `AsAIAgent()` creates an **agent entity** (`AgentEntity`), but `AddWorkflow`
registers a **workflow orchestration** (`WorkflowOrchestrator`). When the outer workflow calls the
resolution node, it dispatches via `AgentEntity.GetAgent()` — the workflow is not in that registry.

## The Native Sub-Workflow Pattern

MAF's `DurableExecutorDispatcher` has three dispatch paths:
- `ExecuteAgentAsync` — for `AIAgent` nodes registered via `AddAIAgent`
- `ExecuteActivityAsync` — for tool/function calls registered as activities
- `ExecuteSubWorkflowAsync` — for **nested `Workflow` objects** with `ExecutionMode.Subworkflow`

`WorkflowExecutorInfo.IsSubworkflowExecutor` and `.SubWorkflow` are set when the outer workflow's
graph contains a node whose `ExecutorBinding` has `ExecutionMode.Subworkflow`. The dispatcher then
calls `ExecuteSubWorkflowAsync`, which invokes the sub-workflow via Durable Task sub-orchestration
— **not** via an agent entity lookup.

The `WorkflowBuilder` accepts `ExecutorBinding` on its `AddEdge` methods. A `Workflow` object can
be used directly as an `ExecutorBinding` (or via implicit conversion) with `ExecutionMode.Subworkflow`.
The `DurableWorkflowOptions.RegisterWorkflowExecutors` method registers all executor bindings from
a workflow — including sub-workflow bindings — with the durable layer.

## Architecture After Fix

```
OrderIntake → FulfillmentDecision
  ├─(CanFullyFulfill=true)──────────────────────────────→ CustomerMessaging
  └─(CanFullyFulfill=false)→ [orderResolutionWorkflow]  → CustomerMessaging
                                    ↑
                        Sub-workflow (Durable sub-orchestration)
                        Dispatched via ExecuteSubWorkflowAsync
                        Not via AgentEntity.GetAgent()
```

`orderResolutionWorkflow` (the handoff workflow) is referenced in the outer graph as a sub-workflow
node — the dispatcher launches it as a Durable Task sub-orchestration. It does NOT need `AsAIAgent()`.

---

## Implementation Steps

### Step 1 — Remove `AsAIAgent()` and use the `Workflow` as a sub-workflow node directly

**File:** `source/DurableAgent.Functions/Extensions/AgentExtensions.cs`

Replace:
```csharp
AIAgent orderResolutionWorkflowAgent = orderResolutionWorkflow.AsAIAgent(
    id: null,
    name: "order-resolution-workflow",
    description: "...");
```

With — use the workflow directly as an `ExecutorBinding` in `AddEdge`:
```csharp
// No AsAIAgent() call — pass orderResolutionWorkflow directly to AddEdge.
// The WorkflowBuilder treats a Workflow as an ExecutorBinding with ExecutionMode.Subworkflow,
// and the DurableExecutorDispatcher routes it via ExecuteSubWorkflowAsync.
```

Update `WorkflowBuilder` edges to pass `orderResolutionWorkflow` (the `Workflow` object) instead
of `orderResolutionWorkflowAgent` (the `AIAgent` wrapper):
```csharp
Workflow orderProcessingWorkflow = new WorkflowBuilder(orderIntakeAgent)
    .WithName("order-processing-workflow")
    .WithDescription("...")
    .AddEdge(orderIntakeAgent, fulfillmentDecisionAgent)
    .AddEdge<object>(fulfillmentDecisionAgent, customerMessagingAgent, condition: IsFullyFulfilled)
    .AddEdge<object>(fulfillmentDecisionAgent, orderResolutionWorkflow, condition: RequiresResolution)
    .AddEdge(orderResolutionWorkflow, customerMessagingAgent)
    .WithOutputFrom(customerMessagingAgent)
    .Build();
```

**Verification:** `dotnet build` — no compiler errors means `Workflow` is accepted as `ExecutorBinding`.

**Fallback if `Workflow` is not implicitly convertible to `ExecutorBinding`:**  
Check if `WorkflowBuilder` has an `AddSubWorkflow` overload or if `Workflow` exposes an
`AsSubWorkflowExecutor()` method. If neither, we cast via `(ExecutorBinding)orderResolutionWorkflow`
and check for a compilation error. If it doesn't compile, we need the explicit binding approach in Step 1b.

### Step 1b (fallback) — Explicit sub-workflow binding via `ExecutionMode.Subworkflow`

If Step 1 doesn't compile, construct the binding explicitly:
```csharp
// Check if ExecutorBinding has a constructor or factory that accepts a Workflow
// The framework's WorkflowAnalyzer.CreateExecutorInfo sets IsSubworkflowExecutor based on
// the binding's ExecutionMode. We need ExecutionMode.Subworkflow to be set.
```

This may require inspecting the MAF source further or using reflection to confirm the API.

### Step 2 — Update `ConfigureDurableOptions` registration

Remove `options.Workflows.AddWorkflow(orderResolutionWorkflow, ...)` — with the native sub-workflow
path, the sub-workflow is registered automatically when `RegisterWorkflowExecutors` processes the
outer workflow's graph (which now contains a sub-workflow node).

If the sub-workflow still needs to be explicitly registered (for the durable layer to know its
orchestration name), keep the `AddWorkflow` call but without an HTTP endpoint:
```csharp
options.Workflows.AddWorkflow(orderResolutionWorkflow, exposeStatusEndpoint: false, exposeMcpToolTrigger: false);
options.Workflows.AddWorkflow(orderProcessingWorkflow, exposeStatusEndpoint: true, exposeMcpToolTrigger: false);
```

**Verification:** Run `dotnet build`; confirm no errors.

### Step 3 — Update/add unit tests

**File:** `source/DurableAgent.Functions.Tests/Extensions/AgentExtensionsTests.cs` (if exists)
or add new test class.

Tests to add/update:
- `WhenFulfillmentCannotFulfill_ThenOrderResolutionWorkflowIsInvoked` — verify the conditional edge
  routes to the resolution sub-workflow node (not an AIAgent)
- `WhenFulfillmentCanFulfill_ThenCustomerMessagingIsInvoked` — happy path unchanged
- `WhenOrderResolutionWorkflowRegistered_ThenItAppearsAsSubWorkflowNode` — structural test: inspect
  the built `orderProcessingWorkflow` graph to assert the resolution node has `ExecutionMode.Subworkflow`

**Verification:** `dotnet test` — 216+ tests pass.

### Step 4 — Local end-to-end test

Run the Aspire AppHost and submit an order with a deliberate stock shortfall to trigger the
exception path. Confirm in logs:
- `dafx-order-processing-workflow` executes and reaches superstep with FulfillmentDecision
- `dafx-order-processing-workflow` routes to `dafx-order-resolution-workflow` (not an agent entity)
- `dafx-order_resolution_workflow` executes as a **sub-orchestration** (not an entity operation)
- Resolution completes and CustomerMessaging sends the final message

### Step 5 — Commit and push

```
fix: use native sub-workflow dispatch for order resolution

Replace AsAIAgent() sub-workflow wrapping with the framework's native
ExecutionMode.Subworkflow dispatch path. The WorkflowBuilder accepts a
Workflow object directly as an ExecutorBinding; DurableExecutorDispatcher
routes it via ExecuteSubWorkflowAsync (Durable sub-orchestration) rather
than AgentEntity.GetAgent() (agent entity lookup).

This eliminates the registration mismatch that caused
"Agent 'order_resolution_workflow' not found".
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|---|---|---|
| `Workflow` not implicitly convertible to `ExecutorBinding` | Medium | Check for `AsSubWorkflow()` extension or `ExecutorBinding.FromWorkflow()` factory |
| Sub-workflow must still be explicitly registered with `AddWorkflow` | High | Keep `AddWorkflow` call for outer and sub-workflow; the dispatcher still needs the orchestration registered |
| `WithOutputFrom(customerMessagingAgent)` incompatible with sub-workflow output shape | Low | Sub-workflow output flows through the graph's edge to CustomerMessaging as a message |
| `RegisterWorkflowExecutors` auto-registers sub-workflows from the outer graph | Unknown | If not, explicit `AddWorkflow` for sub-workflow is required |

## Key API Facts (verified from NuGet XML docs)

- `ExecutionMode.Subworkflow` — field on `Microsoft.Agents.AI.Workflows.ExecutionMode`
- `WorkflowExecutorInfo.SubWorkflow` — `Workflow` reference, set when `IsSubworkflowExecutor=true`
- `DurableExecutorDispatcher.ExecuteSubWorkflowAsync(TaskOrchestrationContext, WorkflowExecutorInfo, string)` — the dispatch method
- `DurableWorkflowOptions.RegisterWorkflowExecutors(Workflow)` — registers executors from a workflow graph
- `DurableWorkflowOptions.TryRegisterAgent(ExecutorBinding, DurableAgentsOptions)` — registers agent bindings
- `WorkflowBuilder.AddEdge(ExecutorBinding, ExecutorBinding)` — both `AIAgent` and `Workflow` are `ExecutorBinding` (or implicitly convertible)
- `AsAIAgent(Workflow, string id, string name, string description, ...)` — only one overload; wraps as agent, WRONG for this use case
- `AddWorkflow(DurableWorkflowOptions, Workflow, bool exposeStatusEndpoint, bool exposeMcpToolTrigger)` — registers workflow orchestration, NOT in agent registry

## Files to Change

| File | Change |
|---|---|
| `source/DurableAgent.Functions/Extensions/AgentExtensions.cs` | Remove `AsAIAgent()` call; pass `Workflow` directly to `AddEdge` |
| `source/DurableAgent.Functions.Tests/Extensions/AgentExtensionsTests.cs` | Add/update structural graph tests |

## Files NOT to Change

- All 4 resolution agent configs (OrderResolution, Substitution, Promotion, Escalation) — unchanged
- `OrderResolutionResult.cs` — unchanged
- Existing 216 tests — should all still pass
