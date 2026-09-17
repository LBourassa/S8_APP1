using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using SondageAPI.Repositories;
using SondageAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Contrôleurs ---
builder.Services.AddControllers();

// --- Persistance (fichiers JSON / texte) et services métier ---
builder.Services.AddSingleton<ParticipantRepository>();
builder.Services.AddSingleton<ParticipationRepository>();
builder.Services.AddSingleton<ServiceSondage>();
builder.Services.AddScoped<ServiceParticipation>();

// --- Authentification par clé d'API (en-tête X-API-KEY) ---
builder.Services
    .AddAuthentication(ServiceAuthentification.SchemaParDefaut)
    .AddScheme<AuthenticationSchemeOptions, ServiceAuthentification>(
        ServiceAuthentification.SchemaParDefaut, _ => { });
builder.Services.AddAuthorization();

// --- Swagger / OpenAPI (avec le schéma de sécurité X-API-KEY pour Postman) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sondage API",
        Version = "v1",
        Description = "API de sondage sécurisée - Coup de Sonde"
    });

    options.AddSecurityDefinition(ServiceAuthentification.NomEntete, new OpenApiSecurityScheme
    {
        Name = ServiceAuthentification.NomEntete,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Clé d'API client. Exemple : \"X-API-KEY: {cle}\""
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(ServiceAuthentification.NomEntete, document)] = new List<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
