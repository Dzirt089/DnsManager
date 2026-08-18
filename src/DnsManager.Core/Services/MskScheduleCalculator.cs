namespace DnsManager.Core.Services;

/// <summary>
/// Расчёт ближайшего будущего срабатывания ежедневного расписания.
/// Время хранится как «стенное» время МСК; момент срабатывания конвертируется в UTC.
/// </summary>
public static class MskScheduleCalculator
{
	private static readonly TimeZoneInfo MskTimeZone = FindMskTimeZone();

	/// <summary>Часовой пояс Москвы (Windows-идентификатор, с fallback на IANA).</summary>
	public static TimeZoneInfo MskTimeZoneInfo => MskTimeZone;

	/// <summary>Возвращает ближайший будущий момент UTC для указанного времени МСК. Если время уже прошло — следующий день.</summary>
	public static DateTimeOffset NextUtc(TimeSpan timeMsk, DateTime utcNow)
		=> NextUtc(timeMsk, utcNow, MskTimeZone);

	/// <summary>Перегрузка с явным часовым поясом для тестов.</summary>
	public static DateTimeOffset NextUtc(TimeSpan timeMsk, DateTime utcNow, TimeZoneInfo mskTimeZone)
	{
		ArgumentNullException.ThrowIfNull(mskTimeZone);
		if (timeMsk < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(timeMsk), "Время не может быть отрицательным.");
		if (timeMsk >= TimeSpan.FromDays(1))
			throw new ArgumentOutOfRangeException(nameof(timeMsk), "Время должно быть меньше 24 часов.");

		var nowMsk = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), mskTimeZone);

		var todayTriggerUtc = TimeZoneInfo.ConvertTimeToUtc(nowMsk.Date + timeMsk, mskTimeZone);
		if (todayTriggerUtc > utcNow)
			return new DateTimeOffset(DateTime.SpecifyKind(todayTriggerUtc, DateTimeKind.Utc));

		var tomorrowTriggerUtc = TimeZoneInfo.ConvertTimeToUtc(nowMsk.Date.AddDays(1) + timeMsk, mskTimeZone);
		return new DateTimeOffset(DateTime.SpecifyKind(tomorrowTriggerUtc, DateTimeKind.Utc));
	}

	private static TimeZoneInfo FindMskTimeZone()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
		}
	}
}
