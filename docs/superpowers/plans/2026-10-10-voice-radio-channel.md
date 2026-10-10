# Voice Radio Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a separate radio push-to-talk mode that uses the ordinary chat radio channel, validates access on the server, and plays voice with a radio effect.

**Architecture:** Keep the existing proximity voice route and packet transport, extending each frame with a transmission mode and optional radio channel ID. A client-side bridge exposes the channel currently resolved by the chat selector; the server reuses the radio system's channel, telecom, jammer/EMP, and receiver rules through an Orbitra partial helper, then sends one immutable compressed payload to eligible player sessions. Radio playback uses the existing buffered Opus stream with a bounded PCM radio filter, while the existing HUD overlay displays the channel and remains constrained to the active gameplay viewport.

**Tech Stack:** C# ECS systems, Robust network messages over Lidgren `UnreliableSequenced`, Opus capture/decoding, Robust UI controller/input bindings, bounded OpenAL buffered sources, NUnit tests, YAML/FTL localization.

**Spec:** `docs/superpowers/specs/2026-10-10-voice-radio-channel-design.md`

## Global Constraints

- Product and code prefix: `Orbitra`; fork-owned files live under `_Orbitra` where possible.
- Canonical markers are `Orbitra-Edit`, `Orbitra edit start/end`, and `Orbitra added start/end`; explanatory comments and marker reasons are Russian.
- Existing `PushToTalk` proximity behavior, microphone selection/test, input/output volume settings, PDA/ID behavior, and unrelated passport changes remain intact.
- Radio voice never creates a duplicate text chat message and never trusts a client-supplied permission or display name.
- Use a bounded queue and allocation-free hot-path processing for every received/sent voice frame.
- Do not commit or push changes from this implementation session.

## Review Focus

- A client sends an unknown, unavailable, or malformed radio channel; the server must drop it without an exception (Task 3 tests).
- A player loses or disables a headset while a transmission is active; the next frame must stop transmitting/receiving (Task 3 tests).
- A receiver has several radios or `ReceiveAllChannels`/`GlobalReceive`; it must receive one frame per session and preserve existing map/telecom rules (Task 3 tests).
- A long radio transmission or low FPS causes decoder/backlog pressure; playback must remain bounded and recover after underflow (Task 4 tests).
- The separated HUD is narrower than the full window and names/channel labels are long; the card must stay inside the playable viewport (Task 5 tests/smoke).

---

### Task 1: Shared voice protocol and radio policy

**Files:**
- Create: `Content.Shared/_Orbitra/VoiceChat/OrbitraVoiceTransmissionMode.cs`
- Modify: `Content.Shared/_Orbitra/VoiceChat/MsgOrbitraVoiceFrame.cs`
- Modify: `Content.Shared/_Orbitra/VoiceChat/OrbitraVoiceChatPolicy.cs`
- Test: `Content.Tests/Shared/_Orbitra/VoiceChat/OrbitraVoiceChatPolicyTest.cs`

**Interfaces:**
- Produces `OrbitraVoiceTransmissionMode { Proximity, Radio }`.
- Extends `MsgOrbitraVoiceFrame` with `TransmissionMode` and nullable/empty-safe `RadioChannelId`; `Proximity` remains the default when reading older frames.
- Produces policy helpers for valid mode/channel combinations and bounded radio channel IDs without resolving prototypes on the client.

- [ ] **Step 1: Add failing policy tests** for default proximity mode, radio frames requiring a non-empty channel ID, rejection of oversized/invalid channel IDs, and round-trip message serialization preserving mode/channel/payload.
- [ ] **Step 2: Run the focused test** with `dotnet test Content.Tests/Content.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~OrbitraVoiceChatPolicy` and confirm the new assertions fail.
- [ ] **Step 3: Implement the enum, message fields, guarded `ReadFromBuffer`/`WriteToBuffer`, estimate size, and policy helpers** while preserving sequence channel 1 and `UnreliableSequenced`.
- [ ] **Step 4: Re-run the focused test** and require all existing and new voice policy tests to pass.

### Task 2: Chat channel bridge and configurable radio PTT

