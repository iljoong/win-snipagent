using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SnipAgent.App.Overlays.Markdown;

namespace SnipAgent.Tests;

public class AiAnswerMarkdownPresenterTests
{
    [Fact]
    public void TryGetAllowedHttpUri_AllowsOnlyAbsoluteHttpOrHttps()
    {
        Assert.True(AiAnswerMarkdownPresenter.TryGetAllowedHttpUri("https://example.com", out var httpsUri));
        Assert.NotNull(httpsUri);
        Assert.True(AiAnswerMarkdownPresenter.TryGetAllowedHttpUri("http://example.com", out var httpUri));
        Assert.NotNull(httpUri);
        Assert.False(AiAnswerMarkdownPresenter.TryGetAllowedHttpUri("file:///tmp/test", out _));
        Assert.False(AiAnswerMarkdownPresenter.TryGetAllowedHttpUri("/relative/path", out _));
        Assert.False(AiAnswerMarkdownPresenter.TryGetAllowedHttpUri("javascript:alert(1)", out _));
    }

    [Fact]
    public void BuildBlockedImageIndicator_ContainsAltText()
    {
        Assert.Equal("[Image blocked: chart]", AiAnswerMarkdownPresenter.BuildBlockedImageIndicator(" chart "));
        Assert.Equal("[Image blocked: no description]", AiAnswerMarkdownPresenter.BuildBlockedImageIndicator(null));
    }

    [Fact]
    public void Render_ShowsBlockedImageIndicator_AndLiteralHtml()
    {
        RunSta(() =>
        {
            var presenter = new AiAnswerMarkdownPresenter(_ => { });
            var document = presenter.Render("![chart](https://example.com/img.png)\n\n<div>raw</div>");
            var renderedText = GetDocumentText(document);
            Assert.Contains("[Image blocked: chart]", renderedText);
            Assert.Contains("<div>raw</div>", renderedText);
        });
    }

