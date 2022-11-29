using System;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.Window;
using Orts.Graphics.Xna;
using Orts.Settings;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class CarIdentifierOverlay : OverlayBase
    {
        private enum ViewMode
        {
            Cars,
            Trains,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private ViewMode viewMode;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextShape contentText;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly System.Drawing.Font textFont;

        public CarIdentifierOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) : base(owner, catalog ?? CatalogManager.Catalog)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.settings = settings;
            userCommandController = viewer.UserCommandController;
            this.viewer = viewer;
            contentText = TextShape.Instance(owner.Game, null);
            contentText.OutlineRenderOptions = new OutlineRenderOptions(2.0f, System.Drawing.Color.White, System.Drawing.Color.Red);
            textFont = FontManager.Scaled(owner.DefaultFontName, System.Drawing.FontStyle.Regular)[20];
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            Color color = Color.White;
            color.A = 128;
            contentText.DrawString(new Vector2(200,200), color, "Some test text", textFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Bottom, SpriteEffects.None, spriteBatch);
            base.Draw(spriteBatch);
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayCarLabels, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayCarLabels, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        protected override void Initialize()
        {
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.CarIdentifierOverlay], out ViewMode mode))
            {
                viewMode = mode.Previous();
                TabAction();
            }
            base.Initialize();
        }

        private void TabAction()
        {
            viewMode = viewMode.Next();
            settings.PopupSettings[ViewerWindowType.CarIdentifierOverlay] = viewMode.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