**Files:**
- Modify: `Content.Shared/Input/ContentKeyFunctions.cs`
- Modify: `Resources/keybinds.yml`
- Modify: `Content.Client/UserInterface/Systems/Chat/ChatUIController.cs`
- Create: `Content.Client/_Orbitra/UserInterface/OrbitraRadioChannelSelection.cs`
- Create: `Content.Client/_Orbitra/UserInterface/ChatUIController.Orbitra.cs`
- Test: `Content.Tests/Client/_Orbitra/VoiceChat/OrbitraVoiceRadioSelectionTest.cs`
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceChatSystem.cs`
- Modify: `Resources/Locale/ru-RU/_Orbitra/common_ui.ftl`
- Modify: `Resources/Locale/en-US/_Orbitra/common_ui.ftl`

**Interfaces:**
- Produces `ContentKeyFunctions.RadioPushToTalk`, with a default key that remains user-rebindable.
- Produces `OrbitraRadioChannelSelection.Resolve(RadioChannelPrototype? explicitChannel, RadioChannelPrototype? defaultChannel, bool radioSelected)` as the pure fallback rule used by the UI bridge.
- Produces `ChatUIController.SelectedRadioChannel` and `SelectedRadioChannelChanged`, resolved from the same `:x`, `.x`, `;`, and default-channel parsing used by text chat.
- The chat controller calls a partial Orbitra hook from `UpdateSelectedChannel`; selecting radio without a prefix resolves the existing default radio channel, while a typed department prefix updates the shared state.
- `OrbitraVoiceChatSystem` consumes the property/event and never creates a second channel selector.

- [ ] **Step 1: Add a client selection test seam** covering explicit department prefix, common prefix, default channel, and clearing an unavailable selection; keep the resolver pure so it can run without a live window.
- [ ] **Step 2: Run the focused client/shared tests** and confirm the seam fails before the bridge exists.
- [ ] **Step 3: Add the key function and keybind**, register `RadioPushToTalk` beside `PushToTalk`, and ensure shutdown unregisters both commands.
- [ ] **Step 4: Implement the partial chat-controller bridge** with a cached `RadioChannelPrototype?`, event notification only on change, and Orbitra marker/comment in Russian at the base-file hook.
- [ ] **Step 5: Update the client voice input state** so proximity and radio PTT cannot transmit simultaneously; a radio press with no valid channel is ignored and shows the localized unavailable-channel feedback without starting capture twice.
- [ ] **Step 6: Run the focused client build** after the bridge and keybind changes; require zero errors.

### Task 3: Server radio authorization and routing

**Files:**
- Create: `Content.Server/_Orbitra/VoiceChat/RadioSystem.Orbitra.cs`
- Modify: `Content.Server/_Orbitra/VoiceChat/OrbitraVoiceChatSystem.cs`
- Modify: `Content.Shared/_Orbitra/VoiceChat/OrbitraVoiceChatPolicy.cs`
- Test: `Content.Tests/Server/_Orbitra/VoiceChat/OrbitraVoiceRadioRoutingTest.cs`

**Interfaces:**
- Produces an internal `RadioSystem` partial helper that resolves a valid transmitting source from a living player (`WearingHeadsetComponent`/encryption keys or intrinsic transmitter) and collects eligible player `INetChannel`s for a `RadioChannelPrototype`.
- The helper applies existing `RadioSendAttemptEvent`, `RadioReceiveAttemptEvent`, telecom-server, map, `ReceiveAllChannels`, `GlobalReceive`, EMP/jammer, and intercom channel constraints, maps headset/device receivers to their owning actor, deduplicates sessions, and excludes the speaker.
- `OrbitraVoiceChatSystem.OnVoiceFrame` selects proximity or radio routing from the shared mode; the outgoing frame contains authoritative speaker, position, mode, and channel ID.

- [ ] **Step 1: Write routing tests** for common/department authorization, unknown channel, missing headset, receiver without a channel, `ReceiveAllChannels`, duplicate radios, and disconnected/excluded speaker sessions.
- [ ] **Step 2: Run the focused server test** and confirm the new routing assertions fail.
- [ ] **Step 3: Implement the `RadioSystem` partial helper** by extracting only the reusable eligibility logic needed for voice; keep text chat and replay logging untouched and add Russian Orbitra markers/comments.
- [ ] **Step 4: Extend server voice validation** to resolve the prototype from the channel ID, reject invalid mode/channel combinations, call the helper for radio frames, and keep current proximity filtering/rate limiting unchanged.
- [ ] **Step 5: Run the server routing tests** and require all cases to pass without creating `ChatMessage` events.
- [ ] **Step 6: Run `dotnet build Content.Server/Content.Server.csproj --configuration Tools --no-restore --nologo -v:q` and require zero errors.

### Task 4: Radio playback DSP and bounded long-talk behavior

**Files:**
- Create: `Content.Client/_Orbitra/VoiceChat/OrbitraRadioVoiceFilter.cs`
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoicePlaybackStream.cs`
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceChatSystem.cs`
- Test: `Content.Tests/Shared/_Orbitra/VoiceChat/OrbitraRadioVoiceFilterTest.cs`

**Interfaces:**
- Produces `OrbitraRadioVoiceFilter.Apply(Span<short> samples, ref uint state)` (or equivalent allocation-free stateful API) that applies bounded band-limiting/quantization and low-level deterministic radio noise without clipping outside `short`.
- `OrbitraVoicePlaybackStream` accepts a radio flag/filter state, keeps the existing bounded pending/queued buffers, and never blocks the render/update loop.
- Decoder state tracks whether a speaker is radio or proximity and resets playback/filter state when the mode/channel changes.

- [ ] **Step 1: Add filter tests** for deterministic output, bounded amplitude, no allocation-visible state growth, and silence remaining near silence.
- [ ] **Step 2: Run the focused filter test** and confirm failure.
- [ ] **Step 3: Implement the small PCM filter** with fixed constants and a deterministic integer noise state; use it only for radio frames so proximity audio is byte-for-byte unaffected.
- [ ] **Step 4: Integrate the filter and mode/channel reset** into the existing decoder/playback path; preserve underflow recovery and queue trimming.
- [ ] **Step 5: Run policy/filter tests** and the client build, requiring zero errors.

### Task 5: HUD channel state, radio card, and localization

**Files:**
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceSpeakerOverlay.cs`
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceSpeakerCardControl.cs`
- Modify: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceChatSystem.cs`
- Create: `Content.Client/_Orbitra/VoiceChat/OrbitraVoiceSpeakerLayout.cs`
- Modify: `Resources/Locale/ru-RU/_Orbitra/common_ui.ftl`
- Modify: `Resources/Locale/en-US/_Orbitra/common_ui.ftl`
- Test: `Content.Tests/Client/_Orbitra/VoiceChat/OrbitraVoiceSpeakerLayoutTest.cs`

