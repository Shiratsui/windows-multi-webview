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

    public static Task<CoreWebView2Environment> CreateAsync(string userDataFolder, bool useHighGpuArguments)
    {
        var options = useHighGpuArguments
            ? new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = HighGpuBrowserArguments
            }
            : null;

        return CoreWebView2Environment.CreateAsync(
            userDataFolder: userDataFolder,
            options: options);
    }
}
