﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AtraShared.Integrations.GMCMAttributes;

namespace ExperimentalLagReduction.Framework;
internal sealed class ModConfig
{
    public bool OverrideGiftTastes { get; set; } = true;

    public bool ForceLazyTextureLoad { get; set; } =
        #if DEBUG
            true;
        #else
            false;
        #endif

    [GMCMSection("Scheduler", 0)]
    public bool UseAlternativeScheduler { get; set; } = true;

    [GMCMSection("Scheduler", 0)]
    public bool AllowModAddedDoors { get; set; } = true;

    [GMCMSection("Scheduler", 0)]
    public bool AllowPartialPaths { get; set; } = true;
}