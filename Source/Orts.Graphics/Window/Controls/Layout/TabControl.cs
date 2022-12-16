using System;
using System.Drawing;

using Orts.Common;

namespace Orts.Graphics.Window.Controls.Layout
{
    public class TabChangedEventArgs<T> : EventArgs where T: Enum
    {
        public T Tab { get; }

        public TabChangedEventArgs(T tab)
        {
            Tab = tab;
        }
    }

    public class TabControl<T> : ControlLayout where T : Enum
    {
        private class TabData
        {
            internal T Tab;
            internal Label TabLabel;
            internal ControlLayout TabLayout;
        }

#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly ControlLayout tabHeader;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly Font highlightFont;
        private readonly bool hideEmptyTabs;

        public EnumArray<Action<ControlLayout>, T> TabLayouts { get; } = new EnumArray<Action<ControlLayout>, T>();

        private readonly EnumArray<TabData, T> tabData = new EnumArray<TabData, T>();

        public T CurrentTab { get; private set; }

        public ControlLayout Client { get; private protected set; }

        public event EventHandler<TabChangedEventArgs<T>> TabChanged;

        public TabControl(FormBase window, int width, int height, bool hideEmptyTabs = false) : base(window, 0, 0, width, height)
        {
            ControlLayout verticalLayout = AddLayoutVertical();
            tabHeader = verticalLayout.AddLayoutHorizontal(window?.Owner.TextFontDefault.Height ?? throw new ArgumentNullException(nameof(window)));
            verticalLayout.AddHorizontalSeparator(true);
            highlightFont = FontManager.Scaled(window.Owner.FontName, FontStyle.Bold | FontStyle.Underline)[window.Owner.FontSize];
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
                ActivateTab((T)label.Tag);
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
                        TabLabel = new Label(this.Window, labelWidth, tabHeader.RemainingHeight, item.GetLocalizedDescription(), HorizontalAlignment.Center)
                        {
                            Tag = item,
                        },
                    };
                    tabHeader.Add(tabData[item].TabLabel);
                    tabData[item].TabLabel.OnClick += TabLabel_OnClick;
                }
            }

            base.Initialize();
            ActivateTab(CurrentTab);
        }

        private void ActivateTab(T tab)
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
            TabChanged?.Invoke(this, new TabChangedEventArgs<T>(CurrentTab));
        }

        public void TabAction()
        {
            do
            {
                CurrentTab = CurrentTab.Next();
            }
            while (tabData[CurrentTab] == null);

            ActivateTab(CurrentTab);
        }

        public void TabAction(T tab)
        {
            while (tabData[tab] == null)
            {
                tab = tab.Next();
            }
            ActivateTab(tab);
        }
    }
}
