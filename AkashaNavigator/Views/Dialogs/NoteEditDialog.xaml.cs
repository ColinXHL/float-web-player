using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 笔记编辑对话框
/// 用于编辑笔记项或目录的名称
/// </summary>
public partial class NoteEditDialog : AnimatedWindow
{
#region Properties

    /// <summary>
    /// 对话框结果：true=确定，false=取消
    /// </summary>
    public bool Result { get; private set; }

    /// <summary>
    /// 输入的文本
    /// </summary>
    public string InputText => TxtInput.Text?.Trim() ?? string.Empty;

    /// <summary>
    /// URL 文本（仅在 showUrl 模式下有效）
    /// </summary>
    public string UrlText => TxtUrl.Text?.Trim() ?? string.Empty;

#endregion

#region Fields

    private readonly bool _showUrl;
    private readonly bool _isConfirmDialog;

#endregion

#region Constructor

    /// <summary>
    /// 创建编辑对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="prompt">提示文本</param>
    /// <param name="showUrl">是否显示 URL 输入框</param>
    /// <param name="isConfirmDialog">是否为确认对话框（只显示消息和按钮）</param>
    /// <param name="defaultUrl">默认 URL 值（仅在 showUrl 为 true 时有效）</param>
    public NoteEditDialog(string title, string defaultValue, string prompt = "请输入新名称：", bool showUrl = false,
                          bool isConfirmDialog = false, string? defaultUrl = null)
    {
        InitializeComponent();

        _showUrl = showUrl;
        _isConfirmDialog = isConfirmDialog;

        TitleText.Text = title;
        PromptText.Text = prompt;
        TxtInput.Text = defaultValue;

        // 如果是确认对话框模式
        if (isConfirmDialog)
        {
            // 隐藏输入框，只显示提示文本
            TxtInput.Visibility = Visibility.Collapsed;
            PromptText.TextWrapping = TextWrapping.Wrap;
            PromptText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            PromptText.FontSize = 13;
            PromptText.Margin = new Thickness(0, 0, 0, 16);

            // 确定按钮始终启用
            BtnConfirm.IsEnabled = true;
        }
        else
        {
            // 如果需要显示 URL 输入框
            if (showUrl)
            {
                UrlPromptGrid.Visibility = Visibility.Visible;
                TxtUrl.Visibility = Visibility.Visible;

                // 设置默认 URL
                if (!string.IsNullOrWhiteSpace(defaultUrl))
                {
                    TxtUrl.Text = defaultUrl;
                }
            }

            // 选中所有文本
            TxtInput.SelectAll();

            // 聚焦输入框
            Loaded += (s, e) => TxtInput.Focus();

            UpdateConfirmButton();
        }
    }

#endregion

#region Private Methods

    /// <summary>
    /// 更新确定按钮状态
    /// </summary>
    private void UpdateConfirmButton()
    {
        // 确认对话框模式下始终启用
        if (_isConfirmDialog)
        {
            BtnConfirm.IsEnabled = true;
            return;
        }

        var titleValid = !string.IsNullOrWhiteSpace(TxtInput.Text);
        var urlValid = !_showUrl || !string.IsNullOrWhiteSpace(TxtUrl.Text);
        BtnConfirm.IsEnabled = titleValid && urlValid;
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 主容器点击事件 - 取消输入框焦点
    /// </summary>
    private void MainContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 将焦点移到窗口本身，从而取消输入框的焦点
        FocusManager.SetFocusedElement(this, this);
        Keyboard.ClearFocus();
    }

    /// <summary>
    /// 输入框文本变化
    /// </summary>
    private void TxtInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateConfirmButton();
    }

    /// <summary>
    /// URL 输入框文本变化
    /// </summary>
    private void TxtUrl_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateConfirmButton();
    }

    /// <summary>
    /// 输入框按键事件
    /// </summary>
    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && BtnConfirm.IsEnabled)
        {
            Result = true;
            CloseWithAnimation();
        }
        else if (e.Key == Key.Escape)
        {
            Result = false;
            CloseWithAnimation();
        }
    }

    /// <summary>
    /// 获取当前 URL 按钮点击
    /// </summary>
    private void BtnGetCurrentUrl_Click(object sender, RoutedEventArgs e)
    {
        // 通过 Owner 链找到 PlayerWindow 获取当前 URL
        var owner = Owner;
        while (owner != null)
        {
            if (owner is PlayerWindow playerWindow)
            {
                var currentUrl = playerWindow.CurrentUrl;
                if (!string.IsNullOrWhiteSpace(currentUrl))
                {
                    TxtUrl.Text = currentUrl;
                }
                return;
            }
            owner = owner.Owner;
        }
    }

    /// <summary>
    /// 确定按钮点击
    /// </summary>
    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        CloseWithAnimation();
    }

    /// <summary>
    /// 取消按钮点击
    /// </summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        CloseWithAnimation();
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        CloseWithAnimation();
    }

#endregion
}
}
