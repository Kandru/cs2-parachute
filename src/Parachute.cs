using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Parachute.Enums;
using Parachute.Classes;
using Parachute.Utils;
using System.Globalization;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Modules.Utils;


namespace Parachute
{
    public partial class Parachute : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "CS2 Parachute";
        public override string ModuleAuthor => "Originally by Franc1sco Franug / rewritten by Jon-Mailes Graeffe <mail@jonni.it> and Kalle <kalle@kandru.de>";

        private readonly Dictionary<CCSPlayerController, PlayerData> _playerData = [];
        private ParachuteState _state = ParachuteState.Disabled;

        private const int CACHE_UPDATE_INTERVAL = 4;
        private const byte LIFE_ALIVE = (byte)LifeState_t.LIFE_ALIVE;
        private const int USE_BUTTON = (int)PlayerButtons.Use;
        private const PlayerButtons MOVEMENT_BUTTONS = PlayerButtons.Moveleft | PlayerButtons.Moveright;

        public override void Load(bool hotReload)
        {
            if (!string.IsNullOrEmpty(Config.Parachute.ParachuteModel))
            {
                _precacheModels.Add(Config.Parachute.ParachuteModel);
            }

            if (!Config.Enabled)
            {
                Console.WriteLine(Localizer["parachute.disabled"]);
                return;
            }

            CreateListener();

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
                    data.Reset();
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

            LocalizedString readyCenterMsg = Localizer["parachute.readyCenter"];
            foreach (CCSPlayerController? player in Utilities.GetPlayers().Where(static p => !p.IsBot && !p.IsHLTV))
            {
                player.PrintToCenter(readyCenterMsg);
            }
        }

        private void ListenerOnTick()
        {
            foreach (var kvp in _playerData.ToList())
            {
                var player = kvp.Key;
                var data = kvp.Value;

                if (!player.IsValid || player.Pawn?.Value == null)
                {
                    if (data.Parachute != null)
                    {
                        Prop.RemoveParachute(data.Parachute);
                        data.Reset();
                    }
                    _playerData.Remove(player);
                    continue;
                }

                var pawn = player.Pawn.Value;
                data.TicksSinceLastUpdate++;

                ProcessPlayerParachute(player, data, pawn);
            }
        }

        private void ProcessPlayerParachute(CCSPlayerController player, PlayerData data, CBasePlayerPawn pawn)
        {
            int currentButtons = (int)player.Buttons;
            byte currentLifeState = pawn.LifeState;
            bool isOnGround = pawn.GroundEntity.Value != null;
            bool forceUpdate = data.TicksSinceLastUpdate >= CACHE_UPDATE_INTERVAL;

            if (!forceUpdate && data.LastButtonState == currentButtons &&
                data.LastLifeState == currentLifeState && data.LastGroundState == isOnGround)
            {
                if (data.Parachute != null)
                {
                    UpdateParachutePhysics(player, data, pawn);
                }

                return;
            }

            data.LastButtonState = currentButtons;
            data.LastLifeState = currentLifeState;
            data.LastGroundState = isOnGround;
            data.TicksSinceLastUpdate = 0;

            bool shouldHaveParachute = (currentButtons & USE_BUTTON) != 0 &&
                                    currentLifeState == LIFE_ALIVE &&
                                    !isOnGround &&
                                    pawn.MoveType != MoveType_t.MOVETYPE_LADDER &&
                                    (!Config.DisableWhenCarryingHostage ||
                                     player.PlayerPawn?.Value?.HostageServices?.CarriedHostageProp.Value == null);

            bool hasParachute = data.Parachute != null;

            if (hasParachute && !shouldHaveParachute)
            {
                RemovePlayerParachute(data);
            }
            else if (!hasParachute && shouldHaveParachute)
            {
                CreatePlayerParachute(player, data);
            }
            else if (hasParachute)
            {
                UpdateParachutePhysics(player, data, pawn);
            }
        }

        private void OnMapStart(string mapName)
        {
            DisableParachutes(true);
        }

        private static void RemovePlayerParachute(PlayerData data)
        {
            if (data.Parachute != null)
            {
                Prop.RemoveParachute(data.Parachute);
                data.Parachute = null;
                data.NextSoundTime = 0;
            }
        }

        private void CreatePlayerParachute(CCSPlayerController player, PlayerData data)
        {
            data.Parachute = Prop.CreateParachute(Config, player);
            data.NextSoundTime = 0;
        }

        private void UpdateParachutePhysics(CCSPlayerController player, PlayerData data, CBasePlayerPawn pawn)
        {
            Vector velocity = pawn.AbsVelocity;

            if (!Config.Parachute.IsHoverboard)
            {
                if (velocity.Z < 0.0f)
                {
                    velocity.Z = -Config.Parachute.FallSpeed;
                }
            }
            else
            {
                float viewAngleX = pawn.V_angle.X;
                velocity.Z = viewAngleX < 0.0f ? velocity.Z * Config.Parachute.HoverboardMovementModifier :
                           viewAngleX > 0.0f ? -Config.Parachute.HoverboardMovementModifier : 0.0f;
            }

            if ((player.Buttons & MOVEMENT_BUTTONS) != 0)
            {
                velocity.X *= Config.Parachute.SideMovementModifier;
                velocity.Y *= Config.Parachute.SideMovementModifier;
            }

            if (!string.IsNullOrEmpty(Config.Parachute.ParachuteSound) && data.Parachute?.IsValid == true)
            {
                long currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                if (data.NextSoundTime <= currentTime)
                {
                    _ = data.Parachute.EmitSound(Config.Parachute.ParachuteSound);
                    data.NextSoundTime = currentTime + (long)(Config.Parachute.ParachuteSoundInterval * 1000L);
                }
            }
        }

        private void OnMapEnd()
        {
            DisableParachutes(true);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            AddPlayerToList(@event.Userid);
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            if (@event.Userid?.IsValid == true)
            {
                _ = _playerData.Remove(@event.Userid);
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
                Server.PrintToChatAll(Localizer["parachute.delay"].Value.Replace("{seconds}", Config.RoundStartDelay.ToString(CultureInfo.CurrentCulture)));
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
            if (@event.Userid?.IsValid == true && _playerData.TryGetValue(@event.Userid, out PlayerData? data) && data.Parachute != null)
            {
                Prop.RemoveParachute(data.Parachute);
                data.Reset();
            }
            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (Config.DisableOnRoundEnd)
            {
                DisableParachutes();
            }

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
