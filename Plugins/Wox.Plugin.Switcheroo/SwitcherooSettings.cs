using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Switcheroo
{
    public class SwitcherooSettings
    {
        [JsonProperty]
        public bool OverrideAltTab { get; set; } = false;

        [JsonProperty]
        public bool SwapTitleAndSubtitle { get; set; } = false;

        [JsonProperty]
        public bool ApplicationNameFirst { get; set; } = false;
    }
}
