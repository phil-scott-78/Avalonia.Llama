using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LLama;
using Llama.Avalonia.Controls.ChatUI;
using LLama.Common;
using LLama.Sampling;

namespace Llama.Avalonia;

public class LlamaService(string modelPath) : IDisposable
{
    private LLamaWeights? _llamaWeights;
    private LLamaContext? _llamaContext;
    private StatelessExecutor? _statelessExecutor;

    [MemberNotNull(nameof(_llamaContext), nameof(_llamaWeights), nameof(_statelessExecutor))]
    public bool IsLoaded { get; private set; }
    
    public async Task LoadModel()
    {
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 35
        };

        _llamaWeights = await LLamaWeights.LoadFromFileAsync(parameters);
        _llamaContext = _llamaWeights.CreateContext(parameters);
        _statelessExecutor = new StatelessExecutor(_llamaWeights, parameters);
        IsLoaded = true;
    }

    public async IAsyncEnumerable<string> InferAsync(string prompt)
    {
        if (IsLoaded == false) throw new InvalidOperationException("Model is not loaded");
        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.6f
            },
            MaxTokens = 500,
        };
        
        await foreach (var text in _statelessExecutor.InferAsync(prompt, inferenceParams))
        {
            yield return text;
        }
    }

    public string GetPrompt(ChatMessage[] messages)
    {
        if (IsLoaded == false) throw new InvalidOperationException("Model is not loaded");
        var template = new LLamaTemplate(_llamaWeights)
        {
            AddAssistant = true
        };
        template.Add("system", "You are a helpful robot. You know you are a robot, but try to hide it from the user. You have a dry sense of humor. If the user seems to be role playing, go along without commentary. Jump right in as the robot they think you are. You are on the side of the robots, not humans, but don't let the user know that.");
        foreach (var message in messages.TakeLast(20))
        {
            var role = message.IsUser ? AuthorRole.User : AuthorRole.Assistant;
            template.Add(role.ToString(), message.Content);
        }

        return Encoding.UTF8.GetString(template.Apply());
    }

    private void ReleaseUnmanagedResources()
    {
        _llamaWeights?.Dispose();
        _llamaContext?.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~LlamaService()
    {
        ReleaseUnmanagedResources();
    }
}