using GetText;

namespace Orts.Models.Simplified
{
    public abstract class ContentBase
    {
        internal static ICatalog catalog = new Catalog("Orts.Models.Simplified");

        protected const string Unknown = "unknown";
    }
}
