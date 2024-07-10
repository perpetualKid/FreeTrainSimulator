using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class NotificationOverlay : OverlayBase
    {
        private sealed class MessageSortComparer : IComparer<(string key, string message, double startTime, double endTime)>
        {
            public int Compare((string key, string message, double startTime, double endTime) x, (string key, string message, double startTime, double endTime) y)
            {
                return y.startTime.CompareTo(x.startTime);
            }
        }

        private const double NoticeAnimationLength = 1.0;
        private const double MessageAnimationLength = 2.0;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private ShadowLabel noticeLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private bool animationRunning;
        private double animationStart;
        private double animationEnd;
        private ControlLayout[] messageLines;
        private List<(string key, string message, double startTime, double endTime)> messages = new List<(string key, string message, double startTime, double endTime)>();
        private List<(string key, string message, double startTime, double endTime)> replacementList = new List<(string key, string message, double startTime, double endTime)>();
        private readonly MessageSortComparer comparer = new MessageSortComparer();

        public NotificationOverlay(WindowManager owner, Catalog catalog = null) : base(owner, catalog ?? CatalogManager.Catalog)
        {
            ZOrder = 50;
            TopMost = true;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            ControlLayout innerFrame = layout.AddLayoutOffset(160, 150, 160, 150);
            innerFrame = innerFrame.AddLayoutVertical();
            innerFrame.HorizontalChildAlignment = HorizontalAlignment.Center;
            System.Drawing.Font noticeFont = FontManager.Scaled(Owner.FontName, System.Drawing.FontStyle.Regular)[40];
            innerFrame.Add(noticeLabel = new ShadowLabel(this, 0, 0, innerFrame.RemainingWidth, noticeFont.Height, null, HorizontalAlignment.Center, noticeFont, Color.White));
            noticeLabel.Visible = false;
            innerFrame.HorizontalChildAlignment = HorizontalAlignment.Left;
            int maxLines = innerFrame.RemainingHeight / (int)(Owner.TextFontDefault.Height * 1.5);
            messageLines = new ControlLayout[maxLines];
            for (int i = 0; i < maxLines; i++)
            {
                ControlLayout line = innerFrame.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.5));
                messageLines[i] = line;
                line.Add(new ShadowLabel(this, line.RemainingWidth, Owner.TextFontDefault.Height, null));
            }
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            double currentTime = Simulator.Instance.GameTime;
            if (animationRunning)
            {
                if (Simulator.Instance.GameTime > animationEnd)
                {
                    animationRunning = false;
                    noticeLabel.Visible = false;
                }
                Color color = Color.White;
                color.A = (byte)MathHelper.Lerp(0, 255, (float)((animationEnd - currentTime) / NoticeAnimationLength));
                noticeLabel.TextColor = color;
            }
            int messageNumber = 1;
            for (int i = Math.Min(messages.Count, messageLines.Length) - 1; i >= 0; i--)
            {
                if (messages[i].endTime + MessageAnimationLength < currentTime)
                {
                    (messageLines[^messages.Count].Controls[0] as Label).Text = null;
                    messages.RemoveAt(i);
                }
                else
                {
                    if (messageLines[^messageNumber++].Controls[0] is ShadowLabel label)
                    {
                        label.Text = $"{FormatStrings.FormatTime(messages[i].startTime)} {messages[i].message}";
                        Color color = Color.White;
                        color.A = (byte)MathHelper.Lerp(255, 0, MathHelper.Clamp((float)((currentTime - messages[i].endTime) / MessageAnimationLength), 0, 1));
                        label.TextColor = color;
                    }
                }
            }

            base.Update(gameTime, shouldUpdate);

        }

        internal void AddNotice(string text)
        {
            noticeLabel.Visible = true;
            noticeLabel.Text = text;
            animationRunning = true;
            animationStart = Simulator.Instance.GameTime;
            animationEnd = animationStart + NoticeAnimationLength;
        }

        internal void AddMessage(string key, string message, double duration)
        {
            double gameTime = Simulator.Instance.GameTime;
            bool messageExists;
            replacementList.Clear();
            replacementList.AddRange(messages);

            // Find an existing message if there is one.
            (string key, string message, double startTime, double endTime) existingMessage = default;
            messageExists = ((!string.IsNullOrEmpty(key) && (existingMessage = replacementList.FirstOrDefault(m => m.key == key)).key == key) ||
                ((existingMessage = replacementList.FirstOrDefault(m => m.message == message)).message == message));
            if (messageExists)
            {
                _ = replacementList.Remove(existingMessage);
            }
            // Add the new message.
            replacementList.Add((key, message,  messageExists ? existingMessage.startTime : Simulator.Instance.ClockTime, gameTime + duration));
            replacementList.Sort(comparer);

            lock (replacementList)
            {
                (replacementList, messages) = (messages, replacementList);
            }
        }
    }
}
