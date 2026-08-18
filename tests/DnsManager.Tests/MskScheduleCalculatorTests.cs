using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class MskScheduleCalculatorTests
{
	private static readonly TimeZoneInfo Msk = TimeZoneInfo.CreateCustomTimeZone(
		"MSK", TimeSpan.FromHours(3), "MSK", "MSK");

	[Fact]
	public void NextUtc_BeforeScheduledTime_ReturnsToday()
	{
		var nowUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 13:00 МСК

		var next = MskScheduleCalculator.NextUtc(new TimeSpan(16, 55, 0), nowUtc, Msk);

		Assert.Equal(new DateTimeOffset(2026, 1, 1, 13, 55, 0, TimeSpan.Zero), next);
	}

	[Fact]
	public void NextUtc_AfterScheduledTime_ReturnsTomorrow()
	{
		var nowUtc = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc); // 18:00 МСК

		var next = MskScheduleCalculator.NextUtc(new TimeSpan(16, 55, 0), nowUtc, Msk);

		Assert.Equal(new DateTimeOffset(2026, 1, 2, 13, 55, 0, TimeSpan.Zero), next);
	}

	[Fact]
	public void NextUtc_ExactlyAtScheduledTime_ReturnsTomorrow()
	{
		var nowUtc = new DateTime(2026, 1, 1, 13, 55, 0, DateTimeKind.Utc); // ровно 16:55 МСК

		var next = MskScheduleCalculator.NextUtc(new TimeSpan(16, 55, 0), nowUtc, Msk);

		Assert.Equal(new DateTimeOffset(2026, 1, 2, 13, 55, 0, TimeSpan.Zero), next);
	}

	[Fact]
	public void NextUtc_InvalidTime_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			MskScheduleCalculator.NextUtc(TimeSpan.FromDays(1), DateTime.UtcNow, Msk));
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			MskScheduleCalculator.NextUtc(TimeSpan.FromHours(-1), DateTime.UtcNow, Msk));
	}
}
