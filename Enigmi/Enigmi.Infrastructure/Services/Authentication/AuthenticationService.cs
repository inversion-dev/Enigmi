using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigmi.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace Enigmi.Infrastructure.Services.Authentication;
public class AuthenticationService : IAuthenticationService
{
    private Settings Settings { get; }

    public AuthenticationService(Settings settings)
    {
        Settings = settings.ThrowIfNull();
    }
    
    public string GenerateJwtToken(string stakingAddress)
    {
        stakingAddress.ThrowIfNullOrWhitespace();
        
        var claims = new[]
        {
            new Claim(Constants.Claim.StakeAddress, stakingAddress),
            new Claim(ClaimTypes.Name, stakingAddress)
        };
        
        var securityToken = new JwtSecurityToken(
            null,
            null,
            claims, 
            null, 
            DateTime.UtcNow.AddHours(8), 
            GetSigningCredentials());

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(securityToken);
        return tokenValue.ThrowIfNullOrWhitespace();
    }

    private SigningCredentials GetSigningCredentials()
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.JwtTokenConfiguration.SecretKey)),
            SecurityAlgorithms.HmacSha256);
        return signingCredentials;
    }

    private ClaimsPrincipal? ValidateToken(string tokenString)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);
        
        ClaimsPrincipal? principal;
        try
        {
            principal = handler.ValidateToken(tokenString,
                new TokenValidationParameters()
                {
                    RequireAudience = false,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = GetSigningCredentials().Key,
                }, out _);
        }
        catch (Exception)
        {
            return null;
        }

        if (token.ValidTo < DateTime.UtcNow)
        {
            return null;
        }

        return principal;
    }

    public ClaimsPrincipal? ValidateJwtIfPresent(ReadOnlyDictionary<string, IEnumerable<string?>>? headers)
    {
        headers.ThrowIfNull();
        
        if (headers.TryGetValue(HeaderNames.Authorization, out var tokens))
        {
            string token = tokens
                .Where(x => !string.IsNullOrEmpty(x) && x.InvariantIgnoreCaseStartsWith(JwtBearerDefaults.AuthenticationScheme))
                .Select(x => x!.Split()[1])
                .Single();

            return ValidateToken(token);
        }

        return null;
    }
}