using Amazon;
using Amazon.S3;
using BeautySky.Models;
using BeautySky.Services;
using BeautySky.Services.Vnpay;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Security.Claims;
using System.Text;

namespace BeautySky
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Thêm session và cache
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Thêm IHttpContextAccessor để truy cập session trong controller
            builder.Services.AddHttpContextAccessor();

            // Thêm controllers với cấu hình InvalidModelStateResponseFactory
            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        return new BadRequestObjectResult(context.ModelState);
                    };
                });

            // Thêm Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(option =>
            {
                option.SwaggerDoc("v1", new OpenApiInfo { Title = "BeautySky", Version = "v1" });
                option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter a valid token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                option.AddSecurityRequirement(new OpenApiSecurityRequirement
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

            // Thêm DbContext
            builder.Services.AddDbContext<ProjectSwpContext>(option =>
            {
                option.UseSqlServer(builder.Configuration.GetConnectionString("MyDBConnection"));
            });

            // Cấu hình Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    //ValidateLifetime = true, // set token thời hạn 3h theo AccountController và GoogleController 
                    ValidAudience = builder.Configuration["JWT:ValidAudience"],
                    ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
                options.CallbackPath = "/signin-google";
                options.Events.OnCreatingTicket = async context =>
                {
                    var dbContext = context.HttpContext.RequestServices.GetService<ProjectSwpContext>();
                    var email = context.Principal.FindFirstValue(ClaimTypes.Email);
                    var name = context.Principal.FindFirstValue(ClaimTypes.Name);
                    var existingUser = dbContext.Users.FirstOrDefault(u => u.Email == email);

                    if (existingUser == null)
                    {
                        var newUser = new User
                        {
                            UserName = email.Split('@')[0],
                            FullName = name,
                            Email = email,
                            Password = "GOOGLE_LOGIN",
                            ConfirmPassword = "GOOGLE_LOGIN",
                            Phone = "Chưa cập nhật",
                            Address = "Chưa cập nhật",
                            RoleId = 1,
                            DateCreate = DateTime.UtcNow,
                            IsActive = true
                        };
                        dbContext.Users.Add(newUser);
                        await dbContext.SaveChangesAsync();
                    }
                };
            });

            // Thêm Amazon S3
            builder.Services.AddSingleton<IAmazonS3>(option =>
            {
                var configuration = option.GetRequiredService<IConfiguration>();
                return new AmazonS3Client(
                    configuration["AWS:AccessKey"],
                    configuration["AWS:SecretKey"],
                    RegionEndpoint.GetBySystemName(configuration["AWS:Region"])
                );
            });

            // Thêm các dịch vụ khác
            builder.Services.AddSingleton<S3Service>();
            builder.Services.AddScoped<IVnPayService, VnPayService>();

            // Thêm CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy => policy.AllowAnyOrigin()
                                    .AllowAnyMethod()
                                    .AllowAnyHeader());
            });


            //send email
            builder.Services.AddTransient<IEmailService, EmailService>();
            var app = builder.Build();

            // Cấu hình pipeline HTTP
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowAll");
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseSession();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}