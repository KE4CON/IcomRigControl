using System.IO.Ports;

namespace IcomRigControl.Services;

/// <summary>
/// Lists the serial ports the operating system currently knows about, so the
/// Settings window can offer a pick-list instead of making the user type a port
/// name. This is also how a paired Bluetooth radio (e.g. the IC-705 over Bluetooth)
/// is selected: once paired, its outgoing Bluetooth serial port shows up here like
/// any USB port, and the existing serial CI-V path drives it — no Bluetooth-specific
/// code needed. See CLAUDE.md IC-705 Bluetooth.
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
