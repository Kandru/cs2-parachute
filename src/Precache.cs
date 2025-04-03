using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private List<string> _precacheModels = [];

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            // add static models to precache
            foreach (var model in _precacheModels)
            {
                manifest.AddResource(model);
            }
        }
    }
}
