# Orbitra docking artwork

## Maps and render

The connection scene uses two grids from Space Station 14. Map coordinates below use metres. Image coordinates use pixels from the top left. Both grids retain the game scale of 32 pixels per tile.

| Texture | Map and attribution | Registration |
| --- | --- | --- |
| `shuttle.png` | NT-Arrivals 1101 (micro), by Peperos. [Map at the upstream revision](https://github.com/space-wizards/space-station-14/blob/985fedaab065b2c35485e2aa5c244bddbbc031e5/Resources/Maps/Shuttles/arrivals_micro.yml) · [shuttle credits](../../../../shuttle_attributions.txt) | 296 × 224. The east-facing glass docking airlock is entity 71 at (3.5, −2.5). Its outer tile edge is at pixel (292, 112). The 288 × 224 grid image has four extra transparent columns on either side to retain the outboard clamps. |
| `port.png` | Saltern Station, by deltanedas, Emisse and PJB. [Map at the upstream revision](https://github.com/space-wizards/space-station-14/blob/985fedaab065b2c35485e2aa5c244bddbbc031e5/Resources/Maps/saltern.yml) · [station credits](../../../../map_attributions.txt) | 832 × 768. The west-facing glass emergency docking airlock is entity 9377 at (−44.5, 8.5). Its outer edge is at pixel (32, 384). The crop starts at (288, 592) in the 4416 × 2784 station grid render. |
| `doors.png` | AftrLite’s directional glass docking airlocks over the mapped floors. [Airlock sprite credits](../../../Structures/Doors/Airlocks/Glass/shuttle-base.rsi/meta.json) | 640 × 32. Each of the ten 64 × 32 columns contains both door tiles. Columns 0 through 8 show the nine opening frames in RSI order. Column 9 holds the open state. Centre each column on the shared tile edge. |

The captures use Orbitra’s `Content.MapRenderer` at [bed19561760db7d02866dffe8b8d2177eff000c3](https://github.com/Endennsss/orbitra-station/tree/bed19561760db7d02866dffe8b8d2177eff000c3) and RobustToolbox at [d56b0d0bcf94c76bef7fc18625bf226607dca092](https://github.com/space-wizards/RobustToolbox/tree/d56b0d0bcf94c76bef7fc18625bf226607dca092). Each map was rendered from its YAML file with markers hidden and transparent space. The two outboard clamp tips on the shuttle use the original directional `docking-clamp` frames. The door strip uses the background captured immediately before the door draw, the opposing clamp, and the authored `opening` and `open` frames. The mapped airlocks have unlit indicators in the captures.

The offline renderer omitted the noncommercial directional fan and clear drinking glass sprite layers, plus the greyscale floor overlay’s `checkerNWSE`, `quartertile_overlay` and `quartertile_overlay_180` states. The open doorway shows the shuttle’s orange floor and the station’s plating. [Fan metadata](../../../Structures/Piping/Atmospherics/directionalfan.rsi/meta.json) · [glass metadata](../../../Objects/Consumable/Drinks/glass_clear.rsi/meta.json) · [floor overlay metadata](../../../Decals/Overlays/greyscale.rsi/meta.json)

## Game artwork credits

Each link below opens the bundled artist credit, original source and license for a sprite set or floor tile used by the renderer within these two image bounds. The metadata records the upstream contributors and state-specific attributions. The artwork retains its individual terms.

### [CC-BY-SA-3.0](https://creativecommons.org/licenses/by-sa/3.0/)

| Texture directory | Artist and source records |
| --- | --- |
| Clothing/Hands/Gloves/Color | [yellow](../../../Clothing/Hands/Gloves/Color/yellow.rsi/meta.json) |
| Clothing/Head/Hats | [beret](../../../Clothing/Head/Hats/beret.rsi/meta.json), [brownfedora](../../../Clothing/Head/Hats/brownfedora.rsi/meta.json) |
| Clothing/Mask | [breath](../../../Clothing/Mask/breath.rsi/meta.json) |
| Clothing/Neck/Cloaks | [trans](../../../Clothing/Neck/Cloaks/trans.rsi/meta.json) |
| Clothing/OuterClothing/Coats | [detective](../../../Clothing/OuterClothing/Coats/detective.rsi/meta.json), [jensencoat](../../../Clothing/OuterClothing/Coats/jensencoat.rsi/meta.json) |
| Clothing/Uniforms/Jumpsuits/Civilian | [ancient](../../../Clothing/Uniforms/Jumpsuits/Civilian/ancient.rsi/meta.json) |
| Clothing/Uniforms/Jumpsuits/Security | [detective](../../../Clothing/Uniforms/Jumpsuits/Security/detective.rsi/meta.json), [detective_grey](../../../Clothing/Uniforms/Jumpsuits/Security/detective_grey.rsi/meta.json) |
| Clothing/Uniforms/Jumpsuits/Service | [bartender](../../../Clothing/Uniforms/Jumpsuits/Service/bartender.rsi/meta.json) |
| Decals | [dirty](../../../Decals/dirty.rsi/meta.json), [markings](../../../Decals/markings.rsi/meta.json) |
| Objects/Consumable/Drinks | [whiskeybottle](../../../Objects/Consumable/Drinks/whiskeybottle.rsi/meta.json) |
| Objects/Consumable/Food/Baked | [pizza](../../../Objects/Consumable/Food/Baked/pizza.rsi/meta.json) |
| Objects/Consumable/Food/Tins | [meat](../../../Objects/Consumable/Food/Tins/meat.rsi/meta.json) |
| Objects/Fun/Instruments | [structureinstruments](../../../Objects/Fun/Instruments/structureinstruments.rsi/meta.json) |
| Objects/Fun/Plushies | [lizard](../../../Objects/Fun/Plushies/lizard.rsi/meta.json), [narsie](../../../Objects/Fun/Plushies/narsie.rsi/meta.json) |
| Objects/Fun/Tabletop | [chessboard](../../../Objects/Fun/Tabletop/chessboard.rsi/meta.json), [parchis](../../../Objects/Fun/Tabletop/parchis.rsi/meta.json) |
| Objects/Misc | [bedsheets](../../../Objects/Misc/bedsheets.rsi/meta.json), [module](../../../Objects/Misc/module.rsi/meta.json) |
| Objects/Misc/Lights | [lamp](../../../Objects/Misc/Lights/lamp.rsi/meta.json) |
| Objects/Specific/Medical | [firstaidkits](../../../Objects/Specific/Medical/firstaidkits.rsi/meta.json) |
| Objects/Storage/EntityProvider | [packaged_lights](../../../Objects/Storage/EntityProvider/packaged_lights.rsi/meta.json) |
| Objects/Tanks | [oxygen](../../../Objects/Tanks/oxygen.rsi/meta.json), [red](../../../Objects/Tanks/red.rsi/meta.json) |
| Objects/Tools | [bucket](../../../Objects/Tools/bucket.rsi/meta.json), [crowbar](../../../Objects/Tools/crowbar.rsi/meta.json), [lantern](../../../Objects/Tools/lantern.rsi/meta.json) |
| Objects/Tools/Hydroponics | [clippers](../../../Objects/Tools/Hydroponics/clippers.rsi/meta.json), [hatchet](../../../Objects/Tools/Hydroponics/hatchet.rsi/meta.json), [hoe](../../../Objects/Tools/Hydroponics/hoe.rsi/meta.json), [spade](../../../Objects/Tools/Hydroponics/spade.rsi/meta.json) |
| Objects/Tools/Toolboxes | [toolbox_red](../../../Objects/Tools/Toolboxes/toolbox_red.rsi/meta.json), [toolbox_yellow](../../../Objects/Tools/Toolboxes/toolbox_yellow.rsi/meta.json) |
| Structures | [barricades](../../../Structures/barricades.rsi/meta.json), [catwalk](../../../Structures/catwalk.rsi/meta.json), [cryostorage](../../../Structures/cryostorage.rsi/meta.json), [stairs](../../../Structures/stairs.rsi/meta.json) |
| Structures/Doors/Misc | [secret-door](../../../Structures/Doors/Misc/secret-door.rsi/meta.json) |
| Structures/Furniture | [chairs](../../../Structures/Furniture/chairs.rsi/meta.json), [furniture](../../../Structures/Furniture/furniture.rsi/meta.json) |
| Structures/Furniture/Carpets | [black_carpet](../../../Structures/Furniture/Carpets/black_carpet.rsi/meta.json), [blue_carpet](../../../Structures/Furniture/Carpets/blue_carpet.rsi/meta.json), [chapel_carpet](../../../Structures/Furniture/Carpets/chapel_carpet.rsi/meta.json), [green_carpet](../../../Structures/Furniture/Carpets/green_carpet.rsi/meta.json) |
| Structures/Furniture/Tables | [carpet](../../../Structures/Furniture/Tables/carpet.rsi/meta.json), [generic](../../../Structures/Furniture/Tables/generic.rsi/meta.json), [reinforced](../../../Structures/Furniture/Tables/reinforced.rsi/meta.json), [wood](../../../Structures/Furniture/Tables/wood.rsi/meta.json) |
| Structures/Machines | [computers](../../../Structures/Machines/computers.rsi/meta.json), [gravity_generator_core](../../../Structures/Machines/gravity_generator_core.rsi/meta.json) |
| Structures/Machines/VendingMachines | [changs](../../../Structures/Machines/VendingMachines/changs.rsi/meta.json), [chapdrobe](../../../Structures/Machines/VendingMachines/chapdrobe.rsi/meta.json), [cola](../../../Structures/Machines/VendingMachines/cola.rsi/meta.json), [snack](../../../Structures/Machines/VendingMachines/snack.rsi/meta.json), [vendomat](../../../Structures/Machines/VendingMachines/vendomat.rsi/meta.json), [youtool](../../../Structures/Machines/VendingMachines/youtool.rsi/meta.json) |
| Structures/Piping | [disposal](../../../Structures/Piping/disposal.rsi/meta.json) |
| Structures/Piping/Atmospherics | [gascanisterport](../../../Structures/Piping/Atmospherics/gascanisterport.rsi/meta.json), [pipe](../../../Structures/Piping/Atmospherics/pipe.rsi/meta.json), [scrubber](../../../Structures/Piping/Atmospherics/scrubber.rsi/meta.json), [vent](../../../Structures/Piping/Atmospherics/vent.rsi/meta.json) |
| Structures/Power/Cables | [lv_cable](../../../Structures/Power/Cables/lv_cable.rsi/meta.json), [mv_cable](../../../Structures/Power/Cables/mv_cable.rsi/meta.json) |
| Structures/Power/Generation | [wallmount_generator](../../../Structures/Power/Generation/wallmount_generator.rsi/meta.json) |
| Structures/Shuttles | [gyroscope](../../../Structures/Shuttles/gyroscope.rsi/meta.json), [thruster](../../../Structures/Shuttles/thruster.rsi/meta.json) |
| Structures/Storage | [canister](../../../Structures/Storage/canister.rsi/meta.json), [closet](../../../Structures/Storage/closet.rsi/meta.json), [morgue](../../../Structures/Storage/morgue.rsi/meta.json), [tanks](../../../Structures/Storage/tanks.rsi/meta.json), [wall_locker](../../../Structures/Storage/wall_locker.rsi/meta.json) |
| Structures/Storage/Crates | [coffin](../../../Structures/Storage/Crates/coffin.rsi/meta.json) |
| Structures/Wallmounts | [air-alarm](../../../Structures/Wallmounts/air-alarm.rsi/meta.json), [apc](../../../Structures/Wallmounts/apc.rsi/meta.json), [defib-cabinet](../../../Structures/Wallmounts/defib-cabinet.rsi/meta.json), [extinguisher-cabinet](../../../Structures/Wallmounts/extinguisher-cabinet.rsi/meta.json), [fire-alarm](../../../Structures/Wallmounts/fire-alarm.rsi/meta.json), [intercom](../../../Structures/Wallmounts/intercom.rsi/meta.json), [posters](../../../Structures/Wallmounts/posters.rsi/meta.json), [screen](../../../Structures/Wallmounts/screen.rsi/meta.json), [signs](../../../Structures/Wallmounts/signs.rsi/meta.json) |
| Structures/Wallmounts/Lighting | [emergency-light](../../../Structures/Wallmounts/Lighting/emergency-light.rsi/meta.json), [light-small](../../../Structures/Wallmounts/Lighting/light-small.rsi/meta.json), [light-tube](../../../Structures/Wallmounts/Lighting/light-tube.rsi/meta.json) |
| Structures/Walls | [grille](../../../Structures/Walls/grille.rsi/meta.json), [shuttle-diagonal](../../../Structures/Walls/shuttle-diagonal.rsi/meta.json), [shuttle](../../../Structures/Walls/shuttle.rsi/meta.json), [solid-reinforced](../../../Structures/Walls/solid-reinforced.rsi/meta.json), [solid-rust](../../../Structures/Walls/solid-rust.rsi/meta.json), [solid](../../../Structures/Walls/solid.rsi/meta.json) |
| Structures/Windows | [window-base-reinf](../../../Structures/Windows/window-base-reinf.rsi/meta.json), [window-base](../../../Structures/Windows/window-base.rsi/meta.json), [window-shuttle](../../../Structures/Windows/window-shuttle.rsi/meta.json), [window-tinted](../../../Structures/Windows/window-tinted.rsi/meta.json) |
| Tiles | [arcadeblue.png](../../../Tiles/attributions.yml) |
| Tiles/Shuttle | [shuttle-black.png](../../../Tiles/Shuttle/attributions.yml), [shuttle-blue.png](../../../Tiles/Shuttle/attributions.yml), [shuttle-gray.png](../../../Tiles/Shuttle/attributions.yml), [shuttle-orange.png](../../../Tiles/Shuttle/attributions.yml), [shuttle-white.png](../../../Tiles/Shuttle/attributions.yml) |
| Tiles/Wood | [wood-tile.png](../../../Tiles/Wood/attributions.yml), [wood.png](../../../Tiles/Wood/attributions.yml) |

### [CC-BY-SA-4.0](https://creativecommons.org/licenses/by-sa/4.0/)

| Texture directory | Artist and source records |
| --- | --- |
| Structures/Doors/Airlocks/Glass | [airlock-base](../../../Structures/Doors/Airlocks/Glass/airlock-base.rsi/meta.json), [airlock-external](../../../Structures/Doors/Airlocks/Glass/airlock-external.rsi/meta.json), [centcomm-base](../../../Structures/Doors/Airlocks/Glass/centcomm-base.rsi/meta.json), [firelock-base](../../../Structures/Doors/Airlocks/Glass/firelock-base.rsi/meta.json), [shuttle-base](../../../Structures/Doors/Airlocks/Glass/shuttle-base.rsi/meta.json) |
| Structures/Doors/Airlocks/Standard | [airlock-base](../../../Structures/Doors/Airlocks/Standard/airlock-base.rsi/meta.json), [airlock-maints](../../../Structures/Doors/Airlocks/Standard/airlock-maints.rsi/meta.json), [engineering-base](../../../Structures/Doors/Airlocks/Standard/engineering-base.rsi/meta.json), [firelock-base](../../../Structures/Doors/Airlocks/Standard/firelock-base.rsi/meta.json), [service-base](../../../Structures/Doors/Airlocks/Standard/service-base.rsi/meta.json) |
| Structures/Machines | [gravity_generator_mini](../../../Structures/Machines/gravity_generator_mini.rsi/meta.json) |
| Structures/Power/Cables | [hv_cable](../../../Structures/Power/Cables/hv_cable.rsi/meta.json) |
| Structures/Wallmounts | [camera](../../../Structures/Wallmounts/camera.rsi/meta.json) |

### [CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/)

| Texture directory | Artist and source records |
| --- | --- |
| Decals | [bricktile](../../../Decals/bricktile.rsi/meta.json), [minitile](../../../Decals/minitile.rsi/meta.json), [wood_trim](../../../Decals/wood_trim.rsi/meta.json) |
| Structures/Power | [substation](../../../Structures/Power/substation.rsi/meta.json) |
| Tiles | [freezer.png](../../../Tiles/attributions.yml), [lattice.png](../../../Tiles/attributions.yml), [plating.png](../../../Tiles/attributions.yml) |
| Tiles/Basic/Dark | [dark-mono.png](../../../Tiles/Basic/Dark/attributions.yml), [dark.png](../../../Tiles/Basic/Dark/attributions.yml) |
| Tiles/Basic/Gray | [gray-maint.png](../../../Tiles/Basic/Gray/attributions.yml), [gray.png](../../../Tiles/Basic/Gray/attributions.yml) |
| Tiles/Checker | [checker-dark.png](../../../Tiles/Checker/attributions.yml) |
