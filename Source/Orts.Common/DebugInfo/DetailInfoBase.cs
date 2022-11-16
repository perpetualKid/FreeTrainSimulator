using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
    public class DetailInfoBase : InformationDictionary, INameValueInformationProvider
    {
        protected bool UpdateNeeded { get; set; }

        public DetailInfoBase(bool includeFormattingOptions = false)
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

        public InformationDictionary DetailInfo
        {
            get
            {
                UpdateNeeded = true;
                return this;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public virtual INameValueInformationProvider Next { get; set; }

        public virtual int MultiElementCount { get; protected set; }
    }

}
