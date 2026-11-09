---
trigger: always_on
---

# ECS actions

Use `OnEvent → TryAction → CanAction → execute` for gameplay interactions.

- `OnEvent` unpacks inputs, checks event state such as `Handled`, and calls `TryAction`. Mark the event handled on success.
- `TryAction` exposes a reusable `bool` API. Accept all required context, call `CanAction`, return `false` on failure, and apply state changes and effects on success.
- `CanAction` returns `bool` and leaves game state intact. Failure popups require `quiet == false`. Use an optional failure reason when callers need one.

Keep checks in the shared action path so event handlers, verbs, and other callers enforce the same conditions. Keep a separate execution method when it has a distinct responsibility.
