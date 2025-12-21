using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 记录笔记对话框
/// 用于创建新的笔记项，支持选择目录和新建目录
/// </summary>
public partial class RecordNoteDialog : AnimatedWindow
{
#region Properties

    /// <summary>
    /// 对话框结果：true=确定，false=取消
    /// </summary>
    public bool Result { get; private set; }

    /// <summary>
    /// 创建的笔记项（确认后可用）
    /// </summary>
    public NoteItem? CreatedNote { get; private set; }

#endregion

#region Fields

    private readonly string _url;
    private readonly string _defaultTitle;
    private string? _selectedFolderId;
    private ObservableCollection<FolderTreeItem> _folderTreeItems = new();

#endregion

#region Constructor

    /// <summary>
    /// 创建记录笔记对话框
    /// </summary>
    /// <param name="url">要记录的 URL</param>
    /// <param name="title">默认标题（通常是页面标题）</param>
    public RecordNoteDialog(string url, string title)
    {
        InitializeComponent();

        _url = url ?? string.Empty;
        _defaultTitle = title ?? string.Empty;

        // 初始化 UI
        TxtTitle.Text = _defaultTitle;
        TxtUrl.Text = _url;

        // 加载目录树
        LoadFolderTree();

        // 更新确定按钮状态
        UpdateConfirmButton();
    }

#endregion

#region Folder Tree

