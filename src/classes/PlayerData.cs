using CounterStrikeSharp.API.Core;

namespace Parachute.Classes
{
    public class PlayerData
    {
        public CDynamicProp? Parachute;
        public long NextSoundTime;

        public int LastButtonState;
        public byte LastLifeState;
        public bool LastGroundState;

        public int TicksSinceLastUpdate;

        public void Reset()
        {
            Parachute = null;
            NextSoundTime = 0;
            LastButtonState = 0;
            LastLifeState = 0;
            LastGroundState = false;

            TicksSinceLastUpdate = 0;
        }
    }
}