# Parcel Tracking API

A RESTful API built with .NET 10 for:
- Tracking parcels
- Managing delivery addresses
- Estimating delivery times
- Viewing parcel analytics

## Technologies Used
- **C# / .NET 10**
- **PostgreSQL** (via Entity Framework Core)
- **Docker & Docker Compose**
- **Swagger / ScalarUI** for API Documentation
- **xUnit, FluentAssertions, Testcontainers** for Testing

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for the database)
- Visual Studio 2026, JetBrains Rider, or VS Code

## Getting Started

### 1. Clone the repository
```bash
git clone https://github.com/MuhammedDotNet/Parcel-Tracking-API.git
cd Parcel-Tracking-API
```

### 2. Start the Database (Docker)
The API requires a PostgreSQL database. You can quickly start one using the provided `docker-compose.yml` file.

```bash
# Start PostgreSQL in the background
docker-compose up -d
```

This will start a PostgreSQL 17 container on port `5434` with the default database `parceltracking`.

### 3. Connection String & Settings
The application is configured to connect to the Docker container by default. You can see the configuration in `src/ParcelTracking.Api/appsettings.Development.json` (or `appsettings.json`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5434;Database=parceltracking;Username=parcel;Password=parcel123"
}
```

*Note: Entity Framework Core migrations will automatically apply when the API starts.*

### 4. Run the API
```bash
cd src/ParcelTracking.Api
dotnet run
```

Once running, navigate to `https://localhost:<port>/scalar/v1` in your browser to view the API documentation and test endpoints.

## Testing
The project includes unit and integration tests. The integration tests use **Testcontainers**, which means they will automatically spin up their own isolated PostgreSQL Docker container during the test run.

```bash
# Navigate to the solution root
dotnet test
```

## Manual Testing
A `ParcelTracking.http` file is included in the root directory. You can use this file with tools like the VS Code REST Client extension or Visual Studio's `.http` file support to manually test all available endpoints.
