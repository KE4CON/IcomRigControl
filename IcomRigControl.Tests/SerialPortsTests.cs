using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SerialPortsTests
{
    [Fact]
    public void List_ReturnsWithoutThrowing_AndIsSorted()
    {
        // Enumeration must never throw (it runs at Settings-window construction).
        var ports = SerialPorts.List();
        Assert.NotNull(ports);

        // Whatever ports exist on the CI machine, the result is sorted (stable UI).
        var sorted = new System.Collections.Generic.List<string>(ports);
        sorted.Sort(System.StringComparer.OrdinalIgnoreCase);
        Assert.Equal(sorted, ports);
    }
}
