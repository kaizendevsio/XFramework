using XFramework.Domain.Shared.Contracts.Requests;

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

public interface ICreateHandler<TModel> : IRequestHandler<Create<TModel>, CmdResponse<TModel>>;
public interface IPatchHandler<TModel> : IRequestHandler<Patch<TModel>, CmdResponse<TModel>>;
public interface IReplaceHandler<TModel> : IRequestHandler<Replace<TModel>, CmdResponse<TModel>>;
public interface IDeleteHandler<TModel> : IRequestHandler<Delete<TModel>, CmdResponse<TModel>>;