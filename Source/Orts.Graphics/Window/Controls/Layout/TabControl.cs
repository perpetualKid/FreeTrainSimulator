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

        private T currentTab;
        private readonly Font highlightFont;

        public EnumArray<Action<ControlLayout>, T> TabLayouts { get; } = new EnumArray<Action<ControlLayout>, T>();

        private readonly EnumArray<TabData, T> tabData = new EnumArray<TabData, T>();

        public ControlLayout Client { get; private protected set; }

        public TabControl(WindowBase window, int width, int height) : base(window, 0, 0, width, height)
        {
            ControlLayout verticalLayout = AddLayoutVertical();
            Client = verticalLayout.AddLayoutHorizontal(window?.Owner.TextFontDefault.Height ?? throw new ArgumentNullException(nameof(window)));
            int labelWidth = RemainingWidth / EnumExtension.GetLength<T>();
            foreach (T item in EnumExtension.GetValues<T>())
            {
                tabData[item] = new TabData()
                {
                    Tab = item,
                    TabLabel = new Label(this.Window, labelWidth, Client.RemainingHeight, item.GetDescription(), HorizontalAlignment.Center)
                    { 
                        Tag = item,
                    },
                };
                Client.Add(tabData[item].TabLabel);
                tabData[item].TabLabel.OnClick += TabLabel_OnClick;
            }
            verticalLayout.AddHorizontalSeparator(true);
            highlightFont = new Font(window.Owner.TextFontDefaultBold, window.Owner.TextFontDefaultBold.Style | FontStyle.Underline);
            Client = verticalLayout.AddLayoutVertical();
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
            base.Initialize();
            TabAction(currentTab);
        }

        private void TabAction(T tab)
        { 
            currentTab = tab;
            foreach (TabData tabDataItem in tabData)
            {
                tabDataItem.TabLabel.Font = Window.Owner.TextFontDefault;
            }
            Client.Clear();
            tabData[currentTab].TabLabel.Font = highlightFont;
            if (tabData[currentTab].TabLayout != null)
            {
                Client.Controls.Add(tabData[currentTab].TabLayout);
            }
            else
            {
                if (TabLayouts[currentTab] != null)
                {
                    tabData[currentTab].TabLayout = Client.AddLayoutVertical();
                    TabLayouts[currentTab](tabData[currentTab].TabLayout);
                    Client.Initialize();
                }
            }
        }

        public void TabAction()
        {
            currentTab = currentTab.Next();
            TabAction(currentTab);
        }

        internal override void Draw(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Point offset)
        {
            base.Draw(spriteBatch, offset);
        }
    }
}
