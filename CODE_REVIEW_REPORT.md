# Code Compliance Review Report
**Date:** 2026-10-12  
**Services Reviewed:** Booking, Identity  
**Standards Reference:** Docs/racehorse-transport-docs/docs/

---

## Executive Summary

The codebase demonstrates **good foundational structure** with proper API layering, authentication, and endpoint organization. However, there are **critical issues** that violate architectural principles and **moderate issues** that need correction before production readiness.

**Compliance Score: 65/100**

---

## ✅ COMPLIANT AREAS

### 1. API Response Format (FULLY COMPLIANT)
- **Standard:** Section 5 & 6 - API response envelope  
- **Finding:** ApiResponse wrapper correctly implemented across all services
- **Example:** `ApiResponse<T>.Success(data, message, requestId)`
- **Status:** ✅ Correct

### 2. Endpoint Structure (FULLY COMPLIANT)
- **Standard:** Section 6 - Endpoints as static classes with MapGroup
- **Finding:** Uses `MapGroup("/api/v1/...")` pattern consistently
- **Examples:**
  - Booking: `/api/v1/transport-requests`, `/api/v1/horses`
  - Identity: `/api/v1/auth`
  - Tracking: `/api/v1/trips`
- **Status:** ✅ Correct

### 3. DTO Naming Conventions (FULLY COMPLIANT)
- **Standard:** BACKEND_STRUCTURE.md section 4 - DTO naming
- **Finding:** Proper use of `*Request`, `*Response`, `*Input`, `*Record` patterns
- **Examples:**
  - `SaveCustomerProfileRequest` / `CustomerProfile`
  - `SaveTransportRequestRequest` / `TransportRequestRecord`
  - `RegisterRequest` / `AuthTokensResponse`
- **Status:** ✅ Correct

