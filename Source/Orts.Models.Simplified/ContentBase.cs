using GetText;

using Orts.Common.Info;

namespace Orts.Models.Simplified
{
    public abstract class ContentBase
    {
        internal static ICatalog catalog = new Catalog("Orts.Models.Simplified", RuntimeInfo.LocalesFolder);

        protected const string Unknown = "unknown";
    }
}
