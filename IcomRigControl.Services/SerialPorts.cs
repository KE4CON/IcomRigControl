using System.IO.Ports;

namespace IcomRigControl.Services;

/// <summary>
/// Lists the serial ports the operating system currently knows about, so the
/// Settings window can offer a pick-list instead of making the user type a port
/// name. Refreshable, since plugging/unplugging a USB radio changes what's available.
/// </summary>
public static class SerialPorts
{
    public static List<string> List()
    {
        try
        {
            var names = SerialPort.GetPortNames();
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return new List<string>(names);
        }
        catch
        {
            // Enumeration can throw on some platforms/permissions — just offer none.
            return new List<string>();
        }
    }
}
