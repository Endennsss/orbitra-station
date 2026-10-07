# Orbitra Voice Chat Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Add a testable built-in Opus push-to-talk voice channel with server-authoritative proximity routing.

**Architecture:** Shared code owns the bounded wire protocol and policy constants. The client owns OpenAL capture, Concentus encoding/decoding, input state, and short-lived playback sources. The server owns attachment/alive validation, rate limits, authoritative speaker coordinates, and recipient selection. The feature is isolated under `_Orbitra` and sends no raw PCM.

**Tech Stack:** C#, RobustToolbox `NetMessage`, OpenAL capture via OpenTK, Concentus Opus, NUnit tests, YAML keybinds.

**Spec:** `docs/superpowers/specs/2026-10-07-voice-chat-design.md`

## Global Constraints

- Work only in `features/voice-chat-test` worktree.
- Preserve upstream boundaries; fork-owned source belongs under `_Orbitra` and uses Orbitra markers for edits outside it.
- Do not add unbounded allocations or queues on the per-frame path.
- Do not trust client-supplied speaker identity, position, or recipient lists.
- Keep all new user-facing text/localization in both Russian and English where strings are introduced.
- Never claim runtime verification without a fresh build/test result; stop runtime processes before finishing.

## Review Focus

- Wire compatibility and malformed packet handling.
- Server cannot be used to impersonate another speaker or broadcast outside proximity.
- Capture and playback resources are released on key-up, disconnect, and shutdown.
- The default key does not break existing input bindings and is rebindable.
- Tests cover validation, cadence, routing geometry, and codec framing without requiring a physical microphone.

---

### Task 1: Shared protocol and policy (TDD)

- [ ] Add a shared `OrbitraVoiceChatPolicy` with sample rate, frame size, payload and range limits.
- [ ] Add `MsgOrbitraVoiceFrame` with client-to-server and server-to-client serialization, authoritative speaker/position fields, and unreliable delivery.
- [ ] Add focused tests for payload bounds, cadence, sequence wrap, and protocol round-trip.
- [ ] Run the shared test filter and commit `feat: add voice chat wire protocol`.

### Task 2: Client input and capture/encoding (TDD)

- [ ] Add `PushToTalk` key function and `V` default binding/localized label.
- [ ] Add Concentus package reference and an Orbitra client system that starts/stops OpenAL capture from the binding, drains complete frames, encodes Opus, and sends bounded messages.
- [ ] Add pure encoder framing tests that do not require an audio device.
- [ ] Run the client build and focused tests; commit `feat: capture and encode voice frames`.

### Task 3: Server validation and proximity routing (TDD)

- [ ] Register the frame message on the server and validate channel attachment, alive state, payload, cadence, and sequence.
- [ ] Build a proximity filter from the authoritative speaker coordinates, exclude the speaker, and send the authoritative frame to nearby sessions only.
- [ ] Add tests for dead/unattached rejection, recipient range, same-map requirement, and rate limit.
- [ ] Run the server build and focused tests; commit `feat: route voice frames by proximity`.

### Task 4: Client decode and playback

- [ ] Register the server-to-client frame callback.
- [ ] Add per-speaker Opus decoders and bounded decoded-frame queues.
- [ ] Decode and play positional short-lived audio sources through the existing public audio API, cleaning sources and decoders on disconnect/round cleanup/shutdown.
- [ ] Add malformed-frame and queue-overflow tests.
- [ ] Run client/shared/server builds and tests; commit `feat: decode and play proximity voice`.

### Task 5: Verification and test-branch handoff

- [ ] Run YAML validation for edited resources and the narrow affected project builds.
- [ ] Run the focused NUnit test filters and record results in the progress ledger.
- [ ] Start a local client/server smoke test only if the build passes, capture logs/screenshots when the runtime is available, then stop every process.
- [ ] Review `git diff`, confirm no changes leaked into master, and report branch/commit/test status.
