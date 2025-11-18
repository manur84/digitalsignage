using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace DigitalSignage.Tests.Wpf;

/// <summary>
/// Executes WPF-dependent test code on an STA thread so controls can be created safely.
/// </summary>
public static class StaTestHelper
{
    public static void Run(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        ExceptionDispatchInfo? capturedException = null;
        using var completed = new ManualResetEvent(false);

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        completed.WaitOne();
        capturedException?.Throw();
    }
}
