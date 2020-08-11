using System;
using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;

using NuGet.Versioning;

namespace Orts.Updater
{
    public enum UpdateChannel
    {
        [Description("Continous integration builds which may contain serious defects. For developers only.")] 
        ci,
        [Description("Not in use")] 
        alpha,
        [Description("Infrequent updates to official, hand-picked versions. Recommended for most users.")] 
        beta,
        [Description("")] 
        release,
        [Description("Reverting to Official Open Rails. Once updating to this version, you can not use auto-update to update to OR MG Ultimate, " +
            "but will need to manually download ORTS MG Ultimate again")]
        official,
    }

    public class UpdateChannels
    {
        [JsonProperty("channels")]
        public List<ChannelInfo> Channels { get; private set; }

        public static UpdateChannels Empty { get; } = new UpdateChannels() { Channels = new List<ChannelInfo>() };
    }

    public class ChannelInfo

    {
        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("date")]
        public DateTime Date { get; private set; }

        [JsonProperty("url")]
        public Uri DownloadUrl { get; private set; }

        [JsonProperty("version")]
        public string Version { get; private set; }

        [JsonProperty("log")]
        public Uri LogUrl { get; private set; }

        public string NormalizedVersion
        {
            get
            {
                if (!SemanticVersion.TryParse(Version, out SemanticVersion result))
                    return Version;
                return result.ToNormalizedString();
            }
        }
    }
}
