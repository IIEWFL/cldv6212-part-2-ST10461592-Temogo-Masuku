using ABC_Retail.Services;
using ABC_Retail.Services.Storage;

namespace ABC_Retail
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Retrieve the connection string from configuration
            var storageConnectionString = builder.Configuration.GetConnectionString("storageConnectionString")
                ?? throw new InvalidOperationException("Connection string 'storageConnectionString' not found.");

            // Register storage services
            builder.Services.AddSingleton(new BlobStorageService(storageConnectionString));
            builder.Services.AddSingleton(new QueueStorageService(storageConnectionString, "audit-queue"));
            builder.Services.AddSingleton(new FileShareStorageService(storageConnectionString, "retail-fileshare"));

            // Register the unified table storage service (handles all entity types)
            builder.Services.AddSingleton<TableStorageService>(provider =>
                new TableStorageService(storageConnectionString));

            // Register business services
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IAuditLogService, AuditLogService>();

            //Register the Function Service                               //this is new
            builder.Services.AddHttpClient<FunctionService>();
            builder.Services.AddSingleton<FunctionService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
