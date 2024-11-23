using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Parachute
{
    public class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("Lerp")] public float Lerp { get; set; } = 0.8f;
        [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 20f;
        [JsonPropertyName("MovementModifier")] public float MovementModifier { get; set; } = 9f;
        [JsonPropertyName("SideMovementModifier")] public float SideMovementModifier { get; set; } = 9f;
        [JsonPropertyName("MaxVelocity")] public float MaxVelocity { get; set; } = 750f;
        [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
        [JsonPropertyName("EnableSounds")] public bool EnableSounds { get; set; } = true;
        [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = true;
    }

    public enum ParachuteFlags : byte
    {
        SetTeamColor = 1,
        MountAsBackpack = 2,
        EndlessBladeRotation = 4,
        MountAsCarpet = 8,
        IsAirplane = 16,
        IsVehicle = 32,
    }

    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        public PluginConfig Config { get; set; } = null!;
        public void OnConfigParsed(PluginConfig config) { Config = config; }

        private Dictionary<int, Dictionary<string, string>> _parachutePlayers = new();
        private readonly Dictionary<string, Dictionary<string, (ParachuteFlags, float)>> _parachuteModels = new()
        {
            {"standard", new Dictionary<string, (ParachuteFlags, float)> { { "models/props_survival/parachute/chute.vmdl", (ParachuteFlags.SetTeamColor, 1.0f) } } },
            {"ceiling_fan", new Dictionary<string, (ParachuteFlags, float)> { { "models/props/de_inferno/ceiling_fan_blade.vmdl", (ParachuteFlags.MountAsBackpack | ParachuteFlags.EndlessBladeRotation, 1.0f) } } },
            {"cat_carpet", new Dictionary<string, (ParachuteFlags, float)> { { "models/props/de_dust/hr_dust/dust_cart/cart_carpet.vmdl", (ParachuteFlags.MountAsCarpet, 1.0f) } } },
            {"airplane_small", new Dictionary<string, (ParachuteFlags, float)> { { "models/vehicles/airplane_small_01/airplane_small_01.vmdl", (ParachuteFlags.IsAirplane | ParachuteFlags.SetTeamColor, 0.3f) } } },
            {"airplane_medium", new Dictionary<string, (ParachuteFlags, float)> { { "models/vehicles/airplane_medium_01/airplane_medium_01_landed.vmdl", (ParachuteFlags.IsAirplane | ParachuteFlags.SetTeamColor, 0.09f) } } },
            {"taxi_city", new Dictionary<string, (ParachuteFlags, float)> { { "models/props_vehicles/taxi_city.vmdl", (ParachuteFlags.IsVehicle | ParachuteFlags.SetTeamColor, 0.3f) } } },
        };
        private readonly Dictionary<string, Dictionary<string, float>> _parachuteSounds = new()
        {
            {"standard", new Dictionary<string, float> { { "Weapon_Knife.Slash", 0.5f } } },
            {"ceiling_fan", new Dictionary<string, float> { { "Weapon_Knife.Slash", 0.5f } } },
            {"cat_carpet", new Dictionary<string, float> { { "Weapon_Knife.Slash", 0.5f } } },
            {"airplane_small", new Dictionary<string, float> { { "Weapon_Knife.Slash", 0.5f } } },
            {"taxi_city", new Dictionary<string, float> { { "Weapon_Knife.Slash", 0.5f } } },
        };
        private bool _enabled = false;
        private int _enableAfterTime = 0;

        public override void Load(bool hotReload)
        {
            Console.WriteLine(Localizer["parachute.loaded"]);
            if (!Config.Enabled)
            {
                Console.WriteLine(Localizer["parachute.disabled"]);
                return;
            }
            if (hotReload)
            {
                _enabled = true;
                Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            }
            // register sound events
            InitializeEmitSound();
            // register event handler
            CreateEventHandler();
        }

        public override void Unload(bool hotReload)
        {
            _enabled = false;
            RemoveListener();
            ResetParachutes();
            Console.WriteLine(Localizer["parachute.unloaded"]);
        }

        private void CreateEventHandler()
        {
            RegisterListener<Listeners.OnTick>(ListenerOnTick);
            RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
            RegisterEventHandler<EventRoundStart>(EventOnRoundStart);
            RegisterEventHandler<EventRoundFreezeEnd>(EventOnRoundFreezeEnd);
            RegisterEventHandler<EventPlayerDeath>(EventOnPlayerDeath);
            RegisterEventHandler<EventRoundEnd>(EventOnRoundEnd);
        }

        private void RemoveListener()
        {
            RemoveListener<Listeners.OnTick>(ListenerOnTick);
            RemoveListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        }

        private void LaunchParachute(CCSPlayerController player)
        {
            if (_parachutePlayers.ContainsKey(player.UserId ?? -1)) return;
            _parachutePlayers.Add(player.UserId ?? -1, new Dictionary<string, string>());
            // get random parachute
            _parachutePlayers[player.UserId ?? -1]["type"] = _parachuteModels.ElementAt(Random.Shared.Next(_parachuteModels.Count)).Key;
            _parachutePlayers[player.UserId ?? -1]["prop"] = SpawnProp(
                player,
                _parachuteModels[_parachutePlayers[player.UserId ?? -1]["type"]].Keys.First()
            ).ToString();
            if (Config.EnableSounds && _parachuteSounds.ContainsKey(_parachutePlayers[player.UserId ?? -1]["type"]))
            {
                var soundEntry = _parachuteSounds[_parachutePlayers[player.UserId ?? -1]["type"]].ElementAt(Random.Shared.Next(_parachuteSounds[_parachutePlayers[player.UserId ?? -1]["type"]].Count));
                _parachutePlayers[player.UserId ?? -1]["sound"] = soundEntry.Key;
                _parachutePlayers[player.UserId ?? -1]["sound_time"] = soundEntry.Value.ToString();
                _parachutePlayers[player.UserId ?? -1]["sound_next"] = "0";
            }
        }

        private void RemoveParachute(int UserId)
        {
            if (!_parachutePlayers.ContainsKey(UserId)) return;
            if (_parachutePlayers[UserId].ContainsKey("prop")) RemoveProp(int.Parse(_parachutePlayers[UserId]["prop"]));
            _parachutePlayers.Remove(UserId);
        }

        private void ResetParachutes()
        {
            foreach (int userid in _parachutePlayers.Keys)
            {
                CCSPlayerController player = Utilities.GetPlayerFromUserid(userid)!;
                if (player == null || player.Pawn == null || player.Pawn.Value == null) continue;
                RemoveParachute(player.UserId ?? -1);
            }
            _parachutePlayers.Clear();
        }

        private void ListenerOnTick()
        {
            // remove listener if not enabled to save resources
            if (!_enabled && _enableAfterTime == 0)
            {
                RemoveListener<Listeners.OnTick>(ListenerOnTick);
                return;
            }
            // enable after delay
            if (!_enabled && _enableAfterTime > 0 && (int)Server.CurrentTime >= _enableAfterTime)
            {
                _enabled = true;
                _enableAfterTime = 0;
                Server.PrintToChatAll(Localizer["parachute.readyChat"]);
                Utilities.GetPlayers().ForEach(player => player.PrintToCenter(Localizer["parachute.readyCenter"]));
            }
            // worker
            foreach (var player in Utilities.GetPlayers())
            {
                // sanity checks
                if (!_enabled
                || player == null
                || player.Pawn == null
                || player.Pawn.Value == null
                || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null
                || player.IsBot) continue;
                // pre-check
                if ((player.Buttons & PlayerButtons.Use) == 0
                    // if player is not alive
                    || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE
                    // if player is not in the air
                    || player.Pawn.Value.GroundEntity.Value != null
                    // if player carries a hostage and this is not allowed due to configuration
                    || (Config.DisableWhenCarryingHostage && player.PlayerPawn.Value.HostageServices!.CarriedHostageProp.Value != null)
                    || player.Pawn.Value.MoveType == MoveType_t.MOVETYPE_LADDER)
                {
                    if (_parachutePlayers.ContainsKey(player.UserId ?? -1) && _parachutePlayers[player.UserId ?? -1].ContainsKey("prop")) RemoveProp(int.Parse(_parachutePlayers[player.UserId ?? -1]["prop"]), true);
                    // stop interaction
                    continue;
                }
                // launch parachute
                if (!_parachutePlayers.ContainsKey(player.UserId ?? -1)) LaunchParachute(player);
                else
                {
                    Vector velocity = player.Pawn.Value.AbsVelocity;
                    float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
                    float eyeAngle = player.Pawn.Value.V_angle.Y;
                    float movementAngle = MathF.Atan2(velocity.Y, velocity.X) * 180 / MathF.PI;
                    // Determine movement direction
                    bool moveForward = (player.Buttons & PlayerButtons.Forward) != 0;
                    bool moveBack = (player.Buttons & PlayerButtons.Back) != 0;
                    bool moveLeft = (player.Buttons & PlayerButtons.Moveleft) != 0;
                    bool moveRight = (player.Buttons & PlayerButtons.Moveright) != 0;
                    // Adjust yaw and speed based on movement direction
                    if (moveForward && !moveBack) { speed += Config.MovementModifier; }
                    if (moveBack && !moveForward) { eyeAngle += 180; speed += Config.MovementModifier; }
                    if (moveLeft && !moveRight) { eyeAngle += moveBack ? -25 : moveForward ? 25 : 90; speed += Config.SideMovementModifier; }
                    if (moveRight && !moveLeft) { eyeAngle += moveBack ? 25 : moveForward ? -25 : -90; speed += Config.SideMovementModifier; }
                    // use movementAngle if no movement keys are pressed
                    if (!moveForward && !moveBack && !moveLeft && !moveRight ||
                        moveForward && moveBack || moveLeft && moveRight)
                    {
                        eyeAngle = movementAngle;
                    }
                    else
                    {
                        // Normalize the angle to be within -180 to 180
                        eyeAngle = (eyeAngle + 180) % 360 - 180;
                    }
                    // Convert yaw to radians
                    float radians = eyeAngle * (MathF.PI / 180);
                    // Calculate target velocity
                    float targetX = MathF.Cos(radians) * speed;
                    float targetY = MathF.Sin(radians) * speed;
                    // Apply lerp for smooth movement
                    float speedFactor = 1.0f - (speed / Config.MaxVelocity);
                    targetX = MathLerp(velocity.X, targetX, Config.Lerp * speedFactor);
                    targetY = MathLerp(velocity.Y, targetY, Config.Lerp * speedFactor);
                    velocity.Z = MathF.Max(velocity.Z, -Config.FallSpeed);
                    // Clamp velocity to max limits
                    velocity.X = Math.Clamp(targetX, -Config.MaxVelocity, Config.MaxVelocity);
                    velocity.Y = Math.Clamp(targetY, -Config.MaxVelocity, Config.MaxVelocity);
                    // Update prop every tick to ensure synchrony
                    if (_parachutePlayers[player.UserId ?? -1].ContainsKey("prop"))
                    {
                        UpdateProp(player, int.Parse(_parachutePlayers[player.UserId ?? -1]["prop"]));
                    }
                    // emit sound if any
                    if (_parachutePlayers[player.UserId ?? -1].ContainsKey("sound"))
                    {
                        if (Server.CurrentTime >= float.Parse(_parachutePlayers[player.UserId ?? -1]["sound_next"]))
                        {
                            EmitSound(player, _parachutePlayers[player.UserId ?? -1]["sound"]);
                            _parachutePlayers[player.UserId ?? -1]["sound_next"] = (Server.CurrentTime + float.Parse(_parachutePlayers[player.UserId ?? -1]["sound_time"])).ToString();
                        }
                    }
                }
            }
        }

        private HookResult EventOnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _enabled = false;
            ResetParachutes();
            return HookResult.Continue;
        }

        private HookResult EventOnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            _enabled = false;
            Server.PrintToChatAll(Localizer["parachute.delay"].Value.Replace("{seconds}", Config.RoundStartDelay.ToString(CultureInfo.CurrentCulture)));
            _enableAfterTime = (int)Server.CurrentTime + Config.RoundStartDelay;
            RegisterListener<Listeners.OnTick>(ListenerOnTick);
            return HookResult.Continue;
        }

        private HookResult EventOnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            RemoveParachute(@event.Userid!.UserId ?? -1);
            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _enabled = false;
            ResetParachutes();
            return HookResult.Continue;
        }
    }
}
