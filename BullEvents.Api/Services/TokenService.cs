using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BullEvents.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace BullEvents.Api.Services;

public class TokenService(IConfiguration configuration)
{
    private readonly string _key = configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "BullEvents.Api";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "BullEvents.Client";

    private SigningCredentials SigningCredentials =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);

    public string CreatePendingOtpToken(int userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("purpose", "otp_pending"),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int? ValidatePendingOtpToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var purpose = principal.FindFirstValue("purpose");
            if (purpose != "otp_pending") return null;

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return sub is null ? null : int.Parse(sub);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Mints an access token and returns the id it was minted under.
    ///
    /// The <c>jti</c> is the whole reason this signature grew a third return
    /// value: a JWT is otherwise impossible to withdraw before it expires, and
    /// the caller records this id against a <see cref="UserSession"/> so the
    /// request pipeline can check whether the session still stands. A token
    /// whose id was never recorded fails that check, which is what makes the
    /// two halves impossible to get out of step.
    /// </summary>
    public (string token, DateTime expiresAt, Guid tokenId) CreateAccessToken(
        User user, Company company, IReadOnlyList<int> branchIds)
    {
        var expiresAt = DateTime.UtcNow.AddHours(12);
        var tokenId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            // Written as "role" rather than ClaimTypes.Role so the claim type in
            // the token matches the RoleClaimType the API validates against.
            new("role", user.Role),
            new("company_id", company.Id.ToString()),
            new("company_name", company.Name),
        };

        claims.AddRange(branchIds.Select(id => new Claim("branch_id", id.ToString())));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: SigningCredentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt, tokenId);
    }
}
