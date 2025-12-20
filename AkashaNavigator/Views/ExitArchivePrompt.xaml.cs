using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;

namespace AkashaNavigator.Views
{
/// <summary>
/// 退出归档提示窗口
/// 当用户退出应用且当前页面未归档时显示
/// </summary>
public partial class ExitArchivePrompt : AnimatedWindow
{
#region Enums

    /// <summary>
    /// 用户操作结果
    /// </summary>
    public enum PromptResult
    {
        /// <summary>
        /// 继续退出应用
        /// </summary>
        Exit,

        /// <summary>
        /// 打开归档管理窗口
        /// </summary>
        OpenArchiveManager,

        /// <summary>
        /// 打开快速归档对话框
        /// </summary>
        QuickArchive
    }

#endregion

#region Properties

    /// <summary>
    /// 用户选择的操作结果
    /// </summary>
    public PromptResult Result { get; private set; } = PromptResult.Exit;

    /// <summary>
    /// 当前页面 URL
    /// </summary>
    public string PageUrl { get; }

    /// <summary>
    /// 当前页面标题
    /// </summary>
    public string PageTitle { get; }

#endregion

#region Constructor

    /// <summary>
    /// 创建退出归档提示窗口
    /// </summary>
    /// <param name="url">当前页面 URL</param>
    /// <param name="title">当前页面标题</param>
    public ExitArchivePrompt(string url, string title)
    {
        InitializeComponent();

        PageUrl = url ?? string.Empty;
        PageTitle = title ?? string.Empty;

        // 设置页面预览信息
        TxtPageTitle.Text = string.IsNullOrWhiteSpace(PageTitle) ? "(无标题)" : PageTitle;
        TxtPageUrl.Text = string.IsNullOrWhiteSpace(PageUrl) ? "(无 URL)" : PageUrl;
    }

#endregion

#region Static Methods

    /// <summary>
    /// 检查是否需要显示退出归档提示
    /// </summary>
    /// <param name="url">当前页面 URL</param>
    /// <returns>如果 URL 未归档且非空，返回 true</returns>
    public static bool ShouldShowPrompt(string url)
    {
        // 如果 URL 为空，不显示提示
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // 检查 URL 是否已归档
        return !ArchiveService.Instance.IsUrlArchived(url);
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮点击 - 继续退出应用
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.Exit;
        CloseWithAnimation();
    }

    /// <summary>
    /// 打开归档管理按钮点击
    /// </summary>
    private void BtnOpenArchiveManager_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.OpenArchiveManager;
        CloseWithAnimation();
    }

    /// <summary>
    /// 快速归档按钮点击
    /// </summary>
    private void BtnQuickArchive_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.QuickArchive;
        CloseWithAnimation();
    }

    /// <summary>
    /// 直接退出按钮点击
    /// </summary>
    private void BtnDirectExit_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.Exit;
        CloseWithAnimation();
    }

#endregion
}
}
