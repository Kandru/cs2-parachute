using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Drawing;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private int CreateParachute(CCSPlayerController player, string model, float scale = 1.0f)
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
            // set model
            prop.SetModel(_parachuteModels[model].Keys.First());
            // follow player
            prop.AcceptInput("FollowEntity", player.Pawn.Value, player.Pawn.Value, "!activator");
            // set scale
            prop.CBodyComponent!.SceneNode!.Scale = _parachuteModels[model].Values.First().Item2;
            // get parachute flags
            ParachuteFlags parachuteFlags = _parachuteModels[model].Values.First().Item1;
            // FLAG: SetTeamColor
            if ((parachuteFlags & ParachuteFlags.SetTeamColor) != 0)
                if (player.Team == CsTeam.Terrorist)
                {
                    prop.Render = Color.FromArgb(255, Random.Shared.Next(100, 256), 0, 0);
                }
                else if (player.Team == CsTeam.CounterTerrorist)
                {
                    prop.Render = Color.FromArgb(255, 0, 0, Random.Shared.Next(100, 256));
                }
            return (int)prop.Index;
        }

        private void RemoveParachute(int index)
        {
            var prop = Utilities.GetEntityFromIndex<CDynamicProp>((int)index);
            if (prop == null
            || !prop.IsValid) return;
            // remove prop
            prop.Remove();
        }
    }
}