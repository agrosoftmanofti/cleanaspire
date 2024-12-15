﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using CleanAspire.Domain.Identities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Identity.Data;
using StrongGrid.Resources;
namespace CleanAspire.Api;

public static class IdentityApiAdditionalEndpointsExtensions
{
    // Validate the email address using DataAnnotations like the UserValidator does when RequireUniqueEmail = true.
    private static readonly EmailAddressAttribute _emailAddressAttribute = new();

    public static IEndpointRouteBuilder MapIdentityApiAdditionalEndpoints<TUser>(this IEndpointRouteBuilder endpoints)
            where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var emailSender = endpoints.ServiceProvider.GetRequiredService<IEmailSender<TUser>>();
        var linkGenerator = endpoints.ServiceProvider.GetRequiredService<LinkGenerator>();
        var logger = endpoints.ServiceProvider.GetRequiredService<ILogger<IEndpointRouteBuilder>>();
        string? confirmEmailEndpointName = null;
        var routeGroup = endpoints.MapGroup("/account").WithTags("Authentication", "Account Management");
        routeGroup.MapPost("/logout", async (SignInManager<TUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            logger.LogInformation("User has been logged out successfully.");
            return Results.Ok();
        })
        .RequireAuthorization()
        .WithSummary("Log out the current user.")
        .WithDescription("Logs out the currently authenticated user by signing them out of the system. This endpoint requires the user to be authorized before calling, and returns an HTTP 200 OK response upon successful logout.");

