using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Llama.Avalonia.Controls;

public class MarkdownTextBlock : ContentControl
{
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string>(
            nameof(Markdown),
            defaultValue: string.Empty);

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private readonly StackPanel _rootPanel;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownTextBlock()
    {
        _rootPanel = new StackPanel
        {
            Spacing = 10,
            Orientation = Orientation.Vertical
        };

        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        Content = _rootPanel;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
        {
            UpdateMarkdown();
        }
    }

    private List<Control>? _previousControls;
    private MarkdownDocument? _previousDocument;

    private void UpdateMarkdown()
    {
        if (string.IsNullOrEmpty(Markdown))
        {
            _rootPanel.Children.Clear();
            _previousControls = null;
            _previousDocument = null;
            return;
        }

        var document = Markdig.Markdown.Parse(Markdown, _pipeline);

        // First time or complete reset needed
        if (_previousControls == null || _previousDocument == null)
        {
            _previousControls = document.Select(RenderBlock).ToList();
            _rootPanel.Children.Clear();
            _rootPanel.Children.AddRange(_previousControls);
            _previousDocument = document;
            return;
        }

        // Compare the blocks to determine what's new
        var previousBlockCount = _previousDocument.Count;
        var currentBlockCount = document.Count;

        // Reuse existing controls up to the minimum length
        var minBlockCount = Math.Min(previousBlockCount, currentBlockCount);
        for (var i = 0; i < minBlockCount; i++)
        {
            // Only re-render if the block content has changed
            if (BlocksEqual(_previousDocument[i], document[i])) continue;
            var newControl = RenderBlock(document[i]);
            _previousControls[i] = newControl;
            _rootPanel.Children[i] = newControl;
        }

        // Handle any new blocks
        if (currentBlockCount > previousBlockCount)
        {
            var newBlocks = document.Skip(previousBlockCount)
                .Select(RenderBlock)
                .ToList();
            _previousControls.AddRange(newBlocks);
            _rootPanel.Children.AddRange(newBlocks);
        }
        else if (currentBlockCount < previousBlockCount)
        {
            // Remove any excess blocks if the document got shorter
            for (var i = currentBlockCount; i < previousBlockCount; i++)
            {
                _rootPanel.Children.RemoveAt(currentBlockCount);
            }

            _previousControls.RemoveRange(currentBlockCount, previousBlockCount - currentBlockCount);
        }

        _previousDocument = document;
    }

    private static bool BlocksEqual(Block block1, Block block2)
    {
        // Implement block comparison logic here
        // This could be based on content hash, ToString() comparison, 
        // or a more sophisticated comparison depending on your needs
        return block1.ToPositionText() == block2.ToPositionText();
    }

    private Control RenderBlock(Block block)
    {
        return block switch
        {
            ParagraphBlock paragraph => CreateTextBlock(paragraph, FontWeight.Normal),
            HeadingBlock heading => CreateTextBlock(heading, FontWeight.Bold),
            ListBlock list => BuildListItemControl(list),
            QuoteBlock quoteBlock => BuildQuoteControl(quoteBlock),
            FencedCodeBlock codeBlock => BuildCodeControl(codeBlock),
            _ => new TextBlock()
        };
    }

    private static Border BuildCodeControl(FencedCodeBlock codeBlock)
    {
        var codeText = new StringBuilder();
        foreach (var line in codeBlock.Lines.Lines)
        {
            if (line.Slice.Length > 0)
            {
                codeText.AppendLine(line.Slice.ToString());
            }
        }

        var cb = new TextBlock
        {
            Text = codeText.ToString().Trim(),
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };

        return new Border
        {
            Child = cb,
            Background = new SolidColorBrush(new Color(10, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
        };
    }

    private Border BuildQuoteControl(QuoteBlock quoteBlock)
    {
        var quotePanel = new StackPanel { Spacing = 5 };
        foreach (var content in quoteBlock.Select(RenderBlock))
        {
            quotePanel.Children.Add(content);
        }

        var border = new Border
        {
            Child = quotePanel,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(16, 8, 8, 8),
            Margin = new Thickness(0, 5, 0, 5),
            [!Border.BorderBrushProperty] = new DynamicResourceExtension("SukiAccentColor")
        };
        return border;
    }

    private StackPanel BuildListItemControl(ListBlock list)
    {
        var listPanel = new StackPanel { Spacing = 5 };
        var itemNumber = 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;
            var itemPanel = new DockPanel();

            // Add bullet or number
            var bullet = new TextBlock
            {
                Text = list.IsOrdered ? $"{itemNumber++}. " : "• ",
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 5, 0)
            };
            DockPanel.SetDock(bullet, Dock.Left);
            itemPanel.Children.Add(bullet);

            // Create content panel for the list item
            var contentPanel = new StackPanel();
            contentPanel.Children.AddRange(listItem.Select(RenderBlock));
            itemPanel.Children.Add(contentPanel);
            listPanel.Children.Add(itemPanel);
        }

        return listPanel;
    }

    private TextBlock CreateTextBlock(LeafBlock block, FontWeight weight)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = weight,
        };

        if (textBlock.Inlines != null && block.Inline != null)
        {
            AddInlineContent(textBlock.Inlines, block.Inline);
        }

        return textBlock;
    }

    private static void AddInlineContent(InlineCollection inlines, ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case CodeInline code:
                    inlines.Add(new Run
                    {
                        Text = code.Content,
                        [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("SukiAccentColor")
                    });
                    break;
                case LiteralInline literal:
                    inlines.Add(new Run
                    {
                        Text = literal.Content.ToString(),
                    });
                    break;

                case EmphasisInline emphasis:
                    var span = new Span();

                    switch (emphasis.DelimiterCount)
                    {
                        case 1:
                            span.FontStyle = FontStyle.Italic;
                            break;
                        case 2:
                            span.FontWeight = FontWeight.Bold;
                            break;
                    }

                    AddInlineContent(span.Inlines, emphasis);
                    inlines.Add(span);
                    break;

                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;

                default:
                    if (inline is ContainerInline containerInline)
                    {
                        AddInlineContent(inlines, containerInline);
                    }

                    break;
            }
        }
    }
}