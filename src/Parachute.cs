using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.VisualBasic.CompilerServices;

namespace Parachute;

public class ConfigGen : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 32;
    [JsonPropertyName("AccessFlag")] public string AccessFlag { get; set; } = "";
    [JsonPropertyName("TeleportTicks")] public int TeleportTicks { get; set; } = 300;
    [JsonPropertyName("ParachuteModelEnabled")] public bool ParachuteModelEnabled { get; set; } = false;
    [JsonPropertyName("ParachuteModel")] public string ParachuteModel { get; set; } = "models/props_survival/parachute/chute.vmdl";
    [JsonPropertyName("SideMovementModifier")] public float SideMovementModifier { get; set; } = 1.0075f;
    [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
}

[MinimumApiVersion(139)]
public class Parachute : BasePlugin, IPluginConfig<ConfigGen>
{
    public override string ModuleName => "CS2 Parachute";
    public override string ModuleAuthor => "Franc1sco Franug";
    public override string ModuleVersion => "1.4-kandru";


    public ConfigGen Config { get; set; } = null!;
    public void OnConfigParsed(ConfigGen config) { Config = config; }

    private readonly Dictionary<int?, bool> bUsingPara = new();
    private readonly Dictionary<int?, int> gParaTicks = new();
    private readonly Dictionary<int?, CBaseEntity?> gParaModel = new();
    private bool bParaAllowed;

    public override void Load(bool hotReload)
    {
        if (!Config.Enabled)
        {
            Console.WriteLine("[Parachute] Plugin not enabled!");
            return;
        }

        if (hotReload)
        {
            Utilities.GetPlayers().ForEach(player =>
            {
                bUsingPara.Add(player.UserId, false);
                gParaTicks.Add(player.UserId, 0);
                gParaModel.Add(player.UserId, null);
            });
            
            bParaAllowed = true;
            Server.PrintToChatAll(ChatColors.Orange + "[Parachute] " + ChatColors.Default + "Parachute is now ready to go!");
        }
        
        RegisterListener<Listeners.OnMapStart>(map =>
        {
            if (Config.ParachuteModelEnabled)
            {
                Server.PrecacheModel(Config.ParachuteModel);
            }
        });

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;

            if (player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                bUsingPara.Add(player.UserId, false);
                gParaTicks.Add(player.UserId, 0);
                gParaModel.Add(player.UserId, null);
                return HookResult.Continue;
            }
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (player == null || player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                if (bUsingPara.ContainsKey(player.UserId))
                {
                    bUsingPara.Remove(player.UserId);
                }
                if (gParaTicks.ContainsKey(player.UserId))
                {
                    gParaTicks.Remove(player.UserId);
                }
                if (gParaModel.ContainsKey(player.UserId))
                {
                    gParaModel.Remove(player.UserId);
                }
                return HookResult.Continue;
            }
        });


        RegisterListener<Listeners.OnTick>(() =>
        {
            if (!bParaAllowed) return;
            
            var players = Utilities.GetPlayers();

            foreach (var player in players)
            {
                if (player != null
                && player.IsValid
                && !player.IsBot
                && player.PawnIsAlive
                && (Config.AccessFlag == "" || AdminManager.PlayerHasPermissions(player, Config.AccessFlag)))
                {
                    var buttons = player.Buttons;
                    if ((buttons & PlayerButtons.Use) != 0 && !player.PlayerPawn.Value.OnGroundLastTick)
                    {
                        StartPara(player);

                    } 
                    else if (bUsingPara[player.UserId])
                    {
                        bUsingPara[player.UserId] = false;
                        StopPara(player);
                    }
                }
            }
        });

        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Userid;

            if (bUsingPara[player.UserId])
            {
                bUsingPara[player.UserId] = false;
                StopPara(player);
            }
            return HookResult.Continue;
        }, HookMode.Pre);

        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            bParaAllowed = false;
            
            Server.PrintToChatAll(ChatColors.Orange + "[Parachute] " + ChatColors.Default + "Parachute available in " + Config.RoundStartDelay + " seconds!");
            AddTimer(Config.RoundStartDelay, () =>
            {
                bParaAllowed = true;
                Server.PrintToChatAll(ChatColors.Orange + "[Parachute] " + ChatColors.Default + "Parachute is now ready to go. Press 'E' while in the air to use!");
                Utilities.GetPlayers().ForEach(player => player.PrintToCenter("Parachute ready to go!"));
            });
            
            return HookResult.Continue;
        }, HookMode.Pre);
    }

    private void StopPara(CCSPlayerController player)
    {
        player.GravityScale = 1.0f;
        gParaTicks[player.UserId] = 0;
        if (gParaModel[player.UserId] != null && gParaModel[player.UserId].IsValid)
        {
            gParaModel[player.UserId].Remove();
            gParaModel[player.UserId] = null;
        }
    }

    private void StartPara(CCSPlayerController player)
    {
        if (!bUsingPara[player.UserId])
        {
            bUsingPara[player.UserId] = true;
            player.GravityScale = 0.1f;
            if (Config.ParachuteModelEnabled)
            {
                var entity = Utilities.CreateEntityByName<CBaseProp>("prop_dynamic_override");
                if (entity != null && entity.IsValid)
                {
                    entity.SetModel(Config.ParachuteModel);
                    entity.MoveType = MoveType_t.MOVETYPE_NOCLIP;
                    entity.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
                    entity.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
                    entity.DispatchSpawn();

                    gParaModel[player.UserId] = entity;
                }
            }
        }

        var velocity = player.PlayerPawn.Value.AbsVelocity;

        if (velocity.Z < 0.0f)
        {
            if ((player.Buttons & PlayerButtons.Moveleft) != 0 || (player.Buttons & PlayerButtons.Moveright) != 0)
            {
                velocity.X *= Config.SideMovementModifier;
                velocity.Y *= Config.SideMovementModifier;
            }
            velocity.Z =  Config.FallSpeed * (-1.0f);

            var position = player.PlayerPawn.Value.AbsOrigin!;
            var angle = player.PlayerPawn.Value.AbsRotation!;

            if (gParaTicks[player.UserId] > Config.TeleportTicks)
            {
                player.Teleport(position, angle, velocity);
                gParaTicks[player.UserId] = 0;
            }

            if (gParaModel[player.UserId] != null && gParaModel[player.UserId].IsValid)
            {
                gParaModel[player.UserId].Teleport(position, angle, velocity);
            }

            ++gParaTicks[player.UserId];
        }
    }
}