        routeGroup.MapGet("/profile", async Task<Results<Ok<ProfileResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, HttpContext context, IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }
            logger.LogInformation("User profile retrieved successfully.");
            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager));
        })
        .RequireAuthorization()
        .WithSummary("Retrieve the user's profile")
        .WithDescription("Fetches the profile information of the authenticated user. " +
         "Returns 404 if the user is not found. Requires authorization.");


        routeGroup.MapPost("/profile", async Task<Results<Ok<ProfileResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromBody] ProfileRequest request, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }

            if (!string.IsNullOrEmpty(request.Email) && !_emailAddressAttribute.IsValid(request.Email))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(request.Email)));
            }
            if (user is not ApplicationUser appUser)
                throw new InvalidCastException($"The provided user must be of type {nameof(ApplicationUser)}.");

            appUser.UserName = request.Username;
            appUser.Nickname = request.Nickname;
            appUser.TimeZoneId = request.TimeZoneId;
            appUser.LanguageCode = request.LanguageCode;
            appUser.SuperiorId = request.SuperiorId;
            appUser.TenantId = request.TenantId;
            appUser.AvatarUrl = request.AvatarUrl;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(request.Email))
            {
                var email = await userManager.GetEmailAsync(user);

                if (email != request.Email)
                {
                    await SendConfirmationEmailAsync(user, userManager, context, request.Email, isChange: true);
                }
            }

            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager));
        })
        .RequireAuthorization()
        .Produces<ProfileResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Update user profile information.")
        .WithDescription("Allows users to update their profile, including username, email, nickname, avatar, time zone, and language code.");

        routeGroup.MapPost("/updateEmail", async Task<Results<Ok, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromBody] UpdateEmailRequest request, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }

            if (!string.IsNullOrEmpty(request.NewEmail) && !_emailAddressAttribute.IsValid(request.NewEmail))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(request.NewEmail)));
            }
            var email = await userManager.GetEmailAsync(user);
            if (email != request.NewEmail)
            {
                await SendConfirmationEmailAsync(user, userManager, context, request.NewEmail, isChange: true);
            }

            return TypedResults.Ok();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Update user email address.")
        .WithDescription("Allows users to update their email address and receive a confirmation email if it changes.");


        routeGroup.MapPost("/signup", async Task<Results<Created, ValidationProblem>>
                ([FromBody] SignupRequest request, HttpContext context, [FromServices] IServiceProvider sp) =>
            {
                var userManager = sp.GetRequiredService<UserManager<TUser>>();
                var user = new TUser();
                if (!userManager.SupportsUserEmail)
                {
                    throw new NotSupportedException($"{nameof(MapIdentityApiAdditionalEndpoints)} requires a user store with email support.");
                }
                if (user is not ApplicationUser appUser)
                    throw new InvalidCastException($"The provided user must be of type {nameof(ApplicationUser)}.");
                appUser.Email = request.Email;
                appUser.UserName = request.Email;
                appUser.Nickname = request.Nickname;
                appUser.Provider = request.Provider;
                appUser.TenantId = request.TenantId;
                appUser.TimeZoneId = request.TimeZoneId;
                appUser.LanguageCode = request.LanguageCode;
                var result = await userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return CreateValidationProblem(result);
                }
                var sanitizedEmail = request.Email.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                var sanitizedTenantId = request.TenantId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                logger.LogInformation("User signup successful. Email: {Email}, TenantId: {TenantId}", sanitizedEmail, sanitizedTenantId);
                await SendConfirmationEmailAsync(user, userManager, context, request.Email);
                return TypedResults.Created();
            })
            .AllowAnonymous()
            .WithSummary("User Signup")
            .WithDescription("Allows a new user to sign up by providing required details such as email, password, and tenant-specific information. This endpoint creates a new user account and sends a confirmation email for verification.");

        routeGroup.MapDelete("/deleteOwnerAccount", async Task<Results<Ok, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, SignInManager<TUser> signInManager, [FromBody] DeleteUserRequest request, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }
            var userName = await userManager.GetUserNameAsync(user);
            if (string.IsNullOrEmpty(request.Username) || userName != request.Username)
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidUserName(request.Username)));
            }
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return TypedResults.NotFound();
            }
            await signInManager.SignOutAsync();
            logger.LogInformation("User account deleted successfully.");
            return TypedResults.Ok();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Delete own user account.")
        .WithDescription("Allows users to delete their own account permanently.");

        routeGroup.MapGet("/confirmEmail", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>>
            ([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.FindByIdAsync(userId) is not { } user)
            {
                return TypedResults.Unauthorized();
            }
            try
            {
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return TypedResults.Unauthorized();
            }
            IdentityResult result;
            if (string.IsNullOrEmpty(changedEmail))
            {
                result = await userManager.ConfirmEmailAsync(user, code);
            }
            else
            {
                result = await userManager.ChangeEmailAsync(user, changedEmail, code);
                if (result.Succeeded)
                {
                    result = await userManager.SetUserNameAsync(user, changedEmail);
                }
            }
            if (!result.Succeeded)
            {
                return TypedResults.Unauthorized();
            }
            return TypedResults.Text("Thank you for confirming your email.");
        }).AllowAnonymous()
          .WithSummary("Confirm Email or Update Email Address")
          .WithDescription("Processes email confirmation or email change requests for a user. It validates the confirmation code, verifies the user ID, and updates the email if a new one is provided. Returns a success message upon successful confirmation or email update.")
          .Add(endpointBuilder =>
          {
              var finalPattern = ((RouteEndpointBuilder)endpointBuilder).RoutePattern.RawText;
              confirmEmailEndpointName = $"{nameof(MapIdentityApiAdditionalEndpoints)}-{finalPattern}";
              endpointBuilder.Metadata.Add(new EndpointNameMetadata(confirmEmailEndpointName));
          });


        async Task SendConfirmationEmailAsync(TUser user, UserManager<TUser> userManager, HttpContext context, string email, bool isChange = false)
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var clientBaseUrl = configuration["ClientBaseUrl"];
            if (string.IsNullOrEmpty(clientBaseUrl))
            {
                throw new InvalidOperationException("Client base URL is not configured.");
            }
            var code = isChange
                ? await userManager.GenerateChangeEmailTokenAsync(user, email)
                : await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var userId = await userManager.GetUserIdAsync(user);
  
            // Construct the client-side URL
            var confirmEmailUrl = isChange
                ? $"{clientBaseUrl}/account/profile?userId={userId}&code={code}&changedEmail={email}"
                : $"{clientBaseUrl}/account/confirm-email?userId={userId}&code={code}";
            //var emailContent = isChange ? GenerateChangeEmailContent(confirmEmailUrl)
            //                            : GenerateSignupEmailContent(confirmEmailUrl);
            await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
        }
        routeGroup.MapPost("/forgotPassword", async Task<Results<Ok, ValidationProblem>>
            (HttpContext context, [FromBody] ForgotPasswordRequest resetRequest, [FromServices] IServiceProvider sp) =>
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var clientBaseUrl = configuration["ClientBaseUrl"];
            if (string.IsNullOrEmpty(clientBaseUrl))
            {
                throw new InvalidOperationException("Client base URL is not configured.");
            }
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var email = await userManager.GetEmailAsync(user);
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var resetLink = $"{clientBaseUrl}/account/confirm-password-reset?email={email}&code={code}";
                await emailSender.SendPasswordResetLinkAsync(user, resetRequest.Email, HtmlEncoder.Default.Encode(resetLink));
            }

            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
            // returned a 400 for an invalid code given a valid user email.
            return TypedResults.Ok();
        }).AllowAnonymous()
          .WithSummary("Request a password reset link")
          .WithDescription("Generates and sends a password reset link to the user's email if the email is registered and confirmed.");
        return endpoints;
    }
    private static async Task<ProfileResponse> CreateInfoResponseAsync<TUser>(TUser user, UserManager<TUser> userManager)
        where TUser : class
    {
        if (user is not ApplicationUser appUser)
            throw new InvalidCastException($"The provided user must be of type {nameof(ApplicationUser)}.");
        return new()
        {
            UserId = await userManager.GetUserIdAsync(user) ?? throw new NotSupportedException("Users must have an ID."),
            Username = await userManager.GetUserNameAsync(user) ?? throw new NotSupportedException("Users must have a username."),
            Email = await userManager.GetEmailAsync(user) ?? throw new NotSupportedException("Users must have an email."),
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
            TenantId = appUser.TenantId,
            Nickname = appUser.Nickname,
            LanguageCode = appUser.LanguageCode,
            Provider = appUser.Provider,
            SuperiorId = appUser.SuperiorId,
            TimeZoneId = appUser.TimeZoneId,
            AvatarUrl = appUser.AvatarUrl
        };
    }


    private static ValidationProblem CreateValidationProblem(IdentityResult result)
    {
        // We expect a single error code and description in the normal case.
        // This could be golfed with GroupBy and ToDictionary, but perf! :P
        Debug.Assert(!result.Succeeded);
        var errorDictionary = new Dictionary<string, string[]>(1);

        foreach (var error in result.Errors)
        {
            string[] newDescriptions;

            if (errorDictionary.TryGetValue(error.Code, out var descriptions))
            {
                newDescriptions = new string[descriptions.Length + 1];
                Array.Copy(descriptions, newDescriptions, descriptions.Length);
                newDescriptions[descriptions.Length] = error.Description;
            }
            else
            {
                newDescriptions = [error.Description];
            }

            errorDictionary[error.Code] = newDescriptions;
        }

        return TypedResults.ValidationProblem(errorDictionary);
    }


    static string GenerateSignupEmailContent(string confirmEmailUrl)
    {
        return $@"
        <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        line-height: 1.6;
                        color: #333;
                    }}
                    .email-container {{
                        max-width: 600px;
                        margin: auto;
                        padding: 20px;
                        border: 1px solid #ccc;
                        border-radius: 5px;
                        background: #f9f9f9;
                    }}
                    .email-header {{
                        text-align: center;
                        margin-bottom: 20px;
                    }}
                    .email-link {{
                        display: inline-block;
                        margin: 20px 0;
                        padding: 10px 20px;
                        background: #007bff;
                        color: white;
                        text-decoration: none;
                        border-radius: 5px;
                    }}
                    .email-link:hover {{
                        background: #0056b3;
                    }}
                </style>
            </head>
            <body>
                <div class='email-container'>
                    <p>Hello,</p>
                    <p>Thank you for signing up. Please confirm your email address by clicking the button below:</p>
                    <a href='{HtmlEncoder.Default.Encode(confirmEmailUrl)}' class='email-link'>Confirm Email</a>
                    <p>If the button above doesn't work, copy and paste the following URL into your browser:</p>
                    <p>{HtmlEncoder.Default.Encode(confirmEmailUrl)}</p>
                    <p>Thank you,<br>The Team</p>
                </div>
            </body>
        </html>";
    }

    static string GenerateChangeEmailContent(string confirmEmailUrl)
    {
        return $@"
        <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        line-height: 1.6;
                        color: #333;
                    }}
                    .email-container {{
                        max-width: 600px;
                        margin: auto;
                        padding: 20px;
                        border: 1px solid #ccc;
                        border-radius: 5px;
                        background: #f9f9f9;
                    }}
                    .email-header {{
                        text-align: center;
                        margin-bottom: 20px;
                    }}
                    .email-link {{
                        display: inline-block;
                        margin: 20px 0;
                        padding: 10px 20px;
                        background: #28a745;
                        color: white;
                        text-decoration: none;
                        border-radius: 5px;
                    }}
                    .email-link:hover {{
                        background: #218838;
                    }}
                </style>
            </head>
            <body>
                <div class='email-container'>
                    <p>Hello,</p>
                    <p>We received a request to update your email address. To confirm this change, please click the button below:</p>
                    <a href='{HtmlEncoder.Default.Encode(confirmEmailUrl)}' class='email-link'>Confirm New Email</a>
                    <p>If the button above doesn't work, copy and paste the following URL into your browser:</p>
                    <p>{HtmlEncoder.Default.Encode(confirmEmailUrl)}</p>
                    <p>If you did not request this change, please ignore this email or contact support for assistance.</p>
                    <p>Thank you,<br>The Team</p>
                </div>
            </body>
        </html>";
    }
}

