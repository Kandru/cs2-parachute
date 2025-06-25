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

        private Dictionary<CCSPlayerController, CDynamicProp?> _parachutes = [];
        private Dictionary<CCSPlayerController, long> _parachuteSounds = [];
        private ParachuteState _state = ParachuteState.Disabled;

        public override void Load(bool hotReload)
        {
            // precache model if any
            if (Config.ParachuteModel != "")
            {
                _precacheModels.Add(Config.ParachuteModel);
            }

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
            Console.WriteLine(Localizer["parachute.loaded"]);
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
                Prop.RemoveParachute(kvp.Value);
            }
            _parachutes.Clear();
            _parachuteSounds.Clear();
        }

        private void ListenerOnTick()
        {
            // only run if parachute is enabled
            if (_state < ParachuteState.Enabled)
            {
                return;
            }

            foreach (var player in Utilities.GetPlayers()
                .Where(player => player.IsValid
                        && !player.IsBot
                    && player.Pawn != null
                    && player.Pawn.IsValid
                    && player.Pawn.Value != null))
            {
                // if the player does not use the parachute
                if ((player.Buttons & PlayerButtons.Use) == 0
                    // if player is not alive
                    || player.Pawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE
                    // if player is not in the air
                    || player.Pawn.Value!.GroundEntity.Value != null
                    // if player carries a hostage and this is not allowed (may not work when player is using a bot)
                    || (Config.DisableWhenCarryingHostage
                        && player.PlayerPawn != null
                        && player.PlayerPawn.IsValid
                        && player.PlayerPawn.Value != null
                        && player.PlayerPawn.Value.HostageServices != null
                        && player.PlayerPawn.Value.HostageServices.CarriedHostageProp.Value != null)
                    // if player is using a ladder
                    || player.Pawn.Value!.MoveType == MoveType_t.MOVETYPE_LADDER)
                {
                    // when player is not in the air, remove parachute
                    if (!_parachutes.ContainsKey(player))
                    {
                        continue;
                    }

                    Prop.RemoveParachute(_parachutes[player]);
                    _parachutes.Remove(player);
                    _parachuteSounds.Remove(player);
                }
                else if (!_parachutes.ContainsKey(player))
                {
                    _parachutes.Add(player, Prop.CreateParachute(Config, player));
                    _parachuteSounds.Add(player, 0L);
                }
                else
                {
                    Vector absVelocity = player.Pawn.Value.AbsVelocity;
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
                        if (player.Pawn.Value.V_angle.X < 0.0f)
                        {
                            // set fallspeed upwards
                            absVelocity.Z *= Config.HoverboardMovementModifier;
                        }
                        else if (player.Pawn.Value.V_angle.X > 0.0f)
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
                    if ((player.Buttons & PlayerButtons.Moveleft) != 0 || (player.Buttons & PlayerButtons.Moveright) != 0)
                    {
                        absVelocity.X *= Config.SideMovementModifier;
                        absVelocity.Y *= Config.SideMovementModifier;
                    }
                    // play sound if enabled
                    if (Config.ParachuteSound != ""
                        && _parachutes[player] != null
                        && _parachutes[player]!.IsValid
                        && _parachuteSounds[player] <= (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond))
                    {
                        _parachutes[player]!.EmitSound(Config.ParachuteSound);
                        _parachuteSounds[player] = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond + (long)((Config.ParachuteSoundInterval) * 1000L);
                    }
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
                || !_parachutes.ContainsKey(player))
            {
                return HookResult.Continue;
            }

            Prop.RemoveParachute(_parachutes[player]);
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
