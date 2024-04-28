// COPYRIGHT 2014 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Settings;

namespace Orts.ContentManager
{
    public partial class ContentManagerGUI : Form
    {
        private readonly UserSettings settings;
        private readonly ContentRoot contentManager;

        private static readonly Regex contentBold = new Regex("(?:^|\t)([\\w ]+:)\t", RegexOptions.Multiline);
        private static readonly Regex contentLink = new Regex("\u0001(.*?)\u0002(.*?)\u0001");
        private static readonly Regex contentLinkRTF = new Regex(@"\\'01(.*?)\\'02(.*?)\\'01");

        private ContentBase pendingSelection;
        private readonly List<string> pendingSelections = new List<string>();

        private CancellationTokenSource ctsSearching;
        private CancellationTokenSource ctsExpanding;

        private ConcurrentBag<SearchResult> searchResultsList = new ConcurrentBag<SearchResult>();
        private ConcurrentDictionary<string, string> searchResultDuplicates;

        private readonly Font boldFont;

        public ContentManagerGUI()
        {
            InitializeComponent();

            settings = new UserSettings(Array.Empty<string>());
            contentManager = new ContentRoot(settings.FolderSettings);

            // Start off the tree with the Content Manager itself at the root and expand to show packages.
            treeViewContent.Nodes.Add(CreateContentNode(contentManager));
            treeViewContent.Nodes[0].Expand();

            int width = richTextBoxContent.Font.Height;
            richTextBoxContent.SelectionTabs = new[] { width * 5, width * 15, width * 25, width * 35 };
            boldFont = new Font(richTextBoxContent.Font, FontStyle.Bold);
        }

