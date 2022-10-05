using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Info;
using Orts.Formats.Msts.Files;

namespace Orts.Graphics.Xna
{
    public class TextureManager : ResourceGameComponent<Texture2D>
    {
        [ThreadStatic]
        private static TextureManager instance;

        public bool PreferDDSTextures { get; set; }

        public Texture2D MissingTexture { get; private set; }

        public TextureManager(Game game) : base(game)
        {
            SweepInterval = 60;
            if (null == instance)
                instance = this;
            MissingTexture = LoadTexture(Path.Combine(RuntimeInfo.ContentFolder, "blank.png"));
        }

        public Texture2D GetTextureCached(string path)
        {
            return GetTextureCached(path, MissingTexture);
        }

        /// <summary>
        /// returns a texture loaded from given path.
        /// The texture may have been loaded before, and returned from cache instead.
        /// If DDS texture are preferred per user settings, a DDS texture will be returned if found over an given ACE texture.
        /// If DDS is not found, and ACE texture may be returned (or vice versa).
        /// </summary>
        public Texture2D GetTextureCached(string path, Texture2D defaultTexture)
        {
            if (string.IsNullOrEmpty(path))
                return defaultTexture;

            int identifier = string.GetHashCode(path, StringComparison.OrdinalIgnoreCase);
            if (!currentResources.TryGetValue(identifier, out Texture2D texture))
            {
                if (!previousResources.TryGetValue(identifier, out texture))
                {
                    texture = LoadTexture(path, PreferDDSTextures);
                    if (texture != null)
                        currentResources.Add(identifier, texture);
                    else
                    {
                        Trace.TraceWarning($"Missing texture {path} replaced with default texture");
                        texture = defaultTexture;
                    }
                }
                else
                {
                    currentResources.Add(identifier, texture);
                    previousResources.Remove(identifier);
                }

            }
            return texture;
        }

        /// <summary>
        /// Tries to load a texture from path, but does not add to resource cache.
        /// Calling component holds a reference to the retrieved texture.
        /// Should be used for components permanently holding a texture, or do not need to reuse and manage lifecycle themselves.
        /// </summary>
        public static Texture2D GetTextureStatic(string path, Game game)
        {
            instance ??= game?.Components.OfType<TextureManager>().FirstOrDefault() ?? new TextureManager(game);
            return string.IsNullOrEmpty(path) ? instance.MissingTexture : LoadTexture(path);
        }

        private static Texture2D LoadTexture(string path, bool preferDdsLoading = false)
        {
            string extension = Path.GetExtension(path);
            Texture2D result = null;
            try
            {
                switch (extension)
                {
                    case string _ when ".dds".Equals(extension, StringComparison.OrdinalIgnoreCase):
                        {
                            if (File.Exists(path))
                            {
                                DDSLib.DDSFromFile(path, instance.Game.GraphicsDevice, true, out result);
                            }
                            else
                            {
                                Trace.TraceWarning($"Required dds texture {path} not existing; trying ace texture instead.");
                                result = LoadTexture(Path.ChangeExtension(path, "ace"), false);
                            }
                            break;
                        }
                    case string _ when ".ace".Equals(extension, StringComparison.OrdinalIgnoreCase):
                        {
                            string alternativeTexture;
                            if (preferDdsLoading && File.Exists(alternativeTexture = Path.ChangeExtension(path, "dds")))
                                result = LoadTexture(alternativeTexture, false);
                            else if (File.Exists(path))
                            {
                                result = AceFile.Texture2DFromFile(instance.Game.GraphicsDevice, path);
                            }
                            break;
                        }
                    case string _ when ".gif".Equals(extension, StringComparison.OrdinalIgnoreCase):
                    case string _ when ".jpg".Equals(extension, StringComparison.OrdinalIgnoreCase):
                    case string _ when ".jpeg".Equals(extension, StringComparison.OrdinalIgnoreCase):
                    case string _ when ".png".Equals(extension, StringComparison.OrdinalIgnoreCase):
                        {
                            if (File.Exists(path))
                            {
                                using (FileStream stream = File.OpenRead(path))
                                    result = Texture2D.FromStream(instance.Game.GraphicsDevice, stream);
                            }
                            break;
                        }
                    case string _ when ".bmp".Equals(extension, StringComparison.OrdinalIgnoreCase):
                        {
                            if (File.Exists(path))
                            {
                                using (System.Drawing.Image image = System.Drawing.Image.FromFile(path))
                                {
                                    using (MemoryStream memoryStream = new MemoryStream())
                                    {
                                        image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                                        memoryStream.Position = 0;
                                        return Texture2D.FromStream(instance.Game.GraphicsDevice, memoryStream);
                                    }
                                }
                            }
                            break;
                        }
                    default:
                        Trace.TraceWarning("Unsupported texture format: {0}", path);
                        break;
                }
                if (result == null)
                    Trace.TraceWarning($"File {0} not found or invalid data.");
            }
            catch (Exception ex) when (ex is SystemException)
            {
                Trace.TraceWarning($"Skipped texture with error: {ex.Message} in {path}");
            }
            return result;
        }
    }
}
