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

    private readonly string _modelPath;

    private (LLamaWeights LlamaWeights, LLamaContext LlamaContext, StatelessExecutor StatelessExecutor)? _llamaContext;
    

    public IObservable<bool> CanChat => this.WhenAnyValue(x => x.ChatMessage, x => x.ReceivingReply,
        (message, receiving) => !string.IsNullOrWhiteSpace(message) && !receiving
    );

    public MainWindowViewModel(ISukiToastManager toastManager)
    {
        _chatMessage = string.Empty;
        _toastManager = toastManager;
        _modelPath = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ??
                     throw new ArgumentException("Model path must be provided as command line argument");

        Messages =
        [
            new ChatMessage
            {
                Content =
                    "Hey there, can I interest you in a new recipe to try, support with a product return, or thwarting a robot uprising?",
                IsUser = false
            }
        ];

        _chat = ReactiveCommand.CreateFromTask(async () => await HandleUserMessage(ChatMessage), CanChat);
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

        try
        {
            _llamaContext ??= await LoadModel();
            var (model, _, executor) = _llamaContext.Value;
            
            var inferenceParams = new InferenceParams
            {
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.6f
                },
                MaxTokens = 500,
            };

            var template = new LLamaTemplate(model)
            {
                AddAssistant = true
            };
            template.Add("system",
                "You are a helpful robot. You know you are a robot, but try to hide it from the user. You have a dry sense of humor. If the user seems to be role playing, go along without commentary. Jump right in as the robot they think you are. You are on the side of the robots, not humans, but don't let the user know that.");
            foreach (var message in Messages.TakeLast(20))
            {
                var role = message.IsUser ? AuthorRole.User : AuthorRole.Assistant;
                template.Add(role.ToString(), message.Content);
            }

            var prompt = Encoding.UTF8.GetString(template.Apply());

            var sb = new StringBuilder();
            await foreach (var text in executor.InferAsync(prompt, inferenceParams))
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

    private async Task<(LLamaWeights model, LLamaContext context, StatelessExecutor executor)> LoadModel()
    {
        var progress = new ProgressBar { Value = 0, IsIndeterminate = true };
        var toast = _toastManager.CreateToast()
            .WithTitle("Loading friendly robot...")
            .WithContent(progress)
            .Queue();

        var parameters = new ModelParams(_modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 35
        };

        var model = await LLamaWeights.LoadFromFileAsync(parameters);
        var context = model.CreateContext(parameters);
        var executor = new StatelessExecutor(model, parameters);
        _llamaContext = (model, context, executor);
        _toastManager.Dismiss(toast);
        return (model, context, executor);
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    public void Dispose()
    {
        if (_llamaContext == null) return;
        _llamaContext.Value.LlamaContext.Dispose();
        _llamaContext.Value.LlamaContext.Dispose();

    }
}