using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
    public abstract class DetailInfoProxy : DetailInfoBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        protected DetailInfoBase Source { get; set; }

        public override InformationDictionary DetailInfo
        {
            get
            {
                UpdateNeeded = true;
                return Source?.DetailInfo;
            }
        }

        public override DetailInfoBase NextColumn
        {
            get => Source.NextColumn;
            set => Source.NextColumn = value;
        }
#pragma warning restore CA2227 // Collection properties should be read only

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override int MultiColumnCount
        {
            get => Source?.MultiColumnCount ?? 0;
            protected internal set { if (Source != null) Source.MultiColumnCount = value; }
        }

        public virtual void Next()
        { }

        public virtual void Previous()
        { }
    }


}
