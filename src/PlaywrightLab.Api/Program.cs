using PlaywrightLab.Api.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 啟用 Controller 路由，讓簽核 API 使用標準的 ASP.NET Core Controller 寫法。
builder.Services.AddControllers()
    // 將 enum 序列化成文字，前端看到 Pending/Approved 比看到 0/1 更容易理解。
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// 使用 Singleton 讓整個應用程式生命週期共用同一份記憶體資料。
// 應用程式重新啟動後資料會消失，這是本練習不使用資料庫的預期行為。
builder.Services.AddSingleton<InMemoryApprovalRequestStore>();

var app = builder.Build();

// 應用程式啟動時建立初始簽核資料。
app.Services.GetRequiredService<InMemoryApprovalRequestStore>().Seed();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 將標註了 [ApiController] 的 Controller 加入 HTTP pipeline。
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
