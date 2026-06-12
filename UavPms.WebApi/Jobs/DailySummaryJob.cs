namespace UavPms.WebApi.Jobs;

public class DailySummaryJob {
    public Task Execute () {
        Console.WriteLine("Job Daily started...");

        return Task.CompletedTask;
    }
}