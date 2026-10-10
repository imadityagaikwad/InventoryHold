# AI Usage and Engineering Decisions

## 1. AI-Assisted Planning and Architecture

Before starting implementation, I used AI as a discussion partner to clarify the requirements, explore architectural options, and establish an initial application structure.

We discussed the responsibilities of each layer and how the components would interact before proceeding with implementation.

The agreed structure was:

- **Domain layer:** Inventory entities, hold lifecycle, and core business rules.
- **Contracts layer:** Request, response, and event contracts shared across application boundaries.
- **Infrastructure layer:** MongoDB persistence and transactions, Redis caching, and RabbitMQ event publishing.
- **Web API layer:** HTTP endpoints, request validation, error handling, and application coordination.
- **Frontend:** React and TypeScript UI for viewing inventory and managing holds.
- **Unit tests:** Validation of business rules and important hold lifecycle scenarios.

We also discussed cross-cutting concerns, including transaction boundaries, inventory consistency, idempotent release operations, cache invalidation, and event publishing.

Establishing this structure early provided a consistent direction for implementation. It helped reduce ad hoc design decisions during development, clarified where responsibilities belonged, and made subsequent implementation choices easier to evaluate against the original requirements.

AI supported the exploration of options and trade-offs; architectural decisions were reviewed against the business requirements and implementation constraints.

## 2. Design Decisions and Engineering Judgment

### Decisions retained

**Separation of responsibilities**

The application was organized into distinct layers to keep business logic separate from HTTP, persistence, caching, and messaging concerns. This supports maintainability and makes domain behavior easier to test independently.

**Transactional inventory updates**

Hold creation was designed to deduct stock and persist the corresponding hold within a MongoDB transaction. Cache invalidation and event publishing occur after the transaction commits, avoiding publication of success events before the database operation is committed.

**Idempotent hold release**

Releasing a hold must restore inventory only once. Repeated release requests must not restore stock or publish duplicate release events. Unit tests cover the active-release and repeated-release scenarios.

**Separation of business behavior from infrastructure**

Business rules are kept in the domain layer, while database and messaging operations are handled by infrastructure components. This makes responsibilities clearer and helps isolate business logic during testing.

### Trade-offs and limitations considered

**Database and messaging consistency**

Publishing an event after a successful database commit avoids announcing an uncommitted operation. However, a process failure between the database commit and event publication can still result in a missing event. A transactional outbox would be a possible next step to improve reliability.

**Unit tests versus infrastructure guarantees**

Mock-based unit tests validate application behavior, but they do not prove MongoDB transaction rollback, concurrent stock-update correctness, or messaging failure recovery. These require additional integration and concurrency testing.

The goal was not to accept AI-generated suggestions automatically, but to evaluate implementation choices against correctness, maintainability, and the assignment's requirements.

## 3. Verification of AI-Assisted Implementation

The implementation was verified through automated checks and manual end-to-end testing.

| Verification | Result |
|---|---|
| `dotnet test` | All 8 unit tests passed |
| `dotnet build` | Solution build completed successfully |
| `docker compose config --quiet` | Docker Compose configuration validated successfully |
| `npm run build` | Frontend production build completed successfully |
| Manual hold lifecycle test | Created a hold, verified stock deduction, released the hold, and verified stock restoration |
| Repeated release test | Confirmed that releasing the same hold again did not restore stock a second time |

AI-assisted code and tests were reviewed as part of implementation. Automated checks and manual testing were used to validate the behaviors listed above rather than relying on generated code alone.

## 4. Known Limitations and Future Improvements

The following areas remain candidates for further development and production hardening:

- **Automatic expiration processing:** A scheduled background worker is not yet implemented. Expired holds require explicit processing to restore inventory and publish expiration events.
- **Transactional outbox:** Event publication is not atomic with the MongoDB transaction. An outbox could improve reliability when the database commit succeeds but event publication fails.
- **Integration and concurrency testing:** MongoDB rollback behavior, concurrent inventory deductions, and messaging failure recovery have not been fully integration-tested.

These limitations are documented to distinguish verified functionality from areas that would need additional work before production use.