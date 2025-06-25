using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private readonly List<string> _precacheModels = [];

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            // add static models to precache
            foreach (string model in _precacheModels)
            {
                manifest.AddResource(model);
            }
        }
    }
}
