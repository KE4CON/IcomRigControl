using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AdifAudioFieldTests
{
    [Fact]
    public void FormatQso_IncludesAudioFile_AsAnAppField()
    {
        var qso = new QsoRecord("KE4CON", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59",
            AudioFile: @"C:\audio\QSO_KE4CON.wav");

        string adif = AdifWriter.FormatQso(qso);

        // ADIF app field: <APP_ICOMRIGCONTROL_AUDIOFILE:len>value
        Assert.Contains("<APP_ICOMRIGCONTROL_AUDIOFILE:", adif);
        Assert.Contains(@"QSO_KE4CON.wav", adif);
    }

    [Fact]
    public void FormatQso_OmitsAudioFile_WhenNone()
    {
        var qso = new QsoRecord("KE4CON", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59");
        Assert.DoesNotContain("APP_ICOMRIGCONTROL_AUDIOFILE", AdifWriter.FormatQso(qso));
    }
}
