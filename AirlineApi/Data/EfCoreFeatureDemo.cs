using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Data;

public static class EfCoreFeatureDemo
{
    public static async Task<IReadOnlyList<string>> RunAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var aircraft = new Aircraft
        {
            Id = Guid.NewGuid(),
            Registration = "ZZ" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Model = "Демонстрация",
            SeatCapacity = 10
        };
        db.Aircraft.Add(aircraft);
        notes.Add(db.Entry(aircraft).State.ToString());
        await db.SaveChangesAsync(cancellationToken);
        notes.Add(db.Entry(aircraft).State.ToString());

        aircraft.Model = "Изменено";
        notes.Add(db.Entry(aircraft).State.ToString());
        await db.SaveChangesAsync(cancellationToken);

        var found = await db.Aircraft.FindAsync([aircraft.Id], cancellationToken);
        notes.Add(found is null ? "Missing" : "Found");

        await db.Aircraft.Where(item => item.Id == aircraft.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Model, "ExecuteUpdate"), cancellationToken);
        db.ChangeTracker.Clear();
        var afterUpdate = await db.Aircraft.AsNoTracking().SingleAsync(item => item.Id == aircraft.Id, cancellationToken);
        notes.Add(afterUpdate.Model);

        await db.Aircraft.Where(item => item.Id == aircraft.Id).ExecuteDeleteAsync(cancellationToken);
        var remains = await db.Aircraft.AsNoTracking().AnyAsync(item => item.Id == aircraft.Id, cancellationToken);
        notes.Add(remains ? "StillPresent" : "Deleted");
        return notes;
    }
}
