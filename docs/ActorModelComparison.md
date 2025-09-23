# Tick/Poll vs Actor (Message-Driven) Architecture for BrainSim

This document objectively compares the current BrainSim modular tick (polling) execution model with an actor / virtual-actor approach, and provides a migration heuristic.

## 1. Conceptual Difference
- Tick/Poll (current): Central scheduler/loop (DispatcherTimer) iterates modules each frame. Modules read/write shared UKS and shared module state directly.
- Actor (message-driven / turn-based): Each component owns its state; interaction occurs via asynchronous messages placed in mailboxes. Emphasizes isolation and single-threaded ownership per actor.

## 2. Strengths / Weaknesses
### Tick/Poll (current)
Strengths:
- Simple, predictable ordering.
- Easy synchronous WPF UI integration (single Dispatcher thread).
- Deterministic (with fixed ordering + seeded randomness).
- Low overhead per module; direct memory access.

Weaknesses:
- One slow module blocks all others (head-of-line blocking).
- Hard to scale across processes/nodes; assumes locality.
- Shared mutable UKS invites race risks as background threads appear.
- No inherent back-pressure; fast producers can overwhelm shared data.
- Fault isolation is weak (exceptions can break the loop).

Best When: Medium module count, strong coupling, deterministic simulation passes, single-machine focus.

### Actor / Virtual Actor
Strengths:
- Horizontal scalability (partition actors/grains by key).
- Fault isolation & supervised restarts.
- Natural back-pressure (mailboxes regulate flow).
- Location transparency enables remote execution.
- Strong state ownership model (reduces accidental sharing).
- Elastic activation (virtual actors) for very large sparse graphs.

Weaknesses:
- Messaging overhead (allocation + dispatch + possible serialization).
- More complex debugging (async chains vs linear call stack).
- Global deterministic ordering lost unless explicitly engineered.
- Additional ceremony (interfaces, message contracts).
- Latency overhead penalizes ultra high-frequency micro-updates.
- Needs UI bridging layer (actors should not touch WPF objects directly).

Best When: Distribution, very large knowledge graphs, autonomous behaviors, fault tolerance, external service integration.

## 3. Performance Considerations (Qualitative)
- Per-call overhead: Tick (minimal) vs Actor (enqueue + dispatch).
- Parallelism: Tick = manual; Actor = conceptual concurrency via mailboxes.
- Cache locality: Tick often better (shared in-proc structures).
- Contention risk: Tick higher (shared mutable state); Actor lower (isolated state).
- Degradation under load: Tick can stall globally; Actor degrades more gracefully if work is partitioned.

## 4. Distribution & Scaling Path
- Future multi-machine cognition (perception, language, planning clusters) is easier with actors.
- Virtual actors map naturally to UKS Things (each node or cluster as an actor with lazy activation + persistence).
- Hybrid: keep real-time neural propagation local; offload slower semantic enrichment / external lookups to actors.

## 5. Determinism & Reproducibility
- Tick: Straightforward (fixed order + seeded RNG).
- Actor: Requires logical time / deterministic scheduler layer to reproduce globally consistent runs.

## 6. Fault Tolerance
- Tick: Requires pervasive try/catch; one failure risks the frame.
- Actor: Supervisor/restart policies localize failures.

## 7. Developer Velocity
- Tick: Fast to prototype (override Fire()).
- Actor: Slower initial investment; scales better with complexity.

## 8. Pragmatic Migration Path (Incremental)
1. Introduce internal message channels (e.g., Channel<T>) per module while retaining the tick driver; modules drain channels instead of directly mutating shared structures.
2. Wrap UKS mutations behind a single-writer facade ("UKSService") queuing operations—moves toward actor-like ownership.
3. Isolate slow / I/O modules (e.g., external info fetch) into background worker actors (Task + mailbox pattern) returning results to core.
4. Adopt a lightweight in-process actor runtime or Orleans for remote / large-scale scenarios.

## 9. Reasons to Stay Tick for Now
- Core bottlenecks are algorithmic (graph traversal) not concurrency.
- Need strict stepwise neural propagation semantics.
- Current scale fits comfortably on a single machine.
- Simplicity is aiding rapid feature iteration.

## 10. Signals to Plan Actor Adoption
- CPU underutilized due to sequential Fire() ordering.
- Concurrency bugs emerge from ad hoc background threads.
- Requirement to host millions of passive knowledge nodes.
- Need to integrate distributed cognition or cloud enrichment services.

## 11. Recommended Hybrid Approach
Retain deterministic tick loop for:
- Activation propagation / decay
- Time-sensitive neural dynamics

Use actors for:
- Knowledge enrichment & external fact retrieval
- Long-running analytics (pattern mining, attribute propagation)
- Device / pod I/O abstraction
- Persistence & replay (actor state checkpoints)

## 12. Decision Heuristic
- Emphasize actor migration if distribution + massive UKS scale is imminent.
- Emphasize tick optimization if single-machine fidelity & feature completeness dominate near-term roadmap.

## 13. Optional Next Steps (Choose One to Pursue)
- Provide minimal in-process actor skeleton (single-threaded dispatcher + mailboxes) integrating with ModuleBase.
- Sketch Orleans grain interfaces for `Thing` (e.g., IThingGrain: GetAttributes, AddRelationship, QuerySubgraph).
- Add profiling harness to quantify current loop hotspots before architectural shift.

Request the desired next step to proceed.
