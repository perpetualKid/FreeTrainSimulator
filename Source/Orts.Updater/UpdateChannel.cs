using System;
using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;

using NuGet.Versioning;

namespace Orts.Updater
{
    [Description("UpdateChannel")]
    public enum UpdateChannel
    {
        [Description("Continous integration builds which may contain serious defects. For developers only.")] 
        ci,
        [Description("Regular development builds")] 
        dev,
        [Description("Infrequent updates to official, hand-picked versions. Recommended for most users.")] 
        rc,
        [Description("Stable release versions")] 
        release,
        [Description("Reverting to Official Open Rails. If updating to this version, " +
            "you can not use the updater to change back to OR MG Ultimate, " +
            "and will need to manually download ORTS MG Ultimate again")]
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

        [JsonProperty("hash")]
        public string Hash { get; private set; }

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
