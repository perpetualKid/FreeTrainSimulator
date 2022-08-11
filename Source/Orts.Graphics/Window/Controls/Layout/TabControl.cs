using System;
using System.Drawing;

using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.Graphics.Window.Controls.Layout
{

    public class TabControl<T> : ControlLayout where T : Enum
    {
        private class TabData
        {
            internal T Tab;
            internal Label TabLabel;
            internal ControlLayout TabLayout;
        }

        private readonly ControlLayout tabHeader;
        private readonly Font highlightFont;
        private readonly bool hideEmptyTabs;

        public EnumArray<Action<ControlLayout>, T> TabLayouts { get; } = new EnumArray<Action<ControlLayout>, T>();

        private readonly EnumArray<TabData, T> tabData = new EnumArray<TabData, T>();

        public T CurrentTab { get; private set; }

        public ControlLayout Client { get; private protected set; }

        public TabControl(WindowBase window, int width, int height, bool hideEmptyTabs = false) : base(window, 0, 0, width, height)
        {
            ControlLayout verticalLayout = AddLayoutVertical();
            tabHeader = verticalLayout.AddLayoutHorizontal(window?.Owner.TextFontDefault.Height ?? throw new ArgumentNullException(nameof(window)));
            verticalLayout.AddHorizontalSeparator(true);
            highlightFont = new Font(window.Owner.TextFontDefaultBold, window.Owner.TextFontDefaultBold.Style | FontStyle.Underline);
            Client = verticalLayout.AddLayoutVertical();
            this.hideEmptyTabs = hideEmptyTabs;
        }

        public void UpdateTabLayout(T tab)
        {
            if (TabLayouts[tab] != null)
            {
                tabData[tab].TabLayout.Clear();
                TabLayouts[tab](tabData[tab].TabLayout);
                Client.Initialize();
            }
        }

        private void TabLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is Label label)
            {
                TabAction((T)label.Tag);
            }
        }

        internal override void Initialize()
        {
            int availableTabs = 0;
            foreach (Action<ControlLayout> tabLayout in TabLayouts)
                if (tabLayout != null)
                    availableTabs++;

            if (hideEmptyTabs && (availableTabs == 0))
                return;

            int labelWidth = RemainingWidth / (hideEmptyTabs ? availableTabs : EnumExtension.GetLength<T>());
            foreach (T item in EnumExtension.GetValues<T>())
            {
                if (TabLayouts[item] != null || !hideEmptyTabs)
                {
                    tabData[item] = new TabData()
                    {
                        Tab = item,
                        TabLabel = new Label(this.Window, labelWidth, tabHeader.RemainingHeight, item.GetDescription(), HorizontalAlignment.Center)
                        {
                            Tag = item,
                        },
                    };
                    tabHeader.Add(tabData[item].TabLabel);
                    tabData[item].TabLabel.OnClick += TabLabel_OnClick;
                }
            }

            base.Initialize();
            TabAction(CurrentTab);
        }

        private void TabAction(T tab)
        {
            CurrentTab = tab;
            foreach (TabData tabDataItem in tabData)
            {
                if (null != tabDataItem)
                    tabDataItem.TabLabel.Font = Window.Owner.TextFontDefault;
            }
            Client.Clear();
            tabData[CurrentTab].TabLabel.Font = highlightFont;
            if (tabData[CurrentTab].TabLayout != null)
            {
                Client.Controls.Add(tabData[CurrentTab].TabLayout);
            }
            else
            {
                if (TabLayouts[CurrentTab] != null)
                {
                    tabData[CurrentTab].TabLayout = Client.AddLayoutVertical();
                    TabLayouts[CurrentTab](tabData[CurrentTab].TabLayout);
                    Client.Initialize();
                }
            }
        }

        public void TabAction()
        {
            do
            {
                CurrentTab = CurrentTab.Next();
            }
            while (tabData[CurrentTab] == null);

            TabAction(CurrentTab);
        }

        internal override void Draw(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Point offset)
        {
            base.Draw(spriteBatch, offset);
        }
    }
}
