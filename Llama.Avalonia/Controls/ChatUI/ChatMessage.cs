using ReactiveUI;

namespace Llama.Avalonia.Controls.ChatUI
{
    public class ChatMessage : ReactiveObject
    {
        private string _content = string.Empty;
        private bool _isUser;
        private bool _isWriting;

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public bool IsUser
        {
            get => _isUser;
            set => this.RaiseAndSetIfChanged(ref _isUser, value);
        }

        public bool IsWriting
        {
            get => _isWriting;
            set => this.RaiseAndSetIfChanged(ref _isWriting, value);
        }
    }
}