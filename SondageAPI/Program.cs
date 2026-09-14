using SondageAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Ajout des contrôleurs et de votre service métier
builder.Services.AddControllers();
builder.Services.AddSingleton<SondageService>();

builder.Services.AddEndpointsApiExplorer();
// Génération de Swagger (sans la configuration du cadenas qui plante dans .NET 10)
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Pare-feu de sécurité (Middleware) - C'est ici que la vraie protection se passe
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        var expectedKey = builder.Configuration["ApiKey"] ?? "DefaultSecretKey123";
        
        // Vérifie si la requête Postman contient le header X-API-KEY
        if (!context.Request.Headers.TryGetValue("X-API-KEY", out var extractedKey) || extractedKey != expectedKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Acces non autorise. Cle d'API manquante ou invalide.").ConfigureAwait(false);
            return;
        }
    }
    await next(context).ConfigureAwait(false);
});

app.UseAuthorization();
app.MapControllers();
app.Run();