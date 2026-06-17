using TranslatorApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Translator API",
        Version = "v1",
        Description = "Motor de tradução HTML com suporte a Rich Text via LibreTranslate."
    });
});

builder.Services.AddHttpClient<TranslationService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["LibreTranslate:Url"] ?? "http://localhost:5050");
    client.Timeout = TimeSpan.FromSeconds(200);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseStaticFiles();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Translator API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.Run();