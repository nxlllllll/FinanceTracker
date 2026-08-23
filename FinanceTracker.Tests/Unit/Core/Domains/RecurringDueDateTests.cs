using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class RecurringDueDateTests
{
	private static TimeZoneId Zone(string id)
		=> TimeZoneId.Create(value: id).Value;

	private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0)
		=> new DateTimeOffset(year: year, month: month, day: day, hour: hour, minute: minute, second: 0, offset: TimeSpan.Zero);

	[Test]
	public async Task Next_WhenTheMonthIsTooShort_ShouldClampToItsLastDay()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 31, timeZone: Zone(id: "Etc/UTC"), after: Utc(year: 2026, month: 2, day: 1));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 2, day: 28)).Because(message: """
			2026 is not a leap year, so "the 31st" has to mean the 28th. Rolling into March instead would
			skip the month entirely and charge the user eleven times a year.
		""");
	}

	[Test]
	public async Task Next_WhenTheShortMonthIsInALeapYear_ShouldClampToTheTwentyNinth()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 31, timeZone: Zone(id: "Etc/UTC"), after: Utc(year: 2028, month: 2, day: 1));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2028, month: 2, day: 29)).Because(message: """
			The clamp is per month, not a fixed 28: a calculation that hardcoded February's length would
			fire a day early every fourth year.
		""");
	}

	[Test]
	public async Task Next_WhenTheMonthHasThirtyDays_ShouldClampToTheThirtieth()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 31, timeZone: Zone(id: "Etc/UTC"), after: Utc(year: 2026, month: 4, day: 1));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 4, day: 30));
	}

	[Test]
	public async Task Next_WhenTheBoundIsExactlyTheDueInstant_ShouldReturnTheFollowingMonth()
	{
		DateTimeOffset exactly = Utc(year: 2026, month: 3, day: 15);

		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 15, timeZone: Zone(id: "Etc/UTC"), after: exactly);

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 4, day: 15)).Because(message: """
			The bound is exclusive, and this is the case that decides it. After an execution the next due
			instant is computed from the instant that just fired; an inclusive bound would return the same
			moment and charge the user again on the next run.
		""");
	}

	[Test]
	public async Task Next_ForAZoneBehindUtc_ShouldFallOnTheUsersMidnightNotUtcMidnight()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 1, timeZone: Zone(id: "Pacific/Honolulu"), after: Utc(year: 2026, month: 8, day: 15));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 9, day: 1, hour: 10)).Because(message: """
			Honolulu is UTC-10 year round, so their 1 September begins at 10:00 UTC. Firing at 00:00 UTC
			would put the operation in their 31 August — the behaviour the characterization tests recorded
			before this calculation existed.
		""");
	}

	[Test]
	public async Task Next_ForAZoneAheadOfUtc_ShouldFireWhileUtcIsStillInThePreviousMonth()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 1, timeZone: Zone(id: "Pacific/Auckland"), after: Utc(year: 2026, month: 8, day: 15));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 8, day: 31, hour: 12)).Because(message: """
			New Zealand is UTC+12 in August — daylight saving does not start until late September — so
			their 1 September begins at 12:00 UTC on 31 August. The due instant legitimately sits in the
			previous UTC month, which is exactly what the old day-of-month comparison could not express.
		""");
	}

	[Test]
	public async Task Next_WhenLocalMidnightDoesNotExist_ShouldLandOnTheIntendedLocalDate()
	{
		TimeZoneId zoneId = Zone(id: "America/Santiago");
		TimeZoneInfo zone = zoneId.ToTimeZoneInfo();

		DateTime midnight = new DateTime(year: 2026, month: 9, day: 6, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Unspecified);

		await Assert.That(value: zone.IsInvalidTime(dateTime: midnight)).IsTrue().Because(message: """
			Precondition, not a result. Chile moves its clocks forward at midnight, which is what makes
			this date interesting. If this fails the transition rules have moved and the test needs a new
			date — it does not mean the calculation is wrong, and a test that quietly stopped exercising
			the gap would be worse than one that says so.
		""");

		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 6, timeZone: zoneId, after: Utc(year: 2026, month: 9, day: 1));

		DateTime local = TimeZoneInfo.ConvertTime(dateTimeOffset: due, destinationTimeZone: zone).DateTime;

		await Assert.That(value: local.Date).IsEqualTo(expected: midnight.Date).Because(message: """
			ConvertTimeToUtc throws on a wall clock time that never happens, so without handling this the
			operation would fail once a year and land in unresolvable_events. The result has to stay on the
			intended date rather than slip to the next one.
		""");
	}

	[Test]
	public async Task Next_WhenLocalMidnightHappensTwice_ShouldTakeTheFirstOne()
	{
		TimeZoneId zoneId = Zone(id: "America/Havana");
		TimeZoneInfo zone = zoneId.ToTimeZoneInfo();

		DateTime midnight = new DateTime(year: 2026, month: 11, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Unspecified);

		await Assert.That(value: zone.IsAmbiguousTime(dateTime: midnight)).IsTrue().Because(message: """
			Precondition, not a result. Cuba ends daylight saving at 01:00, so midnight occurs twice that
			night. If this fails the rules have moved and the test needs a new date.
		""");

		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 1, timeZone: zoneId, after: Utc(year: 2026, month: 10, day: 15));

		TimeSpan earliestOffset = zone.GetAmbiguousTimeOffsets(dateTime: midnight).Max();
		DateTimeOffset firstOccurrence = new DateTimeOffset(dateTime: midnight, offset: earliestOffset).ToUniversalTime();

		await Assert.That(value: due).IsEqualTo(expected: firstOccurrence).Because(message: """
			Both instants are "midnight on the 1st". The first is the one a person means; the second is an
			hour of that night they have already lived through, and choosing it would delay the operation
			by an hour once a year for no reason anyone could explain.
		""");
	}

	[Test]
	public async Task Next_ForAZoneWithoutDaylightSaving_ShouldBeAPlainOffset()
	{
		DateTimeOffset due = RecurringDueDate.Next(dayOfMonth: 10, timeZone: Zone(id: "Europe/Moscow"), after: Utc(year: 2026, month: 6, day: 1));

		await Assert.That(value: due).IsEqualTo(expected: Utc(year: 2026, month: 6, day: 9, hour: 21)).Because(message: """
			Moscow has been UTC+3 without daylight saving since 2014, so their 10 June begins at 21:00 UTC
			on the 9th. Included as the ordinary case: most users live in a zone where nothing interesting
			happens, and that path has to stay simple.
		""");
	}
}
