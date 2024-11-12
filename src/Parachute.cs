using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Parachute
{
    public class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 32;
        [JsonPropertyName("SideMovementModifier")] public float SideMovementModifier { get; set; } = 1.0075f;
        [JsonPropertyName("MaxVelocity")] public float MaxVelocity { get; set; } = 400f;
        [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
        [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = false;
        [JsonPropertyName("DisableForBots")] public bool DisableForBots { get; set; } = false;
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
                || (Config.DisableForBots && player.IsBot)) continue;
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
                    var velocity = player.Pawn.Value.AbsVelocity;
                    if (velocity.Z < 0.0f)
                    {
                        if ((player.Buttons & PlayerButtons.Moveleft) != 0 || (player.Buttons & PlayerButtons.Moveright) != 0)
                        {
                            velocity.X *= Config.SideMovementModifier;
                            velocity.Y *= Config.SideMovementModifier;
                            if (velocity.X > Config.MaxVelocity) velocity.X = Config.MaxVelocity;
                            if (velocity.X < -Config.MaxVelocity) velocity.X = -Config.MaxVelocity;
                            if (velocity.Y > Config.MaxVelocity) velocity.Y = Config.MaxVelocity;
                            if (velocity.Y < -Config.MaxVelocity) velocity.Y = -Config.MaxVelocity;
                        }
                        velocity.Z = Config.FallSpeed * (-1.0f);
                        if (_parachutePlayers[player].ContainsKey("prop"))
                        {
                            // update prop every tick to ensure synchroneity
                            UpdateProp(
                                player,
                                int.Parse(_parachutePlayers[player]["prop"])
                            );
                        }
                    }
                }
            }
        }

        private HookResult EventOnRoundStart(EventRoundStart @event, GameEventInfo info)
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
