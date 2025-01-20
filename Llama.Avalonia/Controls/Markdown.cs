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

    private void UpdateMarkdown()
    {
        _rootPanel.Children.Clear();

        if (string.IsNullOrEmpty(Markdown))
            return;

        var document = Markdig.Markdown.Parse(Markdown, _pipeline);
        foreach (var block in document)
        {
            var element = RenderBlock(block);
            _rootPanel.Children.Add(element);
        }
    }

    private Control RenderBlock(Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                return CreateTextBlock(paragraph, FontWeight.Normal);

            case HeadingBlock heading:
                return CreateTextBlock(heading, FontWeight.Bold);

            case ListBlock list:
                var listPanel = new StackPanel { Spacing = 5 };
                int itemNumber = 1;

                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
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
                        foreach (var itemBlock in listItem)
                        {
                            var content = RenderBlock(itemBlock);
                            contentPanel.Children.Add(content);
                        }

                        itemPanel.Children.Add(contentPanel);

                        listPanel.Children.Add(itemPanel);
                    }
                }

                return listPanel;
            case QuoteBlock quoteBlock:
                var quotePanel = new StackPanel { Spacing = 5 };
                foreach (var childBlock in quoteBlock)
                {
                    var content = RenderBlock(childBlock);
                    if (content != null)
                    {
                        quotePanel.Children.Add(content);
                    }
                }

                var border = new Border
                {
                    Child = quotePanel,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    Padding = new Thickness(16, 8, 8, 8),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                    
                border[!Border.BorderBrushProperty] = new DynamicResourceExtension("SukiAccentColor");

                return border;
            case FencedCodeBlock codeBlock:
                var codeText = new StringBuilder();
                if (codeBlock.Lines.Lines == null) return new TextBlock();
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
                    Background = new SolidColorBrush(new Color(10,255,255,255)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                };
            default:
                return new TextBlock();
        }
    }

    private TextBlock CreateTextBlock(LeafBlock block, FontWeight weight)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = weight,
            // FontSize = fontSize
        };

        if (block.Inline != null)
        {
            AddInlineContent(textBlock.Inlines, block.Inline);
        }

        return textBlock;
    }

    private void AddInlineContent(InlineCollection inlines, ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case CodeInline code:
                    inlines.Add( new Run
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


                    if (emphasis.DelimiterCount == 2)
                    {
                        span.FontWeight = FontWeight.Bold;
                    }
                    else if (emphasis.DelimiterCount == 1)
                    {
                        span.FontStyle = FontStyle.Italic;
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