using LoveInLinesAPI.DTOs;
using LoveInLinesAPI.Entities;
using LoveInLinesAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Supabase;

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

app.UseCors("corsPolicy");

app.Run();