public class UpdateEmailRequest
{
    [Required]
    [Description("The new email address. Must be in a valid email format.")]
    [MaxLength(80, ErrorMessage = "Email cannot exceed 80 characters.")]
    [RegularExpression("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email format.")]
    public string NewEmail { get; set; } = string.Empty;
}

public class DeleteUserRequest
{
    [Description("Unique username for the user.")]
    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
    [RegularExpression("^[a-zA-Z0-9_]*$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
    public required string Username { get; init; }
}

public sealed class ProfileRequest
{
    [Description("User's preferred nickname.")]
    [MaxLength(50, ErrorMessage = "Nickname cannot exceed 50 characters.")]
    [RegularExpression("^[a-zA-Z0-9_]*$", ErrorMessage = "Nickname can only contain letters, numbers, and underscores.")]
    public string? Nickname { get; init; }

    [Description("Unique username for the user.")]
    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
    [RegularExpression("^[a-zA-Z0-9_]*$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
    public required string Username { get; init; }
    [Required]
    [Description("User's email address. Must be in a valid email format.")]
    [MaxLength(80, ErrorMessage = "Email cannot exceed 80 characters.")]
    [RegularExpression("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email format.")]
    public required string Email { get; init; }
    [Description("User uploads an avatar image.")]
    public string? AvatarUrl { get; init; }
    [Description("User's time zone identifier, e.g., 'UTC', 'America/New_York'.")]
    [MaxLength(50, ErrorMessage = "TimeZoneId cannot exceed 50 characters.")]
    public string? TimeZoneId { get; set; }

    [Description("User's preferred language code, e.g., 'en-US'.")]
    [MaxLength(10, ErrorMessage = "LanguageCode cannot exceed 10 characters.")]
    [RegularExpression("^[a-z]{2,3}(-[A-Z]{2})?$", ErrorMessage = "Invalid language code format.")]
    public string? LanguageCode { get; set; }
    [Description("Tenant identifier for multi-tenant systems. Must be a GUID in version 7 format.")]
    [MaxLength(50, ErrorMessage = "Nickname cannot exceed 50 characters.")]
    public string? SuperiorId { get; init; }
    [Description("Tenant identifier for multi-tenant systems. Must be a GUID in version 7 format.")]
    [MaxLength(50, ErrorMessage = "Nickname cannot exceed 50 characters.")]
    public string? TenantId { get; set; }
}
public sealed class ProfileResponse
{
    public string? Nickname { get; init; }
    public string? Provider { get; init; }
    public string? TenantId { get; init; }
    public string? AvatarUrl { get; set; }
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required bool IsEmailConfirmed { get; init; }
    public string? TimeZoneId { get; init; }
    public string? LanguageCode { get; init; }
    public string? SuperiorId { get; init; }
}
public sealed class SignupRequest
{
    [Required]
    [Description("User's email address. Must be in a valid email format.")]
    [MaxLength(80, ErrorMessage = "Email cannot exceed 80 characters.")]
    [RegularExpression("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email format.")]
    public required string Email { get; init; }

    [Required]
    [Description("User's password. Must meet the security criteria.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [MaxLength(20, ErrorMessage = "Password cannot exceed 20 characters.")]
    [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,50}$", ErrorMessage = "Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, one number, and one special character.")]
    public required string Password { get; init; }

    [Description("User's preferred nickname.")]
    [MaxLength(50, ErrorMessage = "Nickname cannot exceed 50 characters.")]
    public string? Nickname { get; set; }

    [Description("Authentication provider, e.g., Local or Google.")]
    [MaxLength(20, ErrorMessage = "Provider cannot exceed 20 characters.")]
    public string? Provider { get; set; } = "Local";

    [Description("Tenant identifier for multi-tenant systems. Must be a GUID in version 7 format.")]
    [MaxLength(50, ErrorMessage = "Nickname cannot exceed 50 characters.")]
    public string? TenantId { get; set; }

    [Description("User's time zone identifier, e.g., 'UTC', 'America/New_York'.")]
    [MaxLength(50, ErrorMessage = "TimeZoneId cannot exceed 50 characters.")]
    public string? TimeZoneId { get; set; }

    [Description("User's preferred language code, e.g., 'en-US'.")]
    [MaxLength(10, ErrorMessage = "LanguageCode cannot exceed 10 characters.")]
    [RegularExpression("^[a-z]{2,3}(-[A-Z]{2})?$", ErrorMessage = "Invalid language code format.")]
    public string? LanguageCode { get; set; }
}


