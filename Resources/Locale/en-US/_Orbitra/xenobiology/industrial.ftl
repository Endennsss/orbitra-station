# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Ende (Orbitra)

research-technology-orbitra-industrial-xenobiology = Industrial xenobiology
    .suffix = Orbitra
orbitra-research-technology-orbitra-industrial-xenobiology-description = Single-use induction catalysts for transforming an adult laboratory slime with six unused same-breed extracts into a reusable, plasma-fueled industrial extract.

ent-OrbitraXenoIndustrialCatalyst = industrial induction catalyst
    .desc = A sealed catalyst for industrial slime induction. Its label specifies one living adult laboratory slime and six unused extracts of the same breed.
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractBase = industrial slime extract
    .desc = A dense industrial slime core. Its label specifies liquid plasma and uninterrupted operation on a floor outside containers.
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractMetal = industrial metal extract
    .desc = { ent-OrbitraXenoIndustrialExtractBase.desc }
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractYellow = industrial yellow extract
    .desc = { ent-OrbitraXenoIndustrialExtractBase.desc }
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractPurple = industrial purple extract
    .desc = { ent-OrbitraXenoIndustrialExtractBase.desc }
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractSilver = industrial silver extract
    .desc = { ent-OrbitraXenoIndustrialExtractBase.desc }
    .suffix = Orbitra
ent-OrbitraXenoIndustrialExtractBlue = industrial blue extract
    .desc = { ent-OrbitraXenoIndustrialExtractBase.desc }
    .suffix = Orbitra

orbitra-xeno-industrial-access = Hold the item, stand within reach and make sure you can interact.
orbitra-xeno-industrial-specimen = Industrial induction requires a living, uncontained adult NPC laboratory slime.
orbitra-xeno-industrial-already-induced = This specimen is already undergoing industrial induction.
orbitra-xeno-industrial-unsupported = This breed has no industrial recipe.
orbitra-xeno-industrial-unused = Only unused ordinary extracts can be fed to the specimen.
orbitra-xeno-industrial-breed = The ordinary extract must match the induced specimen's breed.
orbitra-xeno-industrial-induced = Induction started. Feed it { $required } unused ordinary extracts of its own breed.
orbitra-xeno-industrial-progress = Industrial induction: { $current }/{ $required } unused { $breed } extracts consumed. Division and corpse extraction are blocked.
orbitra-xeno-industrial-cycle = Each batch consumes { $plasma } units of liquid plasma after { $seconds } uninterrupted seconds on a non-space floor outside all containers and hands.
orbitra-xeno-industrial-product = Batch output: { $count } × { $name }.
orbitra-xeno-industrial-running = Fueled and correctly placed. Moving it into a container or pausing interrupts the cycle; production restarts with a full cycle.
orbitra-xeno-industrial-stopped = Production stopped. Supply enough liquid plasma and place the extract on a real floor outside containers and hands.
orbitra-xeno-industrial-heading = Industrial induction
orbitra-xeno-industrial-catalog = Research Industrial xenobiology (tier 3 OrbitraBiochemical, 15000 points; requires Colony engineering and Industrial engineering), then print an industrial induction catalyst in the protolathe. Apply the held, single-use catalyst to a living adult NPC laboratory slime of this breed, then feed it { $required } unused ordinary extracts of the same breed, one at a time. This consumes the adult and creates one reusable industrial extract; induced specimens cannot divide or be processed for ordinary extracts.
