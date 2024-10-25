using Smartstore.Engine;
using Smartstore.Engine.Builders;

namespace Smartstore.Pays
{
    internal class Startup : StarterBase
    {
        public override bool Matches(IApplicationContext appContext)
            => appContext.IsInstalled;
    }
}