
using BusinessObject.DTOs.AIDtos;
using BusinessObject.DTOs.BankQR;
using BusinessObject.DTOs.CloudinarySetting;
using BusinessObject.DTOs.EmailSetiings;
using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Mappings;
using CloudinaryDotNet;
using Common.Utilities;
using DataAccess;
using Hubs;
using LibraryManagement.Services.Payments.Transactions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OpenApi.Models;
using Repositories.BankAccountRepositories;
using Repositories.CartRepositories;
using Repositories.CategoryRepositories;
using Repositories.ConversationRepositories;
using Repositories.DiscountCodeRepositories;
using Repositories.EmailRepositories;
using Repositories.FavoriteRepositories;
using Repositories.FeedbackRepositories;
using Repositories.Logout;
using Repositories.NotificationRepositories;
using Repositories.OrderRepositories;
using Repositories.ProductRepositories;
using Repositories.ProfileRepositories;
using Repositories.ProviderApplicationRepositories;
using Repositories.ReportRepositories;
using Repositories.RentalViolationRepositories;
using Repositories.RepositoryBase;
using Repositories.TransactionRepositories;
using Repositories.UserRepositories;
using Services.AI;
using Services.Authentication;
using Services.CartServices;
using Services.CloudServices;
using Services.CategoryServices;
using Services.ConversationServices;
using Services.DiscountCodeServices;
using Services.EmailServices;
using Services.FavoriteServices;
using Services.FeedbackServices;
using Services.NotificationServices;
using Services.OrderServices;
using Services.Payments.VNPay;
using Services.ProductServices;
using Services.ProfileServices;
using Services.RentalViolationServices;
using Services.ProviderApplicationServices;
using Services.ProviderBankServices;
using Services.ProviderFinanceServices;
using Services.ReportService;
using Services.RevenueServices;
using Services.Transactions;
using Services.UserServices;
using ShareItAPI.Middlewares;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

