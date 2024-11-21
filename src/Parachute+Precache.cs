using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Parachute
{
    public partial class Parachute : BasePlugin
    {
        private readonly List<string> _precacheModels = new List<string>
        {
        };

        private void OnServerPrecacheResources(ResourceManifest manifest)
        {
            // add static models to precache
            foreach (var model in _precacheModels)
            {
                manifest.AddResource(model);
            }
            // add _parachuteModels to precache
            foreach (var parachuteModel in _parachuteModels)
            {
                foreach (var model in parachuteModel.Value)
                {
                    manifest.AddResource(model.Key);
                }
            }
        }
    }
}
