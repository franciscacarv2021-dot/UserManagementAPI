using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var apiKey = builder.Configuration["Authentication:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Configure Authentication:ApiKey before starting the API.");
}

var expectedApiKey = Encoding.UTF8.GetBytes(apiKey);
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    var statusCode = context.Response.StatusCode;
    var title = statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Conflict",
        >= 500 => "Server error",
        _ => "Request failed"
    };

    await CreateProblemResult(context, statusCode, title).ExecuteAsync(context);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/users"),
    branch => branch.UseMiddleware<TokenAuthenticationMiddleware>(expectedApiKey));
app.UseMiddleware<RequestResponseLoggingMiddleware>();

var users = new ConcurrentDictionary<Guid, User>();
var userEndpoints = app.MapGroup("/api/users").WithTags("Users");

userEndpoints.MapGet("/", (int? skip, int? take) =>
{
    var offset = Math.Max(skip ?? 0, 0);
    var pageSize = Math.Clamp(take ?? 100, 1, 100);
    var page = users.Values
        .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(user => user.Id)
        .Skip(offset)
        .Take(pageSize)
        .ToArray();

    return Results.Ok(page);
});

userEndpoints.MapGet("/{id:guid}", (Guid id) =>
    users.TryGetValue(id, out var user)
        ? Results.Ok(user)
        : Results.NotFound())
    .WithName("GetUserById");

userEndpoints.MapPost("/", (UserRequest request) =>
{
    var errors = ValidateUserRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var user = new User(Guid.NewGuid(), request.Name!.Trim(), request.Email!.Trim());
    users[user.Id] = user;
    return Results.Created($"/api/users/{user.Id}", user);
});

userEndpoints.MapPut("/{id:guid}", (Guid id, UserRequest request) =>
{
    var errors = ValidateUserRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    if (!users.TryGetValue(id, out var existingUser))
    {
        return Results.NotFound();
    }

    var updatedUser = existingUser with
    {
        Name = request.Name!.Trim(),
        Email = request.Email!.Trim()
    };

    return users.TryUpdate(id, updatedUser, existingUser)
        ? Results.Ok(updatedUser)
        : Results.Conflict();
});

userEndpoints.MapDelete("/{id:guid}", (Guid id) =>
    users.TryRemove(id, out _)
        ? Results.NoContent()
        : Results.NotFound());

app.Run();

static Dictionary<string, string[]> ValidateUserRequest(UserRequest request)
{
    const int maxNameLength = 100;
    const int maxEmailLength = 254;

    var errors = new Dictionary<string, string[]>();
    var name = request.Name?.Trim();
    var email = request.Email?.Trim();

    if (string.IsNullOrWhiteSpace(name))
    {
        errors["name"] = ["Name is required."];
    }
    else if (name.Length > maxNameLength)
    {
        errors["name"] = [$"Name must be {maxNameLength} characters or fewer."];
    }

    if (string.IsNullOrWhiteSpace(email)
        || email.Length > maxEmailLength
        || !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail)
        || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
    {
        errors["email"] = ["A valid email address is required."];
    }

    return errors;
}

static IResult CreateProblemResult(HttpContext context, int statusCode, string title) =>
    Results.Problem(
        statusCode: statusCode,
        title: title,
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = context.TraceIdentifier
        });

record User(Guid Id, string Name, string Email);
record UserRequest(string? Name, string? Email);
