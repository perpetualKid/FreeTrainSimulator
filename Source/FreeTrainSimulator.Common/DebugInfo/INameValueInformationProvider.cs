using System.Collections.Generic;

namespace FreeTrainSimulator.Common.DebugInfo
{

    public interface INameValueInformationProvider
    {
        public InformationDictionary DetailInfo { get; }

        public Dictionary<string, FormatOption> FormattingOptions { get; }
    }
}
