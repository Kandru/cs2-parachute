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
        [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 0.1f;
        [JsonPropertyName("SpeedMultiplier")] public float SpeedMultiplier { get; set; } = 1.1f;
        [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
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

        private Dictionary<CCSPlayerController, CDynamicProp?> _parachutes = [];
        private readonly Dictionary<string, Dictionary<string, (ParachuteFlags, float)>> _parachuteModels = new()
        {
            {"standard", new Dictionary<string, (ParachuteFlags, float)> { { "models/props_survival/parachute/chute.vmdl", (ParachuteFlags.SetTeamColor, 1.0f) } } },
            // {"ceiling_fan", new Dictionary<string, (ParachuteFlags, float)> { { "models/props/de_inferno/ceiling_fan_blade.vmdl", (ParachuteFlags.MountAsBackpack | ParachuteFlags.EndlessBladeRotation, 1.0f) } } },
            // {"cat_carpet", new Dictionary<string, (ParachuteFlags, float)> { { "models/props/de_dust/hr_dust/dust_cart/cart_carpet.vmdl", (ParachuteFlags.MountAsCarpet, 1.0f) } } },
            // {"airplane_small", new Dictionary<string, (ParachuteFlags, float)> { { "models/vehicles/airplane_small_01/airplane_small_01.vmdl", (ParachuteFlags.IsAirplane | ParachuteFlags.SetTeamColor, 0.3f) } } },
            // {"airplane_medium", new Dictionary<string, (ParachuteFlags, float)> { { "models/vehicles/airplane_medium_01/airplane_medium_01_landed.vmdl", (ParachuteFlags.IsAirplane | ParachuteFlags.SetTeamColor, 0.09f) } } },
            // {"taxi_city", new Dictionary<string, (ParachuteFlags, float)> { { "models/props_vehicles/taxi_city.vmdl", (ParachuteFlags.IsVehicle | ParachuteFlags.SetTeamColor, 0.3f) } } },
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
                RegisterListener<Listeners.OnTick>(ListenerOnTick);
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
            DeregisterEventHandler<EventRoundStart>(EventOnRoundStart);
            DeregisterEventHandler<EventRoundFreezeEnd>(EventOnRoundFreezeEnd);
            DeregisterEventHandler<EventPlayerDeath>(EventOnPlayerDeath);
            DeregisterEventHandler<EventRoundEnd>(EventOnRoundEnd);
        }

        private void ResetParachutes()
        {
            Dictionary<CCSPlayerController, CDynamicProp?> _parachutesCopy = new(_parachutes);
            foreach (var kvp in _parachutesCopy)
            {
                RemoveParachute(kvp.Value);
            }
            _parachutes.Clear();
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
            if (_enabled)
                foreach (var player in Utilities.GetPlayers())
                {
                    // sanity checks
                    if (!_enabled
                    || player == null
                    || !player.IsValid
                    || player.IsBot
                    || player.PlayerPawn == null
                    || !player.PlayerPawn.IsValid
                    || player.PlayerPawn.Value == null) continue;
                    // if the player does not use the parachute
                    if ((player.Buttons & PlayerButtons.Use) == 0
                        // if player is not alive
                        || player.PlayerPawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE
                        // if player is not in the air
                        || player.PlayerPawn.Value.GroundEntity.Value != null
                        // if player carries a hostage and this is not allowed due to configuration
                        || (Config.DisableWhenCarryingHostage && player.PlayerPawn.Value.HostageServices!.CarriedHostageProp.Value != null)
                        || player.PlayerPawn.Value.MoveType == MoveType_t.MOVETYPE_LADDER)
                    {
                        // when player is not in the air, remove parachute
                        if (!_parachutes.ContainsKey(player)) continue;
                        RemoveParachute(_parachutes[player]);
                        _parachutes.Remove(player);
                    }
                    else if (!_parachutes.ContainsKey(player))
                    {
                        _parachutes.Add(player, CreateParachute(player, "standard"));
                    }
                    else
                    {
                        Vector absVelocity = player.PlayerPawn.Value.AbsVelocity;
                        if (absVelocity.Z >= 0.0f) continue;
                        absVelocity.Z = -Config.FallSpeed;
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
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_parachutes.ContainsKey(player)) return HookResult.Continue;
            RemoveParachute(_parachutes[player]);
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
