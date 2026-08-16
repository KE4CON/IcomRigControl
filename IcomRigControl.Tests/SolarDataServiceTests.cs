using System.Linq;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SolarDataServiceTests
{
    private const string SampleXml = """
    <solar>
      <solardata>
        <updated>15 Aug 2026 1200 GMT</updated>
        <solarflux>134</solarflux>
        <aindex>5</aindex>
        <kindex>2</kindex>
        <sunspots>76</sunspots>
        <calculatedconditions>
          <band name="80m-40m" time="day">Fair</band>
          <band name="80m-40m" time="night">Good</band>
          <band name="20m-17m" time="day">Good</band>
          <band name="10m-6m" time="day">Poor</band>
        </calculatedconditions>
      </solardata>
    </solar>
    """;

    [Fact]
    public void Parse_ReadsIndicesAndBandConditions()
    {
        SolarData d = SolarDataService.Parse(SampleXml);

        Assert.Equal("134", d.SolarFlux);
        Assert.Equal("5", d.AIndex);
        Assert.Equal("2", d.KIndex);
        Assert.Equal("76", d.Sunspots);
        Assert.Equal(4, d.Bands.Count);

        var twenty = d.Bands.First(b => b.Band == "20m-17m" && b.TimeOfDay == "day");
        Assert.Equal("Good", twenty.Condition);
    }

    [Fact]
    public void Parse_MissingFields_DoNotThrow()
    {
        SolarData d = SolarDataService.Parse("<solar><solardata></solardata></solar>");
        Assert.Equal("", d.SolarFlux);
        Assert.Empty(d.Bands);
    }
}
