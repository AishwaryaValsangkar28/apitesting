using DotNetEnv;
using System;

try
{
    // Load environment variables from .env file
    DotNetEnv.Env.Load();
    Console.WriteLine("Environment variables loaded");

    // Fetch environment variables for the database connection
    var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? throw new InvalidOperationException("DB_SERVER not set");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? throw new InvalidOperationException("DB_NAME not set");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? throw new InvalidOperationException("DB_USER not set");
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD not set");

    Console.WriteLine($"DB_SERVER: {Environment.GetEnvironmentVariable("DB_SERVER")}");
    Console.WriteLine($"DB_NAME: {Environment.GetEnvironmentVariable("DB_NAME")}");
    Console.WriteLine($"DB_USER: {Environment.GetEnvironmentVariable("DB_USER")}");
    Console.WriteLine($"DB_PASSWORD: {Environment.GetEnvironmentVariable("DB_PASSWORD")}");

    // Construct the connection string
    var connectionString = $"Server={dbServer};Database={dbName};User Id={dbUser};Password={dbPassword};";

    // Create the WebApplicationBuilder
    var builder = WebApplication.CreateBuilder(args);

    // Override the connection string in the configuration
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS configuration
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.UseCors("AllowAll");
    app.MapControllers();
    app.Run();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    throw;
}












//using DotNetEnv;
//using System;


////load environemnt variables from .env file
//DotNetEnv.Env.Load();

//var builder = WebApplication.CreateBuilder(args);

////// Replace placeholders in the connection string with environment variables
////builder.Configuration["ConnectionStrings:DefaultConnection"] =
////    builder.Configuration["ConnectionStrings:DefaultConnection"]
////        .Replace("{DB_SERVER}", Environment.GetEnvironmentVariable("DB_SERVER"))
////        .Replace("{DB_DATABASE}", Environment.GetEnvironmentVariable("DB_DATABASE"))
////        .Replace("{DB_USERNAME}", Environment.GetEnvironmentVariable("DB_USERNAME"))
////        .Replace("{DB_PASSWORD}", Environment.GetEnvironmentVariable("DB_PASSWORD"));

//// Add services to the container.
//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//// CORS configuration
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll",
//        builder =>
//        {
//            builder
//                .AllowAnyOrigin()
//                .AllowAnyMethod()
//                .AllowAnyHeader();
//        });
//});

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//// CORS middleware
//app.UseCors("AllowAll");

//app.MapControllers();

//app.Run();
