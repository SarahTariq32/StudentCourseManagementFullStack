# StudentCourseManagement

A Student Course Management Web API built with **ASP.NET Core 8**, **Entity Framework Core**, and **Microsoft SQL Server**, following **Clean Architecture** principles.

## Tech Stack

- .NET 8 / ASP.NET Core Web API
- C#
- Entity Framework Core 8
- Microsoft SQL Server
- JWT Authentication & Authorization
- FluentValidation
- Swagger / OpenAPI
- Repository Pattern & DTOs
- Clean Architecture

## Architecture

```text
StudentCourseManagement/
├── StudentCourseManagement.API
├── StudentCourseManagement.Application
├── StudentCourseManagement.Domain
└── StudentCourseManagement.Infrastructure
```

- **API** — Controllers and HTTP request handling
- **Application** — Services, DTOs, interfaces, and business logic
- **Domain** — Core entities and domain models
- **Infrastructure** — Database access, EF Core, repositories, and mappings

## Main Features

- User registration and login with JWT authentication
- Role-based access for Admin and Student users
- Student and Course CRUD operations
- Student-specific access to their own profile
- Refresh token support for renewing expired access tokens
- DTO validation using FluentValidation
- Global exception handling with application logging
- Entity Framework Core with SQL Server
- Repository and service-based architecture
- Swagger API documentation and testing

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/Auth/register` | Register a user |
| POST | `/api/Auth/login` | Login and receive authentication tokens |
| POST | `/api/Auth/refresh` | Refresh an expired access token |
| GET | `/api/Courses` | Get all courses |
| GET | `/api/Courses/{id}` | Get a course |
| POST | `/api/Courses` | Create a course |
| PUT | `/api/Courses/{id}` | Update a course |
| DELETE | `/api/Courses/{id}` | Delete a course |
| GET | `/api/Students` | Get all students |
| GET | `/api/Students/{id}` | Get a student |
| POST | `/api/Students` | Create a student |
| PUT | `/api/Students/{id}` | Update a student |
| DELETE | `/api/Students/{id}` | Delete a student |

Authorization for endpoints depends on the authenticated user's role and access permissions.

## Database

The application uses **Microsoft SQL Server** with Entity Framework Core for data access. Database configuration is provided through application configuration rather than hardcoded values.

## Running the Project

Configure the SQL Server connection string and JWT settings in `appsettings.json` or environment configuration.

```bash
dotnet build StudentCourseManagement.sln
dotnet run --project StudentCourseManagement.API
```

Then open Swagger:

```text
https://localhost:<port>/swagger
```
