using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Windows;

namespace AkashaNavigator.Views.Windows;

public partial class PluginSettingsWindow : AnimatedWindow
{
    private readonly PluginSettingsViewModel _viewModel;
    private readonly IPluginSettingsEditSessionCoordinator _editSessionCoordinator;
    private readonly IOverlayManager _overlayManager;
    private readonly ILogService _logService;
    private readonly IPluginResourceUpdateService _pluginResourceUpdateService;

    private SettingsUiRenderer? _renderer;

    public PluginSettingsWindow(
        PluginSettingsViewModel viewModel,
        IPluginSettingsEditSessionCoordinator editSessionCoordinator,
        IOverlayManager overlayManager,
        ILogService logService,
        IPluginResourceUpdateService pluginResourceUpdateService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _editSessionCoordinator = editSessionCoordinator ?? throw new ArgumentNullException(nameof(editSessionCoordinator));
        _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _pluginResourceUpdateService =
            pluginResourceUpdateService ??
            throw new ArgumentNullException(nameof(pluginResourceUpdateService));

        InitializeComponent();

        DataContext = _viewModel;
        TitleText.Text = $"{_viewModel.PluginName} - 设置";
        Title = $"{_viewModel.PluginName} - 设置";

        RenderSettings();
    }

    private void RenderSettings()
    {
        SettingsContainer.Children.Clear();

        if (_viewModel.SettingsDefinition == null)
        {
            var noSettingsText = new System.Windows.Controls.TextBlock {
                Text = "此插件没有可配置的设置项",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            SettingsContainer.Children.Add(noSettingsText);
            return;
        }

        _renderer = new SettingsUiRenderer(_viewModel.SettingsDefinition, _viewModel.Config);
        _renderer.ValueChanged += OnSettingValueChanged;
        _renderer.ButtonAction += OnButtonAction;

        var settingsPanel = _renderer.Render();
        SettingsContainer.Children.Add(settingsPanel);
    }

    private void OnSettingValueChanged(object? sender, SettingsValueChangedEventArgs e)
    {
        _viewModel.UpdateValue(e.Key, e.Value);
    }

    private void OnButtonAction(object? sender, SettingsButtonActionEventArgs e)
    {
        HandleButtonAction(e.Action, e.RelativePath);
    }

    private void HandleButtonAction(string action, string? relativePath)
    {
        if (string.IsNullOrEmpty(action))
            return;

        switch (action)
        {
            case SettingsButtonActions.EnterEditMode:
                EnterOverlayEditMode();
                break;

            case SettingsButtonActions.ResetConfig:
                ResetToDefaults();
                break;

            case SettingsButtonActions.OpenPluginFolder:
                OpenPluginFolder(relativePath);
                break;

            case SettingsButtonActions.UpdatePluginResources:
                UpdatePluginResources();
                break;

            default:
                _viewModel.NotifyAction(action);
                break;
        }
    }

    private async void UpdatePluginResources()
    {
        try
        {
            var result = await _pluginResourceUpdateService.UpdatePluginResourcesAsync(
                _viewModel.PluginId,
                refreshRepository: true);
            if (result.IsFailure)
            {
                _viewModel.ShowError(
                    $"检查插件数据更新失败：{result.Error?.Message ?? "未知错误"}");
                return;
            }

            var outcomes = result.Value!;
            var failed = outcomes.Where(item => !item.Succeeded).ToArray();
            if (failed.Length > 0)
            {
                _viewModel.ShowWarning(
                    $"插件数据更新未完成，将在下次启动重试：{failed[0].ErrorMessage ?? "未知错误"}");
                return;
            }

            var updated = outcomes.Count(item => item.Updated);
            var sourceVersion = outcomes
                .FirstOrDefault(item => item.Updated)?.SourceVersion;
            _viewModel.ShowSuccess(
                outcomes.Count == 0
                    ? "此插件没有独立数据资源"
                    : updated == 0
                        ? "插件数据已经是最新版本"
                        : string.IsNullOrWhiteSpace(sourceVersion)
                            ? $"已更新 {updated} 个插件数据资源"
                            : $"已更新 {updated} 个插件数据资源（来源版本 {sourceVersion}）");
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PluginSettingsWindow), ex, "手动更新插件资源失败");
            _viewModel.ShowError("更新插件数据失败，请查看日志");
        }
    }

    private void EnterOverlayEditMode()
    {
        if (!_viewModel.IsCurrentProfileActive())
        {
            _viewModel.ShowWarning("请先激活此 Profile 后再调整覆盖层位置");
            return;
        }

        var overlay = _overlayManager.GetOverlay(_viewModel.PluginId);

        if (overlay == null)
        {
            var x = _viewModel.Config.Get("overlay.x", 100.0);
            var y = _viewModel.Config.Get("overlay.y", 100.0);
            var size = _viewModel.Config.Get("overlay.size", 200.0);

            var options = new OverlayOptions { X = x, Y = y, Width = size, Height = size };
            overlay = _overlayManager.CreateOverlay(_viewModel.PluginId, options);
        }

        overlay.EditModeExited += OnOverlayEditModeExited;
        _editSessionCoordinator.EnterOverlayEditSession(this, overlay);
    }

    private async void OnOverlayEditModeExited(object? sender, EventArgs e)
    {
        if (sender is not OverlayWindow overlay)
            return;

        overlay.EditModeExited -= OnOverlayEditModeExited;

        _viewModel.UpdateValue("overlay.x", overlay.Left);
        _viewModel.UpdateValue("overlay.y", overlay.Top);
        _viewModel.UpdateValue("overlay.size", overlay.Width);

        _renderer?.RefreshValues();

        await _viewModel.SaveAsync(notifyUser: false, reloadPlugin: false);
    }

    private void OpenPluginFolder(string? relativePath)
    {
        try
        {
            var directory = PluginSettingsPathResolver.ResolveDirectory(_viewModel.PluginDirectory, relativePath);
            if (directory != null && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
                return;
            }

            _viewModel.ShowWarning("目标目录不存在，请确认插件资源已完整安装");
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PluginSettingsWindow), ex, "打开插件目录失败");
            _viewModel.ShowWarning("打开目录失败，请检查插件文件是否完整");
        }
    }

    private void ResetToDefaults()
    {
        if (_viewModel.SettingsDefinition?.Sections == null)
            return;

        foreach (var section in _viewModel.SettingsDefinition.Sections)
        {
            if (section.Items == null)
                continue;

            foreach (var item in section.Items)
            {
                ResetItemToDefault(item);
            }
        }

        _renderer?.RefreshValues();
        _viewModel.MarkDirty();
    }

    private void ResetItemToDefault(SettingsItem item)
    {
        if (string.IsNullOrEmpty(item.Key))
            return;

        if (item.Default.HasValue)
        {
            var defaultValue = item.Default.Value;
            switch (defaultValue.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetString());
                    break;
                case System.Text.Json.JsonValueKind.Number:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetDouble());
                    break;
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetBoolean());
                    break;
            }
        }
        else
        {
            _viewModel.RemoveValue(item.Key);
        }

        if (item.Items == null)
            return;

        foreach (var subItem in item.Items)
        {
            ResetItemToDefault(subItem);
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        ResetToDefaults();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        CloseWithAnimation();
    }

    public static bool HasSettingsUi(string pluginDirectory)
    {
        if (string.IsNullOrEmpty(pluginDirectory))
            return false;

        var manifestResult = PluginManifest.LoadFromFile(
            Path.Combine(pluginDirectory, AppConstants.PluginManifestFileName));
        var settingsUiPath = PluginSettingsPathResolver.ResolveSettingsFile(
            pluginDirectory,
            manifestResult.IsSuccess ? manifestResult.Manifest?.Settings : null);
        return settingsUiPath != null && File.Exists(settingsUiPath);
    }
}
