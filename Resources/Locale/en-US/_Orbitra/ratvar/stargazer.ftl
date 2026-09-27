ent-OrbitraRatvarStargazer = stargazer
    .desc = A brass pedestal with a softly glowing lens.
ent-OrbitraRatvarEnchantEffect = brass radiance
orbitra-ratvar-scripture-stargazer = Stargazer
orbitra-ratvar-scripture-stargazer-desc = Creates a weapon enchanter. Rituals take 6 seconds, with a 180-second cooldown. Requires transmission coverage. Each item receives one permanent random blessing.
orbitra-ratvar-enchant-denied = Requires your cult's ready, idle and powered stargazer and an unenchanted weapon held in hand. Clothing is not supported.
orbitra-ratvar-enchant-success = The weapon receives Ratvar's blessing!
orbitra-ratvar-enchant-status = Cooldown: { $seconds } s. Ritual: { $busy ->
    [true] in progress
   *[other] idle
    }.
orbitra-ratvar-enchant-glow = The item glows with a faint brass light.
orbitra-ratvar-enchant-description = Blessing: { $kind ->
    [Sharpness] sharpness
    [Tiny] tiny
    [Burn] fire
   *[SoulTap] soul tap
    }, strength: { $level }.
