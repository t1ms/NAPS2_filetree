using System;
using System.Linq;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Ui;
using NAPS2.Images;
using NAPS2.ImportExport.Images;

namespace NAPS2.EtoForms.Desktop;

internal class DocumentTreeView
{
    private readonly DocumentManager _documentManager;
    private readonly DesktopForm _parentForm;
    private readonly TreeGridView _treeView;
    private readonly TreeGridItem _rootItem;
    private readonly UiImageList _imageList;
    private readonly ImageTransfer _imageTransfer;

    private class GroupTreeItem : TreeGridItem
    {
        public DocumentGroup Group { get; }

        public GroupTreeItem(DocumentGroup group)
        {
            Group = group;
            Values = new object[] { string.IsNullOrWhiteSpace(group.IndexField) ? "Document" : group.IndexField };
        }

        public void UpdateText()
        {
            Values = new object[] { string.IsNullOrWhiteSpace(Group.IndexField) ? "Document" : Group.IndexField };
        }
    }

    public DocumentTreeView(DocumentManager documentManager, DesktopForm parentForm, UiImageList imageList, ImageTransfer imageTransfer)
    {
        _documentManager = documentManager;
        _parentForm = parentForm;
        _imageList = imageList;
        _imageTransfer = imageTransfer;

        _rootItem = new TreeGridItem { Expanded = true, Values = new object[] { "Documents" } };

        _treeView = new TreeGridView
        {
            DataStore = _rootItem,
            Width = 200,
            ShowHeader = false,
            Border = BorderType.None
        };
        _treeView.Columns.Add(new GridColumn { DataCell = new TextBoxCell(0), AutoSize = true });

        _treeView.SelectionChanged += TreeView_SelectionChanged;
        _treeView.MouseDoubleClick += TreeView_MouseDoubleClick;
        _treeView.KeyDown += TreeView_KeyDown;

        _treeView.AllowDrop = true;
        _treeView.DragOver += TreeView_DragOver;
        _treeView.DragDrop += TreeView_DragDrop;

        var contextMenu = new ContextMenu();
        var mergeWithPrevItem = new ButtonMenuItem { Text = "Merge with Previous" };
        mergeWithPrevItem.Click += MergeWithPrevItem_Click;
        contextMenu.Items.Add(mergeWithPrevItem);

        var mergeWithNextItem = new ButtonMenuItem { Text = "Merge with Next" };
        mergeWithNextItem.Click += MergeWithNextItem_Click;
        contextMenu.Items.Add(mergeWithNextItem);

        var deleteItem = new ButtonMenuItem { Text = "Delete" };
        deleteItem.Click += DeleteItem_Click;
        contextMenu.Items.Add(deleteItem);

        _treeView.ContextMenu = contextMenu;

        _documentManager.GroupsChanged += DocumentManager_GroupsChanged;
        
        RefreshTree();
    }

    private void TreeView_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(_imageTransfer.TypeName))
        {
            e.Effects = DragEffects.Move;
        }
    }

    private void TreeView_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(_imageTransfer.TypeName))
        {
            var dataBytes = e.Data.GetData(_imageTransfer.TypeName) as byte[];
            if (dataBytes == null) return;
            var dataObj = _imageTransfer.FromBinaryData(dataBytes);
            
            // Note: TreeGridView doesn't expose a GetItemAt property directly in all Eto versions natively
            // But we can check if there's a selected item or hovering logic if DropEventArgs provides it.
            // Actually, DragEventArgs doesn't give us the target node out of the box in Eto without some work.
            // We can just rely on the Selection if the user selects the target node first, 
            // OR use e.ControlLocation if we have hit testing.
            // Fortunately, Eto's TreeGridView Drop might not have easy hit testing.
            // Let's assume the user has clicked on or dragged over the node, making it selected?
            // Actually, if we just use _treeView.SelectedItem as the drop target, it's a fallback.
            if (dataObj.ProcessId == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                if (_treeView.SelectedItem is GroupTreeItem targetGroupItem)
                {
                    // Move the selected items to the new group
                    foreach (var image in _imageList.Selection)
                    {
                        image.DocumentGroupId = targetGroupItem.Group.Id;
                        image.InvalidateThumbnail();
                    }
                    _documentManager.AddGroup(""); // force GroupsChanged
                }
            }
        }
    }

    private void TreeView_KeyDown(object? sender, Eto.Forms.KeyEventArgs e)
    {
        if (e.Key == Keys.F2 && _treeView.SelectedItem is GroupTreeItem groupItem)
        {
            BeginEditNode(groupItem);
        }
    }

    private void TreeView_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_treeView.SelectedItem is GroupTreeItem groupItem)
        {
            BeginEditNode(groupItem);
        }
    }

    private void BeginEditNode(GroupTreeItem groupItem)
    {
        var dialog = new Dialog<bool>
        {
            Title = "Rename Document",
            ClientSize = new Eto.Drawing.Size(250, 80)
        };
        var textBox = new TextBox { Text = groupItem.Group.IndexField };
        var okBtn = new Button { Text = "OK" };
        okBtn.Click += (s, e) => { dialog.Close(true); };
        
        dialog.Content = new StackLayout
        {
            Padding = 10,
            Spacing = 5,
            Items = { textBox, new StackLayoutItem(okBtn, HorizontalAlignment.Right) }
        };

        dialog.DefaultButton = okBtn;

        if (dialog.ShowModal(_parentForm))
        {
            groupItem.Group.IndexField = textBox.Text;
            groupItem.UpdateText();
            _treeView.ReloadItem(groupItem);
        }
    }

    private void MergeWithPrevItem_Click(object? sender, EventArgs e)
    {
        if (_treeView.SelectedItem is GroupTreeItem item)
        {
            _documentManager.MergeWithPrevious(item.Group);
        }
    }

    private void MergeWithNextItem_Click(object? sender, EventArgs e)
    {
        if (_treeView.SelectedItem is GroupTreeItem item)
        {
            int index = _documentManager.Groups.IndexOf(item.Group);
            if (index >= 0 && index < _documentManager.Groups.Count - 1)
            {
                var nextGroup = _documentManager.Groups[index + 1];
                _documentManager.MergeWithPrevious(nextGroup);
            }
        }
    }

    private void DeleteItem_Click(object? sender, EventArgs e)
    {
        if (_treeView.SelectedItem is GroupTreeItem item)
        {
            _documentManager.DeleteGroup(item.Group);
        }
    }

    private void DocumentManager_GroupsChanged(object? sender, EventArgs e)
    {
        Application.Instance.Invoke(RefreshTree);
    }

    private void RefreshTree()
    {
        _rootItem.Children.Clear();
        foreach (var group in _documentManager.Groups)
        {
            _rootItem.Children.Add(new GroupTreeItem(group));
        }
        _treeView.ReloadData();
    }

    private void TreeView_SelectionChanged(object? sender, EventArgs e)
    {
        if (_treeView.SelectedItem is GroupTreeItem item)
        {
            _parentForm.FilterToDocumentGroup(item.Group.Id);
        }
        else
        {
            _parentForm.FilterToDocumentGroup(null);
        }
    }

    public void SelectGroup(Guid groupId)
    {
        var item = _rootItem.Children.OfType<GroupTreeItem>().FirstOrDefault(i => i.Group.Id == groupId);
        if (item != null)
        {
            _treeView.SelectedItem = item;
        }
    }

    public LayoutElement CreateView()
    {
        return _treeView.Scale();
    }
}
