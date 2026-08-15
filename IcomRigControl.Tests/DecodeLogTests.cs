using System.IO;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class DecodeLogTests
{
    [Fact]
    public void Save_WritesTheText_AndReturnsAnExistingTxtPath()
    {
        string path = DecodeLog.Save("RTTY", "CQ CQ DE KE4CON");
        try
        {
            Assert.True(File.Exists(path), "the saved file should exist");
            Assert.EndsWith(".txt", path);
            Assert.Contains("KE4CON", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
