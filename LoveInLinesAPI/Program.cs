using LoveInLinesAPI.DTOs;
using LoveInLinesAPI.Entities;
using LoveInLinesAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Web;
using static Supabase.Gotrue.Constants;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<ISupaFileService, SupaFileService>();

builder.Services.AddSingleton<Supabase.Client>(_ =>
    new Supabase.Client(
    Environment.GetEnvironmentVariable("supabaseloveurl"),
    Environment.GetEnvironmentVariable("supabaseapikey"),
        new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true
        }));

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
            .AddJwtBearer(o =>
            {
                o.IncludeErrorDetails = true;
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("drawingsjwtsecret"))
                    ),
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                };
                o.Events = new JwtBearerEvents()
                {
                    //
                };
            });



string frontURL = builder.Configuration["frontURL"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "corsPolicy",
                     policy =>
                     {
                         policy.WithOrigins(frontURL)
                         .AllowAnyMethod()
                         .AllowAnyHeader();
                     });
});

var app = builder.Build();

app.UseAuthentication();

app.MapGet("api/generateCode", () =>
{
    string code = GenerateRoomId();
    var response = new RoomCodeResponse()
    {
        Code = code,
    };
    return response;
});

string GenerateRoomId()
{
    return Guid.NewGuid().ToString();
}

app.MapPost("api/uploadImage", async ([FromForm] IFormFile File,
    [FromServices] ISupaFileService supaFileService,
    [FromServices] Supabase.Client supaClient) =>
{
    try
    {
        if (File == null) return Results.BadRequest();

        var imageuri = await supaFileService.UploadFile(File);

        var drawing = new Drawing
        {
            ImageURL = imageuri,
            TotalLikes = 0
        };

        await supaClient.From<Drawing>().Insert(drawing);

        return Results.Ok(drawing.ImageURL);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.TraceInformation($"{ex.Message}");
        Console.WriteLine(ex.Message.ToString());
        throw;
    }

}).DisableAntiforgery();

app.MapGet("api/getDrawings", async ([FromServices] Supabase.Client supaClient) =>
{
    try
    {
        var response = await supaClient.From<Drawing>().Get();

        var drawings = response.Models;

        var drawing_response = new List<DrawingResponse>();

        foreach (var drawing in drawings)
        {
            var temp = new DrawingResponse
            {
                DrawingURL = drawing.ImageURL,
                TotalLikes = drawing.TotalLikes
            };
            drawing_response.Add(temp);
        }

        return drawing_response;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.TraceInformation($"{ex.Message}");
        Console.WriteLine($"{ex.Message}");
        throw;
    }
}).DisableAntiforgery();

app.MapGet("api/SignInGithub", async (Supabase.Client supaclient) =>
{
    var signInUrl = supaclient.Auth.SignIn(Provider.Github);

    return signInUrl.Result.Uri;
});

app.MapGet("api/SignInGoogle", async (Supabase.Client supaclient) =>
{
    var signInUrl = supaclient.Auth.SignIn(Provider.Google);

    return signInUrl.Result.Uri;
});


app.MapGet("api/GetSession", (Supabase.Client supaclient,HttpContext context) =>
{
    //the context has a auth parameter, use this to validate the required auth endpoints (ex: upload, like drawing, etc)
    Console.WriteLine(context);
    return supaclient.Auth.CurrentSession;
});




app.MapPost("api/CallBackURI", async (CallbackRequest callbackRequest, Supabase.Client supaclient) =>
{
    var session = await supaclient.Auth.GetSessionFromUrl(callbackRequest.Uri);

    await supaclient.Auth.SetSession(session.AccessToken, session.RefreshToken);

    var authRes = new AuthResponse()
    {
        Token = session.AccessToken,
        RefreshToken = session.RefreshToken,
        ExpiresAt = session.ExpiresAt()
    };

    return Results.Ok(authRes);
});


app.UseCors("corsPolicy");

app.Run();
