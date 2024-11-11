using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Drawing;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private int SpawnProp(CCSPlayerController player, string model, float scale = 1.0f)
        {
            // sanity checks
            if (player == null
            || player.Pawn == null || !player.Pawn.IsValid || player.Pawn.Value == null
            || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return -1;
            // create dynamic prop
            CDynamicProp prop;
            prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override")!;
            // set attributes
            prop.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            prop.Collision.SolidType = SolidType_t.SOLID_NONE;
            prop.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
            prop.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NONE;
            // spawn it
            prop.DispatchSpawn();
            prop.SetModel(model);
            prop.Teleport(new Vector(-999, -999, -999));
            prop.AnimGraphUpdateEnabled = false;
            prop.CBodyComponent!.SceneNode!.Scale = scale;
            // set team color
            if (player.Team == CsTeam.Terrorist)
            {
                prop.Render = Color.FromArgb(255, Random.Shared.Next(30, 256), 0, 0);
            }
            else if (player.Team == CsTeam.CounterTerrorist)
            {
                prop.Render = Color.FromArgb(255, 0, 0, Random.Shared.Next(30, 256));
            }
            return (int)prop.Index;
        }

        private void UpdateProp(CCSPlayerController player, int index, int offset_z = 0, int offset_angle = 0)
        {
            var prop = Utilities.GetEntityFromIndex<CDynamicProp>((int)index);
            // sanity checks
            if (prop == null
            || player == null
            || player.Pawn == null || !player.Pawn.IsValid || player.Pawn.Value == null
            || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;
            // get player pawn
            var playerPawn = player!.Pawn!.Value;
            // teleport it to player
            Vector playerOrigin = new Vector(
                (float)Math.Round(playerPawn.AbsOrigin!.X, 5),
                (float)Math.Round(playerPawn.AbsOrigin!.Y, 5),
                (float)Math.Round(playerPawn.AbsOrigin!.Z, 5) + offset_z
            );
            Vector propOrigin = new Vector(
                (float)Math.Round(prop.AbsOrigin!.X, 5),
                (float)Math.Round(prop.AbsOrigin!.Y, 5),
                (float)Math.Round(prop.AbsOrigin!.Z, 5)
            );
            QAngle playerRotation = new QAngle(
                0,
                (float)Math.Round(playerPawn.AbsRotation!.Y, 5) + offset_angle,
                0
            );
            QAngle propRotation = new QAngle(
                0,
                (float)Math.Round(prop.AbsRotation!.Y, 5),
                0
            );
            if (playerOrigin.X == propOrigin.X
                && playerOrigin.Y == propOrigin.Y
                && playerOrigin.Z == propOrigin.Z
                && playerRotation.Y == propRotation.Y) return;
            prop.Teleport(playerOrigin, playerRotation);
        }

        private void RemoveProp(int index, bool softRemove = false)
        {
            var prop = Utilities.GetEntityFromIndex<CDynamicProp>((int)index);
            // remove plant entity
            if (prop == null)
                return;
            if (softRemove)
            {
                if (prop.AbsOrigin!.X == -999
                    && prop.AbsOrigin!.Y == -999
                    && prop.AbsOrigin!.Z == -999)
                    return;
                prop.Teleport(new Vector(-999, -999, -999));
            }
            else
                prop.Remove();
        }
    }
}