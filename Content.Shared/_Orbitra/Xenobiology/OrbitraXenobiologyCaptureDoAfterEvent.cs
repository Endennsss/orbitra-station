// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Xenobiology;

[Serializable, NetSerializable]
public sealed partial class OrbitraXenobiologyCaptureDoAfterEvent : SimpleDoAfterEvent;
