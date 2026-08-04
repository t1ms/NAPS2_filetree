using Eto.Forms;
using NAPS2.Config;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.ImportExport;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Configures a folder that is monitored while NAPS2 is open. The selected profile supplies its
/// existing auto-save settings, including filename tokens and the desired document separation.
/// </summary>
public class HotFolderSettingsForm : EtoDialogBase
{
    private readonly IHotFolderService _hotFolderService;
    private readonly IProfileManager _profileManager;
    private readonly CheckBox _enabled = new() { Text = "Enable hot folder while NAPS2 is running" };
    private readonly TextBox _watchFolder = new();
    private readonly TextBox _destinationFolder = new();
    private readonly ComboBox _profile = new();
    private readonly Label _status = new();

    public HotFolderSettingsForm(Naps2Config config, IHotFolderService hotFolderService,
        IProfileManager profileManager) : base(config)
    {
        _hotFolderService = hotFolderService;
        _profileManager = profileManager;
        _hotFolderService.StatusChanged += HotFolderServiceOnStatusChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotFolderService.StatusChanged -= HotFolderServiceOnStatusChanged;
        base.OnClosed(e);
    }

    protected override void BuildLayout()
    {
        Title = "Hot Folder";
        FormStateController.DefaultExtraLayoutSize = new Eto.Drawing.Size(170, 0);
        FormStateController.FixedHeightLayout = true;

        var profiles = _profileManager.Profiles.Where(p => p.AutoSaveSettings != null).ToList();
        _profile.Items.AddRange(profiles.Select(p => new ListItem { Key = p.DisplayName, Text = p.DisplayName }));
        _enabled.Checked = Config.Get(c => c.EnableHotFolder);
        _watchFolder.Text = Config.Get(c => c.HotFolderPath);
        _destinationFolder.Text = Config.Get(c => c.HotFolderDestinationPath);
        _profile.SelectedKey = Config.Get(c => c.HotFolderProfileName);
        if (_profile.SelectedIndex < 0 && profiles.Count > 0)
        {
            _profile.SelectedIndex = 0;
        }
        UpdateStatus();

        LayoutController.Content = L.Column(
            _enabled,
            C.Spacer(),
            C.Label("Watch folder"),
            L.Row(_watchFolder, C.Button("Browse...", BrowseWatchFolder)),
            C.Label("Destination folder"),
            L.Row(_destinationFolder, C.Button("Browse...", BrowseDestinationFolder)),
            C.Label("Processing profile"),
            _profile,
            C.Label("The selected profile's Auto Save settings control output format, separation, and filename tokens."),
            C.Spacer(),
            _status,
            C.Filler(),
            L.Row(
                C.Filler(),
                L.OkCancel(C.OkButton(this, Save), C.CancelButton(this))
            )
        );
    }

    private void BrowseWatchFolder()
    {
        var dialog = CreateFolderDialog(_watchFolder.Text);
        if (dialog.ShowDialog(this) == DialogResult.Ok)
        {
            if (HotFolderService.TryGetExistingDirectory(dialog.Directory, out var path))
            {
                _watchFolder.Text = path;
            }
        }
    }

    private void BrowseDestinationFolder()
    {
        var dialog = CreateFolderDialog(_destinationFolder.Text);
        if (dialog.ShowDialog(this) == DialogResult.Ok)
        {
            if (HotFolderService.TryGetExistingDirectory(dialog.Directory, out var path))
            {
                _destinationFolder.Text = path;
            }
        }
    }

    private static SelectFolderDialog CreateFolderDialog(string? currentPath)
    {
        var dialog = new SelectFolderDialog();
        if (HotFolderService.TryGetExistingDirectory(currentPath, out var path))
        {
            dialog.Directory = path;
        }
        return dialog;
    }

    private bool Save()
    {
        string? watchFolder = null;
        string? destinationFolder = null;
        if (_enabled.IsChecked())
        {
            if (!HotFolderService.TryGetExistingDirectory(_watchFolder.Text, out var normalizedWatchFolder))
            {
                MessageBox.Show(this, "Choose an existing, accessible watch folder.", MessageBoxType.Warning);
                return false;
            }
            if (!HotFolderService.TryGetExistingDirectory(_destinationFolder.Text, out var normalizedDestinationFolder))
            {
                MessageBox.Show(this, "Choose an existing, accessible destination folder.", MessageBoxType.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(_profile.SelectedKey))
            {
                MessageBox.Show(this, "Choose a profile with Auto Save configured.", MessageBoxType.Warning);
                return false;
            }
            watchFolder = normalizedWatchFolder;
            destinationFolder = normalizedDestinationFolder;
        }
        else
        {
            watchFolder = HotFolderService.NormalizeConfiguredPath(_watchFolder.Text) ?? "";
            destinationFolder = HotFolderService.NormalizeConfiguredPath(_destinationFolder.Text) ?? "";
        }

        if (watchFolder.Length > 0 && destinationFolder.Length > 0 &&
            HotFolderService.IsPathInsideOrEqual(destinationFolder, watchFolder))
        {
            MessageBox.Show(this, "The destination folder must be outside the watch folder.",
                MessageBoxType.Warning);
            return false;
        }

        var transaction = Config.User.BeginTransaction();
        transaction.Set(c => c.EnableHotFolder, _enabled.IsChecked());
        transaction.Set(c => c.HotFolderPath, watchFolder);
        transaction.Set(c => c.HotFolderDestinationPath, destinationFolder);
        transaction.Set(c => c.HotFolderProfileName, _profile.SelectedKey);
        transaction.Commit();
        _hotFolderService.Start();
        if (_enabled.IsChecked() && !_hotFolderService.IsActive)
        {
            Config.User.Set(c => c.EnableHotFolder, false);
            MessageBox.Show(this, _hotFolderService.StatusText, MessageBoxType.Warning);
            return false;
        }
        return true;
    }

    private void UpdateStatus()
    {
        _status.Text = _hotFolderService.StatusText;
    }

    private void HotFolderServiceOnStatusChanged(object? sender, EventArgs e)
    {
        Invoker.Current.Invoke(UpdateStatus);
    }
}