**Interfaces:**
- `ShowLocal`/`TouchSpeaker` carry transmission mode and localized channel label; proximity speakers retain the current card appearance.
- Radio speakers render a clear channel badge and radio microphone state; names and labels are clipped within the existing card width.
- Card placement continues to use active `ViewportContainer` bounds and remains valid in both default and separated HUD layouts.

- [ ] **Step 1: Add deterministic layout tests** for full-width and separated viewport rectangles, narrow widths, long names, and multiple cards through `OrbitraVoiceSpeakerLayout.CalculateCardRect`.
- [ ] **Step 2: Implement mode/channel data in the overlay and card drawing** with no per-frame collection mutation during enumeration.
- [ ] **Step 3: Add Russian and English strings** for radio PTT, unavailable channel, transmitting state, and channel labels; preserve existing voice settings text.
- [ ] **Step 4: Run YAML/localization validation and a client build**; require no parser errors and zero build errors.

### Task 6: End-to-end verification

**Files:**
- Verify: all files from Tasks 1–5.
- Verify: `run-voice-chat-test.bat` and existing two-client test setup.

- [ ] **Step 1: Run shared policy/filter tests** with the narrow filters used in Tasks 1 and 4.
- [ ] **Step 2: Run server routing tests** and the Server Tools build.
- [ ] **Step 3: Run the Client Tools build** with `dotnet build Content.Client/Content.Client.csproj --configuration Tools --no-restore --nologo -v:q`.
- [ ] **Step 4: Run YAML/language validation** on changed prototypes/localization and `git diff --check`; fix every reported error.
- [ ] **Step 5: Run a short headless client smoke and stop all client/server processes** after the check.
- [ ] **Step 6: Use `run-voice-chat-test.bat` for a two-client manual pass:** select `Common`, verify both hear radio voice with the radio effect; select an available department, verify only that department hears it; remove/disable the headset, verify transmission/receiving stops; hold radio PTT for at least 30 seconds at low FPS and verify audio continues and the card stays inside the correct HUD area.
