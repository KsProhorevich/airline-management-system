namespace AirlineClient.Services;

public static class BookingFormRules
{
    public static IReadOnlyList<string> Validate(int seatCount, Guid? passengerId, bool dispatcher)
    {
        var errors = new List<string>();
        if (seatCount is < 1 or > 9)
        {
            errors.Add("Число мест должно быть от 1 до 9.");
        }

        if (dispatcher && (passengerId is null || passengerId == Guid.Empty))
        {
            errors.Add("Диспетчер должен выбрать пассажира.");
        }

        return errors;
    }
}
