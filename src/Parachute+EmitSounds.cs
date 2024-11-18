using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private static MemoryFunctionVoid<CBaseEntity, string, int, float, float>? CBaseEntity_EmitSoundParamsFunc = null;

        private void InitializeEmitSound()
        {
            string FUNC_SIGNATURE = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FUNC_SIGNATURE = "\\x48\\x8B\\xC4\\x48\\x89\\x58\\x2A\\x48\\x89\\x70\\x2A\\x55\\x57\\x41\\x56\\x48\\x8D\\xA8\\x2A\\x2A\\x2A\\x2A\\x48\\x81\\xEC\\x2A\\x2A\\x2A\\x2A\\x45\\x33\\xF6";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                FUNC_SIGNATURE = "\\x48\\xB8\\x2A\\x2A\\x2A\\x2A\\x2A\\x2A\\x2A\\x2A\\x55\\x48\\x89\\xE5\\x41\\x55\\x41\\x54\\x49\\x89\\xFC\\x53\\x48\\x89\\xF3";
            }
            else
            {
                return;
            }
            try
            {
                CBaseEntity_EmitSoundParamsFunc = new(FUNC_SIGNATURE);
            }
            catch (Exception e)
            {
                // log error
                Console.WriteLine(Localizer["core.error"].Value.Replace("{error}", e.Message));
            }
        }

        public void EmitSound(CBaseEntity entity, string soundEventName, int pitch = 1, float volume = 1f, float delay = 1f)
        {
            if (entity is null
            || entity.IsValid is not true
            || string.IsNullOrEmpty(soundEventName) is true
            || CBaseEntity_EmitSoundParamsFunc is null) return;
            //invoke play sound from an entity
            try
            {
                CBaseEntity_EmitSoundParamsFunc.Invoke(entity, soundEventName, pitch, volume, delay);
            }
            catch (Exception e)
            {
                // log error
                Console.WriteLine(Localizer["core.error"].Value.Replace("{error}", e.Message));
            }
        }
    }
}
