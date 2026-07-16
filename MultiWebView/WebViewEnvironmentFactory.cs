using Microsoft.Web.WebView2.Core;

namespace MultiWebView;

public static class WebViewEnvironmentFactory
{
    private const string HighGpuBrowserArguments =
        "--disable-background-timer-throttling " +
        "--disable-backgrounding-occluded-windows " +
        "--disable-renderer-backgrounding " +
        "--disable-features=CalculateNativeWinOcclusion,IntensiveWakeUpThrottling " +
        "--enable-gpu-rasterization " +
        "--enable-zero-copy " +
        "--ignore-gpu-blocklist " +
        "--autoplay-policy=no-user-gesture-required";

    private const string LiteBrowserArguments =
        "--autoplay-policy=no-user-gesture-required";

    public static Task<CoreWebView2Environment> CreateAsync(string userDataFolder, WebViewPerformanceMode mode)
    {
        var arguments = mode switch
        {
            WebViewPerformanceMode.Gpu => HighGpuBrowserArguments,
            WebViewPerformanceMode.Lite => LiteBrowserArguments,
            _ => null
        };

        var options = string.IsNullOrWhiteSpace(arguments)
            ? null
            : new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = arguments
            };

        return CoreWebView2Environment.CreateAsync(
            userDataFolder: userDataFolder,
            options: options);
    }
}
