using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// 开荒笔记管理窗口
/// 显示笔记树，支持搜索、排序、编辑和删除操作
/// </summary>
public partial class PioneerNoteWindow : AnimatedWindow
{
#region Events

    /// <summary>
    /// 选择笔记项事件（双击打开 URL）
    /// </summary>
    public event EventHandler<string>? NoteItemSelected;

#endregion

#region Fields

    private ObservableCollection<NoteTreeNode> _treeNodes = new();
    private string _searchKeyword = string.Empty;

#endregion

#region Constructor

    public PioneerNoteWindow()
    {
        InitializeComponent();
        LoadNoteTree();
        UpdateSortButton();
    }

#endregion

#region Private Methods

    /// <summary>
    /// 加载笔记树
    /// </summary>
    private void LoadNoteTree()
    {
        _treeNodes.Clear();

        var noteData = PioneerNoteService.Instance.GetNoteTree();
        var sortDirection = noteData.SortOrder;

        // 如果有搜索关键词，显示搜索结果
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            LoadSearchResults();
            return;
        }

        // 构建树形结构
        // 先添加根目录下的目录（按时间排序）
        var rootFolders = noteData.Folders.Where(f => f.ParentId == null).ToList();
        rootFolders = sortDirection == SortDirection.Ascending
                          ? rootFolders.OrderBy(f => f.CreatedTime).ToList()
                          : rootFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var folder in rootFolders)
        {
            var folderNode = BuildFolderNode(folder, noteData, sortDirection);
            _treeNodes.Add(folderNode);
        }

        // 添加根目录下的笔记项
        var rootItems = noteData.Items.Where(i => i.FolderId == null).ToList();

        rootItems = SortItems(rootItems, sortDirection);

        foreach (var item in rootItems)
        {
            var itemNode = BuildItemNode(item);
            _treeNodes.Add(itemNode);
        }

        NoteTree.ItemsSource = _treeNodes;
        SetupTreeItemTemplate();

        // 更新空状态提示
        var hasContent = _treeNodes.Count > 0;
        EmptyHint.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 加载搜索结果（以树形结构展现，只显示匹配的目录和笔记项）
    /// </summary>
    private void LoadSearchResults()
    {
        var searchResults = PioneerNoteService.Instance.SearchNotes(_searchKeyword);
        var noteData = PioneerNoteService.Instance.GetNoteTree();
        var sortDirection = noteData.SortOrder;

        // 收集所有匹配项的目录 ID
        var matchedFolderIds = new HashSet<string>();
        foreach (var item in searchResults)
        {
            if (!string.IsNullOrEmpty(item.FolderId))
            {
                // 添加该目录及其所有父目录
                var folderId = item.FolderId;
                while (!string.IsNullOrEmpty(folderId))
                {
                    matchedFolderIds.Add(folderId);
                    var folder = noteData.Folders.FirstOrDefault(f => f.Id == folderId);
                    folderId = folder?.ParentId;
                }
            }
        }

        // 构建树形结构，只包含匹配的目录（按时间排序）
        var rootFolders = noteData.Folders.Where(f => f.ParentId == null && matchedFolderIds.Contains(f.Id)).ToList();
        rootFolders = sortDirection == SortDirection.Ascending
                          ? rootFolders.OrderBy(f => f.CreatedTime).ToList()
                          : rootFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var folder in rootFolders)
        {
            var folderNode = BuildSearchFolderNode(folder, noteData, sortDirection, searchResults, matchedFolderIds);
            if (folderNode.Children?.Count > 0)
            {
                _treeNodes.Add(folderNode);
            }
        }

        // 添加根目录下的匹配笔记项
        var rootItems = searchResults.Where(i => i.FolderId == null).ToList();
        rootItems = SortItems(rootItems, sortDirection);

        foreach (var item in rootItems)
        {
            var itemNode = BuildItemNode(item);
            _treeNodes.Add(itemNode);
        }

        NoteTree.ItemsSource = _treeNodes;
        SetupTreeItemTemplate();

        // 更新空状态提示
        var hasContent = _treeNodes.Count > 0;
        EmptyHint.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
        if (!hasContent && !string.IsNullOrWhiteSpace(_searchKeyword))
        {
            EmptyHint.Text = "未找到匹配的笔记";
        }
        else
        {
            EmptyHint.Text = "暂无笔记内容";
        }
    }

    /// <summary>
    /// 构建搜索结果的目录节点（只包含匹配的子项）
    /// </summary>
    private NoteTreeNode BuildSearchFolderNode(NoteFolder folder, PioneerNoteData noteData, SortDirection sortDirection,
                                               List<NoteItem> searchResults, HashSet<string> matchedFolderIds)
    {
        var node = new NoteTreeNode { Id = folder.Id,
                                      Title = folder.Name,
                                      Icon = folder.Icon ?? "📁",
                                      IsFolder = true,
                                      RecordedTime = folder.CreatedTime,
                                      Children = new ObservableCollection<NoteTreeNode>() };

        // 添加匹配的子目录（按时间排序）
        var childFolders =
            noteData.Folders.Where(f => f.ParentId == folder.Id && matchedFolderIds.Contains(f.Id)).ToList();
        childFolders = sortDirection == SortDirection.Ascending
                           ? childFolders.OrderBy(f => f.CreatedTime).ToList()
                           : childFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var childFolder in childFolders)
        {
            var childNode =
                BuildSearchFolderNode(childFolder, noteData, sortDirection, searchResults, matchedFolderIds);
            if (childNode.Children?.Count > 0)
            {
                node.Children.Add(childNode);
            }
        }

        // 添加目录下匹配的笔记项
        var items = searchResults.Where(i => i.FolderId == folder.Id).ToList();
        items = SortItems(items, sortDirection);

        foreach (var item in items)
        {
            var itemNode = BuildItemNode(item);
            node.Children.Add(itemNode);
        }

        return node;
    }

    /// <summary>
    /// 构建目录节点
    /// </summary>
    private NoteTreeNode BuildFolderNode(NoteFolder folder, PioneerNoteData noteData, SortDirection sortDirection)
    {
        var node = new NoteTreeNode { Id = folder.Id,
                                      Title = folder.Name,
                                      Icon = folder.Icon ?? "📁",
                                      IsFolder = true,
                                      RecordedTime = folder.CreatedTime,
                                      Children = new ObservableCollection<NoteTreeNode>() };

        // 添加子目录（按时间排序）
        var childFolders = noteData.Folders.Where(f => f.ParentId == folder.Id).ToList();
        childFolders = sortDirection == SortDirection.Ascending
                           ? childFolders.OrderBy(f => f.CreatedTime).ToList()
                           : childFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var childFolder in childFolders)
        {
            var childNode = BuildFolderNode(childFolder, noteData, sortDirection);
            node.Children.Add(childNode);
        }

        // 添加目录下的笔记项
        var items = noteData.Items.Where(i => i.FolderId == folder.Id).ToList();

        items = SortItems(items, sortDirection);

        foreach (var item in items)
        {
            var itemNode = BuildItemNode(item);
            node.Children.Add(itemNode);
        }

        return node;
    }

    /// <summary>
    /// 构建笔记项节点
    /// </summary>
    private NoteTreeNode BuildItemNode(NoteItem item)
    {
        return new NoteTreeNode { Id = item.Id,
                                  Title = item.Title,
                                  Url = item.Url,
                                  Icon = "🔗",
                                  IsFolder = false,
                                  RecordedTime = item.RecordedTime,
                                  FolderId = item.FolderId };
    }

    /// <summary>
    /// 排序笔记项
    /// </summary>
    private List<NoteItem> SortItems(List<NoteItem> items, SortDirection direction)
    {
        return direction == SortDirection.Ascending ? items.OrderBy(i => i.RecordedTime).ToList()
                                                    : items.OrderByDescending(i => i.RecordedTime).ToList();
    }

    /// <summary>
    /// 设置树项模板
    /// </summary>
    private void SetupTreeItemTemplate()
    {
        // 模板已在 XAML 中定义，此处仅用于刷新绑定
    }

    /// <summary>
    /// 更新排序按钮文本
    /// </summary>
    private void UpdateSortButton()
    {
        var sortOrder = PioneerNoteService.Instance.CurrentSortOrder;
        BtnSort.Content = sortOrder == SortDirection.Descending ? "↓ 最新" : "↑ 最早";
    }

    /// <summary>
    /// 刷新笔记树
    /// </summary>
    private void RefreshNoteTree()
    {
        // 重新加载树
        LoadNoteTree();

        // 强制刷新 TreeView 的 ItemsSource
        var temp = NoteTree.ItemsSource;
        NoteTree.ItemsSource = null;
        NoteTree.ItemsSource = temp;
    }

    /// <summary>
    /// 显示编辑对话框
    /// </summary>
    private void ShowEditDialog(NoteTreeNode node)
    {
        // 如果是笔记项，显示 URL 输入框
        var showUrl = !node.IsFolder;
        var editDialog = new NoteEditDialog(node.IsFolder ? "编辑目录" : "编辑笔记", node.Title, "请输入新名称：",
                                            showUrl: showUrl, isConfirmDialog: false, defaultUrl: node.Url);

        editDialog.Owner = this;
        editDialog.ShowDialog();

        if (editDialog.Result && !string.IsNullOrWhiteSpace(editDialog.InputText))
        {
            try
            {
                if (node.IsFolder)
                {
                    PioneerNoteService.Instance.UpdateFolder(node.Id!, editDialog.InputText);
                }
                else
                {
                    // 更新笔记项，包括 URL
                    PioneerNoteService.Instance.UpdateNote(node.Id!, editDialog.InputText, editDialog.UrlText);
                }
                RefreshNoteTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 显示删除确认对话框
    /// </summary>
    private void ShowDeleteConfirmDialog(NoteTreeNode node)
    {
        var message = node.IsFolder ? $"确定要删除目录 \"{node.Title}\" 及其所有内容吗？此操作不可撤销。"
                                    : $"确定要删除笔记 \"{node.Title}\" 吗？此操作不可撤销。";

        // 使用自定义对话框而不是系统 MessageBox
        // 参数顺序: title, defaultValue, prompt, showUrl, isConfirmDialog
        var confirmDialog = new NoteEditDialog("确认删除", "", message, false, true);
        confirmDialog.Owner = this;
        confirmDialog.ShowDialog();

        if (confirmDialog.Result)
        {
            try
            {
                if (node.IsFolder)
                {
                    PioneerNoteService.Instance.DeleteFolder(node.Id!, true);
                }
                else
                {
                    PioneerNoteService.Instance.DeleteNote(node.Id!);
                }
                RefreshNoteTree();
            }
            catch (Exception ex)
            {
                var errorDialog = new NoteEditDialog("错误", "", $"删除失败: {ex.Message}", false, true);
                errorDialog.Owner = this;
                errorDialog.ShowDialog();
            }
        }
    }

    /// <summary>
    /// 显示新建目录对话框
    /// </summary>
    private void ShowNewFolderDialog(string? parentId = null)
    {
        var editDialog = new NoteEditDialog("新建目录", "", "请输入目录名称：");

        editDialog.Owner = this;
        editDialog.ShowDialog();

        if (editDialog.Result && !string.IsNullOrWhiteSpace(editDialog.InputText))
        {
            try
            {
                PioneerNoteService.Instance.CreateFolder(editDialog.InputText, parentId);
                RefreshNoteTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 显示移动对话框
    /// </summary>
    private void ShowMoveDialog(NoteTreeNode node)
    {
        if (node.IsFolder)
            return;

        // 获取所有目录用于选择
        var noteData = PioneerNoteService.Instance.GetNoteTree();
        var folders = noteData.Folders;

        // 创建目录选择对话框
        var moveDialog = new NoteMoveDialog(folders, node.FolderId);
        moveDialog.Owner = this;
        moveDialog.ShowDialog();

        if (moveDialog.Result)
        {
            try
            {
                PioneerNoteService.Instance.MoveNote(node.Id!, moveDialog.SelectedFolderId);
                RefreshNoteTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 搜索框文本变化
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchKeyword = SearchBox.Text.Trim();
        LoadNoteTree();
    }

    /// <summary>
    /// 新建目录按钮点击
    /// </summary>
    private void BtnNewFolder_Click(object sender, RoutedEventArgs e)
    {
        // 获取当前选中的目录作为父目录
        string? parentId = null;
        if (NoteTree.SelectedItem is NoteTreeNode selectedNode && selectedNode.IsFolder)
        {
            parentId = selectedNode.Id;
        }
        ShowNewFolderDialog(parentId);
    }

    /// <summary>
    /// 记录笔记按钮点击
    /// </summary>
    private void BtnRecordNote_Click(object sender, RoutedEventArgs e)
    {
        ShowRecordNoteDialog();
    }

    /// <summary>
    /// 显示记录笔记对话框
    /// </summary>
    private void ShowRecordNoteDialog()
    {
        // 使用完整的笔记对话框，支持选择目录
        var noteDialog = new RecordNoteDialog("", "");
        noteDialog.Owner = this;
        noteDialog.ShowDialog();

        if (noteDialog.Result && noteDialog.CreatedNote != null)
        {
            // 笔记已创建，刷新树
            RefreshNoteTree();
        }
    }

    /// <summary>
    /// 排序切换按钮点击
    /// </summary>
    private void BtnSort_Click(object sender, RoutedEventArgs e)
    {
        PioneerNoteService.Instance.ToggleSortOrder();
        UpdateSortButton();
        RefreshNoteTree();
    }

    /// <summary>
    /// 删除项按钮点击
    /// </summary>
    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            // 查找对应的节点
            var node = FindNodeById(id, _treeNodes);
            if (node != null)
            {
                ShowDeleteConfirmDialog(node);
            }
        }
        e.Handled = true;
    }

    /// <summary>
    /// 根据 ID 查找节点
    /// </summary>
    private NoteTreeNode? FindNodeById(string id, IEnumerable<NoteTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
                return node;

            if (node.Children != null && node.Children.Count > 0)
            {
                var found = FindNodeById(id, node.Children);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    /// <summary>
    /// 笔记树双击事件
    /// </summary>
    private void NoteTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NoteTree.SelectedItem is NoteTreeNode node && !node.IsFolder && !string.IsNullOrEmpty(node.Url))
        {
            CloseWithAnimation(() => NoteItemSelected?.Invoke(this, node.Url));
        }
    }

    /// <summary>
    /// 笔记树选择变化事件
    /// </summary>
    private void NoteTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // 设置右键菜单
        if (e.NewValue is NoteTreeNode node)
        {
            SetupContextMenu(node);
        }
    }

    /// <summary>
    /// 设置右键菜单
    /// </summary>
    private void SetupContextMenu(NoteTreeNode node)
    {
        var contextMenu = new ContextMenu { Style = FindResource("DarkContextMenuStyle") as Style };

        // 编辑菜单项
        var editItem = new MenuItem { Header = "✏️ 编辑", Style = FindResource("DarkMenuItemStyle") as Style };
        editItem.Click += (s, e) => ShowEditDialog(node);
        contextMenu.Items.Add(editItem);

        // 移动菜单项（仅笔记项可移动）
        if (!node.IsFolder)
        {
            var moveItem = new MenuItem { Header = "📂 移动到...", Style = FindResource("DarkMenuItemStyle") as Style };
            moveItem.Click += (s, e) => ShowMoveDialog(node);
            contextMenu.Items.Add(moveItem);
        }

        // 删除菜单项
        var deleteItem = new MenuItem { Header = "🗑️ 删除", Style = FindResource("DarkMenuItemStyle") as Style };
        deleteItem.Click += (s, e) => ShowDeleteConfirmDialog(node);
        contextMenu.Items.Add(deleteItem);

        // 如果是目录，添加新建子目录选项
        if (node.IsFolder)
        {
            contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(
                                                      System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)) });

            var newFolderItem =
                new MenuItem { Header = "📁 新建子目录", Style = FindResource("DarkMenuItemStyle") as Style };
            newFolderItem.Click += (s, e) => ShowNewFolderDialog(node.Id);
            contextMenu.Items.Add(newFolderItem);
        }

        // 如果是笔记项，添加打开选项
        if (!node.IsFolder && !string.IsNullOrEmpty(node.Url))
        {
            contextMenu.Items.Insert(0, new Separator { Background = new System.Windows.Media.SolidColorBrush(
                                                            System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)) });

            var openItem = new MenuItem { Header = "🔗 打开", Style = FindResource("DarkMenuItemStyle") as Style };
            openItem.Click += (s, e) =>
            { CloseWithAnimation(() => NoteItemSelected?.Invoke(this, node.Url)); };
            contextMenu.Items.Insert(0, openItem);
        }

        NoteTree.ContextMenu = contextMenu;
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    /// <summary>
    /// 树容器点击事件 - 点击空白区域取消选中并使搜索框失去焦点
    /// </summary>
    private void TreeContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在 TreeViewItem 上
        var hitElement = e.OriginalSource as DependencyObject;
        while (hitElement != null)
        {
            if (hitElement is TreeViewItem)
            {
                // 点击在 TreeViewItem 上，不处理
                return;
            }
            hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
        }

        // 点击在空白区域，清除选中
        ClearTreeViewSelection();

        // 使搜索框失去焦点
        ClearSearchBoxFocus();
    }

    /// <summary>
    /// 使搜索框失去焦点
    /// </summary>
    private void ClearSearchBoxFocus()
    {
        if (SearchBox.IsFocused)
        {
            // 将焦点移到其他元素
            NoteTree.Focus();
        }
    }

    /// <summary>
    /// 内容区点击事件 - 使搜索框失去焦点
    /// </summary>
    private void ContentArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在搜索框上
        var hitElement = e.OriginalSource as DependencyObject;
        while (hitElement != null)
        {
            if (hitElement is TextBox)
            {
                // 点击在搜索框上，不处理
                return;
            }
            hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
        }

        // 使搜索框失去焦点
        ClearSearchBoxFocus();
    }

    /// <summary>
    /// 清除 TreeView 选中状态
    /// </summary>
    private void ClearTreeViewSelection()
    {
        if (NoteTree.SelectedItem != null)
        {
            // 遍历所有 TreeViewItem 并取消选中
            ClearTreeViewItemSelection(NoteTree);
        }
    }

    /// <summary>
    /// 递归清除 TreeViewItem 选中状态
    /// </summary>
    private void ClearTreeViewItemSelection(ItemsControl parent)
    {
        foreach (var item in parent.Items)
        {
            var treeViewItem = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = false;
                if (treeViewItem.HasItems)
                {
                    ClearTreeViewItemSelection(treeViewItem);
                }
            }
        }
    }

    /// <summary>
    /// TreeViewItem 右键点击事件 - 先选中该项再显示菜单
    /// </summary>
    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 获取被右键点击的 TreeViewItem
        var treeViewItem = sender as TreeViewItem;
        if (treeViewItem != null)
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
                clickedItem = System.Windows.Media.VisualTreeHelper.GetParent(clickedItem);
            }

            // 选中该项
            treeViewItem.IsSelected = true;
            treeViewItem.Focus();

            // 设置右键菜单
            if (treeViewItem.DataContext is NoteTreeNode node)
            {
                SetupContextMenu(node);
            }

            e.Handled = true;
        }
    }

#endregion
}

/// <summary>
/// 笔记树节点模型
/// </summary>
public class NoteTreeNode
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 标题/名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL（仅笔记项有）
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = "🔗";

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// 记录/创建时间
    /// </summary>
    public DateTime RecordedTime { get; set; }

    /// <summary>
    /// 所属目录 ID
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// 子节点
    /// </summary>
    public ObservableCollection<NoteTreeNode>? Children { get; set; }

    /// <summary>
    /// 格式化的时间显示
    /// </summary>
    public string FormattedTime => RecordedTime.ToString("MM/dd HH:mm");
}
}
