
using BusinessObject.DTOs.Login;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using Repositories.UserRepositories;
using Services.Authentication;
using Services.UserServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using ShareItAPI.Middlewares;
using Microsoft.OpenApi.Models;
using BusinessObject.DTOs.CloudinarySetting;
using Microsoft.Extensions.Options;
using CloudinaryDotNet;
using Repositories.Logout;
using Services.CloudServices;
using Repositories.ProfileRepositories;
using Services.ProfileServices;
using LibraryManagement.Services.Payments.Transactions;
using Services.Transactions;
using Services.Payments.VNPay;
using BusinessObject.DTOs.BankQR;

namespace ShareItAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Add DbContext with SQL Server
            builder.Services.AddDbContext<ShareItDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<ILoggedOutTokenRepository, LoggedOutTokenRepository>();
            builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddHttpClient<GoogleAuthService>();
            
            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddHttpClient();

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

            var app = builder.Build();

            app.UseMiddleware<TokenValidationMiddleware>();

            // Configure the HTTP request pipeline.
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
        }
    }
}
