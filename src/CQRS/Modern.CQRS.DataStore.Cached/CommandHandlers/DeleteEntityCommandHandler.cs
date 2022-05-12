﻿using MediatR;
using Microsoft.Extensions.Logging;
using Modern.Cache.Abstractions;
using Modern.CQRS.DataStore.Abstractions.Commands;
using Modern.CQRS.DataStore.Cached.Abstract;
using Modern.Exceptions;
using Modern.Repositories.Abstractions;

namespace Modern.CQRS.DataStore.Cached.CommandHandlers;

/// <summary>
/// The mediator command handler that deletes the entity in the data store with the given entity id.<br/>
/// This method does NOT query the entity from the data store before deletion
/// </summary>
/// <exception cref="ArgumentNullException">Thrown if provided id is null</exception>
/// <exception cref="InternalErrorException">Thrown if an error occurred while deleting the entity in the data store</exception>
public class DeleteEntityCommandHandler<TEntityDto, TEntityDbo, TId, TRepository> :
    BaseMediatorHandler<TEntityDto, TEntityDbo, TId>,
    IRequestHandler<DeleteEntityCommand<TId>>
    where TEntityDto : class
    where TEntityDbo : class
    where TId : IEquatable<TId>
    where TRepository : class, IModernCrudRepository<TEntityDbo, TId>
{
    private const string HandlerName = nameof(DeleteEntityCommandHandler<TEntityDto, TEntityDbo, TId, TRepository>);

    /// <summary>
    /// The repository instance
    /// </summary>
    protected readonly TRepository Repository;

    /// <summary>
    /// The cache
    /// </summary>
    protected readonly IModernCache<TEntityDto, TId> Cache;

    /// <summary>
    /// The repository instance
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the class
    /// </summary>
    /// <param name="repository">The generic repository</param>
    /// <param name="cache">Cache</param>
    /// <param name="logger">The logger</param>
    public DeleteEntityCommandHandler(TRepository repository, IModernCache<TEntityDto, TId> cache,
        ILogger<DeleteEntityCommandHandler<TEntityDto, TEntityDbo, TId, TRepository>> logger)
    {
        ArgumentNullException.ThrowIfNull(repository, nameof(repository));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        Repository = repository;
        Cache = cache;
        Logger = logger;
    }

    /// <summary>
    /// <inheritdoc cref="IRequestHandler{TRequest,TResponse}.Handle"/>
    /// </summary>
    public async Task<Unit> Handle(DeleteEntityCommand<TId> request, CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));
            ArgumentNullException.ThrowIfNull(request.Id, nameof(request.Id));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} id: {id}", EntityName, HandlerName, request.Id);

            Logger.LogDebug("Deleting {name} entity with id '{id}' in db...", EntityName, request.Id);
            await Repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entity with id {id}", EntityName, request.Id);

            Logger.LogDebug("Deleting {name} entity with id '{id}' from cache...", EntityName, request.Id);
            await Cache.DeleteAsync(request.Id).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entity with id '{id}'", EntityName, request.Id);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to delete a {name} entity by id '{id}': {reason}", EntityName, request.Id, ex.Message);
            throw CreateProperException(ex);
        }
    }
}
