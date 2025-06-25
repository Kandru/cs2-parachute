using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Parachute.Utils;
using System.Globalization;


namespace Parachute
{
    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        private readonly Dictionary<CCSPlayerController, CDynamicProp?> _parachutes = [];
        private readonly Dictionary<CCSPlayerController, long> _parachuteSounds = [];
        private ParachuteState _state = ParachuteState.Disabled;

        public override void Load(bool hotReload)
        {
            // precache model if any
            if (Config.ParachuteModel != "")
            {
                _precacheModels.Add(Config.ParachuteModel);
            }
            // check if enabled
            if (!Config.Enabled)
            {
                Console.WriteLine(Localizer["parachute.disabled"]);
                return;
            }
            // check if hot reloaded
            if (hotReload)
            {
                foreach (CCSPlayerController? player in Utilities.GetPlayers().Where(static p => !p.IsBot && !p.IsHLTV))
                {
                    AddPlayerToList(player);
                }
                _state = ParachuteState.Enabled;
                Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            }
            // create all listeners and register events
            CreateListener();
            Console.WriteLine(Localizer["parachute.loaded"]);
        }

        public override void Unload(bool hotReload)
        {
            _state = ParachuteState.Disabled;
            RemoveListener();
            ResetParachutes(true);
            Console.WriteLine(Localizer["parachute.unloaded"]);
        }

        private void CreateListener()
        {
            RegisterListener<Listeners.OnTick>(ListenerOnTick);
            RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventRoundStart>(EventOnRoundStart);
            RegisterEventHandler<EventRoundFreezeEnd>(EventOnRoundFreezeEnd);
            RegisterEventHandler<EventPlayerDeath>(EventOnPlayerDeath);
            RegisterEventHandler<EventRoundEnd>(EventOnRoundEnd);
        }

        private void RemoveListener()
        {
            RemoveListener<Listeners.OnTick>(ListenerOnTick);
            RemoveListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
            RemoveListener<Listeners.OnMapStart>(OnMapStart);
            RemoveListener<Listeners.OnMapEnd>(OnMapEnd);
            DeregisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            DeregisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            DeregisterEventHandler<EventRoundStart>(EventOnRoundStart);
            DeregisterEventHandler<EventRoundFreezeEnd>(EventOnRoundFreezeEnd);
            DeregisterEventHandler<EventPlayerDeath>(EventOnPlayerDeath);
            DeregisterEventHandler<EventRoundEnd>(EventOnRoundEnd);
        }

        private void ResetParachutes(bool force = false)
        {
            Dictionary<CCSPlayerController, CDynamicProp?> _parachutesCopy = new(_parachutes);
            foreach (KeyValuePair<CCSPlayerController, CDynamicProp?> kvp in _parachutesCopy)
            {
                Prop.RemoveParachute(kvp.Value);
                _parachutes[kvp.Key] = null;
            }
            _parachuteSounds.Clear();
            if (force)
            {
                _parachutes.Clear();
            }
        }

