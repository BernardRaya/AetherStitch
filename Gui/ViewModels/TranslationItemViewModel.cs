using AetherStitch.Models;

namespace Gui.ViewModels
{
    public class TranslationItemViewModel : ViewModelBase
    {
        private readonly Translation _translation;
        private string _target;

        public TranslationItemViewModel(Translation translation)
        {
            _translation = translation;
            _target = translation.Target;
        }

        public Translation Translation => _translation;

        public string Key => _translation.Key;
        public string Source => _translation.Source;

        public string Target
        {
            get => _target;
            set
            {
                if (SetProperty(ref _target, value))
                {
                    _translation.Target = value;
                    UpdateTranslationStatus();
                    OnPropertyChanged(nameof(IsTranslated));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public StringType Type => _translation.Type;
        public TranslationStatus Status => _translation.Status;

        public bool IsTranslated => _translation.Status == TranslationStatus.Translated;

        public string StatusText => IsTranslated ? "已翻译" : "待翻译";

        public string TypeText => Type == StringType.Literal ? "字面量" : "插值字符串";

        public int UsageCount => _translation.UsageCount;

        public IReadOnlyList<ContextReference> Contexts => _translation.Contexts;

        public string FirstContextLocation
        {
            get
            {
                if (Contexts.Count == 0) return "无上下文";
                var first = Contexts[0];
                return $"{first.FilePath}:{first.LineNumber}";
            }
        }

        private void UpdateTranslationStatus()
        {
            if (!string.IsNullOrEmpty(_target) && _target != _translation.Source)
            {
                _translation.Status = TranslationStatus.Translated;
                _translation.TranslatedAt = DateTime.UtcNow;
            }
            else
            {
                _translation.Status = TranslationStatus.Pending;
                _translation.TranslatedAt = null;
            }
        }

        // 公开方法以便外部调用
        public new void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
        }
    }
}
