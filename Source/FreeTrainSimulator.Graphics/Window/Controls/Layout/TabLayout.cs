using System;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls.Layout
{
    /// <summary>
    /// Tabbed layout, similar to <cref="TabControl"/> but without UI interaction to change tabs, and no tab header.
    /// Changing between tabs only happens through other means of iinput
    /// </summary>
    public class TabLayout<T> : ControlLayout where T : Enum
    {
        private class TabData
        {
            internal T Tab;
            internal ControlLayout TabLayout;
        }

#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly ControlLayout tabHeader;
        private readonly Label tabLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly bool hideEmptyTabs;

        public EnumArray<Action<ControlLayout>, T> TabLayouts { get; } = new EnumArray<Action<ControlLayout>, T>();

        private readonly EnumArray<TabData, T> tabData = new EnumArray<TabData, T>();

        public T CurrentTab { get; private set; }

        public ControlLayout Client { get; private protected set; }

        public string Name { get; }

        public TabLayout(FormBase window, int x, int y, int width, int height, bool hideEmptyTabs = false) : base(window, x, y, width, height)
        {
            ControlLayout verticalLayout = AddLayoutVertical();
            tabHeader = verticalLayout.AddLayoutHorizontal(window?.Owner.TextFontDefault.Height ?? throw new ArgumentNullException(nameof(window)));
            tabHeader.Add(tabLabel = new Label(Window, 0, 0, tabHeader.RemainingWidth, tabHeader.RemainingHeight, null, HorizontalAlignment.Left, Window.Owner.TextFontDefaultBold, Color.White, OutlineRenderOptions.Default));
            verticalLayout.AddLayoutHorizontalLineOfText();
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
            tabLabel.Text = tab.GetLocalizedDescription();
        }
    }
}
