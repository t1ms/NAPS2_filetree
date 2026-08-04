using NAPS2.EtoForms.Ui;
using Xunit;

namespace NAPS2.Lib.Tests.WinForms;

public class DesktopFormTests
{
    [Fact]
    public void DragMove_CancelledDoesNotMove()
    {
        var confirmationShown = false;

        var shouldMove = DesktopForm.ShouldMoveDraggedImages(2, 1, () =>
        {
            confirmationShown = true;
            return false;
        });

        Assert.False(shouldMove);
        Assert.True(confirmationShown);
    }

    [Fact]
    public void DragMove_ConfirmedMoves()
    {
        var shouldMove = DesktopForm.ShouldMoveDraggedImages(2, 2, () => true);

        Assert.True(shouldMove);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(2, 0)]
    public void DragMove_InvalidDropDoesNotAskForConfirmation(int position, int imageCount)
    {
        var confirmationShown = false;

        var shouldMove = DesktopForm.ShouldMoveDraggedImages(position, imageCount, () =>
        {
            confirmationShown = true;
            return true;
        });

        Assert.False(shouldMove);
        Assert.False(confirmationShown);
    }
}