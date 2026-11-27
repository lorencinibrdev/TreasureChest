using Adw;
using Gtk;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Runners;
using AlertDialog = Adw.AlertDialog;

namespace TreasureChest.Source.Dialogs;

public static class MissingRunnerDialog
{
    public static Task<bool> ShowAsync(Widget parent, GameEntry gameEntry, ERunnerType runnerType)
    {
        var extraBox = Box.New(Orientation.Vertical, 12);
        extraBox.MarginTop = 12;
        extraBox.MarginBottom = 12;
        
        var titleLabel = Label.New(gameEntry.Title);
        titleLabel.AddCssClass("title-4");

        var infoLabel = Label.New(I18n._($"This game requires {runnerType} to run on Linux. Would you like to download and configure it now?"));
        infoLabel.Wrap = true;
        infoLabel.Justify = Justification.Center;
        infoLabel.AddCssClass("dim-label");
        
        extraBox.Append(titleLabel);
        extraBox.Append(infoLabel);
        
        var dialog = AlertDialog.New(
            heading: I18n._("Compatibility Runner Required"),
            body: I18n._($"No {runnerType} runner is currently installed."));

        dialog.ExtraChild = extraBox;
        
        dialog.AddResponse("cancel", I18n._("Cancel"));
        dialog.AddResponse("download", I18n._("Download Runner"));
        
        dialog.SetResponseAppearance("download", ResponseAppearance.Suggested);
        dialog.SetDefaultResponse("download");
        dialog.SetCloseResponse("cancel");

        var tcs = new TaskCompletionSource<bool>();

        dialog.OnResponse += (_, args) => tcs.TrySetResult(args.Response == "download");
        
        dialog.Present(parent);

        return tcs.Task;
    }
}