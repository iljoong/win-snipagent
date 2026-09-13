using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Wpf.Ui.Markdown;
using Wpf.Ui.Markdown.Renderers;
using Wpf.Ui.Markdown.Renderers.Wpf;
using Wpf.Ui.Markdown.Renderers.Wpf.Extensions;
using Wpf.Ui.Markdown.Renderers.Wpf.Inlines;
using Block = System.Windows.Documents.Block;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Inline = System.Windows.Documents.Inline;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using RichTextBox = System.Windows.Controls.RichTextBox;
using TableCell = System.Windows.Documents.TableCell;
using TableRow = System.Windows.Documents.TableRow;
using TextBox = System.Windows.Controls.TextBox;

namespace SnipAgent.App.Overlays.Markdown;

internal sealed class AiAnswerMarkdownPresenter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    private readonly Action<Uri> _openHttpLink;

    public AiAnswerMarkdownPresenter(Action<Uri> openHttpLink)
    {
        _openHttpLink = openHttpLink ?? throw new ArgumentNullException(nameof(openHttpLink));
    }

    /// <summary>
    /// Converts an AI answer Markdown string into a styled FlowDocument using the
    /// overlay's constrained rendering policy. Blank input returns an empty styled
    /// document.
    /// </summary>
    public FlowDocument Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            var emptyDocument = new FlowDocument();
            ApplyOverlayStyles(emptyDocument);
            return emptyDocument;
        }

        var renderer = new SafeWpfRenderer(_openHttpLink);
        var document = Wpf.Ui.Markdown.Markdown.ToFlowDocument(markdown, Pipeline, renderer);
        ApplyOverlayStyles(document);
        return document;
    }

    internal static bool TryGetAllowedHttpUri(string? url, out Uri? uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    internal static string BuildBlockedImageIndicator(string? altText)
    {
        var trimmedAltText = string.IsNullOrWhiteSpace(altText) ? "no description" : altText.Trim();
        return $"[Image blocked: {trimmedAltText}]";
    }

    internal static string ExtractInlinePlainText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var current = container.FirstChild; current is not null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case LineBreakInline:
                    sb.Append(' ');
                    break;
                case LinkInline nestedLink when nestedLink.IsImage:
                    sb.Append(BuildBlockedImageIndicator(ExtractInlinePlainText(nestedLink)));
                    break;
                case ContainerInline nestedContainer:
                    sb.Append(ExtractInlinePlainText(nestedContainer));
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Opens a validated absolute HTTP(S) URI in the default browser.
    /// </summary>
    public static void OpenHttpLinkInDefaultBrowser(Uri uri)
    {
        if (!TryGetAllowedHttpUri(uri.AbsoluteUri, out var allowedUri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(allowedUri!.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open markdown link '{allowedUri}': {ex.Message}");
        }
    }

    private static void ApplyOverlayStyles(FlowDocument document)
    {
        document.PagePadding = new Thickness(0);
        document.TextAlignment = TextAlignment.Left;
        document.LineHeight = 24;
        document.FontFamily = new FontFamily("Segoe UI");
        document.FontSize = 15;
        document.Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
        document.Background = Brushes.Transparent;

        var resources = document.Resources;
        resources[Styles.DocumentStyleKey] = CreateStyle<FlowDocument>(
            new Setter(TextElement.FontFamilyProperty, new FontFamily("Segoe UI")),
            new Setter(TextElement.FontSizeProperty, 15.0),
            new Setter(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))),
            new Setter(FlowDocument.PagePaddingProperty, new Thickness(0)));
        resources[Styles.ParagraphStyleKey] = CreateStyle<Paragraph>(
            new Setter(Block.MarginProperty, new Thickness(0, 0, 0, 10)));
        resources[Styles.Heading1StyleKey] = CreateHeadingStyle(26, new Thickness(0, 12, 0, 10));
        resources[Styles.Heading2StyleKey] = CreateHeadingStyle(22, new Thickness(0, 12, 0, 8));
        resources[Styles.Heading3StyleKey] = CreateHeadingStyle(19, new Thickness(0, 10, 0, 8));
        resources[Styles.Heading4StyleKey] = CreateHeadingStyle(17, new Thickness(0, 10, 0, 6));
        resources[Styles.Heading5StyleKey] = CreateHeadingStyle(16, new Thickness(0, 8, 0, 6));
        resources[Styles.Heading6StyleKey] = CreateHeadingStyle(15, new Thickness(0, 8, 0, 6));
        resources[Styles.CodeStyleKey] = CreateStyle<Run>(
            new Setter(TextElement.FontFamilyProperty, new FontFamily("Consolas")),
            new Setter(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))),
            new Setter(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1))));
        resources[Styles.QuoteBlockStyleKey] = CreateStyle<Section>(
            new Setter(Block.MarginProperty, new Thickness(0, 6, 0, 10)),
            new Setter(Block.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x4B, 0x86, 0xF7))),
            new Setter(Block.BorderThicknessProperty, new Thickness(3, 0, 0, 0)),
            new Setter(Block.PaddingProperty, new Thickness(10, 4, 0, 0)));
        resources[Styles.ThematicBreakStyleKey] = CreateStyle<System.Windows.Shapes.Line>(
            new Setter(System.Windows.Shapes.Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A))),
            new Setter(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.0),
            new Setter(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 10)),
            new Setter(FrameworkElement.HeightProperty, 1.0),
            new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
            new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        resources[Styles.TaskListStyleKey] = CreateStyle<CheckBox>(
            new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))),
            new Setter(Control.MarginProperty, new Thickness(0, 0, 6, 0)));
        resources[Styles.HyperlinkStyleKey] = CreateHyperlinkStyle();
        resources[Styles.StrikeThroughStyleKey] = CreateStyle<Span>(
            new Setter(Inline.TextDecorationsProperty, TextDecorations.Strikethrough));
        resources[Styles.SubscriptStyleKey] = CreateStyle<Span>(
            new Setter(Inline.BaselineAlignmentProperty, BaselineAlignment.Subscript),
            new Setter(TextElement.FontSizeProperty, 12.0));
        resources[Styles.SuperscriptStyleKey] = CreateStyle<Span>(
            new Setter(Inline.BaselineAlignmentProperty, BaselineAlignment.Superscript),
            new Setter(TextElement.FontSizeProperty, 12.0));
        resources[Styles.InsertedStyleKey] = CreateStyle<Span>(
            new Setter(Inline.TextDecorationsProperty, TextDecorations.Underline));
        resources[Styles.MarkedStyleKey] = CreateStyle<Span>(
            new Setter(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x6A, 0x54, 0x00))));
    }

    private static Style CreateHeadingStyle(double size, Thickness margin)
    {
        return CreateStyle<Paragraph>(
            new Setter(TextElement.FontSizeProperty, size),
            new Setter(TextElement.FontWeightProperty, FontWeights.SemiBold),
            new Setter(Block.MarginProperty, margin));
    }

    private static Style CreateHyperlinkStyle()
    {
        var style = CreateStyle<Hyperlink>(
            new Setter(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x86, 0xB7, 0xFF))),
            new Setter(Inline.TextDecorationsProperty, TextDecorations.Underline),
            new Setter(TextElement.FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(new Trigger
        {
            Property = FrameworkContentElement.IsKeyboardFocusedProperty,
            Value = true,
            Setters =
            {
                new Setter(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2B, 0x53, 0x8A))),
                new Setter(TextElement.ForegroundProperty, Brushes.White)
            }
        });

        return style;
    }

    private static Style CreateStyle<T>(params Setter[] setters)
    {
        var style = new Style(typeof(T));
        foreach (var setter in setters)
        {
            style.Setters.Add(setter);
        }

        return style;
    }

    private sealed class SafeWpfRenderer : WpfRenderer
    {
        private readonly Action<Uri> _openHttpLink;

        public SafeWpfRenderer(Action<Uri> openHttpLink)
        {
            _openHttpLink = openHttpLink;
        }

        protected override void LoadRenderers()
        {
            ObjectRenderers.Clear();
            ObjectRenderers.Add(new SafeCodeBlockRenderer());
            ObjectRenderers.Add(new ListRenderer());
            ObjectRenderers.Add(new HeadingRenderer());
            ObjectRenderers.Add(new ParagraphRenderer());
            ObjectRenderers.Add(new QuoteBlockRenderer());
            ObjectRenderers.Add(new ThematicBreakRenderer());
            ObjectRenderers.Add(new SafeAutolinkInlineRenderer(_openHttpLink));
            ObjectRenderers.Add(new SafeCodeInlineRenderer());
            ObjectRenderers.Add(new DelimiterInlineRenderer());
            ObjectRenderers.Add(new EmphasisInlineRenderer());
            ObjectRenderers.Add(new HtmlEntityInlineRenderer());
            ObjectRenderers.Add(new LineBreakInlineRenderer());
            ObjectRenderers.Add(new SafeLinkInlineRenderer(_openHttpLink));
            ObjectRenderers.Add(new LiteralInlineRenderer());
            ObjectRenderers.Add(new SafeTableRenderer());
            ObjectRenderers.Add(new TaskListRenderer());
        }
    }

    private sealed class SafeCodeInlineRenderer : WpfObjectRenderer<CodeInline>
    {
        protected override void Write(WpfRenderer renderer, CodeInline obj)
        {
            var run = new Run(obj.Content)
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1)),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))
            };

            renderer.WriteInline(run);
        }
    }

    private sealed class SafeAutolinkInlineRenderer : WpfObjectRenderer<AutolinkInline>
    {
        private readonly Action<Uri> _openHttpLink;

        public SafeAutolinkInlineRenderer(Action<Uri> openHttpLink)
        {
            _openHttpLink = openHttpLink;
        }

        protected override void Write(WpfRenderer renderer, AutolinkInline link)
        {
            if (TryGetAllowedHttpUri(link.Url, out var uri))
            {
                var hyperlink = new Hyperlink
                {
                    NavigateUri = uri,
                    ToolTip = link.Url
                };
                hyperlink.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.HyperlinkStyleKey);
                hyperlink.Click += (_, _) => _openHttpLink(uri!);
                renderer.Push(hyperlink);
                renderer.WriteText(link.Url);
                renderer.Pop();
                return;
            }

            renderer.WriteText(link.Url);
        }
    }

    private sealed class SafeLinkInlineRenderer : WpfObjectRenderer<LinkInline>
    {
        private readonly Action<Uri> _openHttpLink;

        public SafeLinkInlineRenderer(Action<Uri> openHttpLink)
        {
            _openHttpLink = openHttpLink;
        }

        protected override void Write(WpfRenderer renderer, LinkInline link)
        {
            var resolvedUrl = link.GetDynamicUrl?.Invoke() ?? link.Url;
            if (link.IsImage)
            {
                var altText = ExtractInlinePlainText(link);
                renderer.WriteText(BuildBlockedImageIndicator(altText));
                return;
            }

            if (TryGetAllowedHttpUri(resolvedUrl, out var uri))
            {
                var hyperlink = new Hyperlink
                {
                    NavigateUri = uri,
                    ToolTip = string.IsNullOrWhiteSpace(link.Title) ? uri!.AbsoluteUri : link.Title
                };
                hyperlink.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.HyperlinkStyleKey);
                hyperlink.Click += (_, _) => _openHttpLink(uri!);
                renderer.Push(hyperlink);
                if (link.FirstChild is null)
                {
                    renderer.WriteText(uri!.AbsoluteUri);
                }
                else
                {
                    ((RendererBase)renderer).WriteChildren(link);
                }

                renderer.Pop();
                return;
            }

            if (link.FirstChild is null)
            {
                renderer.WriteText(string.IsNullOrWhiteSpace(resolvedUrl) ? "link" : resolvedUrl);
                return;
            }

            ((RendererBase)renderer).WriteChildren(link);
        }
    }

    private sealed class SafeCodeBlockRenderer : WpfObjectRenderer<CodeBlock>
    {
        protected override void Write(WpfRenderer renderer, CodeBlock block)
        {
            var codeText = GetBlockText(block);
            var textBox = new TextBox
            {
                Text = codeText,
                IsReadOnly = true,
                IsReadOnlyCaretVisible = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Child = textBox
            };

            renderer.WriteBlock(new BlockUIContainer(border)
            {
                Margin = new Thickness(0, 4, 0, 10)
            });
        }

        private static string GetBlockText(CodeBlock block)
        {
            if (block is not LeafBlock leafBlock || leafBlock.Lines.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < leafBlock.Lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(leafBlock.Lines.Lines?[i].Slice.ToString() ?? string.Empty);
            }

            return builder.ToString();
        }
    }

    private sealed class SafeTableRenderer : WpfObjectRenderer<MarkdigTable>
    {
        protected override void Write(WpfRenderer renderer, MarkdigTable table)
        {
            var flowTable = new System.Windows.Documents.Table
            {
                CellSpacing = 0
            };

            foreach (var _ in table.ColumnDefinitions)
            {
                flowTable.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            flowTable.RowGroups.Add(rowGroup);

            foreach (var row in table)
            {
                if (row is not MarkdigTableRow markdigRow)
                {
                    continue;
                }

                var flowRow = new TableRow();
                rowGroup.Rows.Add(flowRow);
                foreach (var cell in markdigRow)
                {
                    if (cell is not MarkdigTableCell markdigCell)
                    {
                        continue;
                    }

                    var flowCell = new TableCell
                    {
                        Padding = new Thickness(8, 4, 8, 4),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = markdigRow.IsHeader
                            ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C))
                            : Brushes.Transparent
                    };

                    renderer.Push(flowCell);
                    ((RendererBase)renderer).Write(markdigCell);
                    renderer.Pop();
                    flowRow.Cells.Add(flowCell);
                }
            }

            var tableDocument = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))
            };
            tableDocument.Blocks.Add(flowTable);

            var tableViewer = new RichTextBox
            {
                IsReadOnly = true,
                IsReadOnlyCaretVisible = true,
                IsDocumentEnabled = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Document = tableDocument
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 10),
                Child = tableViewer
            };

            renderer.WriteBlock(new BlockUIContainer(border));
        }
    }
}
