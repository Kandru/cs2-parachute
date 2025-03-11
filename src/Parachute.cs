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
            if (_parachutePlayers.ContainsKey(player.UserId ?? -1)) return;
            _parachutePlayers.Add(player.UserId ?? -1, new Dictionary<string, string>());
            // get random parachute
            _parachutePlayers[player.UserId ?? -1]["type"] = _parachuteModels.ElementAt(Random.Shared.Next(_parachuteModels.Count)).Key;
            _parachutePlayers[player.UserId ?? -1]["prop"] = SpawnProp(
                player,
                _parachuteModels[_parachutePlayers[player.UserId ?? -1]["type"]].Keys.First()
            ).ToString();
        }

        private void RemoveParachute(int UserId)
        {
            if (!_parachutePlayers.ContainsKey(UserId)) return;
            if (_parachutePlayers[UserId].ContainsKey("prop")) RemoveProp(int.Parse(_parachutePlayers[UserId]["prop"]));
            _parachutePlayers.Remove(UserId);
        }

        private void ResetParachutes()
        {
            Dictionary<int, Dictionary<string, string>> _parachutePlayersCopy = new(_parachutePlayers);
            foreach (int userid in _parachutePlayersCopy.Keys)
            {
                CCSPlayerController? player = Utilities.GetPlayerFromUserid(userid);
                if (player == null
                    || !player.IsValid
                    || player.Pawn == null
                    || !player.PlayerPawn.IsValid
                    || player.Pawn.Value == null) continue;
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
                    if (_parachutePlayers.ContainsKey(player.UserId ?? -1) && _parachutePlayers[player.UserId ?? -1].ContainsKey("prop"))
                        RemoveProp(int.Parse(_parachutePlayers[player.UserId ?? -1]["prop"]), true);
                    // stop interaction
                    continue;
                }
                // launch parachute
                if (!_parachutePlayers.ContainsKey(player.UserId ?? -1)) LaunchParachute(player);
                else
                {
                    Vector absVelocity = player.Pawn.Value.AbsVelocity;
                    // Determine movement direction
                    absVelocity.Z = -Config.FallSpeed;
                    // Update prop every tick to ensure synchrony
                    if (_parachutePlayers[player.UserId ?? -1].ContainsKey("prop"))
                    {
                        UpdateProp(player, int.Parse(_parachutePlayers[player.UserId ?? -1]["prop"]));
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
