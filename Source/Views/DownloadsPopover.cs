using Adw;
using Gtk;
using TreasureChest.Source.Services.Downloads;

namespace TreasureChest.Source.Views;

public class DownloadsPopover
{
    // Private Members:
    private readonly Box _taskBox;
    private readonly Dictionary<string, ActionRow> _downloadRows = [];
    private readonly Dictionary<string, ProgressBar> _progressBars = [];

    public DownloadsPopover(MenuButton targetMenuButton)
    {
        var popover = Popover.New();
        popover.Position = PositionType.Bottom;

        var scroll = ScrolledWindow.New();
        scroll.SetSizeRequest(350, 400);
        scroll.HscrollbarPolicy = PolicyType.Never;

        _taskBox = Box.New(Orientation.Vertical, 0);
        _taskBox.AddCssClass("linked");
        _taskBox.MarginTop = 12;
        _taskBox.MarginBottom = 12;
        _taskBox.MarginStart = 12;
        _taskBox.MarginEnd = 12;

        scroll.SetChild(_taskBox);
        popover.Child = scroll;
        targetMenuButton.Popover = popover;

        DownloadQueueService.Instance.TaskAdded += HandleDownloadAdded;
        DownloadQueueService.Instance.TaskUpdated += HandleDownloadUpdated;
        DownloadQueueService.Instance.TaskCompleted += HandleDownloadCompleted;
    }
    
    private void HandleDownloadAdded(DownloadTask task)
    {
        var row = ActionRow.New();
        row.Title = task.Title;
        row.Subtitle = "Starting...";

        var progressBar = ProgressBar.New();
        progressBar.Valign = Align.Center;
        progressBar.MarginEnd = 12;
        progressBar.SetSizeRequest(100, -1);

        var cancelBtn = Button.NewFromIconName("window-close-symbolic");
        cancelBtn.AddCssClass("flat");
        cancelBtn.AddCssClass("circular");
        cancelBtn.Valign = Align.Center;
        cancelBtn.OnClicked += (_, _) => DownloadQueueService.Instance.CancelDownload(task);

        row.AddSuffix(progressBar);
        row.AddSuffix(cancelBtn);
        
        _downloadRows[task.Id] = row;
        _progressBars[task.Id] = progressBar;
        _taskBox.Append(row);
    }
    
    private void HandleDownloadUpdated(DownloadTask task)
    {
        if (!_downloadRows.TryGetValue(task.Id, out var row) || !_progressBars.TryGetValue(task.Id, out var progress))
            return;

        progress.Fraction = task.Progress;

        var mbps = task.SpeedBytesPerSecond / 1024 / 1024;
        row.Subtitle = task.Status == EDownloadStatus.Downloading 
            ? $"{mbps:F1} MB/s" 
            : task.Status.ToString();
    }

    private void HandleDownloadCompleted(DownloadTask task)
    {
        if (!_downloadRows.TryGetValue(task.Id, out var row) || !_progressBars.TryGetValue(task.Id, out var progress))
            return;

        progress.Fraction = 1.0;
        row.Subtitle = "Completed";
    }
}