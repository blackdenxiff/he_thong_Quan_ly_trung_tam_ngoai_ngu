using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QLTrungTamTiengAnh_Web.Controllers;
using QuanLyTrungTamNgoaiNgu.Data;
using Xunit;

namespace QLTrungTamTiengAnh_Web.Tests;

public sealed class CoSoAccessTests
{
    // These requests run through MVC authorization and antiforgery filters. A test
    // authentication handler supplies identities; application cookie login is separate.
    [Fact]
    public async Task Anonymous_data_request_is_challenged()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/CoSo/DuLieu");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GiaoVien")]
    [InlineData("HocVien")]
    public async Task Other_roles_cannot_read_administrative_data(string role)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/CoSo/DuLieu");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("QuanLy")]
    public async Task Administrative_roles_can_read_persisted_data(string role)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/CoSo/DuLieu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("LuuCoSo", "{\"maCoSo\":\"CS-NEW\",\"tenCoSo\":\"New\",\"diaChi\":\"Address\"}")]
    [InlineData("DoiTrangThaiCoSo", "{\"coSoId\":2,\"trangThai\":false}")]
    [InlineData("LuuPhongHoc", "{\"coSoId\":1,\"tenPhong\":\"P101\",\"sucChua\":30}")]
    [InlineData("XoaPhongHoc", "{\"phongHocId\":1}")]
    [InlineData("DieuChuyenNhanSu", "{\"nhanSuId\":1,\"coSoHienTaiId\":1,\"coSoMoiId\":2,\"lyDo\":\"Transfer\"}")]
    public async Task Authenticated_mutation_without_antiforgery_token_is_rejected(string action, string body)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var request = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/CoSo/" + action, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = database.CreateContext();
        Assert.Equal(3, context.CoSos.Count());
        Assert.Empty(context.PhongHocs);
        Assert.Empty(context.LichSuDieuChuyens);
        Assert.Equal(CoSoDatabase.SourceId, context.NhanSus.Single().CoSoID);
    }

    [Fact]
    public async Task Valid_antiforgery_token_allows_administrator_to_save_branch()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        await AddAntiforgeryTokenAsync(client);
        using var body = new StringContent(
            "{\"maCoSo\":\"CS-NEW\",\"tenCoSo\":\"Cơ sở mới\",\"diaChi\":\"Địa chỉ mới\"}",
            Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/CoSo/LuuCoSo", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("success").GetBoolean());
        await using var context = database.CreateContext();
        Assert.Contains(context.CoSos, x => x.MaCoSo == "CS-NEW");
    }

    [Fact]
    public async Task Omitting_branch_status_does_not_implicitly_suspend_branch()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        using var server = CreateServer(database);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        await AddAntiforgeryTokenAsync(client);
        using var body = new StringContent("{\"coSoId\":2}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/CoSo/DoiTrangThaiCoSo", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = database.CreateContext();
        Assert.True((await context.CoSos.FindAsync(CoSoDatabase.DestinationId))!.TrangThai);
    }

    private static async Task AddAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/test-token");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Add("RequestVerificationToken",
            token.RootElement.GetProperty("requestToken").GetString());
    }

    private static IHost CreateServer(CoSoDatabase database) => new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddScoped<ApplicationDbContext>(_ => database.CreateContext());
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddAuthorization();
            services.AddControllersWithViews().AddApplicationPart(typeof(CoSoController).Assembly);
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller}/{action}/{id?}");
                endpoints.MapGet("/test-token", async context =>
                {
                    var tokens = context.RequestServices.GetRequiredService<IAntiforgery>()
                        .GetAndStoreTokens(context);
                    await context.Response.WriteAsJsonAsync(new { tokens.RequestToken });
                });
            });
        })).Start();

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, CoSoDatabase.ApproverId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            ], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