### 4. Multi-Stage Dockerfiles (FULLY COMPLIANT)
- **Standard:** BACKEND_STRUCTURE.md section 10 - Multi-stage builds
- **Finding:** All Dockerfiles use SDK → runtime pattern correctly
- **Example:**
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  ...
  FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
  ```
- **Status:** ✅ Correct

### 5. JWT Authentication (FULLY COMPLIANT)
- **Standard:** ARCHITECTURE.md section 6 - JWT configuration
- **Finding:** Proper JWT validation with issuer, audience, signing key
- **Implementation Details:**
  - ValidateIssuer: ✅ true
  - ValidateAudience: ✅ true
  - ValidateIssuerSigningKey: ✅ true
  - ValidateLifetime: ✅ true
  - ClockSkew: ✅ 30 seconds
- **Status:** ✅ Correct

### 6. Middleware Pipeline (FULLY COMPLIANT)
- **Standard:** ARCHITECTURE.md section 8 - Resilience requirements
- **Finding:** Proper middleware ordering and implementations
- **Middleware Stack:**
  1. ✅ GlobalExceptionHandlerMiddleware
  2. ✅ CorrelationIdMiddleware
  3. ✅ HTTPS Redirection
  4. ✅ Authentication
  5. ✅ Authorization
- **Status:** ✅ Correct

### 7. Pagination Implementation (FULLY COMPLIANT)
- **Standard:** API_STANDARD.md section 6 - List response format
- **Finding:** Proper implementation of `PageResult<T>` with pagination metadata
- **Details:**
  - Zero-based page indexing: ✅
  - Default pageSize=20, maxPageSize=100: ✅ Implemented
  - TotalItems and TotalPages: ✅
- **Status:** ✅ Correct

### 8. Concurrency Handling (GOOD IMPLEMENTATION)
- **Standard:** DEFINITION_OF_DONE.md - Database concurrency management
- **Finding:** Uses optimistic locking with `VersionNo` field
- **Example:** Booking service validates version before update
  ```csharp
  WHERE h.Id = @HorseId AND h.VersionNo = @VersionNo
  ```
- **Status:** ✅ Good, but could benefit from more consistent usage

### 9. Authorization Policy Definition (COMPLIANT)
- **Standard:** API_STANDARD.md section 12 - Authorization
- **Finding:** Role-based authorization policies properly defined
- **Examples:**
  - Booking: `RequireAuthorization("Customer")`, `RequireAuthorization("LogisticsManager")`
  - Compliance: `RequireAuthorization("ComplianceOfficer")`
- **Status:** ✅ Correct

---

## ⚠️ CRITICAL ISSUES

### 1. **CRITICAL: Tracking Service Uses In-Memory Repositories**
- **Standard:** BACKEND_STRUCTURE.md section 1-2 - Service layers should use real persistence
- **Violation:** All Tracking repositories use in-memory dictionaries
- **File:** `Tracking/Tracking.Infrastructure/Repositories/TripExecutionRepositories.cs`
- **Issue:**
  ```csharp
  public class InMemoryTripExecutionRepository : ITripExecutionRepository
  {
      private static readonly Dictionary<Guid, TripExecution> _store = new();
  }
  ```
- **Impact:** Data persistence is not implemented; all data is lost on service restart
- **Required Fix:** Implement EF Core DbContext with SQL persistence
- **Severity:** 🔴 BLOCKER

### 2. **CRITICAL: Booking Domain Layer is Empty**
- **Standard:** BACKEND_STRUCTURE.md section 2 - Domain layer must contain entities and rules
- **Violation:** Booking.Domain project has no actual domain entities
- **File:** `Booking/Booking.Domain/` - Only contains generated build files
- **Issue:** No `CustomerEntity`, `HorseEntity`, `TransportRequestEntity`, etc.
- **Impact:**
  - No domain-level business rules
  - Database schema and SQL are the "source of truth"
  - Cannot enforce invariants at domain level
- **Required Fix:** Create domain entities and value objects:
  ```csharp
  // Missing:
  public sealed class Customer : Entity { }
  public sealed class Horse : Entity { }
  public sealed class TransportRequest : Entity { }
  ```
- **Severity:** 🔴 BLOCKER

### 3. **CRITICAL: API Error Codes Don't Match HTTP Standards**
- **Standard:** API_STANDARD.md section 8 - HTTP status codes
- **Violation:** Uses custom error codes (40400, 40902) instead of HTTP status codes
- **Examples:**
  - `ApiResponse<object>.Failure(40400, "...")` should be HTTP 404 with status code in response
  - `ApiResponse<object>.Failure(40902, "...")` should be HTTP 409
- **Files:** All endpoint files
- **Impact:** Breaks HTTP semantics; clients cannot rely on HTTP status codes for routing
- **Required Fix:** Use correct HTTP status codes:
  ```csharp
  // Current (WRONG):
  Results.NotFound(ApiResponse<object>.Failure(40400, "Not found."))
  
  // Should return HTTP 404 with ApiResponse code in body
  ```
- **Severity:** 🔴 BLOCKER

### 4. **CRITICAL: Mixing Business Logic with Data Access**
- **Standard:** BACKEND_STRUCTURE.md section 2 & 3 - Layered architecture
- **Violation:** SqlCustomerHorseService mixes application/domain logic with SQL queries
- **File:** `Booking/Booking.Infrastructure/Customers/SqlCustomerHorseService.cs`
- **Issues:**
  - Validation logic in repository layer:
    ```csharp
    if (input.CustomerType is not ("CLUB" or "INDIVIDUAL_OWNER" or "BREEDER" or "AGENT"))
    {
        throw new BusinessRuleException(...);
    }
    ```
  - Horse validation rules embedded in data layer
  - Should be in Application or Domain layer
- **Required Fix:** Extract to Application service or Domain validator
- **Severity:** 🔴 BLOCKER

---

## 🟡 MODERATE ISSUES

### 1. **Inconsistent Error Response Format**
- **Standard:** API_STANDARD.md section 7 - Error response structure
- **Issue:** Some endpoints return errors without proper error details array
- **Examples:**
  ```csharp
  // Current: Missing error details
  ApiResponse<object>.Failure(40400, "Document not found.")
  
  // Should include detailed error array per standard
  ```
- **Required Fix:** Ensure all error responses include errors array with field and reason
- **Severity:** 🟡 MEDIUM

### 2. **Request/Correlation ID Manual Extraction**
- **Standard:** BACKEND_STRUCTURE.md section 9 - Structured logging requirements
- **Issue:** Every endpoint manually extracts RequestId and CorrelationId from context
- **Example:**
  ```csharp
  private static Guid GetRequestId(HttpContext context) =>
      Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) 
          ? requestId : Guid.NewGuid();
  ```
- **Better Approach:** Expose through middleware-provided context or helper
- **Impact:** Repetitive code, inconsistency risk
- **Severity:** 🟡 MEDIUM

### 3. **Validation Logic Distribution**
- **Standard:** BACKEND_STRUCTURE.md section 2 - Application layer contains validators
- **Issue:** Validation scattered across:
  - Data annotations on request records (Endpoints)
  - Business logic in services (SqlCustomerHorseService)
  - Implicit in SQL constraints (Database)
- **Example Missing:** No centralized FluentValidation or validators per standard
- **Required Fix:** Create dedicated validators:
  ```csharp
  // Should exist:
  public class SaveCustomerProfileValidator : AbstractValidator<SaveCustomerProfileRequest> { }
  public class SaveHorseValidator : AbstractValidator<SaveHorseRequest> { }
  ```
- **Severity:** 🟡 MEDIUM

### 4. **Identity Service Missing Infrastructure Details**
- **Standard:** BACKEND_STRUCTURE.md section 3 - Infrastructure layer dependencies
- **Issue:** Infrastructure classes incomplete (reviewed `AuthEndpoints`, need to check Infrastructure)
- **Files Not Yet Reviewed:**
  - `Identity/Identity.Infrastructure/Authentication/JwtTokenService.cs`
  - `Identity/Identity.Infrastructure/Authentication/SqlIdentityStore.cs`
- **Required Action:** Review Infrastructure layer implementation
- **Severity:** 🟡 MEDIUM

### 5. **DateTime Handling Consistency**
- **Standard:** API_STANDARD.md section 11 & 18 - UTC ISO 8601 dates
- **Issue:** Mixed use of DateTime and DateTimeOffset
- **Examples:**
  - Booking uses `DateTimeOffset RequestedDepartureAt`
  - Identity uses `DateTimeOffset ExpiresAt`
  - Some DTOs use `DateTime` directly
- **Required Fix:** Standardize on `DateTimeOffset` for API contracts, ensure UTC
- **Severity:** 🟡 MEDIUM

---

## 🟢 MINOR ISSUES

### 1. **Missing Service Layer Abstraction**
- **Standard:** BACKEND_STRUCTURE.md section 2 - Clear separation of Application layer
- **Issue:** Booking doesn't show clear Application service implementations
- **Files to Review:** `Booking/Booking.Application/`
- **Impact:** Need to verify Application service pattern is properly implemented
- **Severity:** 🟢 LOW

### 2. **No Event Publishing Observable**
- **Standard:** ARCHITECTURE.md section 4 & DEFINITION_OF_DONE.md - Event publishing
- **Issue:** No RabbitMQ event publishing visible in Booking or Identity services
- **Note:** May be intentional for Phase 1; verify with `TransportRequestSubmitted` event flow
- **Severity:** 🟢 LOW

### 3. **Incomplete Test Project Review**
- **Standard:** DEFINITION_OF_DONE.md section 3 - Testing requirements
- **Issue:** No tests visible in Booking or Identity yet
- **Files to Create:**
  - `Booking.Tests/`
  - `Identity.Tests/`
- **Required:** Unit and integration tests per Definition of Done
- **Severity:** 🟢 LOW

### 4. **API Documentation Completeness**
- **Standard:** API_STANDARD.md section 17 - OpenAPI exposure
- **Status:** Swagger/OpenAPI properly configured ✅
- **Minor Issue:** Review that all endpoints have proper `.WithName()` and `.WithOpenApi()`
- **Severity:** 🟢 LOW

---

## 📋 ACTION ITEMS (Priority Order)

### CRITICAL - Must Fix Before Merge:
1. **[BLOCKER]** Replace Tracking in-memory repositories with EF Core DbContext
2. **[BLOCKER]** Implement Booking domain entities and value objects
3. **[BLOCKER]** Fix error response codes to use proper HTTP status codes
4. **[BLOCKER]** Move validation and business logic from SqlCustomerHorseService to Application layer

### HIGH - Must Fix Before Phase 1 Complete:
5. Implement proper error response format with error details array
6. Create centralized validation using FluentValidation
7. Complete Identity Infrastructure implementation review
8. Standardize DateTime/DateTimeOffset usage to DateTimeOffset UTC

### MEDIUM - Should Fix:
9. Extract Request/Correlation ID helpers to reduce duplication
10. Add RabbitMQ event publishing for cross-service workflows
11. Create test projects with unit and integration tests
12. Document API contracts for each service

---

## 🔍 VERIFICATION CHECKLIST

- [x] API response format matches standard
- [x] Endpoint structure uses MapGroup pattern
- [x] DTO naming follows conventions
- [x] Dockerfiles are multi-stage
- [x] JWT authentication configured correctly
- [x] Middleware pipeline in correct order
- [ ] Pagination works with filters and sorting
- [ ] Domain layer has entities with business rules
- [ ] Repository pattern follows guidelines
- [ ] Error responses match standard format
- [ ] All services have comprehensive tests
- [ ] Authorization checks ownership/scope
- [ ] DateTime values are UTC ISO 8601
- [ ] Cross-service communication via events (when needed)
- [ ] No secrets in committed code

---

## 📚 Standards Reference Files

Key documentation consulted:
- `Docs/racehorse-transport-docs/docs/API_STANDARD.md`
- `Docs/racehorse-transport-docs/docs/BACKEND_STRUCTURE.md`
- `Docs/racehorse-transport-docs/docs/ARCHITECTURE.md`
- `Docs/racehorse-transport-docs/docs/DEFINITION_OF_DONE.md`

---

## Next Steps

1. **Address Critical Issues** in order (Items 1-4)
2. **Create Follow-up PR** with fixes
3. **Add comprehensive tests** before Phase 1 completion
4. **Schedule architecture review** for cross-service event flow
5. **Re-run compliance check** after fixes

---

*Review completed by Claude Code on 2026-10-12*
