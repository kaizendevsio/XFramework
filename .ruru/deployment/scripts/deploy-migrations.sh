#!/bin/bash
###############################################################################
# XFramework Database Migration Deployment Script
#
# This script handles database migrations for XFramework VSA deployment
#
# Usage:
#   ./deploy-migrations.sh <environment> [options]
#
# Environments: dev, staging, production
#
# Options:
#   --dry-run         Show what would be executed without making changes
#   --rollback        Rollback to previous migration
#   --target <name>   Migrate to specific migration
#   --force           Force migration even if checks fail
#
# Examples:
#   ./deploy-migrations.sh staging
#   ./deploy-migrations.sh production --dry-run
#   ./deploy-migrations.sh production --rollback
###############################################################################

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MIGRATIONS_DIR="$PROJECT_ROOT/src/Kernel/XFramework.Domain/Migrations"

# Default values
ENVIRONMENT=""
DRY_RUN=false
ROLLBACK=false
TARGET_MIGRATION=""
FORCE=false

# Parse arguments
parse_args() {
    if [ $# -eq 0 ]; then
        echo -e "${RED}Error: Environment is required${NC}"
        echo "Usage: $0 <environment> [options]"
        exit 1
    fi

    ENVIRONMENT=$1
    shift

    while [[ $# -gt 0 ]]; do
        case $1 in
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --rollback)
                ROLLBACK=true
                shift
                ;;
            --target)
                TARGET_MIGRATION="$2"
                shift 2
                ;;
            --force)
                FORCE=true
                shift
                ;;
            *)
                echo -e "${RED}Unknown option: $1${NC}"
                exit 1
                ;;
        esac
    done

    # Validate environment
    if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
        echo -e "${RED}Error: Invalid environment '$ENVIRONMENT'${NC}"
        echo "Valid environments: dev, staging, production"
        exit 1
    fi
}

# Log function
log() {
    local level=$1
    shift
    local message="$@"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    case $level in
        INFO)
            echo -e "${BLUE}[INFO]${NC} $timestamp - $message"
            ;;
        SUCCESS)
            echo -e "${GREEN}[SUCCESS]${NC} $timestamp - $message"
            ;;
        WARN)
            echo -e "${YELLOW}[WARN]${NC} $timestamp - $message"
            ;;
        ERROR)
            echo -e "${RED}[ERROR]${NC} $timestamp - $message"
            ;;
    esac
}

# Check prerequisites
check_prerequisites() {
    log INFO "Checking prerequisites..."
    
    # Check if dotnet CLI is installed
    if ! command -v dotnet &> /dev/null; then
        log ERROR "dotnet CLI is not installed"
        exit 1
    fi
    
    # Check if ef tool is installed
    if ! dotnet ef &> /dev/null; then
        log ERROR "Entity Framework Core tools are not installed"
        log INFO "Install with: dotnet tool install --global dotnet-ef"
        exit 1
    fi
    
    # Check if migrations directory exists
    if [ ! -d "$MIGRATIONS_DIR" ]; then
        log ERROR "Migrations directory not found: $MIGRATIONS_DIR"
        exit 1
    fi
    
    log SUCCESS "All prerequisites satisfied"
}

# Load configuration
load_config() {
    log INFO "Loading configuration for environment: $ENVIRONMENT"
    
    local config_file="$PROJECT_ROOT/src/Kernel/XFramework.Core/appsettings.$ENVIRONMENT.json"
    
    if [ ! -f "$config_file" ]; then
        log ERROR "Configuration file not found: $config_file"
        exit 1
    fi
    
    # Export connection string from environment or config
    if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
        log WARN "ConnectionStrings__DefaultConnection not set in environment"
        log INFO "Please set the connection string environment variable"
        exit 1
    fi
    
    log SUCCESS "Configuration loaded"
}

