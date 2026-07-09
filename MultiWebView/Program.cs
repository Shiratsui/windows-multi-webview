namespace MultiWebView;

static class Program
{
    private const string SingleInstanceMutexName = "MultiWebView.SingleInstance";
    private const string ActivateEventName = "MultiWebView.Activate";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        using var cancellation = new CancellationTokenSource();

        var picker = new ProfilePickerForm();
        picker.HandleCreated += (_, _) => StartActivationListener(picker, activateEvent, cancellation.Token);
        picker.FormClosed += (_, _) => cancellation.Cancel();

        Application.Run(picker);
        mutex.ReleaseMutex();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private static void StartActivationListener(
        ProfilePickerForm picker,
        EventWaitHandle activateEvent,
        CancellationToken cancellationToken)
    {
        ThreadPool.RegisterWaitForSingleObject(
            activateEvent,
            (_, timedOut) =>
            {
                if (timedOut || cancellationToken.IsCancellationRequested || picker.IsDisposed)
                {
                    return;
                }

                picker.BeginInvoke(new Action(picker.ActivateFromExternalLaunch));
            },
            null,
            Timeout.Infinite,
            false);
    }
}
