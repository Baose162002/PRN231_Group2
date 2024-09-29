using BusinessObject;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Service.IService;
using Service.Service;
using System.Diagnostics.Contracts;
using System.Reflection.Emit;



var builder = WebApplication.CreateBuilder(args);
// Config OData

var modelBuilder = new ODataConventionModelBuilder();
modelBuilder.EntitySet<User>("User");
modelBuilder.EntitySet<Flower>("Flower");
modelBuilder.EntitySet<Batch>("Batch");
modelBuilder.EntitySet<Company>("Company");
modelBuilder.EntitySet<Review>("Review");
modelBuilder.EntitySet<Delivery>("Delivery");
modelBuilder.EntitySet<Payment>("Payment");
modelBuilder.EntitySet<Order>("Order");
modelBuilder.EntitySet<OrderDetail>("OrderDetail");

// Add services to the container.

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

// Configure DbContext
builder.Services.AddDbContext<FlowerShopContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Dependency Injection
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddControllers().AddOData(
    options => options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(null).AddRouteComponents(
        "odata",
        modelBuilder.GetEdmModel()));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseODataBatching();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
