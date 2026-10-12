using System.Runtime.Versioning;
using System.Web;
using Gtk;
using WebKit;
using Dialog = Adw.Dialog;
using HeaderBar = Adw.HeaderBar;

namespace TreasureChest.Source.Views;

[SupportedOSPlatform("linux")]
public class LoginDialog
{
    // Private Members:
    private readonly Dialog _modalDialog;
    private readonly WebView _webView;
    private readonly Window _parentWindow;
    private readonly TaskCompletionSource<string?> _tcs = new();
    private const string AuthUrl = "https://auth.gog.com/auth" +
                                   "?client_id=46899977096215655" +
                                   "&redirect_uri=https://embed.gog.com/on_login_success?origin=client" +
                                   "&response_type=code" +
                                   "&layout=client2";
    public LoginDialog(Window parentWindow)
    {
        _parentWindow = parentWindow;
        
        _modalDialog = Dialog.New();
        _modalDialog.Title = "Login";
        _modalDialog.ContentWidth = 480;
        _modalDialog.ContentHeight = 640;

        var headerBar = HeaderBar.New();
        var contentBox = Box.New(Orientation.Vertical, 0);
        contentBox.Append(headerBar);

        _webView = WebView.New();
        _webView.Vexpand = true;
        _webView.Hexpand = true;
        contentBox.Append(_webView);
        
        _modalDialog.SetChild(contentBox);
        
        _webView.OnLoadChanged += OnLoadChanged;
        
        _modalDialog.OnClosed += (_, _) => _tcs.TrySetResult(null);
    }

    public async Task<string?> ShowAndAuthenticateAsync()
    {
        _webView.LoadUri(AuthUrl);
        _modalDialog.Present(_parentWindow);
        return await _tcs.Task;
    }

    private void OnLoadChanged(WebView sender, WebView.LoadChangedSignalArgs args)
    {
        if (args.LoadEvent != LoadEvent.Finished) 
            return;
        
        var uriString = _webView.GetUri();

        if (string.IsNullOrEmpty(uriString))
            return;
            
        var uri = new Uri(uriString);

        if (!uri.AbsolutePath.Contains("on_login_success")) 
            return;
        
        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        var code = queryParams["code"];
                
        _tcs.TrySetResult(code);
        _modalDialog.Close();
    }
}