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

- [x] Add a shared `OrbitraVoiceChatPolicy` with sample rate, frame size, payload and range limits.
- [x] Add `MsgOrbitraVoiceFrame` with client-to-server and server-to-client serialization, authoritative speaker/position fields, and unreliable delivery.
- [x] Add focused tests for payload bounds, cadence, sequence wrap, proximity geometry, and codec framing.
- [x] Run the focused test filter and record the result in the progress ledger.

### Task 2: Client input and capture/encoding (TDD)

- [ ] Add `PushToTalk` key function and `V` default binding/localized label.
- [x] Add Concentus package reference and an Orbitra client system that starts/stops OpenAL capture from the binding, drains complete frames, encodes Opus, and sends bounded messages.
- [x] Add pure encoder framing tests that do not require an audio device.
- [x] Run the client build and focused tests.

### Task 3: Server validation and proximity routing (TDD)

- [x] Register the frame message on the server and validate channel attachment, alive state, payload, cadence, and sequence.
- [x] Build a proximity filter from the authoritative speaker coordinates, exclude the speaker, and send the authoritative frame to nearby sessions only.
- [x] Add policy tests for recipient range, same-map requirement, and rate limit; the entity/session rejection path is kept server-side.
- [x] Run the server build and focused tests.

### Task 4: Client decode and playback

- [x] Register the server-to-client frame callback.
- [x] Add per-speaker Opus decoders and bounded decoded-frame sources.
- [x] Decode and play positional short-lived audio sources through the existing public audio API, cleaning sources and decoders on round cleanup/shutdown.
- [x] Validate malformed-frame and source-count bounds in the receive path.
- [x] Run client/shared/server builds and focused tests.

### Task 5: Verification and test-branch handoff

- [x] Run YAML validation for edited resources and the narrow affected project builds.
- [x] Run the focused NUnit test filter and record the result in the progress ledger.
- [ ] Start a local client/server smoke test only if the build passes; this remains optional because microphone hardware and a display session are not available in this verification run.
- [ ] Review `git diff`, confirm no changes leaked into master, and report branch/commit/test status.
