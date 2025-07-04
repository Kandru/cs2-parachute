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
            // sanity checks
            if (player == null
            || player.Pawn == null
            || !player.Pawn.IsValid
            || player.Pawn.Value == null
            || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            {
                return null;
            }
            // create dynamic prop
            CDynamicProp? prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override")!;
            if (prop == null
                || !prop.IsValid)
            {
                return null;
            }
            // set attributes
            prop.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            prop.Collision.SolidType = SolidType_t.SOLID_NONE;
            prop.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
            prop.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
            // spawn it
            prop.DispatchSpawn();
            // set model
            prop.SetModel(Config.ParachuteModel);
            // set scale
            prop.CBodyComponent!.SceneNode!.Scale = Config.ParachuteModelSize;
            // follow player
            prop.AcceptInput("FollowEntity", player.Pawn.Value, player.Pawn.Value, "!activator");
            // SetTeamColor
            if (Config.EnableTeamColors)
            {
                if (player.Team == CsTeam.Terrorist)
                {
                    prop.Render = Color.FromArgb(255, Random.Shared.Next(100, 256), 0, 0);
                }
                else if (player.Team == CsTeam.CounterTerrorist)
                {
                    prop.Render = Color.FromArgb(255, 0, 0, Random.Shared.Next(100, 256));
                }
            }

            return prop;
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