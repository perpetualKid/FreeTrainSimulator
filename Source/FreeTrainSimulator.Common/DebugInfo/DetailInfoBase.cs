using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.DebugInfo
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
                UpdateNeeded = false;
        }

        public virtual InformationDictionary DetailInfo
        {
            get
            {
                UpdateNeeded = true;
                return this;
            }
        }

        public virtual Dictionary<string, FormatOption> FormattingOptions { get; }

#pragma warning disable CA2227 // Collection properties should be read only
        public virtual DetailInfoBase NextColumn { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        public virtual int MultiColumnCount { get; protected internal set; }

    }
}