namespace ShareItAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            // Configure CORS from Frontend BaseUrl based on environment
            var environment = builder.Environment.EnvironmentName;
            var frontendBaseUrl = builder.Configuration[$"FrontendSettings:{environment}:BaseUrl"] ?? "https://localhost:7045";
            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.WithOrigins(frontendBaseUrl)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                })
                .AddOData(opt => opt
                    .EnableQueryFeatures()
                    .AddRouteComponents("odata", GetEdmModel())
                    .Select()
                    .Filter()
                    .OrderBy()
                    .Expand()
                    .Count()
                    .SetMaxTop(100)
                    .SkipToken()
                );


            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            // Add DbContext with SQL Server
            builder.Services.AddDbContext<ShareItDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                       .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning)),
                ServiceLifetime.Scoped);

            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<ILoggedOutTokenRepository, LoggedOutTokenRepository>();
            builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddHttpClient<GoogleAuthService>();
            builder.Services.AddHttpClient<FacebookAuthService>();

            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddHttpClient();

            builder.Services.AddControllers()
                            .AddJsonOptions(options =>
                            {
                                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                            });

            // Bind thông tin từ appsettings.json vào đối tượng JwtSettings
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

            // Đăng ký JwtService cho IJwtService
            builder.Services.AddScoped<IJwtService, JwtService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"])),
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero, // Không cho phép sai lệch thời gian

                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Lấy token từ query string
                        var accessToken = context.Request.Query["access_token"];

                        // Lấy đường dẫn của request
                        var path = context.HttpContext.Request.Path;

                        // Nếu có token và request đang hướng đến hub của chúng ta
                        if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/reportHub") || path.StartsWithSegments("/chathub")))

                        {
                            // Gán token này để middleware xác thực
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // Thêm Authorization (phân quyền)
            builder.Services.AddAuthorization();

            builder.Services.AddSwaggerGen(c =>
            {
                // ... (các cấu hình khác)

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
            });

            // Bind thông tin từ appsettings.json vào đối tượng CloudSettings
            builder.Services.Configure<CloudSettings>(builder.Configuration.GetSection("CloudSettings"));

            // Đăng ký Cloudinary như một singleton service
            builder.Services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<CloudSettings>>().Value;
                var account = new Account(settings.CloudName, settings.APIKey, settings.APISecret);
                return new Cloudinary(account);
            });

            builder.Services.AddScoped<ITransactionService, TransactionService>();
            builder.Services.AddSingleton<IVnpay, Vnpay>();
            builder.Services.Configure<BankQrConfig>(builder.Configuration.GetSection("BankQrConfig"));

            builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

            builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
            builder.Services.AddScoped<IFeedbackService, FeedbackService>();

            builder.Services.AddScoped<IOrderRepository, OrderRepository>();

            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<IReportRepository, ReportRepository>();

            // Đăng ký Notification
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IOrderService, OrderService>();

            builder.Services.AddAutoMapper(typeof(UserProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(OrderProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(OrderItemProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(ProductProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(CategoryProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(CartMappingProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(RevenueProfile).Assembly);
            builder.Services.AddAutoMapper(typeof(DiscountCodeProfile).Assembly);

            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
            builder.Services.AddScoped<IEmailRepository, EmailRepository>();
            builder.Services.AddScoped<IEmailService, EmailService>();

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationErrorHelper.CreateFormattedValidationErrorResponse;
            });

            builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
            builder.Services.AddScoped<IProviderFinanceService, ProviderFinanceService>();

            builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

            builder.Services.AddScoped<IProviderBankService, ProviderBankService>();
            builder.Services.AddScoped<IProviderApplicationRepository, ProviderApplicationRepository>();
            builder.Services.AddScoped<IProviderApplicationService, ProviderApplicationService>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<UserContextHelper>();

            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();

            // Thêm SignalR service
            builder.Services.AddSignalR();

            // Cấu hình OpenAIOptions bằng cách ánh xạ các giá trị từ section "OpenAI"
            builder.Services.AddHttpClient("OpenAI")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
            builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
            builder.Services.AddScoped<IAiSearchService, AiSearchService>();
            builder.Services.AddMemoryCache();

            // Register CartRepositories
            builder.Services.AddScoped<ICartRepository, CartRepository>();

            // Register CartServices
            builder.Services.AddScoped<ICartService, CartService>();

            builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
            builder.Services.AddScoped<IFavoriteService, FavoriteService>();


            builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
            builder.Services.AddScoped<IConversationService, ConversationService>();

            // Register Revenue Services
            builder.Services.AddScoped<IRevenueService, RevenueService>();

            // Register DiscountCode services
            builder.Services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
            builder.Services.AddScoped<IDiscountCodeService, DiscountCodeService>();
            builder.Services.AddHostedService<DiscountCodeExpirationService>();

            // Register RentalViolation services
            builder.Services.AddScoped<IRentalViolationRepository, RentalViolationRepository>();
            builder.Services.AddScoped<IRentalViolationService, RentalViolationService>();

            builder.WebHost.UseUrls($"http://*:80");

            var app = builder.Build();

            app.UseCors("AllowAll");

            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<TokenValidationMiddleware>();

            // Configure the HTTP request pipeline.
            // Enable Swagger in both Development and Production
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyAPI v1");
                c.ConfigObject.AdditionalItems["persistAuthorization"] = true;
            });

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Cấu hình endpoint
            app.MapHub<NotificationHub>("/notificationHub");
            app.MapHub<ChatHub>("/chathub");
            app.MapHub<ReportHub>("/reportHub");

            app.MapControllers();

            app.Run();

            static IEdmModel GetEdmModel()
            {
                var builder = new ODataConventionModelBuilder();

                // Đăng ký các entity bạn muốn query bằng OData
                builder.EntitySet<ProductDTO>("products");
                builder.EntitySet<UserODataDTO>("users");
                builder.EntitySet<ReportViewModel>("unassigned");
                builder.EntitySet<ReportViewModel>("mytasks");

                return builder.GetEdmModel();
            }
        }
    }
}
