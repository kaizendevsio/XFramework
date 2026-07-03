# XFramework - Comprehensive Knowledge Base

> **Status:** Superseded legacy knowledge-base snapshot. This file is retained for historical context only and must not be treated as the current source of truth.
> **Current guidance:** Use `docs/README.md`, `docs/solutions/README.md`, `docs/solutions/conventions/xframework-best-practices.md`, `docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md`, and `docs/solutions/conventions/xframework-feature-surface-map.md`.
> **Stale terminology:** References below to .NET 9/C#13, CQRS/MediatR, SignalR/StreamFlow, or older logging assumptions are historical/superseded unless a current solution doc confirms them.

## Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture & Design Patterns](#architecture--design-patterns)
3. [Technology Stack](#technology-stack)
4. [Project Structure](#project-structure)
5. [Core Framework Components](#core-framework-components)
6. [Business Modules](#business-modules)
7. [Infrastructure Services](#infrastructure-services)
8. [Data Layer](#data-layer)
9. [Authentication & Security](#authentication--security)
10. [API Design](#api-design)
11. [Configuration](#configuration)
12. [Development Guidelines](#development-guidelines)
13. [Deployment](#deployment)

---

## Project Overview

XFramework is a modern, enterprise-grade .NET 9.0 framework designed for building scalable, multi-tenant applications using Clean Architecture principles. The framework provides a comprehensive foundation for developing microservices-based applications with built-in support for CQRS, authentication, caching, real-time communication, and multi-tenancy.

### Key Features
- **Clean Architecture**: Layered architecture with clear separation of concerns
- **Domain-Driven Design (DDD)**: Business-focused domain modeling
- **CQRS Pattern**: Command Query Responsibility Segregation using MediatR
- **Multi-tenancy**: Built-in tenant isolation and management
- **Microservices Ready**: Modular design with independent business modules
- **Real-time Communication**: SignalR integration for live updates
- **Advanced Caching**: Memory-based caching with invalidation strategies
- **JWT Authentication**: Secure token-based authentication
- **API Generation**: Automatic minimal API generation
- **Audit Trail**: Comprehensive entity tracking and soft delete

---

## Architecture & Design Patterns

### Clean Architecture Layers

```
â”Œ-----------------------------------------â”
â”‚              Presentation               â”‚
â”‚     (Portal, Gateway, Fluid)      â”‚
â”œ-----------------------------------------â”¤
â”‚               Modules                   â”‚
â”‚  (Identity, Community, Wallets, etc.)   â”‚
â”œ-----------------------------------------â”¤
â”‚               Kernel                    â”‚
â”‚         (Core, Domain)                  â”‚
â”œ-----------------------------------------â”¤
â”‚            Infrastructure               â”‚
â”‚         (Integration Services)          â”‚
â”œ-----------------------------------------â”¤
â”‚               Shared                    â”‚
â”‚        (Domain Contracts)               â”‚
â””-----------------------------------------â”˜
```

### Core Design Patterns

1. **CQRS (Command Query Responsibility Segregation)**
   - Separate models for reading and writing
   - MediatR for request/response handling
   - Command and Query handlers

2. **Repository Pattern**
   - Generic CRUD operations
   - Abstracted data access layer

3. **Unit of Work Pattern**
   - Transactional consistency
   - Entity Framework contexts

4. **Dependency Injection**
   - Constructor injection throughout
   - Service registration patterns

5. **Domain Events**
   - Event-driven architecture
   - Decoupled business logic

---

## Technology Stack

### Core Technologies
- **.NET 9.0**: Latest .NET framework with C# 13
- **Entity Framework Core 9.0**: ORM for data access
- **PostgreSQL**: Primary database (via Npgsql)
- **MediatR**: CQRS implementation
- **JWT Bearer**: Authentication mechanism
- **SignalR**: Real-time communication
- **MemoryPack**: High-performance serialization

### Additional Libraries
- **Serilog**: Structured logging with SEQ integration
- **Swashbuckle**: OpenAPI/Swagger documentation
- **WebOptimizer**: Asset optimization
- **OData**: Query capabilities
- **Asp.Versioning**: API versioning

### Development Tools
- **Docker**: Containerization support
- **Azure DevOps**: CI/CD pipelines
- **JetBrains Rider**: Primary IDE support

---

## Project Structure

```
XFramework/
â”œ-- src/
â”‚   â”œ-- Infrastructure/
â”‚   â”‚   â””-- XFramework.Integration/
â”‚   â”œ-- Kernel/
â”‚   â”‚   â”œ-- XFramework.Core/
â”‚   â”‚   â””-- XFramework.Domain/
â”‚   â”œ-- Modules/
â”‚   â”‚   â”œ-- XFramework.Blazor/
â”‚   â”‚   â”œ-- XFramework.Coins/
â”‚   â”‚   â”œ-- XFramework.Community/
â”‚   â”‚   â”œ-- XFramework.IdentityServer/
â”‚   â”‚   â”œ-- XFramework.Inventario/
â”‚   â”‚   â”œ-- XFramework.Communications/
â”‚   â”‚   â”œ-- XFramework.PaymentGateways/
â”‚   â”‚   â”œ-- XFramework.Payments/
â”‚   â”‚   â”œ-- XFramework.SmsGateway/
â”‚   â”‚   â”œ-- XFramework.StreamFlow/
â”‚   â”‚   â””-- XFramework.Wallets/
â”‚   â”œ-- Presentation/
â”‚   â”‚   â”œ-- Portal/
â”‚   â”‚   â”œ-- Fluid/
â”‚   â”‚   â””-- Gateway/
â”‚   â”œ-- Shared/
â”‚   â”‚   â””-- XFramework.Domain.Shared/
â”‚   â””-- Tests/
â”œ-- docs/
â””-- tools/
```

### Layer Responsibilities

#### Infrastructure (`XFramework.Integration`)
- Cross-cutting concerns
- External service integrations
- Caching mechanisms
- JWT token management
- SignalR hub management

#### Kernel
- **Core**: Framework foundations, CQRS implementation, API generation
- **Domain**: Entity Framework contexts, data models, migrations

#### Modules
Independent business domains with their own:
- APIs
- Business logic
- Domain models
- Integration services

#### Presentation
Frontend applications and gateways:
- **Portal**: Administrative interface
- **Gateway**: API gateway and routing
- **Fluid**: Dynamic content management

#### Shared
Common contracts and domain objects used across modules

---

## Core Framework Components

### XApplication Builder

The framework's entry point providing fluent API for application configuration:

```csharp
XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<TBaseRequest>()
    .Run();
```

### Base Command Operations

Generic CRUD commands using the `XCommand` factory:

```csharp
// Command creation patterns
XCommand.Create<TModel>(model)   // Create new entity
XCommand.Patch<TModel>(model)    // Partial update
XCommand.Replace<TModel>(model)  // Full replacement
XCommand.Delete<TModel>(model)   // Soft delete
```

### Entity Constraints

All entities must implement core interfaces:
- `IHasId`: Unique identifier
- `IAuditable`: Creation and modification tracking
- `IHasConcurrencyStamp`: Optimistic concurrency
- `ISoftDeletable`: Soft delete support
- `IHasTenantId`: Multi-tenant isolation

---

## Business Modules

### Module Architecture

Each module follows a consistent structure:
```
ModuleName/
â”œ-- ModuleName.Api/           # Web API endpoints
â”œ-- ModuleName.Core/          # Business logic
â”œ-- ModuleName.Domain.Shared/ # Contracts and DTOs
â””-- ModuleName.Integration/   # External integrations
```

### Available Modules

1. **XFramework.IdentityServer**
   - User authentication and authorization
   - OAuth2/OpenID Connect implementation
   - Role and permission management

2. **XFramework.Community**
   - Social features and user interactions
   - Content management
   - Community moderation

3. **XFramework.Communications**
   - Internal communications system
   - Notification management
   - Communication workflows

4. **XFramework.Wallets**
   - Digital wallet management
   - Transaction processing
   - Balance tracking

5. **XFramework.Coins**
   - Cryptocurrency integration
   - Trading functionalities
   - Market data management

6. **XFramework.PaymentGateways**
   - Payment processing integration
   - Multiple gateway support
   - Transaction validation

7. **XFramework.SmsGateway**
   - SMS service integration
   - Message templating
   - Delivery tracking

8. **XFramework.StreamFlow**
   - Data streaming capabilities
   - Real-time data processing
   - Stream analytics

9. **XFramework.Inventario**
   - Inventory management
   - Stock tracking
   - Asset management

10. **XFramework.Blazor**
    - Server-side Blazor components
    - Interactive UI elements
    - Component library

---

## Infrastructure Services

### JWT Service (`IJwtService`)

Token-based authentication management:
- Token generation and validation
- Refresh token handling
- Claims-based authorization
- Multi-tenant token isolation

### Cache Manager (`ICacheManager`)

High-performance caching system:
- Memory-based caching
- Cache invalidation strategies
- Distributed caching support
- Performance optimization

### SignalR Service (`ISignalRService`)

Real-time communication:
- Hub management
- Client group management
- Message broadcasting
- Connection lifecycle

### CRUD Service (`ICrudService<T>`)

Generic data access operations:
- Standard CRUD operations
- Query optimization
- Pagination support
- Filtering and sorting

---

## Data Layer

### XDbContext

Enhanced Entity Framework context with:

#### Automatic Query Filters
```csharp
// Automatic soft delete filtering
modelBuilder.Entity(clrType).HasQueryFilter(
    p => p.IsDeleted == false && p.IsEnabled == true
);
```

#### Audit Trail Management
- Automatic `CreatedAt` timestamp on insert
- Automatic `ModifiedAt` timestamp on update
- Soft delete with `DeletedAt` timestamp
- Concurrency stamp management

#### Multi-tenancy Enforcement
- Mandatory `TenantId` validation
- Automatic tenant isolation
- Cross-tenant data protection

### BaseModel Properties

```csharp
public abstract class BaseModel
{
    [MemoryPackOrder(100)] public Guid Id { get; set; }
    [MemoryPackOrder(101)] public bool IsEnabled { get; set; }
    [MemoryPackOrder(102)] public bool IsDeleted { get; set; }
    [MemoryPackOrder(103)] public Guid ConcurrencyStamp { get; set; }
    [MemoryPackOrder(104)] public DateTime CreatedAt { get; set; }
    [MemoryPackOrder(105)] public DateTime? ModifiedAt { get; set; }
    [MemoryPackOrder(106)] public DateTime? DeletedAt { get; set; }
    [MemoryPackOrder(107)] public Guid TenantId { get; set; }
}
```

---

## Authentication & Security

### JWT Configuration

- Bearer token authentication
- OpenID Connect integration
- Role-based access control
- Multi-tenant token isolation

### Security Features

- HTTPS enforcement
- CORS policy management
- Request validation
- SQL injection prevention
- XSS protection

### Authorization Patterns

- Claims-based authorization
- Policy-based access control
- Resource-based permissions
- Hierarchical role management

---

## API Design

### Minimal API Generation

Automatic API endpoint generation with:
- RESTful conventions
- OpenAPI documentation
- Versioning support
- Request/response validation

### API Patterns

```csharp
// Standard CRUD endpoints
GET    /api/v1/{resource}           # List with pagination
GET    /api/v1/{resource}/{id}     # Get by ID
POST   /api/v1/{resource}          # Create new
PUT    /api/v1/{resource}/{id}     # Full update
PATCH  /api/v1/{resource}/{id}     # Partial update
DELETE /api/v1/{resource}/{id}     # Soft delete
```

### Request/Response Standards

- Consistent error responses
- Standardized pagination
- Uniform data validation
- Comprehensive logging

---

## Configuration

### Environment-Specific Settings

- Development configurations
- Staging environment setup
- Production optimizations
- Docker containerization

### Key Configuration Areas

1. **Database Connections**
   - PostgreSQL connection strings
   - Connection pooling
   - Migration settings

2. **Authentication**
   - JWT secret keys
   - Token expiration
   - Refresh token settings

3. **Caching**
   - Memory limits
   - Expiration policies
   - Invalidation strategies

4. **Logging**
   - Serilog configuration
   - SEQ integration
   - Log levels

---

## Development Guidelines

### Code Standards

1. **C# 13 Features**
   - Use latest language features
   - Nullable reference types enabled
   - Treat warnings as errors

2. **Architecture Principles**
   - Follow Clean Architecture
   - Maintain separation of concerns
   - Implement SOLID principles

3. **Testing Strategy**
   - Unit tests for business logic
   - Integration tests for APIs
   - Performance testing

### Project Dependencies

#### Infrastructure Layer
- Asp.Versioning.Http (8.1.0)
- Microsoft.AspNetCore.Authentication.JwtBearer (9.0.0)
- Microsoft.AspNetCore.OData (9.1.1)
- Serilog.AspNetCore (9.0.0)
- Swashbuckle.AspNetCore (7.2.0)

#### Domain Layer
- Microsoft.EntityFrameworkCore (9.0.0)
- Npgsql.EntityFrameworkCore.PostgreSQL (9.0.2)
- Microsoft.EntityFrameworkCore.Proxies (9.0.0)

---

## Deployment

### CI/CD Pipeline

Azure DevOps pipelines with:
- Automated testing
- Docker image building
- Multi-environment deployment
- App Service deployment

### Deployment Targets

1. **Azure App Services**
   - .NET Core 9.0 runtime
   - Automatic scaling
   - Health monitoring

2. **On-Premises**
   - Docker containers
   - Kubernetes support
   - Load balancing

### Environment Configuration

- **Development**: Local PostgreSQL, development secrets
- **Staging**: Staging database, limited resources
- **Production**: High availability setup, production secrets

---

## Best Practices

### Performance Optimization

1. **Database**
   - Efficient query patterns
   - Proper indexing
   - Connection pooling

2. **Caching**
   - Strategic cache usage
   - Cache warming
   - Invalidation strategies

3. **API Design**
   - Pagination for large datasets
   - Async/await patterns
   - Resource optimization

### Security Best Practices

1. **Data Protection**
   - Encrypt sensitive data
   - Secure connection strings
   - Regular security audits

2. **Access Control**
   - Principle of least privilege
   - Regular permission reviews
   - Multi-factor authentication

### Monitoring & Logging

1. **Application Monitoring**
   - Health checks
   - Performance metrics
   - Error tracking

2. **Logging Strategy**
   - Structured logging
   - Correlation IDs
   - Centralized log management

---

## Quick Start Guide

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL 12+
- Docker (optional)
- Visual Studio/Rider

### Getting Started

1. **Clone Repository**
   ```bash
   git clone [repository-url]
   cd XFramework
   ```

2. **Database Setup**
   ```bash
   # Update connection string in appsettings.json
   dotnet ef database update
   ```

3. **Run Application**
   ```bash
   dotnet run --project src/Presentation/Gateway/Gateway.Api
   ```

4. **Access APIs**
   - Swagger UI: `https://localhost:5001/swagger`
   - Health Check: `https://localhost:5001/health`

---

## Support & Resources

### Documentation
- API Documentation: Available via Swagger UI
- Database Schema: Auto-generated from Entity Framework
- Architecture Diagrams: In `/docs` folder

### Development Team Contacts
- Architecture Questions: [Architecture Team]
- Database Issues: [Database Team]
- Deployment Support: [DevOps Team]

### Useful Commands

```bash
# Database migrations
dotnet ef migrations add [MigrationName]
dotnet ef database update

# Run tests
dotnet test

# Build solution
dotnet build

# Publish application
dotnet publish -c Release
```

---

*This knowledge base is a living document that should be updated as the framework evolves and new features are added.*
