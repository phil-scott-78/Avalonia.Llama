using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Llama.Avalonia.Controls.ChatUI;

public partial class ChatUI : UserControl
{
    private readonly CompositeDisposable _disposables = new();

    
    public ChatUI()
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
                    handler => (s, e) => handler(e),
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

    private ScrollViewer ChatScroll;
        
        
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        ChatScroll =  e.NameScope.Get<ScrollViewer>("ChatScrollViewer");
    }

    public static readonly StyledProperty<ObservableCollection<ChatMessage>> MessagesProperty =
        AvaloniaProperty.Register<ChatUI, ObservableCollection<ChatMessage>>(nameof(Messages), 
            defaultValue: new ObservableCollection<ChatMessage>());

    public ObservableCollection<ChatMessage> Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }
      

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ChatUI, string>(nameof(Text));
        
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
        
    public static readonly StyledProperty<IImage?> UserImageSourceProperty =
        AvaloniaProperty.Register<ChatUI, IImage?>(nameof(UserImageSource));
        
    public IImage? UserImageSource
    {
        get => GetValue(UserImageSourceProperty);
        set => SetValue(UserImageSourceProperty, value);
    }
        
    public static readonly StyledProperty<IImage?> FriendImageSourceProperty =
        AvaloniaProperty.Register<ChatUI, IImage?>(nameof(FriendImageSource));
        
    public IImage? FriendImageSource
    {
        get => GetValue(FriendImageSourceProperty);
        set => SetValue(FriendImageSourceProperty, value);
    }
        
    private CancellationTokenSource AnimationToken;
        
    private void ScrollToBottom()
    {
        AnimationToken?.Cancel();
        AnimationToken = new CancellationTokenSource();
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
                    Setters = { new Setter { Property = ScrollViewer.OffsetProperty, Value = ChatScroll.Offset } },
                    KeyTime = TimeSpan.FromSeconds(0)
                },
                new KeyFrame()
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = ScrollViewer.OffsetProperty,
                            Value = new Vector(ChatScroll.Offset.X, ChatScroll.Offset.Y + 500)
                        }
                    },
                    KeyTime = TimeSpan.FromMilliseconds(800)
                }
            }
        }.RunAsync(ChatScroll, AnimationToken.Token);
    }
}

public class ContentToControlConverter : IValueConverter
{
    public static ContentToControlConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string)
            return new TextBlock() { Text = (string?)value };

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}