        private static Task<IEnumerable<TreeNode>> ExpandTreeView(ContentBase content, CancellationToken token)
        {

            // Get all the different possible types of content from this content item.
            IEnumerable<TreeNode> childNodes = EnumExtension.GetValues<ContentType>().SelectMany(ct => content.GetContent(ct).Select(c => CreateContentNode(c)));

            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<IEnumerable<TreeNode>>(token);
            }
            IEnumerable<TreeNode> linkChildren = contentLink.Matches(ContentInfo.GetText(content)).Cast<Match>().Select(linkMatch => CreateContentNode(content, linkMatch.Groups[1].Value, (ContentType)Enum.Parse(typeof(ContentType), linkMatch.Groups[2].Value)));
            Debug.Assert(!childNodes.Any() || !linkChildren.Any(), "Content item should not return items from Get(ContentType) and Get(string, ContentType)");
            childNodes = childNodes.Concat(linkChildren);

            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<IEnumerable<TreeNode>>(token);
            }
            return Task.FromResult(childNodes);
        }

        private async void TreeViewContent_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            object lockObj = new object();
            // Are we about to expand a not-yet-loaded node? This is identified by a single child with no text or tag.
            if (e.Node.Tag != null && e.Node.Tag is ContentBase && e.Node.Nodes.Count == 1 && string.IsNullOrEmpty(e.Node.Nodes[0].Text) && e.Node.Nodes[0].Tag == null)
            {
                // Make use of the single child to show a loading message.
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                e.Node.Nodes[0].Text = "Loading...";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                lock (lockObj)
                {
                    if (ctsExpanding != null && !ctsExpanding.IsCancellationRequested)
                        ctsExpanding.Cancel();
                    ctsExpanding = ResetCancellationTokenSource(ctsExpanding);
                }
                try
                {

                    ContentBase content = e.Node.Tag as ContentBase;
                    IEnumerable<TreeNode> nodes = await Task.Run(() => ExpandTreeView(content, ctsExpanding.Token)).ConfigureAwait(true);

                    //// Collapse node if we're going to end up with no child nodes.
                    if (!nodes.Any())
                    {
                        e.Node.Collapse();
                    }

                    // Remove the loading node.
                    e.Node.Nodes.RemoveAt(0);
                    e.Node.Nodes.AddRange(nodes.ToArray());
                }
                catch (TaskCanceledException)
                {
                    e.Node.Nodes[0].Text = string.Empty;
                    e.Node.Collapse();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    e.Node.Nodes.Add(new TreeNode(ex.Message));
                }
            }
            CheckForPendingActions(e.Node);
        }

        private void CheckForPendingActions(TreeNode startNode)
        {
            if (pendingSelection != null)
            {
                TreeNode pendingSelectionNode = startNode.Nodes.OfType<TreeNode>().FirstOrDefault(node => (ContentBase)node.Tag == pendingSelection);
                if (pendingSelectionNode != null)
                {
                    treeViewContent.SelectedNode = pendingSelectionNode;
                    treeViewContent.Focus();
                }
                pendingSelection = null;
            }
            if (pendingSelections.Count > 0)
            {
                TreeNode pendingSelectionNode = startNode.Nodes.OfType<TreeNode>().FirstOrDefault(node => ((ContentBase)node.Tag).Name == pendingSelections[0]);
                if (pendingSelectionNode != null)
                {
                    pendingSelections.RemoveAt(0);
                    treeViewContent.SelectedNode = pendingSelectionNode;
                    treeViewContent.SelectedNode.Expand();
                }
            }
        }

        private static TreeNode CreateContentNode(ContentBase content, string name, ContentType type)
        {
            ContentBase c = content.GetContent(name, type);
            if (c != null)
                return CreateContentNode(c);
            return new TreeNode($"Missing: {name} ({type})");
        }

        private static TreeNode CreateContentNode(ContentBase c)
        {
            return new TreeNode($"{c.Name} ({c.Type})", new[] { new TreeNode() }) { Tag = c };
        }

        private void TreeViewContent_AfterSelect(object sender, TreeViewEventArgs e)
        {
            richTextBoxContent.Clear();

            if (!(e.Node.Tag is ContentBase))
                return;

            Trace.TraceInformation("Updating richTextBoxContent with content {0}", e.Node.Tag as ContentBase);

            richTextBoxContent.Text = ContentInfo.GetText(e.Node.Tag as ContentBase);
            Match boldMatch = contentBold.Match(richTextBoxContent.Text);
            while (boldMatch.Success)
            {
                richTextBoxContent.Select(boldMatch.Groups[1].Index, boldMatch.Groups[1].Length);
                richTextBoxContent.SelectionFont = boldFont;
                boldMatch = contentBold.Match(richTextBoxContent.Text, boldMatch.Groups[1].Index + boldMatch.Groups[1].Length);
            }
            string rawText = richTextBoxContent.Rtf;
            Match linkMatch = contentLinkRTF.Match(rawText);
            while (linkMatch.Success)
            {
                rawText = rawText.Remove(linkMatch.Index, linkMatch.Length);
                rawText = rawText.Insert(linkMatch.Index, $@"{{\field{{\*\fldinst{{HYPERLINK ""{linkMatch.Groups[1].Value}\u0001.{linkMatch.Groups[2].Value}\u0001.""}}}}{{\fldrslt {linkMatch.Groups[1].Value}}}}}");
                linkMatch = contentLinkRTF.Match(rawText);
            }
            richTextBoxContent.Rtf = rawText;
            richTextBoxContent.Select(0, 0);
            richTextBoxContent.SelectionFont = richTextBoxContent.Font;
        }

        private void RichTextBoxContent_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            ContentBase content = treeViewContent.SelectedNode.Tag as ContentBase;
            string[] link = e.LinkText.Split('\u0001', StringSplitOptions.RemoveEmptyEntries);
            if (content != null && link.Length == 2)
            {
                pendingSelection = content.GetContent(link[0], (ContentType)Enum.Parse(typeof(ContentType), link[1]));
                if (treeViewContent.SelectedNode.IsExpanded)
                {
                    TreeNode pendingSelectionNode = treeViewContent.SelectedNode.Nodes.Cast<TreeNode>().FirstOrDefault(node => (ContentBase)node.Tag == pendingSelection);
                    if (pendingSelectionNode != null)
                    {
                        treeViewContent.SelectedNode = pendingSelectionNode;
                        treeViewContent.Focus();
                    }
                    pendingSelection = null;
                }
                else
                {
                    treeViewContent.SelectedNode.Expand();
                }
            }
        }

        private Task SearchContent(ContentBase content, string path, string searchString, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return Task.CompletedTask;

            try
            {
                if (string.IsNullOrEmpty(path))
                    path = contentManager.Name;

                if (content.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                {
                    if (content.Parent != null)
                        searchResultsList.Add(new SearchResult(content, path));
                }
                Parallel.ForEach(EnumExtension.GetValues<ContentType>().SelectMany(ct => content.GetContent(ct)),
                    new ParallelOptions() { CancellationToken = token },
                    async (child) =>
                {
                    await SearchContent(child, path + " / " + child.Name, searchString, token).ConfigureAwait(false);
                });

                Parallel.ForEach(contentLinkRTF.Matches(ContentInfo.GetText(content)).Cast<Match>().Select(linkMatch => content.GetContent(linkMatch.Groups[1].Value,
                    (ContentType)Enum.Parse(typeof(ContentType), linkMatch.Groups[2].Value))).Where(linkContent => linkContent != null),
                    new ParallelOptions() { CancellationToken = token },
                    async (child) =>
                    {
                        if (!searchResultDuplicates.ContainsKey(path + " -> " + child.Name))
                        {
                            searchResultDuplicates.TryAdd(path, content.Name);
                            await SearchContent(child, path + " -> " + child.Name, searchString, token).ConfigureAwait(false);
                        }
                    });

                if (!token.IsCancellationRequested)
                    return UpdateSearchResults(token);
            }
            catch (OperationCanceledException)
            {
            }
            return Task.CompletedTask;
        }

        private async void SearchBox_TextChanged(object sender, EventArgs e)
        {
            lock (searchResultsList)
            {
                if (ctsSearching != null && !ctsSearching.IsCancellationRequested)
                    ctsSearching.Cancel();
                ctsSearching = ResetCancellationTokenSource(ctsSearching);
            }

            searchResultsList = new ConcurrentBag<SearchResult>();
            searchResultDuplicates = new ConcurrentDictionary<string, string>();
            searchResults.Items.Clear();
            searchResults.Visible = searchBox.Text.Length > 0;
            if (!searchResults.Visible)
                return;

            searchResults.Items.Add(string.Empty);

            try
            {
                await Task.Run(() => SearchContent(contentManager, string.Empty, searchBox.Text, ctsSearching.Token)).ConfigureAwait(true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateSearchResults(true, CancellationToken.None);
            }
            return;
        }

        private void UpdateSearchResults(bool done, CancellationToken token)
        {
            while (searchResultsList.TryTake(out SearchResult result) && !token.IsCancellationRequested)
            {
                searchResults.Items.Add(result);
            }
            if (searchResults.Items.Count > 0)
                searchResults.Items[0] = $"[{(done ? "Done" : "Searching")}] {searchResults.Items.Count - 1} results found";
        }

        private Task UpdateSearchResults(CancellationToken token)
        {
            try
            {
                Invoke((MethodInvoker)delegate { UpdateSearchResults(false, token); });
            }
            catch (ObjectDisposedException) //when Form Closing, object may already be disposing while SearchTask cancelling
            { }
            return Task.CompletedTask;
        }

        private void SearchResults_DoubleClick(object sender, EventArgs e)
        {
            if (!(searchResults.SelectedItem is SearchResult result))
                return;

            pendingSelections.Clear();
            pendingSelections.AddRange(result.Path);
            treeViewContent.Focus();
            treeViewContent.CollapseAll();
            treeViewContent.SelectedNode = treeViewContent.Nodes[0];
            treeViewContent.SelectedNode.Expand();
        }

        private static CancellationTokenSource ResetCancellationTokenSource(System.Threading.CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Dispose();
            }
            // Create a new cancellation token source so that can cancel all the tokens again 
            return new CancellationTokenSource();
        }

        private void ContentManagerGUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (null != ctsSearching && !ctsSearching.IsCancellationRequested)
                ctsSearching.Cancel();
            if (null != ctsExpanding && !ctsExpanding.IsCancellationRequested)
                ctsExpanding.Cancel();
        }
    }

    internal class SearchResult
    {
        public string Name { get; }
        public string[] Path { get; }
        private static readonly string[] separators = { " / ", " -> " };
        public SearchResult(ContentBase content, string path)
        {
            int placeEnd = Math.Max(path.LastIndexOf(" / ", StringComparison.OrdinalIgnoreCase), path.LastIndexOf(" -> ", StringComparison.OrdinalIgnoreCase));
            string place = path[..placeEnd];
            Name = $"{content.Name} ({content.Type}) in {place}";
            Path = path.Split(separators, StringSplitOptions.None).Skip(1).ToArray();
        }

        public override string ToString()
        {
            return Name;
        }
    }

}
