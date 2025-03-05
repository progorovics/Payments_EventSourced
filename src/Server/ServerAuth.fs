open System
open System.Security.Claims
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Configure JWT Bearer authentication.
    // Replace "https://your.identityprovider.com" and "your-api-audience" with your actual values.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.Authority <- "https://your.identityprovider.com"
            options.Audience <- "your-api-audience"
            // Optionally, configure additional JWT validation parameters here.
        )
        |> ignore

    // Configure authorization and define a policy that requires a "group" claim.
    builder.Services.AddAuthorization(fun options ->
        options.AddPolicy("GroupPolicy", fun policy ->
            // Only users with "Admins" or "SuperUsers" in the "group" claim can access endpoints using this policy.
            policy.RequireClaim("group", "Admins", "SuperUsers")
            |> ignore
        )
    )
    |> ignore

    let app = builder.Build()

    // Use the authentication and authorization middleware.
    app.UseAuthentication()
    app.UseAuthorization()

    // Public endpoint accessible without authentication.
    app.MapGet("/", fun () -> "Welcome to the microservice!")

    // Secure endpoint that requires the "GroupPolicy" authorization.
    app.MapGet("/secure-data", [<Authorize(Policy = "GroupPolicy")>] (fun (user: ClaimsPrincipal) ->
        let userName =
            if isNull user.Identity || String.IsNullOrWhiteSpace(user.Identity.Name) then "User"
            else user.Identity.Name
        sprintf "Hello %s, you have access to secure data!" userName
    ))

    app.Run()
    0
