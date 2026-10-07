# Orbitra Voice Chat — design specification

## Goal

Provide a first testable in-game voice channel for Orbitra: a player holds push-to-talk, the client captures microphone audio, encodes it with Opus, the server validates and routes frames to nearby living players, and recipients decode and play the stream.

## Scope

- 48 kHz, mono, signed 16-bit PCM input.
- 20 ms Opus VOIP frames (960 samples), sent as unreliable sequenced packets.
- Push-to-talk only; the default key is `V` and remains rebindable.
- Proximity routing with a seven-tile radius. The server decides recipients.
- No raw PCM crosses the network.
- Readiness for a later radio route by keeping transport, codec, and recipient selection separate.

## Protocol and validation

The client sends `MsgOrbitraVoiceFrame` containing only sequence and encoded bytes. The server obtains the speaker from the incoming channel, rejects missing/dead attachments, rejects empty or oversized frames, rate-limits frames to the configured cadence, and creates a server-to-client frame with the authoritative speaker entity and position. The server sends that frame only to players in the same map within the proximity radius, excluding the speaker.

The client never trusts a speaker identity or position from its own outgoing message. The decoder drops malformed frames and recreates a decoder per speaker when a stream starts. Capture and playback fail closed when OpenAL capture or Opus cannot be initialized.

## Audio lifecycle

Pressing the binding starts capture and an Opus encoder. The client drains complete 20 ms samples during client updates, sends frames, and stops capture on key release, disconnect, or shutdown. Received frames are decoded and queued for short-lived audio sources; sources are positional and cleaned after playback. This initial implementation favors an explicit, bounded queue over unbounded buffering.

## Operational limits

- Encoded frame payload: at most 1,500 bytes.
- Accepted cadence: no more than 60 frames per second per channel.
- Proximity radius: seven map units.
- A client with no microphone or unsupported capture device can still join and play other speakers.

## Out of scope

Radio encryption/channel selection, voice activation, recording, admin voice muting, persistence, and UI indicators beyond the keybind state are deferred until the first end-to-end test is stable.
