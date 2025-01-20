using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;

namespace Llama.Avalonia.Controls.ChatUI;

public partial class ChatUi : UserControl
{
    private readonly CompositeDisposable _disposables = new();

    
    public ChatUi()
    {
        InitializeComponent();
        
        this.WhenAnyValue(x => x.Messages)
            .WhereNotNull()
            .Do(messages =>
            {
                // Clear previous subscriptions when Messages collection changes
                _disposables.Clear();

                // Subscribe to collection changes
                var collectionChanges = Observable.FromEvent<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    handler => (_, e) => handler(e),
                    handler => messages.CollectionChanged += handler,
                    handler => messages.CollectionChanged -= handler)
                    .Do(args =>
                    {
                        // Subscribe to property changes of new items
                        if (args.NewItems != null)
                        {
                            foreach (ChatMessage message in args.NewItems)
                            {
                                SubscribeToMessageChanges(message);
                            }
                        }
                    });

                // Subscribe to property changes of existing items
                foreach (var message in messages)
                {
                    SubscribeToMessageChanges(message);
                }

                // Combine collection changes with property change notifications
                collectionChanges
                    .Sample(TimeSpan.FromMilliseconds(250))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => ScrollToBottom())
                    .DisposeWith(_disposables);
            })
            .Subscribe()
            .DisposeWith(_disposables);
    }

    private void SubscribeToMessageChanges(ChatMessage message)
    {
        // Assuming ChatMessage implements INotifyPropertyChanged
        Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            handler => message.PropertyChanged += handler,
            handler => message.PropertyChanged -= handler)
            .Sample(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ScrollToBottom())
            .DisposeWith(_disposables);
    }

    private ScrollViewer? _chatScroll;
   private CancellationTokenSource? _animationToken;

    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _chatScroll =  e.NameScope.Get<ScrollViewer>("ChatScrollViewer");
    }

    public static readonly StyledProperty<ObservableCollection<ChatMessage>> MessagesProperty =
        AvaloniaProperty.Register<ChatUi, ObservableCollection<ChatMessage>>(nameof(Messages), 
            defaultValue: new ObservableCollection<ChatMessage>());

    public ObservableCollection<ChatMessage> Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }
      

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ChatUi, string>(nameof(Text));
        
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
        
    public static readonly StyledProperty<IImage?> UserImageSourceProperty =
        AvaloniaProperty.Register<ChatUi, IImage?>(nameof(UserImageSource));
        
    public IImage? UserImageSource
    {
        get => GetValue(UserImageSourceProperty);
        set => SetValue(UserImageSourceProperty, value);
    }
        
    public static readonly StyledProperty<IImage?> FriendImageSourceProperty =
        AvaloniaProperty.Register<ChatUi, IImage?>(nameof(FriendImageSource));
        
    public IImage? FriendImageSource
    {
        get => GetValue(FriendImageSourceProperty);
        set => SetValue(FriendImageSourceProperty, value);
    }
        
    private void ScrollToBottom()
    {
        if (_chatScroll == null) throw new InvalidOperationException("ChatScroll is not set");
        
        _animationToken?.Cancel();
        _animationToken = new CancellationTokenSource();
        new Animation
        {
            Duration = TimeSpan.FromMilliseconds(800),
            FillMode = FillMode.Forward,
            Easing = new CubicEaseInOut(),
            IterationCount = new IterationCount(1),
            PlaybackDirection = PlaybackDirection.Normal,
            Children =
            {
                new KeyFrame()
                {
                    Setters = { new Setter { Property = ScrollViewer.OffsetProperty, Value = _chatScroll.Offset } },
                    KeyTime = TimeSpan.FromSeconds(0)
                },
                new KeyFrame()
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = ScrollViewer.OffsetProperty,
                            Value = new Vector(_chatScroll.Offset.X, _chatScroll.Offset.Y + 500)
                        }
                    },
                    KeyTime = TimeSpan.FromMilliseconds(800)
                }
            }
        }.RunAsync(_chatScroll, _animationToken.Token);
    }
}

public class ContentToControlConverter : IValueConverter
{
    public static ContentToControlConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return new MarkdownTextBlock { Markdown = s };

        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}