using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
    public abstract class DetailInfoProxy : DetailInfoBase
    {
        protected DetailInfoBase Source { get; set; }

        public override InformationDictionary DetailInfo
        {
            get
            {
                UpdateNeeded = true;
                return Source?.DetailInfo;
            }
        }

        public override INameValueInformationProvider Next
        {
            get => Source.Next;
            set => Source.Next = value;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override int MultiElementCount
        {
            get => Source?.MultiElementCount ?? 0;
            protected internal set { if (Source != null) Source.MultiElementCount = value; }
        }
    }


}
