using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class PushNotifierTests
{
    [Theory]
    [InlineData("KE4CON-alerts", "KE4CON-alerts")]
    [InlineData("my topic!!", "mytopic")]           // spaces + punctuation stripped
    [InlineData("a/b?c", "abc")]
    [InlineData("", "")]
    public void SanitizeTopic_KeepsOnlySafeCharacters(string input, string expected) =>
        Assert.Equal(expected, PushNotifier.SanitizeTopic(input));
}
