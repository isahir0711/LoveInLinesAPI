using LoveInLinesAPI.DTOs;
using LoveInLinesAPI.Entities;
using LoveInLinesAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Supabase;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Web;
using static Supabase.Gotrue.Constants;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<ISupaFileService, SupaFileService>();

builder.Services.AddScoped<Supabase.Client>(_ =>
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
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
    [FromServices] Supabase.Client supaClient, HttpContext context) =>
{
    try
    {
        if (File == null) return Results.BadRequest("There was an error with your drawing");

        var imageuri = await supaFileService.UploadFile(File);

        string token = await context.GetTokenAsync("access_token");
        var user = await supaClient.Auth.GetUser(token);

        if (user == null) { return Results.BadRequest("There's an issue with your account"); }

        var drawing = new Drawing
        {
            ImageURL = imageuri,
            TotalLikes = 0,
            UserId = user.Id,
            UserProfilePic = user.UserMetadata["avatar_url"].ToString()
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


}).DisableAntiforgery().RequireAuthorization();



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
                Id = drawing.Id,
                DrawingURL = drawing.ImageURL,
                TotalLikes = drawing.TotalLikes,
                UserProfilePic = drawing.UserProfilePic
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

app.MapGet("api/drawings/{id}", async (int id,[FromServices] Supabase.Client supaClient) =>
{
    try
    {
        var response = await supaClient.From<Drawing>().Where(d=> d.Id == id).Get();

        var drawing = response.Models.FirstOrDefault();

        if (drawing is null)
        {
            return Results.NotFound();
        }

        var drawingResponse = new DrawingResponse
        {
            Id = drawing.Id,
            DrawingURL = drawing.ImageURL,
            TotalLikes = drawing.TotalLikes,
            UserProfilePic = drawing.UserProfilePic
        };

        return Results.Ok(drawingResponse);
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


app.MapGet("api/GetSession", async (Supabase.Client supaclient,HttpContext context) =>
{
    //the context has a auth parameter, use this to validate the required auth endpoints (ex: upload, like drawing, etc)
    Console.WriteLine(context);
    string token = await context.GetTokenAsync("access_token");
    var user = await supaclient.Auth.GetUser(token);
    var metadata = user.UserMetadata;
    var image = metadata["avatar_url"].ToString();
    return Results.Ok(user);
}).RequireAuthorization();




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


app.UseAuthorization();

app.UseCors("corsPolicy");

app.Run();
