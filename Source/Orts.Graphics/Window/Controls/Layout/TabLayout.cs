using System;

using Orts.Common;

namespace Orts.Graphics.Window.Controls.Layout
{
    /// <summary>
    /// Tabbed layout, similar to TabControl but with no UI interaction.
    /// Changing between tabs only happens through other means input
    /// </summary>
    public class TabLayout<T> : ControlLayout where T : Enum
    {
        private class TabData
        {
            internal T Tab;
            internal ControlLayout TabLayout;
        }

        private readonly bool hideEmptyTabs;

        public EnumArray<Action<ControlLayout>, T> TabLayouts { get; } = new EnumArray<Action<ControlLayout>, T>();

        private readonly EnumArray<TabData, T> tabData = new EnumArray<TabData, T>();

        public T CurrentTab { get; private set; }

        public ControlLayout Client { get; private protected set; }


        public TabLayout(FormBase window, int x, int y, int width, int height, bool hideEmptyTabs = false) : base(window, x, y, width, height)
        {
            Client = AddLayoutVertical();
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

        internal override void Initialize()
        {
            int availableTabs = 0;
            foreach (Action<ControlLayout> tabLayout in TabLayouts)
                if (tabLayout != null)
                    availableTabs++;

            if (hideEmptyTabs && (availableTabs == 0))
                return;

            foreach (T item in EnumExtension.GetValues<T>())
            {
                if (TabLayouts[item] != null || !hideEmptyTabs)
                {
                    tabData[item] = new TabData()
                    {
                        Tab = item,
                    };
                }
            }

            base.Initialize();
            ActivateTab(CurrentTab);
        }

        private void ActivateTab(T tab)
        {
            CurrentTab = tab;
            Client.Clear();
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
    }
}
