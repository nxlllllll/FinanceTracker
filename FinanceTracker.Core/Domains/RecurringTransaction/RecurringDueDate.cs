using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

public static class RecurringDueDate
{
	public static DateTimeOffset Next(
		int dayOfMonth,
		TimeZoneId timeZone,
		DateTimeOffset after)
	{
		TimeZoneInfo zone = timeZone.ToTimeZoneInfo();
		DateTime localAfter = TimeZoneInfo.ConvertTime(dateTimeOffset: after, destinationTimeZone: zone).DateTime;
		DateTimeOffset thisMonth = DueInstant(year: localAfter.Year, month: localAfter.Month, dayOfMonth: dayOfMonth, zone: zone);

		if (thisMonth > after)
			return thisMonth;

		DateTime nextMonth = new DateTime(year: localAfter.Year, month: localAfter.Month, day: 1).AddMonths(months: 1);

		return DueInstant(
			year: nextMonth.Year,
			month: nextMonth.Month,
			dayOfMonth: dayOfMonth,
			zone: zone
		);
	}

	private static DateTimeOffset DueInstant(
		int year,
		int month,
		int dayOfMonth,
		TimeZoneInfo zone)
	{
		int day = Math.Min(val1: dayOfMonth, val2: DateTime.DaysInMonth(year: year, month: month));

		DateTime localMidnight = new DateTime(
			year: year,
			month: month,
			day: day,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Unspecified
		);

		return ToUtc(local: localMidnight, zone: zone);
	}

	private static DateTimeOffset ToUtc(
		DateTime local,
		TimeZoneInfo zone)
	{
		if (zone.IsInvalidTime(dateTime: local))
			return FirstValidInstantAfterGap(local: local, zone: zone);

		if (!zone.IsAmbiguousTime(dateTime: local))
			return new DateTimeOffset(dateTime: local, offset: zone.GetUtcOffset(dateTime: local)).ToUniversalTime();

		TimeSpan earliest = zone.GetAmbiguousTimeOffsets(dateTime: local).Max();
		return new DateTimeOffset(dateTime: local, offset: earliest).ToUniversalTime();
	}

	private static DateTimeOffset FirstValidInstantAfterGap(
		DateTime local,
		TimeZoneInfo zone)
	{
		DateTime candidate = local;
		DateTime limit = local.AddHours(value: 4);

		while (zone.IsInvalidTime(dateTime: candidate) && candidate < limit)
			candidate = candidate.AddMinutes(value: 15);

		return new DateTimeOffset(dateTime: candidate, offset: zone.GetUtcOffset(dateTime: candidate)).ToUniversalTime();
	}
}