        private void ListenerOnTick()
        {
            // only run if parachute is enabled
            if (_state < ParachuteState.Enabled)
            {
                return;
            }

            foreach (var kvp in _parachutes)
            {
                // if the player does not use the parachute
                if ((kvp.Key.Buttons & PlayerButtons.Use) == 0
                    // if player is not alive
                    || kvp.Key.Pawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE
                    // if player is not in the air
                    || kvp.Key.Pawn.Value!.GroundEntity.Value != null
                    // if player carries a hostage and this is not allowed (may not work when player is using a bot)
                    || (Config.DisableWhenCarryingHostage
                        && kvp.Key.PlayerPawn != null
                        && kvp.Key.PlayerPawn.IsValid
                        && kvp.Key.PlayerPawn.Value != null
                        && kvp.Key.PlayerPawn.Value.HostageServices != null
                        && kvp.Key.PlayerPawn.Value.HostageServices.CarriedHostageProp.Value != null)
                    // if player is using a ladder
                    || kvp.Key.Pawn.Value!.MoveType == MoveType_t.MOVETYPE_LADDER)
                {
                    // when player is not in the air, remove parachute
                    if (_parachutes[kvp.Key] == null)
                    {
                        continue;
                    }

                    Prop.RemoveParachute(_parachutes[kvp.Key]);
                    _ = _parachuteSounds.Remove(kvp.Key);
                }
                else if (!_parachutes.ContainsKey(kvp.Key))
                {
                    _parachutes[kvp.Key] = Prop.CreateParachute(Config, kvp.Key);
                    _parachuteSounds.Add(kvp.Key, 0L);
                }
                else
                {
                    Vector absVelocity = kvp.Key.Pawn.Value.AbsVelocity;
                    // if it is a parachute apply falldamage
                    if (!Config.IsHoverboard)
                    {
                        if (absVelocity.Z >= 0.0f)
                        {
                            continue;
                        }
                        // set fallspeed
                        absVelocity.Z = -Config.FallSpeed;
                    }
                    else
                    {
                        // check if player is looking up
                        if (kvp.Key.Pawn.Value.V_angle.X < 0.0f)
                        {
                            // set fallspeed upwards
                            absVelocity.Z *= Config.HoverboardMovementModifier;
                        }
                        else if (kvp.Key.Pawn.Value.V_angle.X > 0.0f)
                        {

                            // set fallspeed downwards
                            absVelocity.Z = -Config.HoverboardMovementModifier;
                        }
                        else
                        {
                            // set fallspeed to 0
                            absVelocity.Z = 0.0f;
                        }
                    }

                    // add horizontal velocity (+1) to player
                    if ((kvp.Key.Buttons & PlayerButtons.Moveleft) != 0 || (kvp.Key.Buttons & PlayerButtons.Moveright) != 0)
                    {
                        absVelocity.X *= Config.SideMovementModifier;
                        absVelocity.Y *= Config.SideMovementModifier;
                    }
                    // play sound if enabled
                    if (Config.ParachuteSound != ""
                        && _parachutes[kvp.Key] != null
                        && _parachutes[kvp.Key]!.IsValid
                        && _parachuteSounds[kvp.Key] <= (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond))
                    {
                        _ = _parachutes[kvp.Key]!.EmitSound(Config.ParachuteSound);
                        _parachuteSounds[kvp.Key] = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) + (long)(Config.ParachuteSoundInterval * 1000L);
                    }
                }
            }
        }

        private void OnMapStart(string mapName)
        {
            ResetParachutes(true);
        }

        private void OnMapEnd()
        {
            ResetParachutes(true);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            AddPlayerToList(player);
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_parachutes.ContainsKey(player))
            {
                return HookResult.Continue;
            }
            _ = _parachutes.Remove(player);
            return HookResult.Continue;
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
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            // check if we should enable the parachute instantly or after a delay
            _state = ParachuteState.Timer;
            if (Config.RoundStartDelay > 0)
            {
                Server.PrintToChatAll(
                    Localizer["parachute.delay"].Value.Replace("{seconds}",
                    Config.RoundStartDelay.ToString(CultureInfo.CurrentCulture)));
                _ = AddTimer(Config.RoundStartDelay, EnableParachutes);
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
                || !_parachutes.TryGetValue(player, out CDynamicProp? value))
            {
                return HookResult.Continue;
            }

            Prop.RemoveParachute(value);
            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            // check if we should reset the parachute on round end
            if (!Config.DisableOnRoundEnd)
            {
                return HookResult.Continue;
            }

            _state = ParachuteState.Disabled;
            ResetParachutes();
            return HookResult.Continue;
        }

        private void AddPlayerToList(CCSPlayerController? player)
        {
            if (player == null
                || !player.IsValid
                || player.IsBot
                || _parachutes.ContainsKey(player))
            {
                return;
            }
            Console.WriteLine($"Adding player {player.PlayerName} to parachute list.");
            _parachutes.Add(player, null);
        }

        private void EnableParachutes()
        {
            if (_state != ParachuteState.Timer)
            {
                return;
            }

            _state = ParachuteState.Enabled;
            Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            Utilities.GetPlayers()
                .Where(player => player.IsValid && !player.IsBot && !player.IsHLTV)
                .ToList()
                .ForEach(player => player.PrintToCenter(Localizer["parachute.readyCenter"]));
        }
    }
}
