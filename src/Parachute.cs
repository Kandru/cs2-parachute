using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Parachute.Enums;
using Parachute.Classes;
using Parachute.Utils;
using System.Globalization;


namespace Parachute
{
    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        private readonly Dictionary<CCSPlayerController, PlayerData> _playerData = [];
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
            // create all listeners and register events
            CreateListener();
            // check if hot reloaded
            if (hotReload)
            {
                foreach (CCSPlayerController? player in Utilities.GetPlayers().Where(static p => !p.IsBot && !p.IsHLTV))
                {
                    AddPlayerToList(player);
                }
                EnableParachutes();
            }
            Console.WriteLine(Localizer["parachute.loaded"]);
        }

        public override void Unload(bool hotReload)
        {
            DisableParachutes(true);
            RemoveListener();
            Console.WriteLine(Localizer["parachute.unloaded"]);
        }

        private void CreateListener()
        {
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

        private void DisableParachutes(bool force = false)
        {
            _state = ParachuteState.Disabled;
            RemoveListener<Listeners.OnTick>(ListenerOnTick);
            foreach (PlayerData data in _playerData.Values)
            {
                if (data.Parachute != null)
                {
                    Prop.RemoveParachute(data.Parachute);
                    data.Parachute = null;
                    data.NextSoundTime = 0;
                }
            }

            if (force)
            {
                _playerData.Clear();
            }
        }

        private void EnableParachutes()
        {
            if (_state != ParachuteState.Disabled || !Config.Enabled)
            {
                return;
            }

            _state = ParachuteState.Enabled;
            RegisterListener<Listeners.OnTick>(ListenerOnTick);
            Server.PrintToChatAll(Localizer["parachute.readyChat"]);
            Utilities.GetPlayers()
                .Where(player => player.IsValid && !player.IsBot && !player.IsHLTV)
                .ToList()
                .ForEach(player => player.PrintToCenter(Localizer["parachute.readyCenter"]));
        }

        private void ListenerOnTick()
        {
            foreach (KeyValuePair<CCSPlayerController, PlayerData> kvp in _playerData)
            {
                CCSPlayerController player = kvp.Key;
                PlayerData data = kvp.Value;
                CBasePlayerPawn? pawn = player.Pawn.Value;

                if (pawn == null)
                {
                    // Clean up if pawn is invalid
                    if (data.Parachute != null)
                    {
                        Prop.RemoveParachute(data.Parachute);
                        data.Parachute = null;
                    }
                    continue;
                }

                bool shouldHave = (player.Buttons & PlayerButtons.Use) != 0 &&
                                pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE &&
                                pawn.GroundEntity.Value == null &&
                                pawn.MoveType != MoveType_t.MOVETYPE_LADDER &&
                                (!Config.DisableWhenCarryingHostage ||
                                 player.PlayerPawn?.Value?.HostageServices?.CarriedHostageProp.Value == null);

                bool hasParachute = data.Parachute != null;

                if (hasParachute && !shouldHave)
                {
                    Prop.RemoveParachute(data.Parachute);
                    data.Parachute = null;
                    data.NextSoundTime = 0;
                }
                else if (!hasParachute && shouldHave)
                {
                    data.Parachute = Prop.CreateParachute(Config, player);
                    data.NextSoundTime = 0;
                }
                else if (hasParachute && shouldHave)
                {
                    Vector velocity = pawn.AbsVelocity;

                    if (!Config.IsHoverboard)
                    {
                        if (velocity.Z < 0.0f)
                        {
                            velocity.Z = -Config.FallSpeed;
                        }
                    }
                    else
                    {
                        float viewAngleX = pawn.V_angle.X;
                        velocity.Z = viewAngleX < 0.0f ? velocity.Z * Config.HoverboardMovementModifier :
                                    viewAngleX > 0.0f ? -Config.HoverboardMovementModifier : 0.0f;
                    }

                    // Side movement
                    if ((player.Buttons & (PlayerButtons.Moveleft | PlayerButtons.Moveright)) != 0)
                    {
                        velocity.X *= Config.SideMovementModifier;
                        velocity.Y *= Config.SideMovementModifier;
                    }

                    // Sound handling
                    if (!string.IsNullOrEmpty(Config.ParachuteSound) && data.Parachute?.IsValid == true)
                    {
                        long currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                        if (data.NextSoundTime <= currentTime)
                        {
                            _ = data.Parachute.EmitSound(Config.ParachuteSound);
                            data.NextSoundTime = currentTime + (long)(Config.ParachuteSoundInterval * 1000L);
                        }
                    }
                }
            }
        }

        private void OnMapStart(string mapName)
        {
            DisableParachutes(true);
        }

        private void OnMapEnd()
        {
            DisableParachutes(true);
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
            if (player?.IsValid == true)
            {
                _ = _playerData.Remove(player);
            }
            return HookResult.Continue;
        }

        private HookResult EventOnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            DisableParachutes();
            return HookResult.Continue;
        }

        private HookResult EventOnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
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
            CCSPlayerController? player = @event.Userid;
            if (player?.IsValid == true && _playerData.TryGetValue(player, out PlayerData? data))
            {
                if (data.Parachute != null)
                {
                    Prop.RemoveParachute(data.Parachute);
                    data.Parachute = null;
                    data.NextSoundTime = 0;
                }
            }
            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (!Config.DisableOnRoundEnd)
            {
                return HookResult.Continue;
            }

            DisableParachutes();
            return HookResult.Continue;
        }

        private void AddPlayerToList(CCSPlayerController? player)
        {
            if (player?.IsValid == true && !player.IsBot && !_playerData.ContainsKey(player))
            {
                _playerData.Add(player, new PlayerData());
            }
        }
    }
}
