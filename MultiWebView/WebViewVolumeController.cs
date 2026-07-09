using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;

namespace MultiWebView;

public static class WebViewVolumeController
{
    private const int ClsctxAll = 23;
    private const int ReapplyIntervalMs = 1000;

    public static async Task ConfigureAsync(WebView2 webView, Func<int> getVolumePercent, Func<bool> getMuted)
    {
        if (webView.CoreWebView2 is null || webView.IsDisposed)
        {
            return;
        }

        await ApplyAsync(webView, getVolumePercent(), getMuted());

        var isApplying = false;
        var timer = new System.Windows.Forms.Timer
        {
            Interval = ReapplyIntervalMs
        };

        timer.Tick += async (_, _) =>
        {
            if (isApplying || webView.IsDisposed || webView.CoreWebView2 is null)
            {
                return;
            }

            isApplying = true;
            try
            {
                await ApplyAsync(webView, getVolumePercent(), getMuted());
            }
            finally
            {
                isApplying = false;
            }
        };

        webView.Disposed += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
        };

        timer.Start();
    }

    public static Task ApplyAsync(WebView2 webView, int volumePercent, bool muted)
    {
        if (webView.CoreWebView2 is null || webView.IsDisposed)
        {
            return Task.CompletedTask;
        }

        var volume = Math.Clamp(volumePercent, 0, 100) / 100f;
        webView.CoreWebView2.IsMuted = muted;
        var browserProcessId = webView.CoreWebView2.BrowserProcessId;
        ApplyToAudioSessions(browserProcessId, volume, muted);
        return Task.CompletedTask;
    }

    private static void ApplyToAudioSessions(uint browserProcessId, float volume, bool muted)
    {
        try
        {
            var targetProcessIds = GetProcessTreeIds((int)browserProcessId);
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);

            var sessionManagerId = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref sessionManagerId, ClsctxAll, IntPtr.Zero, out var sessionManagerObject);
            var sessionManager = (IAudioSessionManager2)sessionManagerObject;
            sessionManager.GetSessionEnumerator(out var sessionEnumerator);
            sessionEnumerator.GetCount(out var sessionCount);

            for (var index = 0; index < sessionCount; index++)
            {
                sessionEnumerator.GetSession(index, out var sessionControl);
                var sessionControl2 = (IAudioSessionControl2)sessionControl;
                sessionControl2.GetProcessId(out var processId);

                if (!targetProcessIds.Contains((int)processId))
                {
                    continue;
                }

                var simpleAudioVolume = (ISimpleAudioVolume)sessionControl;
                simpleAudioVolume.SetMasterVolume(volume, Guid.Empty);
                simpleAudioVolume.SetMute(muted, Guid.Empty);
            }
        }
        catch (COMException)
        {
        }
    }

    private static HashSet<int> GetProcessTreeIds(int rootProcessId)
    {
        var processParents = new Dictionary<int, int?>();

        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                processParents[process.Id] = GetParentProcessId(process);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        var processIds = new HashSet<int> { rootProcessId };
        var added = true;

        while (added)
        {
            added = false;

            foreach (var (processId, parentProcessId) in processParents)
            {
                if (parentProcessId is not null && processIds.Contains(parentProcessId.Value))
                {
                    added |= processIds.Add(processId);
                }
            }
        }

        return processIds;
    }

    private static int? GetParentProcessId(System.Diagnostics.Process process)
    {
        var status = NtQueryInformationProcess(
            process.Handle,
            0,
            out var processBasicInformation,
            Marshal.SizeOf<ProcessBasicInformation>(),
            out _);

        return status == 0
            ? processBasicInformation.InheritedFromUniqueProcessId.ToInt32()
            : null;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator;

    private enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void NotImpl1();
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void NotImpl1();
        void NotImpl2();
        void GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int sessionCount);
        void GetSession(int sessionCount, out IAudioSessionControl session);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        void NotImpl1();
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        void NotImpl1();
        void NotImpl2();
        void NotImpl3();
        void NotImpl4();
        void NotImpl5();
        void NotImpl6();
        void NotImpl7();
        void NotImpl8();
        void NotImpl9();
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
        void GetProcessId(out uint retVal);
        void IsSystemSoundsSession();
        void SetDuckingPreference(bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, Guid eventContext);
        void GetMasterVolume(out float level);
        void SetMute(bool isMuted, Guid eventContext);
        void GetMute(out bool isMuted);
    }
}