# Backup database
backup_database() {
    if [ "$ENVIRONMENT" = "production" ] && [ "$DRY_RUN" = false ]; then
        log INFO "Creating database backup..."
        
        local backup_name="xframework_backup_$(date '+%Y%m%d_%H%M%S').sql"
        local backup_path="$PROJECT_ROOT/backups/$backup_name"
        
        mkdir -p "$PROJECT_ROOT/backups"
        
        # Use pg_dump for PostgreSQL (adjust for your database)
        # This is a placeholder - adjust connection details as needed
        if command -v pg_dump &> /dev/null; then
            pg_dump "$ConnectionStrings__DefaultConnection" > "$backup_path" 2>&1
            
            if [ $? -eq 0 ]; then
                log SUCCESS "Database backed up to: $backup_path"
            else
                log ERROR "Database backup failed"
                if [ "$FORCE" = false ]; then
                    exit 1
                fi
                log WARN "Continuing despite backup failure (--force enabled)"
            fi
        else
            log WARN "pg_dump not found, skipping database backup"
            if [ "$FORCE" = false ]; then
                log ERROR "Cannot proceed without backup in production"
                exit 1
            fi
        fi
    else
        log INFO "Skipping database backup (environment: $ENVIRONMENT, dry-run: $DRY_RUN)"
    fi
}

# List pending migrations
list_pending_migrations() {
    log INFO "Checking for pending migrations..."
    
    cd "$PROJECT_ROOT/src/Kernel/XFramework.Domain"
    
    local pending=$(dotnet ef migrations list --no-build 2>&1 | grep -A 100 "Pending:" || true)
    
    if [ -z "$pending" ]; then
        log INFO "No pending migrations"
        return 0
    else
        log INFO "Pending migrations:"
        echo "$pending"
        return 1
    fi
}

# Apply migrations
apply_migrations() {
    log INFO "Applying database migrations..."
    
    cd "$PROJECT_ROOT/src/Kernel/XFramework.Domain"
    
    local cmd="dotnet ef database update"
    
    if [ -n "$TARGET_MIGRATION" ]; then
        cmd="$cmd $TARGET_MIGRATION"
        log INFO "Targeting specific migration: $TARGET_MIGRATION"
    fi
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "DRY RUN - Would execute:"
        log INFO "$cmd"
        
        # Generate SQL script to show what would be executed
        dotnet ef migrations script --idempotent --output "$PROJECT_ROOT/migration-preview.sql"
        log INFO "Migration SQL script generated: $PROJECT_ROOT/migration-preview.sql"
    else
        log INFO "Executing: $cmd"
        
        if $cmd; then
            log SUCCESS "Migrations applied successfully"
        else
            log ERROR "Migration failed"
            exit 1
        fi
    fi
}

# Rollback migration
rollback_migration() {
    log WARN "Rolling back to previous migration..."
    
    cd "$PROJECT_ROOT/src/Kernel/XFramework.Domain"
    
    # Get the previous migration name
    local prev_migration=$(dotnet ef migrations list --no-build 2>&1 | grep -B 1 "(Applied)" | tail -2 | head -1 | awk '{print $1}')
    
    if [ -z "$prev_migration" ]; then
        log ERROR "Could not determine previous migration"
        exit 1
    fi
    
    log INFO "Rolling back to: $prev_migration"
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "DRY RUN - Would rollback to: $prev_migration"
    else
        if dotnet ef database update "$prev_migration"; then
            log SUCCESS "Rollback completed successfully"
        else
            log ERROR "Rollback failed"
            exit 1
        fi
    fi
}

# Verify migrations
verify_migrations() {
    log INFO "Verifying migrations..."
    
    cd "$PROJECT_ROOT/src/Kernel/XFramework.Domain"
    
    # Check database connection
    if dotnet ef dbcontext info --no-build &> /dev/null; then
        log SUCCESS "Database connection verified"
    else
        log ERROR "Database connection failed"
        exit 1
    fi
    
    # Check for pending migrations
    if list_pending_migrations; then
        log SUCCESS "All migrations applied"
    else
        log WARN "There are pending migrations"
    fi
}

# Main execution
main() {
    echo ""
    log INFO "=================================="
    log INFO "XFramework Migration Deployment"
    log INFO "=================================="
    echo ""
    
    parse_args "$@"
    
    log INFO "Environment: $ENVIRONMENT"
    log INFO "Dry Run: $DRY_RUN"
    log INFO "Rollback: $ROLLBACK"
    [ -n "$TARGET_MIGRATION" ] && log INFO "Target: $TARGET_MIGRATION"
    [ "$FORCE" = true ] && log WARN "Force mode enabled"
    echo ""
    
    check_prerequisites
    load_config
    
    if [ "$ROLLBACK" = true ]; then
        backup_database
        rollback_migration
    else
        backup_database
        apply_migrations
        verify_migrations
    fi
    
    echo ""
    log SUCCESS "Migration deployment completed successfully!"
    echo ""
}

# Run main function
main "$@"