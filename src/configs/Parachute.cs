using System.Text.Json.Serialization;

namespace Parachute.Configs
{
    public class ParachuteConfig
    {
        [JsonPropertyName("is_hoverboard")] public bool IsHoverboard { get; set; } = false;
        [JsonPropertyName("fallspeed")] public float FallSpeed { get; set; } = 0.1f;
        [JsonPropertyName("sidewards_movement_modifier")] public float SideMovementModifier { get; set; } = 1.0075f;
        [JsonPropertyName("hoverboard_movement_modifier")] public float HoverboardMovementModifier { get; set; } = 1.0075f;
        [JsonPropertyName("parachute_model")] public string ParachuteModel { get; set; } = "models/cs2/kandru/hoverboard.vmdl";
        [JsonPropertyName("parachute_model_size")] public float ParachuteModelSize { get; set; } = 1f;
        [JsonPropertyName("parachute_sound")] public string ParachuteSound { get; set; } = "Kandru.Hoverboard";
        [JsonPropertyName("parachute_sound_interval")] public float ParachuteSoundInterval { get; set; } = 1.266f;
        [JsonPropertyName("enable_team_colors")] public bool EnableTeamColors { get; set; } = false;
    }
}