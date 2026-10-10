# Inventory Hold Microservice

A .NET-based inventory reservation service that temporarily holds product stock, prevents overselling through atomic inventory updates, and restores stock when a hold is released or expires.

## Features

- Create inventory holds for one or more products.
- Validate product IDs and requested quantities.
- Prevent overselling using atomic MongoDB stock updates.
- Persist inventory and hold records in MongoDB.
- Execute inventory deductions and hold creation within MongoDB transactions.
- Cache inventory responses in Redis with a 20-second TTL.
- Invalidate cached inventory when stock changes.
- Publish hold-created, hold-released, and hold-expired events to RabbitMQ.
- Release holds idempotently and restore stock.
- Process expired holds through the domain service.
- Provide a React + TypeScript dashboard for inventory, hold creation, lookup, and release.
- Include automated unit tests for core hold-service behavior.

## Technology Stack

| Component | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API |
| Architecture | Domain-oriented layered architecture |
| Database | MongoDB |
| Cache | Redis |
| Messaging | RabbitMQ |
| Frontend | React, TypeScript, Vite |
| Testing | NUnit, Moq |
| Containerization | Docker Compose |

## Architecture

The solution separates domain logic, infrastructure concerns, API endpoints, and shared contracts.

```text
InventoryHold/
├── src/
│   ├── InventoryHold.Contracts/
│   ├── InventoryHold.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Repositories/
│   │   └── Services/
│   ├── InventoryHold.Infrastructure/
│   │   ├── MongoDB/
│   │   ├── Redis/
│   │   └── Messaging/
│   └── InventoryHold.WebApi/
│       └── Controllers/
├── tests/
│   └── InventoryHold.UnitTests/
├── frontend/
│   └── frontend/
├── docker-compose.yml
└── README.md
```

The frontend path shown above reflects the current nested Vite project structure.

### Responsibilities

- **Domain:** Inventory and hold entities, business rules, service interfaces, and hold lifecycle orchestration.
- **Infrastructure:** MongoDB repositories and transactions, Redis caching, and RabbitMQ event publishing.
- **Web API:** HTTP endpoints, request validation, response mapping, and dependency injection.
- **Contracts:** API request and response models.
- **Frontend:** Inventory display and hold-management user interface.

## Prerequisites

Install the following before running the application:

- .NET 10 SDK
- Node.js version supported by the installed Vite version
- npm
- Docker Desktop with Docker Compose
- Git (optional)

## Configuration

The API reads its configuration from `appsettings.json` and supports configuration overrides through the standard ASP.NET Core configuration system.

Typical local development settings:

| Setting | Local value |
|---|---|
| MongoDB connection | `mongodb://localhost:27017/?replicaSet=rs0&directConnection=true` |
| MongoDB database | `InventoryHoldDb` |
| Redis connection | `localhost:6379` |
| RabbitMQ connection | `amqp://inventoryapp:localdevpassword@localhost:5672` |
| Hold duration | 5 minutes |
| React development URL | `http://localhost:5173` |
| API URL | `http://localhost:5087` |

These values are intended for local development only. Do not use development credentials or hard-coded secrets in production.

The MongoDB connection expects the configured `rs0` replica set for transaction support.

## Run the Application

Run the backend and frontend in separate terminals.

### 1. Start infrastructure services

From the repository root:

```powershell
docker compose up -d
docker compose ps
```

The local services use these ports:

- MongoDB: `27017`
- Redis: `6379`
- RabbitMQ: `5672`
- RabbitMQ management UI: `15672`

Open the RabbitMQ management UI at `http://localhost:15672` when the management service is running.

Initialize the MongoDB replica set if it has not already been initialized. The replica set name is `rs0`; follow the initialization procedure configured for the supplied Compose setup.

### 2. Seed sample inventory

Connect to the local MongoDB instance:

```powershell
docker exec -it inventoryhold-mongo mongosh
```

Initialize the replica set first if required by the current environment. Then seed sample inventory:

```javascript
use InventoryHoldDb

db.inventory.updateOne(
  { _id: "P100" },
  {
    $set: {
      name: "Keyboard",
      availableQuantity: 10
    }
  },
  { upsert: true }
)

db.inventory.updateOne(
  { _id: "P200" },
  {
    $set: {
      name: "Mouse",
      availableQuantity: 20
    }
  },
  { upsert: true }
)
```

The inventory collection uses product IDs as document identifiers. Keep the document structure aligned with the repository's MongoDB document mapping.

### 3. Start the .NET API

From the repository root:

```powershell
dotnet run --project src/InventoryHold.WebApi --urls http://localhost:5087
```

The API should listen at:

`http://localhost:5087`

### 4. Start the React frontend

The Vite project is currently located in `frontend/frontend`.

In another terminal:

```powershell
cd frontend/frontend
npm install
npm run dev
```

Open:

`http://localhost:5173`

