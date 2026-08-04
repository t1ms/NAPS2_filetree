using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Lets users assign the filename/index value for each document in the current session.
/// All changes are committed through DocumentManager so names remain unique and exports use them.
/// </summary>
public class DocumentIndexForm : EtoDialogBase
{
    private readonly DocumentManager _documentManager;
    private readonly DropDown _documentSelector = new();
    private readonly TextBox _indexName = new();
    private readonly Label _message = new();
    private bool _syncing;

    public DocumentIndexForm(Naps2Config config, DocumentManager documentManager) : base(config)
    {
        _documentManager = documentManager;
        Title = "Document Indexing";
        IconName = "text_small";
        _documentSelector.SelectedIndexChanged += (_, _) => LoadSelectedDocument();
    }

    protected override void BuildLayout()
    {
        FormStateController.DefaultExtraLayoutSize = new Eto.Drawing.Size(260, 80);
        LayoutController.Content = L.Column(
            C.Label("Choose a document, then enter the filename to use when saving documents separately."),
            C.Label("Document"),
            _documentSelector.Scale(),
            C.Label("Index filename"),
            _indexName.Scale(),
            _message,
            L.Row(
                C.Button("Save Name", SaveCurrentName),
                C.Filler(),
                C.Button("Close", Close)
            )
        );
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LoadDocuments();
    }

    private void LoadDocuments()
    {
        var selectedGroup = SelectedGroup;
        _syncing = true;
        try
        {
            _documentSelector.Items.Clear();
            foreach (var group in _documentManager.Groups)
            {
                _documentSelector.Items.Add(group.IndexField);
            }
            var selectedIndex = selectedGroup == null ? -1 : _documentManager.Groups.IndexOf(selectedGroup);
            _documentSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex :
                (_documentManager.Groups.Any() ? 0 : -1);
        }
        finally
        {
            _syncing = false;
        }
        LoadSelectedDocument();
    }

    private void LoadSelectedDocument()
    {
        if (_syncing) return;
        var group = SelectedGroup;
        _indexName.Text = group?.IndexField ?? "";
        _message.Text = group == null ? "No documents are available." : "";
    }

    private DocumentGroup? SelectedGroup =>
        _documentSelector.SelectedIndex is var index && index >= 0 && index < _documentManager.Groups.Count
            ? _documentManager.Groups[index]
            : null;

    private void SaveCurrentName()
    {
        var group = SelectedGroup;
        if (group == null) return;

        if (!_documentManager.TryRenameGroup(group, _indexName.Text, out var errorMessage))
        {
            _message.Text = errorMessage;
            _indexName.Focus();
            return;
        }

        _message.Text = $"Saved. This document will save as \"{group.IndexField}.pdf\".";
        LoadDocuments();
    }
}