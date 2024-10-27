using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;

namespace ParachutePlugin;

public class ConfigGen : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("FallSpeed")] public float FallSpeed { get; set; } = 32;
    [JsonPropertyName("AccessFlag")] public string AccessFlag { get; set; } = "";
    [JsonPropertyName("TeleportTicks")] public int TeleportTicks { get; set; } = 300;
    [JsonPropertyName("ParachuteModelEnabled")] public bool ParachuteModelEnabled { get; set; } = true;
    [JsonPropertyName("ParachuteModel")] public string ParachuteModel { get; set; } = "models/props_survival/parachute/chute.vmdl";
    [JsonPropertyName("SideMovementModifier")] public float SideMovementModifier { get; set; } = 1.0075f;
    [JsonPropertyName("RoundStartDelay")] public int RoundStartDelay { get; set; } = 10;
    [JsonPropertyName("DisableWhenCarryingHostage")] public bool DisableWhenCarryingHostage { get; set; } = false;
    [JsonPropertyName("DisableForBots")] public bool DisableForBots { get; set; } = false;
}

[MinimumApiVersion(179)]
public class ParachutePlugin : BasePlugin, IPluginConfig<ConfigGen>
{
    private const int MAX_PLAYERS = 256;
    
    public override string ModuleName => "CS2 Parachute";
    public override string ModuleAuthor => "Franc1sco Franug";
    public override string ModuleVersion => "1.5.2-kandru";


    public ConfigGen Config { get; set; } = null!;
    public void OnConfigParsed(ConfigGen config) { Config = config; }

    private bool[] bUsingPara = new bool[MAX_PLAYERS];
    private int[] gParaTicks = new int[MAX_PLAYERS];
    private CBaseEntity?[] gParaModel = new CBaseEntity?[MAX_PLAYERS];

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
            bParaAllowed = true;
            PrintToChatAll("Parachute is ready to go. Press 'E' while in the air to use!");
        }

        RegisterListener<Listeners.OnMapStart>(map =>
        {
            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                 manifest.AddResource(Config.ParachuteModel);
            });
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;
            if (bUsingPara[(int)player.Index]) StopPara(player);

            return HookResult.Continue;
        });


        RegisterListener<Listeners.OnTick>(() =>
        {
            if (!bParaAllowed) return;
            
            var players = Utilities.GetPlayers();

            foreach (var player in players)
            {
                if (player == null || !player.IsValid || !player.PawnIsAlive) continue;
                if (Config.DisableForBots && player.IsBot) continue;
                if (Config.AccessFlag != "" &&
                    !AdminManager.PlayerHasPermissions(player, Config.AccessFlag)) continue;
                
                var buttons = player.Buttons;
                var pawn = player.Pawn.Value!;
                var playerPawn = player.PlayerPawn.Value!;
                if ((buttons & PlayerButtons.Use) != 0 && pawn.GroundEntity.Value == null &&
                    (!Config.DisableWhenCarryingHostage || playerPawn.HostageServices!.CarriedHostageProp.Value == null))
                    StartPara(player);
                else if (bUsingPara[(int)player.Index])
                    StopPara(player);
            }
        });

        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Userid;
            if (bUsingPara[(int)player.Index])
                StopPara(player);

            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            bParaAllowed = false;
            
            PrintToChatAll("Parachute will be available in " + Config.RoundStartDelay + " seconds!");
            AddTimer(Config.RoundStartDelay, () =>
            {
                bParaAllowed = true;
                PrintToChatAll("Parachute is ready to go. Press 'E' while in the air to use!");
                Utilities.GetPlayers().ForEach(player => player.PrintToCenter("Parachute ready to go!"));
            });
            
            return HookResult.Continue;
        }, HookMode.Pre);
    }

    private void StopPara(CCSPlayerController player)
    {
        var pawn = player.Pawn.Value!;
        
        bUsingPara[(int)player.Index] = false;
        
        if (gParaModel[(int)player.Index] != null && gParaModel[(int)player.Index]!.IsValid)
        {
            gParaModel[(int)player.Index]?.Remove();
            gParaModel[(int)player.Index] = null;
        }
    }

    private void StartPara(CCSPlayerController player)
    {
        var pawn = player.Pawn.Value!;
        
        if (!bUsingPara[(int)player.Index])
        {
            bUsingPara[(int)player.Index] = true;
            gParaTicks[(int)player.Index] = 0;
            if (Config.ParachuteModelEnabled)
            {
                var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
                if (entity != null && entity.IsValid)
                {
                    entity.MoveType = MoveType_t.MOVETYPE_NOCLIP;
                    entity.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
                    entity.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
                    // entity.RenderMode = RenderMode_t.kRenderNormal; // shadows test - dont work
                    entity.DispatchSpawn();

                    entity.SetModel(Config.ParachuteModel);
                    //entity.ShadowStrength = 1.0f; // shadows test - dont work
                    //entity.ShapeType = 0; // shadows test - dont work
                    //entity.AcceptInput("EnableShadow"); // shadows test - dont work
                    //entity.AcceptInput("EnableReceivingFlashlight"); // shadows test - dont work

                    // CBaseEntity_SetParent(entity, player); // need fix
                    gParaModel[(int)player.Index] = entity;
                }
            }
        }

        var velocity = pawn.AbsVelocity;

        if (velocity.Z < 0.0f)
        {
            if ((player.Buttons & PlayerButtons.Moveleft) != 0 || (player.Buttons & PlayerButtons.Moveright) != 0)
            {
                velocity.X *= Config.SideMovementModifier;
                velocity.Y *= Config.SideMovementModifier;
            }
            velocity.Z =  Config.FallSpeed * (-1.0f);

            var position = pawn.AbsOrigin!;
            var angle = pawn.AbsRotation!;

            if (gParaTicks[(int)player.Index] > Config.TeleportTicks)
            {
                pawn.Teleport(position, angle, velocity);
                gParaTicks[(int)player.Index] = 0;
            }

            if (gParaModel[(int)player.Index] != null && gParaModel[(int)player.Index]!.IsValid)
            {
                gParaModel[(int)player.Index]?.Teleport(position, angle, velocity);
            }

            ++gParaTicks[(int)player.Index];
        }
    }

    /* // dont work not sure why
    public static string setParentFuncWindowsSig = @"\x4D\x8B\xD9\x48\x85\xD2\x74\x2A";
    public static string setParentFuncLinuxSig = @"\x48\x85\xF6\x74\x2A\x48\x8B\x47\x10\xF6\x40\x31\x02\x75\x2A\x48\x8B\x46\x10\xF6\x40\x31\x02\x75\x2A\xB8\x2A\x2A\x2A\x2A";

    private static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken?, matrix3x4_t?> CBaseEntity_SetParentFunc
        = new(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? setParentFuncLinuxSig : setParentFuncWindowsSig);

    public static void CBaseEntity_SetParent(CBaseEntity childrenEntity, CBaseEntity parentEntity)
    {
        if (!childrenEntity.IsValid || !parentEntity.IsValid) return;

        var origin = parentEntity.AbsOrigin;
        var angle = parentEntity.AbsRotation!;
        CBaseEntity_SetParentFunc.Invoke(childrenEntity, parentEntity, null, null);
        // If not teleported, the childrenEntity will not follow the parentEntity correctly.
        childrenEntity.Teleport(origin, angle, new Vector(IntPtr.Zero));
        Console.WriteLine("CBaseEntity_SetParent() done!");
    }*/

    private void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($" {ChatColors.Orange}[Parachute] {ChatColors.Default}{msg}");
    }
}

