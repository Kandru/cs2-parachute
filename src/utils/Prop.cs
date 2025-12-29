using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Drawing;

namespace Parachute.Utils
{
    public static class Prop
    {
        public static CDynamicProp? CreateParachute(PluginConfig Config, CCSPlayerController player)
        {
            // Early validation - combine checks for better performance
            CBasePlayerPawn? pawn = player?.Pawn?.Value;
            if (pawn?.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            {
                return null;
            }

            // create dynamic prop
            CDynamicProp? prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
            if (prop?.IsValid != true)
            {
                return null;
            }

            // Configure collision and movement
            prop.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            prop.Collision.SolidType = SolidType_t.SOLID_NONE;
            prop.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
            prop.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;

            // spawn it
            prop.DispatchSpawn();

            // Set attributes in logical order (most important first)
            prop.SetModel(Config.Parachute.ParachuteModel);
            prop.CBodyComponent!.SceneNode!.Scale = Config.Parachute.ParachuteModelSize;

            // follow player
            prop.AcceptInput("FollowEntity", pawn, pawn, "!activator");

            // SetTeamColor (optimized team color logic)
            if (Config.Parachute.EnableTeamColors && player?.Team != null)
            {
                SetTeamColor(prop, player.Team);
            }

            return prop;
        }

        private static void SetTeamColor(CDynamicProp prop, CsTeam team)
        {
            Color color = team switch
            {
                CsTeam.Terrorist => Color.FromArgb(255, Random.Shared.Next(100, 256), 0, 0),
                CsTeam.CounterTerrorist => Color.FromArgb(255, 0, 0, Random.Shared.Next(100, 256)),
                _ => Color.White
            };
            prop.Render = color;
        }

        public static void RemoveParachute(CDynamicProp? prop)
        {
            if (prop == null
                || !prop.IsValid)
            {
                return;
            }
            // remove prop
            prop.Remove();
        }
    }
}