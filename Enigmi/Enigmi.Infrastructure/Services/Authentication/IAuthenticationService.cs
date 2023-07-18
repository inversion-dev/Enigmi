using System.Collections.ObjectModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Enigmi.Infrastructure.Services.Authentication;

public interface IAuthenticationService
{
    string GenerateJwtToken(string stakingAddress);
    ClaimsPrincipal? ValidateJwtIfPresent(ReadOnlyDictionary<string, IEnumerable<string?>>? httpRequest);
}