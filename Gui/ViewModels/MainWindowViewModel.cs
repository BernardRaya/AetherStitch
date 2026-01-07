using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using AetherStitch.Models;
using AetherStitch.Services;
using System.Windows;
using Gui.Services;
using Gui.Views;

namespace Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly MappingFileService _mappingService;
        private readonly CodePreviewService _codePreviewService;
        private LocalizationMapping? _currentMapping;
        private TranslationItemViewModel? _selectedTranslation;
        private ContextReference? _selectedContext;
        private string? _currentFilePath;
        private string _filterText = string.Empty;
        private TranslationFilter _currentFilter = TranslationFilter.All;
        private string _projectSourcePath = string.Empty;
        private string _codePreview = string.Empty;

        public MainWindowViewModel()
        {
            _mappingService = new MappingFileService();
            _codePreviewService = new CodePreviewService();
            TranslationItems = new ObservableCollection<TranslationItemViewModel>();
            FilteredTranslations = new ObservableCollection<TranslationItemViewModel>();

            // 初始化命令
            LoadMappingCommand = new RelayCommand(LoadMapping);
            SaveMappingCommand = new RelayCommand(SaveMapping, () => _currentMapping != null);
            PreviousTranslationCommand = new RelayCommand(PreviousTranslation, CanGoPrevious);
            NextTranslationCommand = new RelayCommand(NextTranslation, CanGoNext);
            FilterAllCommand = new RelayCommand(() => CurrentFilter = TranslationFilter.All);
            FilterPendingCommand = new RelayCommand(() => CurrentFilter = TranslationFilter.Pending);
            FilterTranslatedCommand = new RelayCommand(() => CurrentFilter = TranslationFilter.Translated);
            BrowseProjectPathCommand = new RelayCommand(BrowseProjectPath);
            ShowCodePreviewCommand = new RelayCommand<ContextReference>(ShowCodePreview);
            OpenInEditorCommand = new RelayCommand<ContextReference>(OpenInEditor);
            MarkAsTranslatedCommand = new RelayCommand(MarkAsTranslated, () => SelectedTranslation != null);
            MarkAsPendingCommand = new RelayCommand(MarkAsPending, () => SelectedTranslation != null);
            MarkNonAlphaAsTranslatedCommand = new RelayCommand(MarkNonAlphaAsTranslated, () => TranslationItems.Count > 0);
            MarkByRegexCommand = new RelayCommand(MarkByRegex, () => TranslationItems.Count > 0);
        }

        #region Properties

        public ObservableCollection<TranslationItemViewModel> TranslationItems { get; }
        public ObservableCollection<TranslationItemViewModel> FilteredTranslations { get; }

        public TranslationItemViewModel? SelectedTranslation
        {
            get => _selectedTranslation;
            set
            {
                if (SetProperty(ref _selectedTranslation, value))
                {
                    // 自动选择第一个上下文引用
                    if (value?.Contexts.Count > 0)
                    {
                        SelectedContext = value.Contexts[0];
                    }
                    else
                    {
                        SelectedContext = null;
                        CodePreview = "该翻译项没有代码上下文引用";
                    }
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public TranslationFilter CurrentFilter
        {
            get => _currentFilter;
            set
            {
                if (SetProperty(ref _currentFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string ProjectSourcePath
        {
            get => _projectSourcePath;
            set => SetProperty(ref _projectSourcePath, value);
        }

        public ContextReference? SelectedContext
        {
            get => _selectedContext;
            set
            {
                if (SetProperty(ref _selectedContext, value) && value != null)
                {
                    UpdateCodePreview(value);
                }
            }
        }

        public string CodePreview
        {
            get => _codePreview;
            set => SetProperty(ref _codePreview, value);
        }

        public string WindowTitle
        {
            get
            {
                if (string.IsNullOrEmpty(_currentFilePath))
                    return "AetherStitch - 翻译工具";
                return $"AetherStitch - {System.IO.Path.GetFileName(_currentFilePath)}";
            }
        }

        public string StatisticsText
        {
            get
            {
                if (_currentMapping == null)
                    return "未加载文件";

                var total = _currentMapping.Metadata.TotalTranslations;
                var translated = _currentMapping.Metadata.TranslatedCount;
                var pending = _currentMapping.Metadata.PendingCount;
                var percentage = total > 0 ? (translated * 100.0 / total) : 0;

                return $"总计: {total} | 已翻译: {translated} | 待翻译: {pending} | 进度: {percentage:F1}%";
            }
        }

        #endregion

        #region Commands

        public ICommand LoadMappingCommand { get; }
        public ICommand SaveMappingCommand { get; }
        public ICommand PreviousTranslationCommand { get; }
        public ICommand NextTranslationCommand { get; }
        public ICommand FilterAllCommand { get; }
        public ICommand FilterPendingCommand { get; }
        public ICommand FilterTranslatedCommand { get; }
        public ICommand BrowseProjectPathCommand { get; }
        public ICommand ShowCodePreviewCommand { get; }
        public ICommand OpenInEditorCommand { get; }
        public ICommand MarkAsTranslatedCommand { get; }
        public ICommand MarkAsPendingCommand { get; }
        public ICommand MarkNonAlphaAsTranslatedCommand { get; }
        public ICommand MarkByRegexCommand { get; }

        #endregion

        #region Methods

        private async void LoadMapping()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON文件|*.json|所有文件|*.*",
                Title = "选择Mapping文件"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _currentFilePath = dialog.FileName;
                    _currentMapping = await _mappingService.LoadMappingAsync(_currentFilePath);

                    TranslationItems.Clear();
                    foreach (var translation in _currentMapping.Translations)
                    {
                        TranslationItems.Add(new TranslationItemViewModel(translation));
                    }

                    ApplyFilter();

                    // 自动选择第一项
                    if (FilteredTranslations.Count > 0)
                    {
                        SelectedTranslation = FilteredTranslations[0];
                    }

                    // 尝试从Mapping中获取项目路径
                    if (!string.IsNullOrEmpty(_currentMapping.ProjectName))
                    {
                        // 假设项目在Mapping文件的上层目录
                        var mappingDir = System.IO.Path.GetDirectoryName(_currentFilePath);
                        if (!string.IsNullOrEmpty(mappingDir))
                        {
                            ProjectSourcePath = mappingDir;
                        }
                    }

                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(StatisticsText));

                    System.Windows.MessageBox.Show($"成功加载 {_currentMapping.Translations.Count} 个翻译项", "加载成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"加载文件失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SaveMapping()
        {
            if (_currentMapping == null || string.IsNullOrEmpty(_currentFilePath))
                return;

            try
            {
                // 更新元数据
                _currentMapping.UpdatedAt = DateTime.UtcNow;

                // 更新统计信息
                UpdateStatistics();

                await _mappingService.SaveMappingAsync(_currentMapping, _currentFilePath);

                System.Windows.MessageBox.Show("保存成功！", "保存",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            FilteredTranslations.Clear();

            var items = TranslationItems.AsEnumerable();

            // 应用状态过滤
            items = _currentFilter switch
            {
                TranslationFilter.Pending => items.Where(t => !t.IsTranslated),
                TranslationFilter.Translated => items.Where(t => t.IsTranslated),
                _ => items
            };

            // 应用文本过滤
            if (!string.IsNullOrWhiteSpace(_filterText))
            {
                items = items.Where(t =>
                    t.Source.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    t.Target.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    t.Key.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in items)
            {
                FilteredTranslations.Add(item);
            }
        }

        private void PreviousTranslation()
        {
            if (SelectedTranslation == null || FilteredTranslations.Count == 0)
                return;

            var currentIndex = FilteredTranslations.IndexOf(SelectedTranslation);
            if (currentIndex > 0)
            {
                SelectedTranslation = FilteredTranslations[currentIndex - 1];
            }
        }

        private void NextTranslation()
        {
            if (SelectedTranslation == null || FilteredTranslations.Count == 0)
                return;

            var currentIndex = FilteredTranslations.IndexOf(SelectedTranslation);
            if (currentIndex < FilteredTranslations.Count - 1)
            {
                SelectedTranslation = FilteredTranslations[currentIndex + 1];
            }
        }

        private bool CanGoPrevious()
        {
            if (SelectedTranslation == null || FilteredTranslations.Count == 0)
                return false;

            var currentIndex = FilteredTranslations.IndexOf(SelectedTranslation);
            return currentIndex > 0;
        }

        private bool CanGoNext()
        {
            if (SelectedTranslation == null || FilteredTranslations.Count == 0)
                return false;

            var currentIndex = FilteredTranslations.IndexOf(SelectedTranslation);
            return currentIndex < FilteredTranslations.Count - 1;
        }

        private void BrowseProjectPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择项目源代码目录",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrEmpty(ProjectSourcePath) && System.IO.Directory.Exists(ProjectSourcePath))
            {
                dialog.SelectedPath = ProjectSourcePath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectSourcePath = dialog.SelectedPath;
            }
        }

        private void ShowCodePreview(ContextReference? context)
        {
            if (context == null)
                return;

            SelectedContext = context;
        }

        private void UpdateCodePreview(ContextReference context)
        {
            if (string.IsNullOrEmpty(ProjectSourcePath))
            {
                CodePreview = "请先设置项目源码路径";
                return;
            }

            CodePreview = _codePreviewService.GetCodeSnippet(ProjectSourcePath, context);
        }

        private void OpenInEditor(ContextReference? context)
        {
            if (context == null || string.IsNullOrEmpty(ProjectSourcePath))
                return;

            _codePreviewService.OpenInEditor(ProjectSourcePath, context);
        }

        private void MarkAsTranslated()
        {
            if (SelectedTranslation == null)
                return;

            SelectedTranslation.Translation.Status = TranslationStatus.Translated;
            SelectedTranslation.Translation.TranslatedAt = DateTime.UtcNow;

            // 通知属性更新
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.Status));
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.IsTranslated));
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.StatusText));

            UpdateStatistics();
        }

        private void MarkAsPending()
        {
            if (SelectedTranslation == null)
                return;

            SelectedTranslation.Translation.Status = TranslationStatus.Pending;
            SelectedTranslation.Translation.TranslatedAt = null;

            // 通知属性更新
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.Status));
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.IsTranslated));
            SelectedTranslation.OnPropertyChanged(nameof(SelectedTranslation.StatusText));

            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            if (_currentMapping == null)
                return;

            // 重新计算统计信息
            _currentMapping.Metadata.TotalTranslations = _currentMapping.Translations.Count;
            _currentMapping.Metadata.TranslatedCount = _currentMapping.Translations.Count(t => t.Status == TranslationStatus.Translated);
            _currentMapping.Metadata.PendingCount = _currentMapping.Translations.Count(t => t.Status == TranslationStatus.Pending);
            _currentMapping.Metadata.TotalContexts = _currentMapping.Translations.Sum(t => t.Contexts.Count);

            OnPropertyChanged(nameof(StatisticsText));
        }

        private void MarkNonAlphaAsTranslated()
        {
            if (TranslationItems.Count == 0)
                return;

            var result = System.Windows.MessageBox.Show(
                "将所有不包含字母的翻译项标记为已翻译？\n\n" +
                "这将标记以下类型的字符串：\n" +
                "• 纯数字 (123, 45.67)\n" +
                "• 纯符号 (!@#$%)\n" +
                "• 空格和标点\n" +
                "• 不包含任何英文字母的字符串\n\n" +
                "这个操作可以撤销（标记为待翻译）。",
                "批量标记确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            int markedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var item in TranslationItems)
            {
                // 检查源文本是否不包含字母
                if (!ContainsLetters(item.Source))
                {
                    item.Translation.Status = TranslationStatus.Translated;
                    item.Translation.TranslatedAt = now;

                    // 通知属性更新
                    item.OnPropertyChanged(nameof(item.Status));
                    item.OnPropertyChanged(nameof(item.IsTranslated));
                    item.OnPropertyChanged(nameof(item.StatusText));

                    markedCount++;
                }
            }

            UpdateStatistics();
            ApplyFilter(); // 刷新过滤后的列表

            System.Windows.MessageBox.Show(
                $"成功标记 {markedCount} 个不包含字母的翻译项为已翻译。",
                "批量标记完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MarkByRegex()
        {
            if (TranslationItems.Count == 0)
                return;

            // 创建输入对话框
            var dialog = new RegexInputDialog();
            if (dialog.ShowDialog() != true)
                return;

            var pattern = dialog.RegexPattern;
            if (string.IsNullOrWhiteSpace(pattern))
                return;

            try
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern);
                int markedCount = 0;
                int matchedCount = 0;
                var now = DateTime.UtcNow;

                // 先计算匹配数量
                foreach (var item in TranslationItems)
                {
                    if (regex.IsMatch(item.Source))
                    {
                        matchedCount++;
                    }
                }

                if (matchedCount == 0)
                {
                    System.Windows.MessageBox.Show(
                        $"没有找到匹配正则表达式的翻译项。\n\n正则表达式: {pattern}",
                        "无匹配项",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var confirmResult = System.Windows.MessageBox.Show(
                    $"找到 {matchedCount} 个匹配的翻译项。\n\n" +
                    $"正则表达式: {pattern}\n\n" +
                    $"是否将它们全部标记为已翻译？",
                    "批量标记确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes)
                    return;

                // 执行标记
                foreach (var item in TranslationItems)
                {
                    if (regex.IsMatch(item.Source))
                    {
                        item.Translation.Status = TranslationStatus.Translated;
                        item.Translation.TranslatedAt = now;

                        // 通知属性更新
                        item.OnPropertyChanged(nameof(item.Status));
                        item.OnPropertyChanged(nameof(item.IsTranslated));
                        item.OnPropertyChanged(nameof(item.StatusText));

                        markedCount++;
                    }
                }

                UpdateStatistics();
                ApplyFilter(); // 刷新过滤后的列表

                System.Windows.MessageBox.Show(
                    $"成功标记 {markedCount} 个匹配的翻译项为已翻译。",
                    "批量标记完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Text.RegularExpressions.RegexParseException ex)
            {
                System.Windows.MessageBox.Show(
                    $"正则表达式格式错误：\n\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool ContainsLetters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 检查是否包含任何字母（英文、中文等）
            return text.Any(c => char.IsLetter(c));
        }

        #endregion
    }

    public enum TranslationFilter
    {
        All,
        Pending,
        Translated
    }
}
