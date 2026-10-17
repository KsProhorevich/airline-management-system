using AirlineClient.Services;

namespace AirlineApi.Tests;

public sealed class BookingFormRulesTests
{
    [Fact]
    public void Passenger_can_book_without_selecting_another_passenger()
    {
        var errors = BookingFormRules.Validate(2, null, dispatcher: false);
        Assert.Empty(errors);
    }

    [Fact]
    public void Dispatcher_must_choose_a_passenger_and_a_valid_seat_count()
    {
        var errors = BookingFormRules.Validate(0, Guid.Empty, dispatcher: true);
        Assert.Contains(errors, item => item.Contains("1 до 9", StringComparison.Ordinal));
        Assert.Contains(errors, item => item.Contains("пассажира", StringComparison.Ordinal));
    }
}
