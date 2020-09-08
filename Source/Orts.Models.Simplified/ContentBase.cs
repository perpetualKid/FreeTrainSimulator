using GetText;

namespace Orts.Models.Simplified
{
    public abstract class ContentBase
    {
        internal static ICatalog catalog = new Catalog("Orts.Menu.Entities");

        protected const string Unknown = "unknown";
    }
}
