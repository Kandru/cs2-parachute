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
        [JsonPropertyName("DisableOnRoundEnd")] public bool DisableOnRoundEnd { get; set; } = false;
        [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = true;
    }

    public enum ParachuteState
    {
        Disabled,
        Timer,
        Enabled
    }

    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        public PluginConfig Config { get; set; } = null!;
        public void OnConfigParsed(PluginConfig config) { Config = config; }

        private Dictionary<CCSPlayerController, CDynamicProp?> _parachutes = [];
        private ParachuteState _state = ParachuteState.Disabled;

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
                _state = ParachuteState.Enabled;
                Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            }
            // register event handler
            CreateEventHandler();
        }

        public override void Unload(bool hotReload)
        {
            _state = ParachuteState.Disabled;
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
            // only run if parachute is enabled
            if (_state < ParachuteState.Enabled) return;
            foreach (var player in Utilities.GetPlayers()
                .Where(player => player.IsValid
                        && !player.IsBot
                    && player.PlayerPawn != null
                    && player.PlayerPawn.IsValid
                    && player.PlayerPawn.Value != null))
            {
                // if the player does not use the parachute
                if ((player.Buttons & PlayerButtons.Use) == 0
                    // if player is not alive
                    || player.PlayerPawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE
                    // if player is not in the air
                    || player.PlayerPawn.Value!.GroundEntity.Value != null
                    // if player carries a hostage and this is not allowed due to configuration
                    || (Config.DisableWhenCarryingHostage && player.PlayerPawn.Value.HostageServices!.CarriedHostageProp.Value != null)
                    // if player is using a ladder
                    || player.PlayerPawn.Value!.MoveType == MoveType_t.MOVETYPE_LADDER)
                {
                    // when player is not in the air, remove parachute
                    if (!_parachutes.ContainsKey(player)) continue;
                    RemoveParachute(_parachutes[player]);
                    _parachutes.Remove(player);
                }
                else if (!_parachutes.ContainsKey(player))
                {
                    _parachutes.Add(player, CreateParachute(player));
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
            // reset parachute on round start
            _state = ParachuteState.Disabled;
            ResetParachutes();
            return HookResult.Continue;
        }

        private HookResult EventOnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            // check if we should enable the parachute instantly or after a delay
            _state = ParachuteState.Timer;
            if (Config.RoundStartDelay > 0)
            {
                Server.PrintToChatAll(
                    Localizer["parachute.delay"].Value.Replace("{seconds}",
                    Config.RoundStartDelay.ToString(CultureInfo.CurrentCulture)));
                AddTimer(Config.RoundStartDelay, EnableParachutes);
            }
            else
            {
                EnableParachutes();
            }
            return HookResult.Continue;
        }

        private HookResult EventOnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            // remove player from list when he dies
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_parachutes.ContainsKey(player)) return HookResult.Continue;
            RemoveParachute(_parachutes[player]);
            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            // check if we should reset the parachute on round end
            if (!Config.DisableOnRoundEnd) return HookResult.Continue;
            _state = ParachuteState.Disabled;
            ResetParachutes();
            return HookResult.Continue;
        }

        private void EnableParachutes()
        {
            if (_state != ParachuteState.Timer) return;
            _state = ParachuteState.Enabled;
            Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            Utilities.GetPlayers()
                .Where(player => player.IsValid && !player.IsBot && !player.IsHLTV)
                .ToList()
                .ForEach(player => player.PrintToCenter(Localizer["parachute.readyCenter"]));
        }
    }
}
