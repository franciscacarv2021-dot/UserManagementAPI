# User Management API

A minimal ASP.NET Core API for creating, reading, updating, and deleting users. It includes bearer-token authentication, request validation, pagination, structured error responses, and request logging.

## Requirements

- .NET 10 SDK
- An HTTP client such as Postman, or the VS Code REST Client extension for `UserManagementAPI.http`

## Configure the API key

All `/api/users` endpoints require a bearer token. Configure the expected token with .NET User Secrets from the project directory:

```powershell
dotnet user-secrets set "Authentication:ApiKey" "replace-with-a-long-random-value"
```

Use the same value in the HTTP client's `Authorization` header as `Bearer <API_KEY>`. Do not commit API keys to source control. Restart the API after changing the key.

## Run the API

From the project directory:

```powershell
dotnet restore
dotnet watch run --launch-profile http
```

The HTTP launch profile listens at `http://localhost:5194`. Build the project with:

```powershell
dotnet build
```

## Endpoints

Every `/api/users` endpoint requires `Authorization: Bearer <API_KEY>`. The root endpoint does not require authentication.

| Method | Route | Purpose | Success response |
| --- | --- | --- | --- |
| `GET` | `/` | Returns the welcome message. | `200 OK` |
| `GET` | `/api/users/?skip=0&take=10` | Lists users, ordered by name. `skip` defaults to `0`; `take` defaults to `100` and is bounded from `1` to `100`. | `200 OK` |
| `GET` | `/api/users/{id}` | Gets a user by GUID. | `200 OK` |
| `POST` | `/api/users/` | Creates a user. Returns the created user and its URL in the `Location` header. | `201 Created` |
| `PUT` | `/api/users/{id}` | Replaces a user's name and email. | `200 OK` |
| `DELETE` | `/api/users/{id}` | Deletes a user. | `204 No Content` |

The `{id}` route value must be a GUID. Requests that reference a user that does not exist return `404 Not Found`.

### Create and update request body

`POST` and `PUT` accept JSON with `name` and `email` properties:

```json
{
  "name": "Ana Silva",
  "email": "ana@example.com"
}
```

The name is required and limited to 100 characters. The email is required, must be a syntactically valid email address, and is limited to 254 characters. Invalid input returns `400 Bad Request` with validation details. Email uniqueness and mailbox ownership are not checked.

## Authentication and errors

Requests to `/api/users` without the expected bearer token return `401 Unauthorized`. Error responses use Problem Details and include a trace ID. Unhandled exceptions are handled by exception middleware, and HTTP requests are logged with their method, path, status, duration, and trace ID.

## Data storage

Users are stored in an in-memory collection. Data is lost when the API process stops or restarts; this project does not currently use a database.