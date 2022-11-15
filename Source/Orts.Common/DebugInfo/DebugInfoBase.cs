using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
#pragma warning disable CA1010 // Generic interface should also be implemented
    public class DebugInfoBase : NameValueCollection, INameValueInformationProvider
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
        protected bool UpdateNeeded { get; set; }

        public DebugInfoBase(bool includeFormattingOptions = false)
        {
            if (includeFormattingOptions)
                FormattingOptions = new Dictionary<string, FormatOption>();
        }

        public virtual void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                UpdateNeeded = false;
            }
        }

        public NameValueCollection DebugInfo
        {
            get
            {
                UpdateNeeded = true;
                return this;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions { get; }
    }

}
