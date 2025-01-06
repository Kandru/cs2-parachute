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
            prop.CBodyComponent!.SceneNode!.Scale = _parachuteModels[_parachutePlayers[player.UserId ?? -1]["type"]].Values.First().Item2;
            // get parachute flags
            ParachuteFlags parachuteFlags = _parachuteModels[_parachutePlayers[player.UserId ?? -1]["type"]].Values.First().Item1;
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

        private void UpdateProp(CCSPlayerController player, int index)
        {
            var prop = Utilities.GetEntityFromIndex<CDynamicProp>((int)index);
            // sanity checks
            if (prop == null
            || player == null
            || player.Pawn == null || !player.Pawn.IsValid || player.Pawn.Value == null
            || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;
            // get player pawn
            var playerPawn = player!.Pawn!.Value;
            // set vectors and rotation
            Vector playerOrigin = new(
                playerPawn.AbsOrigin!.X,
                playerPawn.AbsOrigin!.Y,
                playerPawn.AbsOrigin!.Z
            );
            QAngle playerRotation = new(
                playerPawn.V_angle!.X,
                playerPawn.V_angle!.Y,
                playerPawn.V_angle!.Z
            );
            QAngle propRotation = new(
                prop.AbsRotation!.X,
                prop.AbsRotation!.Y,
                prop.AbsRotation!.Z
            );
            // get parachute flags
            ParachuteFlags parachuteFlags = _parachuteModels[_parachutePlayers[player.UserId ?? -1]["type"]].Values.First().Item1;
            // FLAG: MountAsBackpack
            if ((parachuteFlags & ParachuteFlags.MountAsBackpack) != 0)
            {
                // rotate prop 90 degrees
                propRotation.X = 90;
                // vertical rotation with the player
                propRotation.Y = playerRotation.Y;
                // move prop 50 units higher
                playerOrigin.Z += 50;
                // Calculate the backwards vector
                var backward = new Vector(
                    -MathF.Sin(playerRotation.Y * (MathF.PI / 180)),
                    MathF.Cos(playerRotation.Y * (MathF.PI / 180)),
                    0
                );
                // calculate the right vector as orthogonal to the backward vector
                var right = new Vector(
                    backward.Y,
                    -backward.X,
                    0
                );
                // move prop 50 units backwards and 10 units to the right
                playerOrigin += (backward * 5) + (right * -20);
            }
            // FLAG: MountAsCarpet
            if ((parachuteFlags & ParachuteFlags.MountAsCarpet) != 0)
            {
                // vertical rotation with the player
                propRotation.Y = playerRotation.Y;
                // rotate prop 90 degrees
                propRotation.Y += 90;
                // move prop 50 units lower
                playerOrigin.Z -= 30;
                // Calculate the backwards vector
                var backward = new Vector(
                    -MathF.Sin(playerRotation.Y * (MathF.PI / 180)),
                    MathF.Cos(playerRotation.Y * (MathF.PI / 180)),
                    0
                );
                // calculate the right vector as orthogonal to the backward vector
                var right = new Vector(
                    backward.Y,
                    -backward.X,
                    0
                );
                playerOrigin += (backward * 3) + (right * 20);
            }
            // FLAG: IsAirplane
            if ((parachuteFlags & ParachuteFlags.IsAirplane) != 0)
            {
                // calculate tilt based on player's movement and angle
                float tiltSpeed = 1.0f; // Speed at which tilt angle is adjusted
                float maxTiltAngle = 55.0f; // Maximum tilt angle in degrees
                float maxSpeedForFullTilt = 2.0f; // Speed at which full tilt is achieved
                float tiltAngle;
                float targetTiltAngle;
                float currentTiltAngle = prop.AbsRotation.Z;
                // Calculate the difference in angle
                float angleDifference = playerPawn.V_angle.Y - playerPawn.V_anglePrevious.Y;
                // Determine target tilt angle based on player's look direction and speed
                if (Math.Abs(angleDifference) > 0.1f) // Check if player is looking left or right
                {
                    float speedFactor = Math.Clamp(Math.Abs(angleDifference) / maxSpeedForFullTilt, 0.0f, 1.0f);
                    targetTiltAngle = maxTiltAngle * speedFactor * -Math.Sign(angleDifference);
                }
                else
                {
                    targetTiltAngle = 0; // Player is not moving, reset tilt angle
                }
                // Smoothly adjust tilt angle to target tilt angle
                if (currentTiltAngle < targetTiltAngle)
                {
                    tiltAngle = Math.Min(currentTiltAngle + tiltSpeed, targetTiltAngle); // Adjust tilt angle positively
                }
                else if (currentTiltAngle > targetTiltAngle)
                {
                    tiltAngle = Math.Max(currentTiltAngle - tiltSpeed, targetTiltAngle); // Adjust tilt angle negatively
                }
                else
                {
                    tiltAngle = currentTiltAngle; // No change needed
                }
                // Set the tilt angle
                propRotation.Z = tiltAngle;
                // vertical rotation with the player
                propRotation.Y = playerRotation.Y;
                // move prop 10 units lower
                playerOrigin.Z -= 10;
                // Calculate the backwards vector
                var backward = new Vector(
                    -MathF.Sin(playerRotation.Y * (MathF.PI / 180)),
                    MathF.Cos(playerRotation.Y * (MathF.PI / 180)),
                    0
                );
                // calculate the right vector as orthogonal to the backward vector
                var right = new Vector(
                    backward.Y,
                    -backward.X,
                    0
                );
                playerOrigin += right * 20;
            }
            // FLAG: IsVehicle
            if ((parachuteFlags & ParachuteFlags.IsVehicle) != 0)
            {
                // vertical rotation with the player
                propRotation.Y = playerRotation.Y;
                // rotate prop 90 degrees
                propRotation.Y -= 90;
                // move prop 10 units lower
                playerOrigin.Z -= 5;
            }
            prop.Teleport(playerOrigin, propRotation, player.Pawn.Value.AbsVelocity);
        }

        private void RemoveProp(int index, bool softRemove = false)
        {
            var prop = Utilities.GetEntityFromIndex<CDynamicProp>((int)index);
            // remove plant entity
            if (prop == null || !prop.IsValid || prop.Handle == IntPtr.Zero || prop.AbsOrigin == null) return;
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

        private float MathLerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat * (1 - by) + secondFloat * by;
        }
    }
}