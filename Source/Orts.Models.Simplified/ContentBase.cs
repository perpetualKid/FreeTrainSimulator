using GetText;

namespace Orts.Menu.Entities
{
    public abstract class ContentBase
    {
        internal static ICatalog catalog = new Catalog("Orts.Menu.Entities");

        protected const string Unknown = "unknown";
    }
}
