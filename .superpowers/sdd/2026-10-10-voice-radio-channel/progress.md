# SDD ledger — plan: docs/superpowers/plans/2026-10-10-voice-radio-channel.md
Pre-flight: shared interfaces — Task 1 protocol fields are consumed by Tasks 3–5; Task 2 chat selection is consumed by client transmission; Task 3 routing determines Task 5 radio HUD metadata.
Ruling: SDD workspace script could not run because Bash/WSL is unavailable on this host; created the documented workspace and ledger manually with the same plan identity.
Task 1: in_progress
Task 1: complete (no commit per repository instruction; tests: dotnet test Content.Tests/Content.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~OrbitraVoiceChatPolicy --nologo -v:minimal -> 12/12 passed)
Task 2: in_progress
Task 2: Ruling: pure channel fallback resolver uses string IDs instead of RadioChannelPrototype objects because prototype IDs have private setters and are loaded from YAML; UI bridge resolves the returned ID through PrototypeManager; cost if wrong: only a test/API signature adjustment.
Task 2: complete (channel bridge, radio keybind, client mode selection; focused test 4/4 passed)
Task 3: complete (server validates mode, authorizes sender headset/intrinsic channels, routes to active radios through existing receive-attempt checks; focused routing test 2/2 passed)
Task 4: complete (allocation-free bounded deterministic radio PCM filter applied only to radio frames; focused test 1/1 passed)
Task 5: complete (radio metadata in speaker HUD, bounded active viewport card layout, Lucide mic remains white and centered above sprite head; focused UI/filter/selection tests 7/7 passed)
Ruling: custom voice frames now carry mode/channel before payload; old pre-radio peers cannot be decoded by this custom message without a version/tag, so compatibility is enforced by the matching client/server build and invalid modes are rejected. Cost if wrong: mixed-version connection needs an explicit protocol marker before rollout.
Task 6: verification complete (YAML linter no errors; Debug and Tools client/server builds exit 0; focused voice suite 21/21 passed; server smoke and 12-second headless client connection clean, processes stopped)
Final review: reviewer found and fixes verified for RadioSendAttemptEvent, stale radio selection, conflicting keybind, shared PTT release, capture device lifecycle, and radio spatial attenuation. Radio playback is global while proximity remains spatial; key-down re-resolves current chat/default channel.
Final verification after review fixes: focused voice suite 21/21 passed; Tools client build exit 0; final server/client headless smoke ran 12 seconds with zero error matches and both processes stopped.
