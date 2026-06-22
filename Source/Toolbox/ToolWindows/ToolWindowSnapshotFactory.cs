using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;

using FreeTrainSimulator.Common.DebugInfo;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Builds immutable <see cref="ToolWindowSnapshot"/> instances from MonoGame-side
    /// <see cref="INameValueInformationProvider"/> sources. Centralizes the conversion of the
    /// MonoGame-specific <see cref="FormatOption"/> colours/styles to BCL types so the hosted tool-window
    /// bridges (debug, track item, track node) share one implementation and the WPF shell never references
    /// MonoGame.
    /// </summary>
    internal static class ToolWindowSnapshotFactory
    {
        /// <summary>
        /// Builds a snapshot from one or more information providers. Null providers (and providers with a
        /// null <see cref="INameValueInformationProvider.DetailInfo"/>) are skipped. Must be called on the
        /// game thread because the providers are mutated there each frame.
        /// </summary>
        public static ToolWindowSnapshot FromProviders(IEnumerable<INameValueInformationProvider> providers)
        {
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();

            if (providers is not null)
            {
                foreach (INameValueInformationProvider provider in providers)
                    AppendProvider(builder, provider);
            }

            return new ToolWindowSnapshot(builder.ToImmutable());
        }

        /// <summary>
        /// Builds a snapshot from a single information provider. Convenience overload for the track
        /// item/node bridges that surface exactly one provider.
        /// </summary>
        public static ToolWindowSnapshot FromProvider(INameValueInformationProvider provider)
        {
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            AppendProvider(builder, provider);
            return new ToolWindowSnapshot(builder.ToImmutable());
        }

        private static void AppendProvider(ImmutableArray<ToolWindowRow>.Builder builder, INameValueInformationProvider provider)
        {
            InformationDictionary detail = provider?.DetailInfo;
            if (detail is null)
                return;

            foreach (string key in detail.Keys)
            {
                FormatOption format = null;
                _ = provider.FormattingOptions?.TryGetValue(key, out format);
                Color? color = format?.TextColor is { } textColor
                    ? Color.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B)
                    : null;
                bool bold = format?.FontStyle.HasFlag(FontStyle.Bold) ?? false;
                builder.Add(new ToolWindowRow(key, detail[key], color, bold));
            }
        }
    }
}