The frontend calls the API at `http://localhost:5087`. The API must allow the frontend origin through its CORS policy.

### 5. Build the frontend for production

From `frontend/frontend`:

```powershell
npm run build
```

Vite writes the production output to the `dist` directory. This build does not start a web server.

## API Endpoints

Base URL: `http://localhost:5087`

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/inventory` | Retrieve inventory, using Redis caching |
| POST | `/api/holds` | Create a new inventory hold |
| GET | `/api/holds/{id}` | Retrieve a hold by ID |
| DELETE | `/api/holds/{id}` | Release a hold |

### Get inventory

```http
GET /api/inventory
```

Example response:

```json
[
  {
    "productId": "P100",
    "name": "Keyboard",
    "availableQuantity": 10
  },
  {
    "productId": "P200",
    "name": "Mouse",
    "availableQuantity": 20
  }
]
```

The inventory endpoint may include an `X-Cache` response header indicating whether the result was served from the cache.

### Create a hold

```http
POST /api/holds
Content-Type: application/json
```

Request:

```json
{
  "items": [
    {
      "productId": "P100",
      "quantity": 2
    },
    {
      "productId": "P200",
      "quantity": 3
    }
  ]
}
```

A successful request creates an active hold, deducts the requested quantities, and returns the hold details, including its ID and expiration time.

Possible errors include:

- `400 Bad Request`: invalid request data.
- `404 Not Found`: requested product does not exist.
- `409 Conflict`: insufficient stock or a conflicting operation.

### Get hold details

```http
GET /api/holds/{id}
```

Returns the hold details when found. A missing hold returns `404 Not Found`.

### Release a hold

```http
DELETE /api/holds/{id}
```

A successful release returns `204 No Content`. Releasing an already released hold is idempotent and does not restore inventory a second time.

## Hold Lifecycle

```text
Create hold
    |
    v
Active hold
    |
    +---- Explicit release ----> Released
    |
    +---- Expiration ----------> Expired
```

A hold starts in the `Active` state. Releasing or expiring an active hold restores its reserved inventory. The repository conditionally transitions hold status to avoid processing the same active hold multiple times.

The domain service provides an expiration-processing operation. Automatic scheduling of that operation depends on a hosted worker or scheduler being configured; do not assume expiration processing runs continuously unless such a component is present.

## Caching and Messaging

### Redis

- Inventory responses are cached under `inventory:all`.
- Cache TTL is 20 seconds.
- Inventory cache entries are invalidated when holds change stock.

### RabbitMQ

The publisher uses the durable exchange `inventory.holds` and queue `inventory.hold.events`.

Routing keys:

- `hold.created`
- `hold.released`
- `hold.expired`

Events are published after the corresponding database operation completes. The current implementation does not use a transactional outbox, so a broker failure after a database commit can result in an event not being published.

## Testing

Run the unit tests from the repository root:

```powershell
dotnet test
```

Build the .NET solution:

```powershell
dotnet build
```

Validate the Docker Compose configuration:

```powershell
docker compose config --quiet
```

Build the frontend:

```powershell
cd frontend/frontend
npm run build
```

The existing unit tests cover hold creation, invalid input, insufficient stock, and release/idempotency behavior. Unit tests do not replace integration tests against MongoDB, Redis, and RabbitMQ.

## End-to-End Verification

With infrastructure, API, and frontend running:

1. Open the React dashboard.
2. Confirm the inventory list displays the seeded products.
3. Create a hold for a quantity that is available.
4. Verify that inventory decreases by the held quantity.
5. Retrieve the hold using its ID.
6. Release the hold.
7. Refresh inventory and verify that stock is restored.
8. Try a nonexistent hold ID and confirm the UI displays a friendly error.
9. Check RabbitMQ's management UI to confirm that events are published.

## Known Limitations

- The dashboard lists holds created or retrieved during the current browser session; there is no list-all-holds API endpoint in the current implementation.
- The unit-of-work test double invokes operations directly and does not simulate MongoDB transaction rollback.
- Event publication occurs after database commit and is not protected by an outbox.
- Expiration processing requires an execution trigger to run periodically.
- Local development credentials and connection strings must be replaced with secure configuration for production.

## Security Notes

- Keep secrets out of source control.
- Use environment variables or a secure secrets manager for deployment configuration.
- Restrict CORS to trusted origins.
- Use HTTPS and appropriate authentication/authorization for production deployments.
- Review container credentials, network exposure, and MongoDB/Redis access controls before deployment.

Note: The connection strings and credentials shown above are sample values intended exclusively for local development and demonstration. The RabbitMQ username and password are configured as development defaults in docker-compose.yml. These values are visible in this repository and must not be reused in production or shared environments.

Before deploying the application, replace the sample credentials and configure sensitive settings through environment variables or a secure secrets manager. Never commit production passwords, API keys, access tokens, or other real secrets to source control.

## License

Add the license and attribution information required by the project owner or assignment.
