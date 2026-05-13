using System.Windows.Forms;

namespace Flicksy.Agent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var appContext = new AgentApplicationContext();
        Application.Run(appContext);
    }
}
