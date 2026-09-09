using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ShellManager.Core.Models;
using ShellManager.Core.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace ShellManager.App;

public sealed partial class MainWindow : Window
{
    private readonly ShellInstallation _installation;
    private readonly ConfigurationService _configuration;
    private readonly ObservableCollection<ConfigFileModel> _files = [];
    private readonly AppWindow _appWindow;
    private bool _loadingEditor;
    private bool _allowClose;
    private readonly StackPanel _propertyPanel = new() { Spacing = 10 };

    public MainWindow()
    {
        InitializeComponent();
        if (NewTitle.Parent is StackPanel menuPanel)
            menuPanel.Children.Insert(0, new Expander { Header = "所选节点属性", IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch, Content = _propertyPanel });
        _propertyPanel.Children.Add(new TextBlock { Text = "从左侧选择配置节点，直接编辑其属性。", TextWrapping = TextWrapping.Wrap });
        ThemeName.SelectionChanged += (_, _) => UpdateThemePreview();
        ThemeView.SelectionChanged += (_, _) => UpdateThemePreview();
        ThemeRadius.ValueChanged += (_, _) => UpdateThemePreview();
        ThemeShadow.Toggled += (_, _) => UpdateThemePreview();
        RootGrid.ActualThemeChanged += (_, _) => UpdateThemePreview();
        UpdateThemePreview();
        _installation = ShellLocator.Locate();
        _configuration = new ConfigurationService(_installation);

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new SizeInt32(1280, 800));
        _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        _appWindow.Closing += AppWindow_Closing;
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        RootGrid.Loaded += (_, _) => LoadConfiguration();
    }

    private ConfigFileModel? SelectedFile => FileList.SelectedItem as ConfigFileModel;

    private void LoadConfiguration(string? selectPath = null)
    {
        _propertyPanel.Children.Clear();
        _propertyPanel.Children.Add(new TextBlock { Text = "请选择配置节点。" });
        _files.Clear();
        if (!_installation.IsDetected)
        {
            SidebarStatus.Text = "未检测到 Shell";
            SidebarStatusDot.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 72, 77));
            SidebarVersion.Text = "请检查安装路径";
            ValidationSummary.Text = "不可用";
            return;
        }

        try
        {
            foreach (var file in _configuration.LoadFiles()) _files.Add(file);
            FileList.ItemsSource = _files;
            PopulateOutline();
            SidebarStatus.Text = "Shell 已连接";
            SidebarVersion.Text = $"版本 {_installation.Version ?? "未知"}";
            ConfigPathText.Text = _installation.ConfigPath;
            InstallPathText.Text = $"程序：{_installation.ExecutablePath}";
            FileCount.Text = _files.Count.ToString();
            NodeCount.Text = _files.Sum(f => CountNodes(f.Nodes)).ToString();
            var issueCount = _files.Sum(f => NssParser.Validate(f.Content).Count(i => i.IsError));
            ValidationSummary.Text = issueCount == 0 ? "配置正常" : $"{issueCount} 个问题";
            ValidationSummary.Foreground = new SolidColorBrush(issueCount == 0
                ? Windows.UI.Color.FromArgb(255, 14, 159, 110)
                : Windows.UI.Color.FromArgb(255, 229, 72, 77));
            BackupList.ItemsSource = _configuration.GetBackups();
            if (_files.Count > 0)
                FileList.SelectedItem = selectPath is null ? _files[0] : _files.FirstOrDefault(f => f.Path.Equals(selectPath, StringComparison.OrdinalIgnoreCase)) ?? _files[0];
            UpdateDirtyState();
        }
        catch (Exception ex) { _ = ShowErrorAsync("读取配置失败", ex); }
    }

    private void PopulateOutline()
    {
        OutlineTree.RootNodes.Clear();
        foreach (var file in _files)
        {
            var fileNode = new TreeViewNode { Content = new NssNode { Kind = "file", Title = file.DisplayName, Line = 1 }, IsExpanded = file.IsMain };
            foreach (var node in file.Nodes) fileNode.Children.Add(CreateTreeNode(node));
            OutlineTree.RootNodes.Add(fileNode);
        }
    }

    private static TreeViewNode CreateTreeNode(NssNode model)
    {
        var node = new TreeViewNode { Content = model };
        foreach (var child in model.Children) node.Children.Add(CreateTreeNode(child));
        return node;
    }

    private static int CountNodes(IEnumerable<NssNode> nodes) => nodes.Sum(n => 1 + CountNodes(n.Children));

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item) return;
        var tag = item.Tag?.ToString() ?? "Overview";
        OverviewPage.Visibility = tag == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        MenusPage.Visibility = tag == "Menus" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePage.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        SourcePage.Visibility = tag == "Source" ? Visibility.Visible : Visibility.Collapsed;
        BackupsPage.Visibility = tag == "Backups" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = tag switch { "Menus" => "菜单管理", "Appearance" => "外观主题", "Source" => "配置源码", "Backups" => "备份记录", _ => "概览" };
        if (tag == "Backups") BackupList.ItemsSource = _configuration.GetBackups();
    }

    private void NavigateTo(string tag)
    {
        var item = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        if (item is not null) Navigation.SelectedItem = item;
    }

    private void OpenMenus_Click(object sender, RoutedEventArgs e) => NavigateTo("Menus");
    private void OpenSource_Click(object sender, RoutedEventArgs e) => NavigateTo("Source");

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedFile is null) return;
        _loadingEditor = true;
        SourceEditor.Text = SelectedFile.Content;
        EditorFileName.Text = SelectedFile.DisplayName;
        EditorStatus.Text = SelectedFile.Path;
        EditorStatus.Foreground = new SolidColorBrush(Colors.Gray);
        _loadingEditor = false;
    }

    private void SourceEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingEditor || SelectedFile is null) return;
        SelectedFile.UpdateFromEditor(SourceEditor.Text);
        SelectedFile.Nodes.Clear();
        foreach (var node in NssParser.ParseOutline(SelectedFile.Content)) SelectedFile.Nodes.Add(node);
        PopulateOutline();
        _propertyPanel.Children.Clear();
        _propertyPanel.Children.Add(new TextBlock { Text = "源码已更新，请重新选择配置节点。" });
        UpdateDirtyState();
    }

    private async void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is null) return;
        var issues = NssParser.Validate(SourceEditor.Text);
        if (issues.Count == 0)
        {
            EditorStatus.Text = "✓ 语法结构检查通过";
            EditorStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 14, 159, 110));
            return;
        }

        var issue = issues[0];
        EditorStatus.Text = issue.ToString();
        EditorStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 72, 77));
        var index = NssText.CharacterIndexForLine(SourceEditor.Text, issue.Line) + issue.Column - 1;
        SourceEditor.SelectionStart = Math.Min(index, SourceEditor.Text.Length);
        SourceEditor.SelectionLength = 0;
        SourceEditor.Focus(FocusState.Programmatic);
        await Task.CompletedTask;
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAllAsync(false);
    private async void Apply_Click(object sender, RoutedEventArgs e) => await SaveAllAsync(true);

    private async Task SaveAllAsync(bool apply)
    {
        var dirty = _files.Where(f => f.IsDirty).ToList();
        try
        {
            foreach (var file in dirty)
            {
                _configuration.Save(file, file.Content);
                file.AcceptChanges();
            }
            if (apply && await ConfirmAsync("应用配置", "配置已安全保存。将使用 Nilesoft Shell 的 -restart -silent 命令重启 Windows 资源管理器。是否立即应用？"))
                _configuration.RestartExplorer();
            UpdateDirtyState();
            if (dirty.Count > 0) LoadConfiguration(SelectedFile?.Path);
        }
        catch (Exception ex) { await ShowErrorAsync("无法保存配置", ex); }
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Any(f => f.IsDirty))
        {
            await ShowMessageAsync("存在未保存更改", "请先保存源码编辑器中的更改，再使用快速添加。");
            return;
        }
        var title = NewTitle.Text.Trim();
        var command = NewCommand.Text.Trim();
        if (title.Length == 0 || command.Length == 0)
        {
            await ShowMessageAsync("信息不完整", "请填写显示名称和执行程序。");
            return;
        }
        try
        {
            var path = _configuration.EnsureManagedFile();
            var type = (NewType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";
            var admin = NewAdmin.IsOn ? " admin" : string.Empty;
            var args = NewArguments.Text.Trim();
            var entry = new StringBuilder().Append("item(title=").Append(NssParser.Quote(title)).Append(" type=").Append(NssParser.Quote(type)).Append(" cmd=").Append(NssParser.Quote(command));
            if (args.Length > 0) entry.Append(" args=").Append(NssSyntax.QuoteInterpolated(args));
            entry.Append(admin).Append(")\r\n");
            var current = File.ReadAllText(path);
            var file = new ConfigFileModel { Path = path, DisplayName = Path.GetFileName(path), Content = current };
            _configuration.Save(file, current.TrimEnd() + "\r\n\r\n" + entry);
            NewTitle.Text = string.Empty; NewCommand.Text = string.Empty; NewArguments.Text = "\"@sel.path\""; NewAdmin.IsOn = false;
            LoadConfiguration(path);
            NavigateTo("Source");
        }
        catch (Exception ex) { await ShowErrorAsync("无法添加菜单项", ex); }
    }

    private async void GenerateTheme_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Any(f => f.IsDirty))
        {
            await ShowMessageAsync("存在未保存更改", "请先保存源码编辑器中的更改，再生成主题。");
            return;
        }
        try
        {
            var name = (ThemeName.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "modern";
            var view = (ThemeView.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "compact";
            var path = _configuration.SaveTheme(name, view, (int)ThemeRadius.Value, ThemeShadow.IsOn);
            LoadConfiguration(path);
            NavigateTo("Source");
        }
        catch (Exception ex) { await ShowErrorAsync("无法生成主题配置", ex); }
    }

    private void OutlineTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedNode?.Content is not NssNode node) return;
        if (node.Kind == "file")
        {
            _propertyPanel.Children.Clear();
            _propertyPanel.Children.Add(new TextBlock { Text = "展开文件并选择需要编辑的配置节点。" });
            return;
        }
        var file = _files.FirstOrDefault(f => ContainsNode(f.Nodes, node));
        if (file is null) return;
        FileList.SelectedItem = file;
        ShowNodeProperties(file, node.Start);
    }

    private void ShowNodeProperties(ConfigFileModel file, int start)
    {
        var document = NssSyntax.Parse(file.Content);
        var node = document.Descendants().FirstOrDefault(n => n.Start == start);
        _propertyPanel.Children.Clear();
        if (node is null) return;
        _propertyPanel.Children.Add(new TextBlock { Text = $"{node.Title} · 第 {node.Line} 行", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        _propertyPanel.Children.Add(new TextBlock { Text = "文字常量可直接填写；表达式保留 NSS 写法。每项修改先更新草稿，点击顶部保存才写入文件。", TextWrapping = TextWrapping.Wrap });
        var locate = new Button { Content = "定位源码" };
        locate.Click += (_, _) =>
        {
            FileList.SelectedItem = file;
            NavigateTo("Source");
            SourceEditor.SelectionStart = Math.Min(NssText.CharacterIndexForLine(SourceEditor.Text, node.Line), SourceEditor.Text.Length);
            SourceEditor.Focus(FocusState.Programmatic);
        };
        _propertyPanel.Children.Add(locate);
        if (document.HasErrors)
        {
            _propertyPanel.Children.Add(new TextBlock { Text = "此文件有结构错误，修正源码后才能编辑属性。", TextWrapping = TextWrapping.Wrap });
            return;
        }
        foreach (var property in node.Properties)
        {
            var literal = NssSyntax.TryGetLiteral(property.Expression, out var text);
            var editor = new TextBox { Header = property.Name + (literal ? " · 文字" : property.IsFlag ? " · 标志（省略赋值）" : " · NSS 表达式"), Text = literal ? text : property.Expression, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            var apply = new Button { Content = "更新此属性" };
            apply.Click += async (_, _) =>
            {
                try
                {
                    if (file.Content != document.Source) throw new InvalidOperationException("源码已经改变，请重新选择节点。");
                    var expression = literal ? (editor.Text == text ? property.Expression : NssSyntax.QuoteLiteral(editor.Text)) : editor.Text;
                    file.Content = document.ReplaceProperty(property, expression);
                    file.Nodes.Clear();
                    foreach (var updated in NssParser.ParseOutline(file.Content)) file.Nodes.Add(updated);
                    PopulateOutline();
                    FileList.SelectedItem = file;
                    _loadingEditor = true;
                    SourceEditor.Text = file.Content;
                    _loadingEditor = false;
                    UpdateDirtyState();
                    ShowNodeProperties(file, start);
                }
                catch (Exception ex) { await ShowErrorAsync("无法更新属性", ex); }
            };
            _propertyPanel.Children.Add(editor);
            _propertyPanel.Children.Add(apply);
        }
        if (node.Properties.Count == 0)
            _propertyPanel.Children.Add(new TextBlock { Text = "此节点没有直接属性。请展开左侧子节点，或定位源码编辑。", TextWrapping = TextWrapping.Wrap });
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !_files.Any(f => f.IsDirty)) return;
        args.Cancel = true;
        if (!await ConfirmAsync("未保存的更改", "还有未保存的配置更改，确定要退出吗？")) return;
        _allowClose = true;
        Close();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = RootGrid.XamlRoot, Title = title, Content = message, PrimaryButtonText = "确定", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = RootGrid.XamlRoot, Title = title, Content = message, CloseButtonText = "确定" };
        await dialog.ShowAsync();
    }

    private Task ShowErrorAsync(string title, Exception ex) => ShowMessageAsync(title, ex.Message);
    private static bool ContainsNode(IEnumerable<NssNode> nodes, NssNode target) => nodes.Any(n => ReferenceEquals(n, target) || ContainsNode(n.Children, target));
    private void UpdateDirtyState() => UnsavedBadge.Visibility = _files.Any(f => f.IsDirty) ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateThemePreview()
    {
        var name = (ThemeName.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "modern";
        var view = (ThemeView.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "compact";
        var dark = name == "black" || (name is "auto" or "modern" && RootGrid.ActualTheme == ElementTheme.Dark);
        var classic = name == "classic";
        var padding = view switch { "small" => 7, "medium" => 10, "large" => 14, "wide" => 18, _ => 4 };
        PreviewMenu.Background = new SolidColorBrush(dark ? Colors.Black : classic ? Windows.UI.Color.FromArgb(255, 240, 240, 240) : Colors.White);
        PreviewMenu.CornerRadius = new CornerRadius(classic ? 0 : 8);
        PreviewSelection.CornerRadius = new CornerRadius(double.IsNaN(ThemeRadius.Value) ? 0 : ThemeRadius.Value);
        PreviewSelection.Background = new SolidColorBrush(classic ? Windows.UI.Color.FromArgb(255, 0, 120, 215) : Windows.UI.Color.FromArgb(255, 103, 92, 245));
        PreviewSelection.Padding = new Thickness(12, padding, 12, padding);
        foreach (var row in new[] { PreviewCopy, PreviewTerminal, PreviewMore })
        {
            row.Padding = new Thickness(12, padding, 12, padding);
            row.Foreground = new SolidColorBrush(dark ? Colors.White : Colors.Black);
        }
        PreviewShadow.Visibility = ThemeShadow.IsOn ? Visibility.Visible : Visibility.Collapsed;
        PreviewShadow.CornerRadius = PreviewMenu.CornerRadius;
    }
}
