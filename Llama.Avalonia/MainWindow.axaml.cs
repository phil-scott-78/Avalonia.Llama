using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData.Binding;
using LLama;
using Llama.Avalonia.Controls.ChatUI;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SukiUI.Controls;
using SukiUI.Toasts;

namespace Llama.Avalonia;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

public partial class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly ReactiveCommand<Unit, Unit> _chat;
    public ICommand Chat => _chat;

    [Reactive] private bool _receivingReply;
    [Reactive] private string _chatMessage;
    [Reactive] private ISukiToastManager _toastManager;
    private readonly LlamaService _llamaService;

    public IObservable<bool> CanChat => this.WhenAnyValue(x => x.ChatMessage, x => x.ReceivingReply,
        (message, receiving) => !string.IsNullOrWhiteSpace(message) && !receiving
    );

    public MainWindowViewModel(ISukiToastManager toastManager, LlamaService llamaService)
    {
        _chatMessage = string.Empty;
        _toastManager = toastManager;
        _llamaService = llamaService;
        
        Messages =
        [
            new ChatMessage
            {
                Content =
                    "Hey there, can I interest you in a new recipe to try, support with a **product return**, or thwarting a robot uprising?",
                IsUser = false
            }
        ];

        _chat = ReactiveCommand.CreateFromTask(async () => await HandleUserMessage(ChatMessage), CanChat);

        _ = llamaService.LoadModel();
    }

    private async Task HandleUserMessage(string content)
    {

        ReceivingReply = true;
        ChatMessage = string.Empty;

        var userMessage = new ChatMessage
        {
            Content = content,
            IsUser = true,
            IsWriting = true
        };
        Messages.Add(userMessage);


        var botMessage = new ChatMessage
        {
            Content = "Hmmm, let me think...",
            IsUser = false,
            IsWriting = true
        };

        Messages.Add(botMessage);

        while (_llamaService.IsLoaded == false)
        {
            await Task.Delay(50);
        }

        // skip the last bot message for the prompt
        var prompt = _llamaService.GetPrompt(Messages.SkipLast(1).ToArray());

        try
        {
            var sb = new StringBuilder();
            await foreach (var text in _llamaService.InferAsync(prompt))
            {
                sb.Append(text);
                botMessage.Content = sb.ToString();
            }
        }
        catch (Exception ex)
        {
            botMessage.Content = $"Error: {ex.Message}";
        }
        finally
        {
            ReceivingReply = false;
            botMessage.IsWriting = false;
        }
    }


    public ObservableCollection<ChatMessage> Messages { get; }


    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chat.Dispose();
            _llamaService.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MainWindowViewModel()
    {
        Dispose(false);
    }
}