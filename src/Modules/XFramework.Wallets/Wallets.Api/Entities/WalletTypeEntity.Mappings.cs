using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletTypeEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class WalletTypeEntityService
{
    /// <summary>
    /// Maps a CreateWalletTypeEntityRequest to a new WalletTypeEntity.
    /// </summary>
    protected virtual partial WalletTypeEntity MapCreateRequestToEntity(CreateWalletTypeEntityRequest request)
    {
        return new WalletTypeEntity
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Desc = request.Desc,
            Type = request.Type,
            CurrencyTypeId = request.CurrencyTypeId,
            MinTransferRule = request.MinTransferRule,
            MaxTransferRule = request.MaxTransferRule,
            BondBalanceRule = request.BondBalanceRule,
            MaintainingBalanceRule = request.MaintainingBalanceRule,
            SystemReferenceId = request.SystemReferenceId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateWalletTypeEntityRequest to an existing WalletTypeEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletTypeEntityRequest request, WalletTypeEntity entity)
    {
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Desc = request.Desc;
        entity.Type = request.Type;
        entity.CurrencyTypeId = request.CurrencyTypeId;
        entity.MinTransferRule = request.MinTransferRule;
        entity.MaxTransferRule = request.MaxTransferRule;
        entity.BondBalanceRule = request.BondBalanceRule;
        entity.MaintainingBalanceRule = request.MaintainingBalanceRule;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletTypeEntity> ApplyFilters(IQueryable<WalletTypeEntity> query, GetWalletTypeEntityListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(w =>
                w.Code.ToLower().Contains(searchLower) ||
                w.Name.ToLower().Contains(searchLower) ||
                (w.Desc != null && w.Desc.ToLower().Contains(searchLower)));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(w => w.Type == request.Type.Value);
        }

        if (request.CurrencyTypeId.HasValue)
        {
            query = query.Where(w => w.CurrencyTypeId == request.CurrencyTypeId.Value);
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for WalletTypeEntity.
/// Implements bidirectional mapping between Domain.Shared.WalletType and WalletTypeEntity.
/// </summary>
public partial class WalletTypeEntity
{
    /// <summary>
    /// Maps a Domain WalletType to a WalletTypeEntity (VSA wrapper).
    /// </summary>
    public static WalletTypeEntity FromDomain(WalletType domain)
    {
        return new WalletTypeEntity
        {
            Id = domain.Id,
            Code = domain.Code,
            Name = domain.Name,
            Desc = domain.Desc,
            Type = domain.Type,
            CurrencyTypeId = domain.CurrencyTypeId,
            MinTransferRule = domain.MinTransferRule,
            MaxTransferRule = domain.MaxTransferRule,
            BondBalanceRule = domain.BondBalanceRule,
            MaintainingBalanceRule = domain.MaintainingBalanceRule,
            SystemReferenceId = domain.SystemReferenceId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletTypeEntity (VSA wrapper) to a Domain WalletType.
    /// </summary>
    public WalletType ToDomain()
    {
        return new WalletType
        {
            Id = this.Id,
            Code = this.Code,
            Name = this.Name,
            Desc = this.Desc,
            Type = this.Type,
            CurrencyTypeId = this.CurrencyTypeId,
            MinTransferRule = this.MinTransferRule,
            MaxTransferRule = this.MaxTransferRule,
            BondBalanceRule = this.BondBalanceRule,
            MaintainingBalanceRule = this.MaintainingBalanceRule,
            SystemReferenceId = this.SystemReferenceId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}