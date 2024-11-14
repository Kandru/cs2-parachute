using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
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
        [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = true;
    }

    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        public PluginConfig Config { get; set; } = null!;
        public void OnConfigParsed(PluginConfig config) { Config = config; }

        private Dictionary<CCSPlayerController, Dictionary<string, string>> _parachutePlayers = new();
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
            if (_parachutePlayers.ContainsKey(player)) return;
            _parachutePlayers.Add(player, new Dictionary<string, string>());
            _parachutePlayers[player]["prop"] = SpawnProp(
                player,
                "models/props_survival/parachute/chute.vmdl"
            ).ToString();
        }

        private void RemoveParachute(CCSPlayerController player)
        {
            if (!_parachutePlayers.ContainsKey(player)) return;
            if (_parachutePlayers[player].ContainsKey("prop")) RemoveProp(int.Parse(_parachutePlayers[player]["prop"]));
            _parachutePlayers.Remove(player);
        }

        private void ResetParachutes()
        {
            foreach (CCSPlayerController player in _parachutePlayers.Keys)
            {
                if (player == null || player.Pawn == null || player.Pawn.Value == null) continue;
                RemoveParachute(player);
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
                    || (Config.DisableWhenCarryingHostage && player.PlayerPawn.Value.HostageServices!.CarriedHostageProp.Value != null))
                {
                    if (_parachutePlayers.ContainsKey(player) && _parachutePlayers[player].ContainsKey("prop")) RemoveProp(int.Parse(_parachutePlayers[player]["prop"]), true);
                    // stop interaction
                    continue;
                }
                // launch parachute
                if (!_parachutePlayers.ContainsKey(player)) LaunchParachute(player);
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
                    if (_parachutePlayers[player].ContainsKey("prop"))
                    {
                        UpdateProp(player, int.Parse(_parachutePlayers[player]["prop"]));
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
            CCSPlayerController player = @event.Userid!;
            RemoveParachute(player);
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
