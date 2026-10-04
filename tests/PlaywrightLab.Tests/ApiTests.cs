using Microsoft.Playwright;

namespace PlaywrightLab.Tests
{
    public class ApiTests
    {
        [Fact]
        public async Task GetWeatherForecast_ShouldReturn200()
        {
            using var playwright = await Playwright.CreateAsync();

            var request = await playwright.APIRequest.NewContextAsync();

            var response = await request.GetAsync(
                "http://localhost:5018/weatherforecast");

            Assert.Equal(200, (int)response.Status);
        }
    }
}