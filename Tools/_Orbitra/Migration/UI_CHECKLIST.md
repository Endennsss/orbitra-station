# Orbitra: entry UI verification

## Scope

The entry screens use the gray Orbitra palette. Device interfaces and the in-round HUD are not part of this redesign. Window styling is explicitly attached, not applied to every new window.

## Interaction inventory

| Screen | Actions to preserve |
| --- | --- |
| Main menu | Username, server address, direct connect, dev lobby, options, quit |
| Lobby | Ready/not ready, join, observe, profile selection, personalization, chat, server information, AHelp, vote, settings, rules, guidebook, updates, disconnect |
| Personalization | Profile menu, create, select, confirm delete, additional actions, section navigation, preview rotation/clothes, randomization locks, save, discard, guarded exit |
| Additional actions | Statistics, remarks, rules, profile import/export, image export, open folder |
| Sections | Appearance, markings/layers/colors/order, job search/priorities/loadouts, antagonists, traits, server-enabled description |
| Settings | General, graphics, bindings, audio, accessibility, administrator; reset/default/apply |
| Child windows | Loadouts, rules, guidebook, changelog, statistics, remarks, AHelp, voting, unsaved-changes confirmation |

## Automated checks

- `OrbitraEditorUiTest`: three species, five resolutions, three UI scales, all editor tabs; centered surface, field bounds, usable footer; profile save/discard/cancel/delete and dynamic menus.
- Entry-window test: external tab changes update the selector, manual position survives content changes and reopening, unrelated windows are not opted into Orbitra styling.
- `OrbitraLobbyUiTest`: adaptive lobby geometry and visibility.
- `OrbitraSettingsMigrationTest`: legacy import, explicit new-value precedence, persistence, repeated import.
- `test_convert_map.py`: exact component/entity/tag conversion, preserved attribution/comments, rejected unknown IDs and missing resources, idempotence.

## Manual acceptance

Use the real connected client, not only the main menu. Check keyboard focus, Escape order, outside-click menu dismissal, server restrictions, import/export dialogs, moving/resizing windows, and content with long translated labels. Automated geometry tests do not establish that every server-dependent action has been exercised manually.

Target matrix: 1280×720, 1920×1080, 2560×1080, 3440×1440 and 5120×1440 at 100%, 125% and 150%. UI dimensions are logical units after scaling.

Keep screenshots outside tracked resources; do not ship temporary audit commands. Stop only the client/server processes launched for this verification.
