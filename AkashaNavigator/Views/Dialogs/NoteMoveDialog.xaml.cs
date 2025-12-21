using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 笔记移动对话框
/// 用于选择目标目录移动笔记项
/// </summary>
public partial class NoteMoveDialog : AnimatedWindow
{
#region Properties

    /// <summary>
    /// 对话框结果：true=确定，false=取消
    /// </summary>
    public bool Result { get; private set; }

    /// <summary>
    /// 选中的目录 ID（null 表示根目录）
    /// </summary>
    public string? SelectedFolderId { get; private set; }

#endregion

#region Constructor

    /// <summary>
    /// 创建移动对话框
    /// </summary>
    /// <param name="folders">可选的目录列表</param>
    /// <param name="currentFolderId">当前所在目录 ID</param>
    public NoteMoveDialog(List<NoteFolder> folders, string? currentFolderId)
    {
        InitializeComponent();

        // 构建目录列表（包含根目录选项）
        var folderItems =
            new List<FolderItem> { new FolderItem { Id = null, Name = "根目录", Icon = "🏠", Indent = 0 } };

        // 添加所有目录（扁平化显示，带缩进）
        AddFoldersRecursive(folderItems, folders, null, 0);

        FolderList.ItemsSource = folderItems;

        // 选中当前目录
        var currentItem = folderItems.FirstOrDefault(f => f.Id == currentFolderId);
        if (currentItem != null)
        {
            FolderList.SelectedItem = currentItem;
        }
        else
        {
            FolderList.SelectedIndex = 0; // 默认选中根目录
        }
    }

#endregion

#region Private Methods

    /// <summary>
    /// 递归添加目录到列表
    /// </summary>
    private void AddFoldersRecursive(List<FolderItem> items, List<NoteFolder> allFolders, string? parentId, int indent)
    {
        var childFolders = allFolders.Where(f => f.ParentId == parentId).OrderBy(f => f.SortOrder).ToList();

        foreach (var folder in childFolders)
        {
            var prefix = new string(' ', indent * 4);
            items.Add(new FolderItem { Id = folder.Id, Name = prefix + folder.Name, Icon = folder.Icon ?? "📁",
                                       Indent = indent });

            // 递归添加子目录
            AddFoldersRecursive(items, allFolders, folder.Id, indent + 1);
        }
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 目录列表选择变化
    /// </summary>
    private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FolderList.SelectedItem is FolderItem item)
        {
            SelectedFolderId = item.Id;
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

/// <summary>
/// 目录列表项
/// </summary>
public class FolderItem
{
    /// <summary>
    /// 目录 ID（null 表示根目录）
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = "📁";

    /// <summary>
    /// 缩进级别
    /// </summary>
    public int Indent { get; set; }
}
}
