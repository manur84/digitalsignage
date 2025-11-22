using System;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace DigitalSignage.Server.Api;

/// <summary>
/// Startup configuration for the REST API
/// </summary>
public class ApiStartup
{
    /// <summary>
    /// Configure services for dependency injection
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // Add controllers
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase
                options.JsonSerializerOptions.WriteIndented = true;
            });

        // Add authentication with custom mobile app handler
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = MobileAppAuthenticationExtensions.SchemeName;
            options.DefaultChallengeScheme = MobileAppAuthenticationExtensions.SchemeName;
        })
        .AddMobileAppAuthentication();

        // Add authorization
        services.AddAuthorization();

        // Add CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Add Swagger/OpenAPI
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Digital Signage API",
                Version = "v1",
                Description = "REST API for Digital Signage Mobile App - provides device management, layout control, and server monitoring capabilities.",
                Contact = new OpenApiContact
                {
                    Name = "Digital Signage",
                }
            });

            // Add Bearer token authentication to Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "Token",
                Description = "Mobile app authentication token. Obtain this token by registering the mobile app and waiting for approval."
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

            // Include XML comments if available
            var xmlFile = $"{typeof(ApiStartup).Assembly.GetName().Name}.xml";
            var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (System.IO.File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        // Add endpoint explorer for minimal APIs
        services.AddEndpointsApiExplorer();
    }

    /// <summary>
    /// Configure the HTTP request pipeline
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Enable Swagger in all environments (useful for mobile app development)
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Signage API v1");
            c.RoutePrefix = "swagger"; // Swagger at /swagger
            c.DocumentTitle = "Digital Signage API";
            c.DefaultModelsExpandDepth(2);
            c.DisplayRequestDuration();
        });

        // Add a redirect from root to Swagger UI
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Response.Redirect("/swagger");
                return;
            }
            await next();
        });

        // Error handling
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        // HTTPS redirection (optional, can be disabled for development)
        // app.UseHttpsRedirection();

        // Routing
        app.UseRouting();

        // CORS
        app.UseCors();

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controllers
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
