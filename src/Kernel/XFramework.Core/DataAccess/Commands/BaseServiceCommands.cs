using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Services;

namespace XFramework.Core.DataAccess.Commands;

public static class XCommand
{
    public static Create<TModel> Create<TModel>(TModel model) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Create<TModel>(model);
    }
    
    public static Patch<TModel> Patch<TModel>(TModel model) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Patch<TModel>(model);
    }

    public static Replace<TModel> Replace<TModel>(TModel model) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Replace<TModel>(model);
    }
    
    public static Delete<TModel> Delete<TModel>(TModel model) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Delete<TModel>(model);
    }
}

public interface ICreateHandler<TModel> : ICommandHandler<Create<TModel>, CmdResponse<TModel>>;
public interface IPatchHandler<TModel> : ICommandHandler<Patch<TModel>, CmdResponse<TModel>>;
public interface IReplaceHandler<TModel> : ICommandHandler<Replace<TModel>, CmdResponse<TModel>>;
public interface IDeleteHandler<TModel> : ICommandHandler<Delete<TModel>, CmdResponse>;