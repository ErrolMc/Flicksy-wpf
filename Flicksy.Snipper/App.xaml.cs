namespace Flicksy.Snipper;

public partial class App : System.Windows.Application
{
    private SnipperSessionController? _sessionController;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        _sessionController = new SnipperSessionController(this);
        _sessionController.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _sessionController?.Dispose();
        base.OnExit(e);
    }
}
