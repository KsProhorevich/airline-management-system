using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace AirlineApi.Configuration;

public static class JwtBearerConfigurator
{
    public static void Configure(JwtBearerOptions options, JwtOptions jwt, bool requireHttpsMetadata)
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return;
                }

                await WriteProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Требуется аутентификация",
                    "Запрос не содержит действительный JWT. Получите токен через POST /api/auth/login.");
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                await WriteProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Доступ запрещён",
                    "У учётной записи недостаточно прав для этой операции.");
            }
        };
    }

    private static Task WriteProblemAsync(HttpContext httpContext, int statusCode, string title, string detail)
    {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            }).ExecuteAsync(httpContext);
    }
}
