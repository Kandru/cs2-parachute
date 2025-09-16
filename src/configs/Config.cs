using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;
using Parachute.Configs;

namespace Parachute
{
    public class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("round_start_delay")] public int RoundStartDelay { get; set; } = 10;
        [JsonPropertyName("disable_on_round_end")] public bool DisableOnRoundEnd { get; set; } = false;
        [JsonPropertyName("disable_when_carrying_hostage")] public bool DisableWhenCarryingHostage { get; set; } = true;
        [JsonPropertyName("parachute")] public ParachuteConfig Parachute { get; set; } = new ParachuteConfig();
    }

    public partial class Parachute : BasePlugin
    {
        public PluginConfig Config { get; set; } = null!;

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
            Config.Update();
        }
    }
}