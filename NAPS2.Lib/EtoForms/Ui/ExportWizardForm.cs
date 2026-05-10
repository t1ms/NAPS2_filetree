using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

public class ExportWizardForm : EtoDialogBase
{
    private readonly RadioButton _separateRadio;
    private readonly RadioButton _singleRadio;

    public ExportWizardForm(Naps2Config config) : base(config)
    {
        _separateRadio = new RadioButton { Text = "Save as separate files in a folder" };
        _singleRadio = new RadioButton(_separateRadio) { Text = "Merge all into a single file" };
        _separateRadio.Checked = true;

        var nextCommand = new ActionCommand(() =>
        {
            IsSeparate = _separateRadio.Checked;
            Result = true;
            Close();
        })
        {
            Text = "Next",
            IconName = "control_play_blue_small"
        };
        var nextButton = C.Button(nextCommand, ButtonImagePosition.Left);
        DefaultButton = nextButton;
    }

    public int DocumentCount { get; set; }

    public bool IsSeparate { get; private set; }

    public bool Result { get; private set; }

    protected override void BuildLayout()
    {
        Title = "Export Options";

        FormStateController.SaveFormState = false;
        FormStateController.RestoreFormState = false;
        FormStateController.Resizable = false;

        LayoutController.Content = L.Column(
            L.Row(C.Label($"You have {DocumentCount} documents ready to save.")),
            C.Spacer().Height(15),
            L.Row(_separateRadio),
            C.Spacer().Height(5),
            L.Row(_singleRadio),
            C.Spacer().Height(15),
            L.Row(
                L.OkCancel(
                    DefaultButton.Scale(),
                    C.CancelButton(this, UiStrings.Cancel).Scale())
            )
        );
    }
}
