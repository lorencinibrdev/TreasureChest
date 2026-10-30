using Adw;
using Cairo;
using Gdk;
using Gtk;
using TreasureChest.Source.Services.Downloads;

namespace TreasureChest.Source.Widgets;

public class DownloadsPopover
{
    // Private Members:
    private readonly MenuButton _menuButton;
    private readonly ListBox _taskBox;
    private readonly DrawingArea _headerProgressRing;
    private double _agregatedProgress;
    private RGBA? _accentColor;
    private readonly Dictionary<string, ActionRow> _downloadRows = [];
    private readonly Dictionary<string, ProgressBar> _progressBars = [];
    private readonly Dictionary<string, Button> _actionButtons = [];
    private readonly Dictionary<string, double> _activeProgress = [];

    public DownloadsPopover(MenuButton targetMenuButton)
    {
        _menuButton = targetMenuButton;
        _menuButton.Visible = false;

        _headerProgressRing = DrawingArea.New();
        _headerProgressRing.SetSizeRequest(34, 34);
        _headerProgressRing.SetDrawFunc(DrawFunc);

        var headerIcon = Image.NewFromIconName("folder-download-symbolic");
        headerIcon.Valign = Align.Center;
        headerIcon.Halign = Align.Center;
        
        var buttonOverlay = Overlay.New();
        buttonOverlay.Child = _headerProgressRing;
        buttonOverlay.AddOverlay(headerIcon);

        _menuButton.Child = buttonOverlay;
        
        var popover = Popover.New();
        popover.Position = PositionType.Bottom;

        var scroll = ScrolledWindow.New();
        scroll.SetSizeRequest(350, 400);
        scroll.HscrollbarPolicy = PolicyType.Never;

        _taskBox = ListBox.New();
        _taskBox.SelectionMode = SelectionMode.None;
        _taskBox.AddCssClass("boxed-list");
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

        StyleManager.GetDefault().OnNotify += (_, _) => _accentColor = null;
    }

    private void DrawFunc(DrawingArea drawingArea, Context cr, int width, int height)
    {
        var cx = width / 2.0;
        var cy = height / 2.0;
        var radius = Math.Min(width, height) / 2.0 - 1.5;

        cr.LineWidth = 3.0;
        cr.LineCap = LineCap.Round;
        
        cr.SetSourceRgba(0.5, 0.5, 0.5, 0.2);
        cr.Arc(cx, cy, radius, 0, 2 * Math.PI);
        cr.Stroke();

        if (!(_agregatedProgress > 0)) 
            return;
        
        // Trying to fetch the user's accent color:
        _accentColor ??= drawingArea.GetStyleContext().LookupColor("accent_bg_color", out var accentColor)
            ? accentColor
            : new RGBA { Red = 0.21f, Green = 0.52f, Blue = 0.89f };
        
        cr.SetSourceRgba(_accentColor.Red, _accentColor.Green, _accentColor.Blue, _accentColor.Alpha);
        
        const double startAngle = -Math.PI / 2;
        var endAngle = startAngle + _agregatedProgress * 2 * Math.PI;
        cr.Arc(cx, cy, radius, startAngle, endAngle);
        cr.Stroke();
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

        var actionButton = Button.NewFromIconName("window-close-symbolic");
        actionButton.AddCssClass("flat");
        actionButton.AddCssClass("circular");
        actionButton.Valign = Align.Center;
        actionButton.TooltipText = "Cancel Download";
        actionButton.MarginTop = 8;
        actionButton.MarginBottom = 8;
        
        actionButton.OnClicked += (_, _) =>
        {
            if (task.Status == EDownloadStatus.Downloading)
            {
                DownloadQueueService.Instance.CancelDownload(task);    
            }

            RemoveTaskRow(task.Id);
        };

        row.AddSuffix(progressBar);
        row.AddSuffix(actionButton);
        
        _downloadRows[task.Id] = row;
        _progressBars[task.Id] = progressBar;
        _actionButtons[task.Id] = actionButton;
        
        _taskBox.Append(row);

        UpdateMenuButtonVisibility();
        
        _activeProgress[task.Id] = 0.0;
        UpdateAggregateProgress();
        UpdateMenuButtonVisibility();
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
        
        _activeProgress[task.Id] = task.Progress;
        UpdateAggregateProgress();
    }

    private void HandleDownloadCompleted(DownloadTask task)
    {
        if (!_downloadRows.TryGetValue(task.Id, out var row) || 
            !_progressBars.TryGetValue(task.Id, out var progress) ||
            !_actionButtons.TryGetValue(task.Id, out var actionButton))
            return;

        progress.Visible = false;
        row.Subtitle = "Completed";

        actionButton.IconName = "edit-clear-symbolic";
        actionButton.TooltipText = "Dismiss";
        
        _activeProgress.Remove(task.Id);
        UpdateAggregateProgress();
    }
    
    private void UpdateMenuButtonVisibility()
    {
        _menuButton.Visible = _downloadRows.Count > 0;
    }

    private void RemoveTaskRow(string taskId)
    {
        if (!_downloadRows.TryGetValue(taskId, out var row)) 
            return;
        
        _taskBox.Remove(row);
        _downloadRows.Remove(taskId);
        _progressBars.Remove(taskId);
        _actionButtons.Remove(taskId);
        _activeProgress.Remove(taskId);
        
        UpdateAggregateProgress();
        UpdateMenuButtonVisibility();
    }
    
    private void UpdateAggregateProgress()
    {
        if (_activeProgress.Count == 0)
        {
            _agregatedProgress = _downloadRows.Count > 0 ? 1.0 : 0.0;
        }
        else
        {
            // Averages out the progress of all active downloads
            _agregatedProgress = _activeProgress.Values.Sum() / _activeProgress.Count;
        }

        // CRITICAL: Tells GTK to clear the drawing area and fire DrawFunc again
        _headerProgressRing.QueueDraw();
    }
}