using System.Collections.Generic;

namespace Orts.Common.DebugInfo
{
    /// <summary>
    /// Specialized dictionary which does not throw but returns null if a key was not found 
    /// </summary>
    public class InformationDictionary : Dictionary<string, string>
    {
        public new string this[string key]
        {
            get
            {
                if (!TryGetValue(key, out string result))
                    base[key] = result = null;
                return result;

            }
            set { base[key] = value; }
        }
    }

    public interface INameValueInformationProvider
    {
        public InformationDictionary DetailInfo { get; }

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public INameValueInformationProvider Next { get => null; }

        public int MultiElementCount { get => 0; }
    }
}
