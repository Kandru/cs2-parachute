using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private readonly List<string> _precacheModels = new List<string>
        {
            "models/props_survival/parachute/chute.vmdl",
        };

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            foreach (var model in _precacheModels)
            {
                manifest.AddResource(model);
            }
        }
    }
}