    [Fact]
    public void Render_AllowsOnlyHttpHyperlinkActivation()
    {
        RunSta(() =>
        {
            var opened = new List<Uri>();
            var presenter = new AiAnswerMarkdownPresenter(opened.Add);
            var document = presenter.Render("[allowed](https://example.com) [blocked](file:///tmp/a)");
            var hyperlinks = GetHyperlinks(document);
            Assert.Single(hyperlinks);
            hyperlinks[0].RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent, hyperlinks[0]));
            Assert.Single(opened);
            Assert.Equal("https://example.com/", opened[0].AbsoluteUri);
        });
    }

    [Fact]
    public void Render_FencedCodeBlock_CreatesScrollableCodeContainer()
    {
        RunSta(() =>
        {
            var presenter = new AiAnswerMarkdownPresenter(_ => { });
            var document = presenter.Render("```txt\nalpha\nbeta\n```");
            var codeTextBoxes = FindDescendants<TextBox>(document);
            Assert.Single(codeTextBoxes);
            Assert.Contains("alpha", codeTextBoxes[0].Text);
            Assert.Contains("beta", codeTextBoxes[0].Text);
            Assert.Equal(ScrollBarVisibility.Auto, codeTextBoxes[0].HorizontalScrollBarVisibility);
        });
    }

    [Fact]
    public void Render_Table_CreatesScrollableTableContainer()
    {
        RunSta(() =>
        {
            var presenter = new AiAnswerMarkdownPresenter(_ => { });
            var document = presenter.Render("| A | B |\n|---|---|\n| 1 | 2 |");
            var tableViewers = FindDescendants<RichTextBox>(document);
            Assert.Single(tableViewers);
            var table = Assert.IsType<Table>(tableViewers[0].Document.Blocks.FirstBlock);
            Assert.Equal(2, table.RowGroups[0].Rows.Count);
            Assert.Equal(2, table.RowGroups[0].Rows[0].Cells.Count);
        });
    }

    [Fact]
    public void Render_InlineCode_UsesLightForeground()
    {
        RunSta(() =>
        {
            var presenter = new AiAnswerMarkdownPresenter(_ => { });
            var document = presenter.Render("`headless product`");
            var renderedText = GetDocumentText(document);
            Assert.Contains("headless product", renderedText);

            var codeRun = FindRun(document, "headless product");
            var codeBrush = Assert.IsType<SolidColorBrush>(codeRun.Foreground);
            Assert.Equal(Color.FromRgb(0xF1, 0xF1, 0xF1), codeBrush.Color);
        });
    }

    private static string GetDocumentText(FlowDocument document)
        => new TextRange(document.ContentStart, document.ContentEnd).Text;

    private static List<Hyperlink> GetHyperlinks(FlowDocument document)
    {
        var hyperlinks = new List<Hyperlink>();
        foreach (var block in document.Blocks)
        {
            CollectHyperlinksFromBlock(block, hyperlinks);
        }

        return hyperlinks;
    }

    private static void CollectHyperlinksFromBlock(Block block, ICollection<Hyperlink> output)
    {
        switch (block)
        {
            case Paragraph paragraph:
                CollectHyperlinksFromInlineCollection(paragraph.Inlines, output);
                break;
            case Section section:
                foreach (var child in section.Blocks)
                {
                    CollectHyperlinksFromBlock(child, output);
                }

                break;
            case System.Windows.Documents.List list:
                foreach (var item in list.ListItems)
                {
                    foreach (var child in item.Blocks)
                    {
                        CollectHyperlinksFromBlock(child, output);
                    }
                }

                break;
        }
    }

    private static void CollectHyperlinksFromInlineCollection(InlineCollection inlines, ICollection<Hyperlink> output)
    {
        foreach (var inline in inlines)
        {
            if (inline is Hyperlink hyperlink)
            {
                output.Add(hyperlink);
            }

            if (inline is Span span)
            {
                CollectHyperlinksFromInlineCollection(span.Inlines, output);
            }
        }
    }

    private static List<T> FindDescendants<T>(FlowDocument document) where T : DependencyObject
    {
        var results = new List<T>();
        foreach (var block in document.Blocks)
        {
            CollectDescendants(block, results);
        }

        return results;
    }

    private static void CollectDescendants<T>(DependencyObject current, ICollection<T> output) where T : DependencyObject
    {
        if (current is T match)
        {
            output.Add(match);
        }

        if (current is BlockUIContainer blockUi)
        {
            if (blockUi.Child is not null)
            {
                CollectDescendants(blockUi.Child, output);
            }

            return;
        }

        if (current is Border border && border.Child is not null)
        {
            CollectDescendants(border.Child, output);
            return;
        }

        if (current is RichTextBox richTextBox)
        {
            foreach (var block in richTextBox.Document.Blocks)
            {
                CollectDescendants(block, output);
            }

            return;
        }

        if (current is FlowDocument flowDocument)
        {
            foreach (var block in flowDocument.Blocks)
            {
                CollectDescendants(block, output);
            }

            return;
        }

        if (current is Section section)
        {
            foreach (var block in section.Blocks)
            {
                CollectDescendants(block, output);
            }
        }
    }

    private static Run FindRun(FlowDocument document, string text)
    {
        foreach (var block in document.Blocks)
        {
            var run = FindRunInBlock(block, text);
            if (run is not null)
            {
                return run;
            }
        }

        throw new Xunit.Sdk.XunitException($"Run containing '{text}' was not found.");
    }

    private static Run? FindRunInBlock(Block block, string text)
    {
        return block switch
        {
            Paragraph paragraph => FindRunInInlines(paragraph.Inlines, text),
            Section section => section.Blocks.Select(child => FindRunInBlock(child, text)).FirstOrDefault(run => run is not null),
            System.Windows.Documents.List list => list.ListItems
                .SelectMany(item => item.Blocks.Select(child => FindRunInBlock(child, text)))
                .FirstOrDefault(run => run is not null),
            _ => null
        };
    }

    private static Run? FindRunInInlines(InlineCollection inlines, string text)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when run.Text.Contains(text, StringComparison.Ordinal):
                    return run;
                case Span span:
                {
                    var nested = FindRunInInlines(span.Inlines, text);
                    if (nested is not null)
                    {
                        return nested;
                    }

                    break;
                }
            }
        }

        return null;
    }

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
