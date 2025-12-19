global using Assignment;
global using Assignment.Models;
using Rotativa.AspNetCore;
using QuestPDF.Infrastructure;   // <-- for license
using QuestPDF.Fluent;          // optional, good to have

var builder = WebApplication.CreateBuilder(args);

// Set QuestPDF license (required for v2023+)
QuestPDF.Settings.License = LicenseType.Community;

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

builder.Services.AddScoped<Helper>();
builder.Services.AddHostedService<Assignment.Services.RentalStatusUpdater>();

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapDefaultControllerRoute();

app.Run();
