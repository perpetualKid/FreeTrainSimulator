using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
#pragma warning disable CA1010 // Generic interface should also be implemented
    public class DebugInfoBase : NameValueCollection, INameValueInformationProvider
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
        protected bool update { get; set; }

        public DebugInfoBase(bool includeFormattingOptions = false)
        {
            if (includeFormattingOptions)
                FormattingOptions = new Dictionary<string, FormatOption>();
        }

        public virtual void Update(GameTime gameTime)
        {
            if (update)
            {
                update = false;
            }
        }

        public NameValueCollection DebugInfo
        {
            get
            {
                update = true;
                return this;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions { get; }
    }

}