    /// <summary>
    /// 加载笔记目录树
    /// </summary>
    private void LoadFolderTree()
    {
        _folderTreeItems.Clear();

        // 添加根目录选项（始终显示在顶部）
        var rootItem = new FolderTreeItem { Id = null, // null 表示根目录
                                            Name = "根目录", Icon = "🏠", IsRoot = true,
                                            Children = new ObservableCollection<FolderTreeItem>() };

        // 获取所有顶级目录
        var folders = PioneerNoteService.Instance.GetFoldersByParent(null);

        // 递归构建目录树，作为根目录的子项
        foreach (var folder in folders)
        {
            var treeItem = BuildFolderTreeItem(folder);
            rootItem.Children.Add(treeItem);
        }

        _folderTreeItems.Add(rootItem);
        FolderTree.ItemsSource = _folderTreeItems;

        // 默认选中根目录
        _selectedFolderId = null;

        // 隐藏空状态提示（因为始终有根目录）
        EmptyFolderHint.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 递归构建目录树项
    /// </summary>
    private FolderTreeItem BuildFolderTreeItem(NoteFolder folder)
    {
        var item = new FolderTreeItem { Id = folder.Id, Name = folder.Name, Icon = folder.Icon ?? "📁",
                                        Children = new ObservableCollection<FolderTreeItem>() };

        // 获取子目录
        var childFolders = PioneerNoteService.Instance.GetFoldersByParent(folder.Id);
        foreach (var childFolder in childFolders)
        {
            var childItem = BuildFolderTreeItem(childFolder);
            item.Children.Add(childItem);
        }

        return item;
    }

    /// <summary>
    /// 刷新目录树
    /// </summary>
    private void RefreshFolderTree()
    {
        LoadFolderTree();
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
    /// 标题输入变化
    /// </summary>
    private void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClearError();
        UpdateConfirmButton();
    }

    /// <summary>
    /// URL 输入变化
    /// </summary>
    private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClearError();
        UpdateConfirmButton();
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
    /// 目录树选择变化
    /// </summary>
    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderTreeItem selectedItem)
        {
            // 根目录的 Id 为 null，其他目录使用实际 Id
            _selectedFolderId = selectedItem.Id;
        }
        else
        {
            // 没有选中任何项时，默认记录到根目录
            _selectedFolderId = null;
        }
    }

    /// <summary>
    /// 点击目录树容器空白区域时取消选中
    /// </summary>
    private void FolderTreeContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在 TreeViewItem 上
        var hitTestResult = VisualTreeHelper.HitTest(FolderTree, e.GetPosition(FolderTree));
        if (hitTestResult != null)
        {
            // 查找点击位置是否在 TreeViewItem 内
            var element = hitTestResult.VisualHit;
            while (element != null && element != FolderTree)
            {
                if (element is TreeViewItem)
                {
                    return; // 点击在 TreeViewItem 上，不处理
                }
                element = VisualTreeHelper.GetParent(element) as Visual;
            }
        }

        // 点击在空白区域，清除选中状态
        ClearTreeViewSelection();
    }

    /// <summary>
    /// 清除 TreeView 选中状态
    /// </summary>
    private void ClearTreeViewSelection()
    {
        if (FolderTree.SelectedItem != null)
        {
            // 递归取消所有项的选中状态
            foreach (var item in _folderTreeItems)
            {
                ClearSelectionRecursive(item);
            }
            _selectedFolderId = null;
        }
    }

    /// <summary>
    /// 递归清除选中状态
    /// </summary>
    private void ClearSelectionRecursive(FolderTreeItem item)
    {
        var container = FolderTree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
        if (container != null)
        {
            container.IsSelected = false;
            foreach (var child in item.Children)
            {
                ClearSelectionInContainer(container, child);
            }
        }
    }

    /// <summary>
    /// 在容器中递归清除选中状态
    /// </summary>
    private void ClearSelectionInContainer(TreeViewItem parentContainer, FolderTreeItem item)
    {
        var container = parentContainer.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
        if (container != null)
        {
            container.IsSelected = false;
            foreach (var child in item.Children)
            {
                ClearSelectionInContainer(container, child);
            }
        }
    }

    /// <summary>
    /// TreeViewItem 右键点击时先选中该项并显示上下文菜单
    /// </summary>
    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem)
        {
            // 检查点击是否在子项上
            var originalSource = e.OriginalSource as DependencyObject;
            var clickedItem = originalSource;

            // 向上遍历找到最近的 TreeViewItem
            while (clickedItem != null && clickedItem != treeViewItem)
            {
                if (clickedItem is TreeViewItem childItem && childItem != treeViewItem)
                {
                    // 点击在子项上，让子项处理
                    return;
                }
                clickedItem = VisualTreeHelper.GetParent(clickedItem);
            }

            // 选中该项
            treeViewItem.IsSelected = true;
            treeViewItem.Focus();

            // 获取选中的数据项
            if (treeViewItem.DataContext is FolderTreeItem folderItem)
            {
                // 根目录不显示上下文菜单
                if (folderItem.IsRoot)
                {
                    e.Handled = true;
                    return;
                }

                // 创建并显示上下文菜单
                var contextMenu = CreateFolderContextMenu();
                contextMenu.PlacementTarget = treeViewItem;
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// 创建文件夹上下文菜单
    /// </summary>
    private ContextMenu CreateFolderContextMenu()
    {
        var contextMenu = new ContextMenu { Style = FindResource("DarkContextMenuStyle") as Style };

        var editMenuItem = new MenuItem { Header = "✏️ 编辑", Style = FindResource("DarkMenuItemStyle") as Style };
        editMenuItem.Click += MenuEditFolder_Click;

        var deleteMenuItem = new MenuItem { Header = "🗑️ 删除", Style = FindResource("DarkMenuItemStyle") as Style };
        deleteMenuItem.Click += MenuDeleteFolder_Click;

        contextMenu.Items.Add(editMenuItem);
        contextMenu.Items.Add(deleteMenuItem);

        return contextMenu;
    }

    /// <summary>
    /// 编辑文件夹菜单点击
    /// </summary>
    private void MenuEditFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is FolderTreeItem selectedItem)
        {
            // 根目录不能编辑
            if (selectedItem.IsRoot)
            {
                return;
            }

            // 打开编辑对话框
            var editDialog = new NoteEditDialog("编辑目录", selectedItem.Name, "请输入新的目录名称：") { Owner = this };

            editDialog.ShowDialog();

            if (editDialog.Result && !string.IsNullOrWhiteSpace(editDialog.InputText))
            {
                try
                {
                    PioneerNoteService.Instance.UpdateFolder(selectedItem.Id!, editDialog.InputText);
                    RefreshFolderTree();
                }
                catch (Exception ex)
                {
                    ShowError($"编辑目录失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 删除文件夹菜单点击
    /// </summary>
    private void MenuDeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is FolderTreeItem selectedItem)
        {
            // 根目录不能删除
            if (selectedItem.IsRoot)
            {
                return;
            }

            // 确认删除
            var confirmDialog = new ConfirmDialog(
                $"确定要删除目录 \"{selectedItem.Name}\" 吗？\n\n该目录下的所有子目录和笔记项也将被删除。",
                "删除目录") { Owner = this };

            confirmDialog.ShowDialog();

            if (confirmDialog.Result == true)
            {
                try
                {
                    PioneerNoteService.Instance.DeleteFolder(selectedItem.Id!, cascade: true);
                    RefreshFolderTree();
                    _selectedFolderId = null; // 重置选中
                }
                catch (Exception ex)
                {
                    ShowError($"删除目录失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 新建目录按钮点击
    /// </summary>
    private void BtnNewFolder_Click(object sender, RoutedEventArgs e)
    {
        ShowNewFolderPanel();
    }

    /// <summary>
    /// 新建目录名称输入框按键
    /// </summary>
    private void TxtNewFolderName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CreateNewFolder();
        }
        else if (e.Key == Key.Escape)
        {
            HideNewFolderPanel();
        }
    }

    /// <summary>
    /// 确认新建目录
    /// </summary>
    private void BtnConfirmNewFolder_Click(object sender, RoutedEventArgs e)
    {
        CreateNewFolder();
    }

    /// <summary>
    /// 取消新建目录
    /// </summary>
    private void BtnCancelNewFolder_Click(object sender, RoutedEventArgs e)
    {
        HideNewFolderPanel();
    }

    /// <summary>
    /// 确定按钮点击
    /// </summary>
    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput())
        {
            return;
        }

        try
        {
            // 创建笔记，使用输入框中的 URL
            var title = TxtTitle.Text.Trim();
            var url = TxtUrl.Text.Trim();
            CreatedNote = PioneerNoteService.Instance.RecordNote(url, title, _selectedFolderId);
            Result = true;
            CloseWithAnimation();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
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

    /// <summary>
    /// 开荒笔记按钮点击
    /// </summary>
    private void BtnPioneerNotes_Click(object sender, RoutedEventArgs e)
    {
        // 打开开荒笔记窗口
        var noteWindow = new PioneerNoteWindow();
        noteWindow.Owner = this.Owner ?? this; // 使用对话框的 Owner 或自己作为 Owner
        noteWindow.ShowDialog();

        // 刷新目录树（可能在开荒笔记中修改了目录）
        RefreshFolderTree();
    }

#endregion

#region New Folder

    /// <summary>
    /// 显示新建目录面板
    /// </summary>
    private void ShowNewFolderPanel()
    {
        NewFolderPanel.Visibility = Visibility.Visible;
        TxtNewFolderName.Text = string.Empty;
        TxtNewFolderName.Focus();
    }

    /// <summary>
    /// 隐藏新建目录面板
    /// </summary>
    private void HideNewFolderPanel()
    {
        NewFolderPanel.Visibility = Visibility.Collapsed;
        TxtNewFolderName.Text = string.Empty;
    }

    /// <summary>
    /// 创建新目录
    /// </summary>
    private void CreateNewFolder()
    {
        var folderName = TxtNewFolderName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        try
        {
            // 在当前选中的目录下创建新目录
            var newFolder = PioneerNoteService.Instance.CreateFolder(folderName, _selectedFolderId);

            // 刷新目录树
            RefreshFolderTree();

            // 隐藏新建面板
            HideNewFolderPanel();

            // 选中新创建的目录
            SelectFolderById(newFolder.Id);
        }
        catch (Exception ex)
        {
            ShowError($"创建目录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据 ID 选中目录
    /// </summary>
    private void SelectFolderById(string folderId)
    {
        // 递归查找并选中目录
        foreach (var item in _folderTreeItems)
        {
            if (SelectFolderInTree(item, folderId))
            {
                break;
            }
        }
    }

    /// <summary>
    /// 在树中递归查找并选中目录
    /// </summary>
    private bool SelectFolderInTree(FolderTreeItem item, string folderId)
    {
        if (item.Id == folderId)
        {
            _selectedFolderId = folderId;
            return true;
        }

        foreach (var child in item.Children)
        {
            if (SelectFolderInTree(child, folderId))
            {
                return true;
            }
        }

        return false;
    }

#endregion

#region Validation

    /// <summary>
    /// 验证输入
    /// </summary>
    private bool ValidateInput()
    {
        var title = TxtTitle.Text?.Trim();
        var url = TxtUrl.Text?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("笔记标题不能为空");
            TxtTitle.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            ShowError("URL 不能为空");
            TxtUrl.Focus();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 清除错误消息
    /// </summary>
    private void ClearError()
    {
        TxtError.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 更新确定按钮状态
    /// </summary>
    private void UpdateConfirmButton()
    {
        BtnConfirm.IsEnabled = !string.IsNullOrWhiteSpace(TxtTitle.Text) && !string.IsNullOrWhiteSpace(TxtUrl.Text);
    }

#endregion
}

/// <summary>
/// 目录树项模型
/// </summary>
public class FolderTreeItem
{
    /// <summary>
    /// 目录 ID（null 表示根目录）
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 目录名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 目录图标
    /// </summary>
    public string Icon { get; set; } = "📁";

    /// <summary>
    /// 是否为根目录
    /// </summary>
    public bool IsRoot { get; set; }

    /// <summary>
    /// 子目录
    /// </summary>
    public ObservableCollection<FolderTreeItem> Children { get; set; } = new();
}
}
