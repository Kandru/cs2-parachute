using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;


namespace Parachute
{
    public class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("IsHoverboard")] public bool IsHoverboard { get; set; } = false;
        [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 0.1f;
        [JsonPropertyName("SideMovementModifier")] public float SideMovementModifier { get; set; } = 1.0075f;
        [JsonPropertyName("HoverboardMovementModifier")] public float HoverboardMovementModifier { get; set; } = 1.0075f;
        [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
        [JsonPropertyName("DisableOnRoundEnd")] public bool DisableOnRoundEnd { get; set; } = false;
        [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = true;
        [JsonPropertyName("ParachuteModel")] public string ParachuteModel { get; set; } = "models/cs2/kandru/hoverboard.vmdl";
        [JsonPropertyName("ParachuteModelSize")] public float ParachuteModelSize { get; set; } = 1f;
        [JsonPropertyName("ParachuteSound")] public string ParachuteSound { get; set; } = "Kandru.Hoverboard";
        [JsonPropertyName("ParachuteSoundInterval")] public float ParachuteSoundInterval { get; set; } = 1.266f;
        [JsonPropertyName("EnableTeamColors")] public bool EnableTeamColors { get; set; } = false;
    }

    public partial class Parachute : BasePlugin
    {
        public PluginConfig Config { get; set; } = null!;

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
            // update config to reflect latest changes of the plugin
            Config.Update();
        }
    }
}