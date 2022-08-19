﻿using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Modern.Data.Paging;
using Modern.Exceptions;
using Modern.Services.DataStore.Abstractions;
using Modern.Services.DataStore.InMemory.Abstractions;
using Modern.Services.DataStore.InMemory.Abstractions.Cache;

namespace Modern.Services.DataStore.InMemory;

/// <summary>
/// Represents a decorator over <see cref="IModernService{TEntityDto,TEntityDbo,TId}"/> entity implementation
/// that adds full InMemory caching of all entities
/// </summary>
/// <typeparam name="TEntityDto">The type of entity returned from the service</typeparam>
/// <typeparam name="TEntityDbo">The type of entity contained in the data store</typeparam>
/// <typeparam name="TId">The type of entity identifier</typeparam>
public class ModernInMemoryService<TEntityDto, TEntityDbo, TId> :
    IModernInMemoryService<TEntityDto, TEntityDbo, TId>
    where TEntityDto : class
    where TEntityDbo : class
    where TId : IEquatable<TId>
{
    private readonly string _entityName = typeof(TEntityDto).Name;
    private readonly string _serviceName = $"{typeof(TEntityDto).Name}Service";

    /// <summary>
    /// The entity service
    /// </summary>
    protected readonly IModernService<TEntityDto, TEntityDbo, TId> Service;

    /// <summary>
    /// The service cache
    /// </summary>
    protected readonly IModernServiceCache<TEntityDto, TId> Cache;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the class
    /// </summary>
    /// <param name="service">The entity service</param>
    /// <param name="cache">The service cache of entities</param>
    /// <param name="logger">The logger</param>
    public ModernInMemoryService(IModernService<TEntityDto, TEntityDbo, TId> service,
        IModernServiceCache<TEntityDto, TId> cache,
        ILogger<ModernInMemoryService<TEntityDto, TEntityDbo, TId>> logger)
    {
        ArgumentNullException.ThrowIfNull(service, nameof(service));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        Service = service;
        Cache = cache;
        Logger = logger;
    }

    /// <summary>
    /// Returns entity id of type <typeparamref name="TId"/>
    /// </summary>
    /// <param name="entityDto">Entity Dto</param>
    /// <returns>Entity id</returns>
    // TODO: use source generators for this
    protected virtual TId GetEntityId(TEntityDto entityDto) => (TId)(entityDto.GetType().GetProperty("Id")?.GetValue(entityDto, null) ?? 0);

    /// <summary>
    /// Returns standardized service exception
    /// </summary>
    /// <param name="ex">Original exception</param>
    /// <returns>Standardized service exception</returns>
    protected virtual Exception CreateProperException(Exception ex)
        => ex switch
        {
            ArgumentException _ => ex,
            EntityConcurrentUpdateException _ => ex,
            EntityAlreadyExistsException _ => ex,
            EntityNotFoundException _ => ex,
            EntityNotModifiedException _ => ex,
            RepositoryErrorException _ => ex,
            TaskCanceledException _ => ex,
            _ => new InternalErrorException(ex.Message, ex)
        };

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.GetByIdAsync"/>
    /// </summary>
    public virtual async Task<TEntityDto> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("{serviceName}.{method} id: {id}", _serviceName, nameof(GetByIdAsync), id);
            }

            var entityDto = await Cache.TryGetByIdAsync(id).ConfigureAwait(false);
            if (entityDto is not null)
            {
                return entityDto;
            }

            entityDto = await Service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            await Cache.AddOrUpdateAsync(GetEntityId(entityDto), entityDto).ConfigureAwait(false);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entity by id '{id}': {reason}", _entityName, id, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.TryGetByIdAsync"/>
    /// </summary>
    public virtual async Task<TEntityDto?> TryGetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("{serviceName}.{method} id: {id}", _serviceName, nameof(TryGetByIdAsync), id);
            }

            var entityDto = await Cache.TryGetByIdAsync(id).ConfigureAwait(false);
            if (entityDto is not null)
            {
                return entityDto;
            }

            entityDto = await Service.TryGetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (entityDto is null)
            {
                return null;
            }

            await Cache.AddOrUpdateAsync(GetEntityId(entityDto), entityDto).ConfigureAwait(false);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entity by id '{id}': {reason}", _entityName, id, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.GetAllAsync"/>
    /// </summary>
    public virtual async Task<List<TEntityDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(GetAllAsync));

            return await Cache.GetAllAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get all {name} entities: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.CountAsync(CancellationToken)"/>
    /// </summary>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("{serviceName}.{method} of all entities", _serviceName, nameof(CountAsync));
            }

            return await Cache.CountAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get count of all {name} entities: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.CountAsync(Func{TEntityDto, bool},CancellationToken)"/>
    /// </summary>
    public virtual async Task<long> CountAsync(Func<TEntityDto, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(CountAsync));

            return await Cache.CountAsync(predicate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entities count by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.ExistsAsync"/>
    /// </summary>
    public virtual async Task<bool> ExistsAsync(Func<TEntityDto, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(ExistsAsync));

            return await Cache.ExistsAsync(predicate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not check {name} entity existence by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.FirstOrDefaultAsync"/>
    /// </summary>
    public virtual async Task<TEntityDto?> FirstOrDefaultAsync(Func<TEntityDto, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(FirstOrDefaultAsync));

            return await Cache.FirstOrDefaultAsync(predicate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get first {name} entity by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.SingleOrDefaultAsync"/>
    /// </summary>
    public virtual async Task<TEntityDto?> SingleOrDefaultAsync(Func<TEntityDto, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(SingleOrDefaultAsync));

            return await Cache.SingleOrDefaultAsync(predicate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get single {name} entity by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.WhereAsync(Func{TEntityDto, bool},CancellationToken)"/>
    /// </summary>
    public virtual async Task<List<TEntityDto>> WhereAsync(Func<TEntityDto, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            LogMethod(nameof(WhereAsync));

            return await Cache.WhereAsync(predicate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entities by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto, TEntityDbo,TId}.WhereAsync(Func{TEntityDto, bool},int,int,CancellationToken)"/>
    /// </summary>
    public virtual async Task<PagedResult<TEntityDto>> WhereAsync(Func<TEntityDto, bool> predicate, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            Guard.Against.NegativeOrZero(pageNumber, nameof(pageNumber));
            Guard.Against.NegativeOrZero(pageSize, nameof(pageSize));

            cancellationToken.ThrowIfCancellationRequested();

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("{serviceName}.{method}. Page number: {pageNumber}, page size: {pageSize}", _serviceName, nameof(WhereAsync), pageNumber, pageSize);
            }

            return await Cache.WhereAsync(predicate, pageNumber, pageSize).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entities by the given predicate: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.AsQueryable"/>
    /// </summary>
    public virtual IEnumerable<TEntityDto> AsEnumerable()
    {
        try
        {
            LogMethod(nameof(AsEnumerable));

            return Cache.AsEnumerable();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entities as Enumerable: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernQueryInMemoryService{TEntityDto,TEntityDbo,TId}.AsQueryable"/>
    /// </summary>
    public virtual IQueryable<TEntityDbo> AsQueryable()
    {
        try
        {
            LogMethod(nameof(AsQueryable));

            return Service.AsQueryable();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not get {name} entities as Queryable: {reason}", _entityName, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.CreateAsync(TEntityDto,CancellationToken)"/>
    /// </summary>
    public virtual async Task<TEntityDto> CreateAsync(TEntityDto entity, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity, nameof(entity));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} entity: {@entity}", _serviceName, nameof(CreateAsync), entity);

            Logger.LogDebug("Creating {name} entity in db...", _entityName);
            var entityDto = await Service.CreateAsync(entity, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Created {name} entity. {@entityDbo}", _entityName, entityDto);

            var entityId = GetEntityId(entityDto);

            Logger.LogDebug("Creating {name} entity with id '{id}' in cache...", _entityName, entityId);
            await Cache.AddOrUpdateAsync(entityId, entityDto).ConfigureAwait(false);
            Logger.LogDebug("Created {name} entity with id '{id}'. {@entityDto}", _entityName, entityId, entityDto);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to create a new {name} entity: {reason}. {@entity}", _entityName, ex.Message, entity);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.CreateAsync(List{TEntityDto},CancellationToken)"/>
    /// </summary>
    public virtual async Task<List<TEntityDto>> CreateAsync(List<TEntityDto> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entities, nameof(entities));
            Guard.Against.NegativeOrZero(entities.Count, nameof(entities));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} entities: {@entities}", _serviceName, nameof(CreateAsync), entities);

            Logger.LogDebug("Creating {name} entities in db...", _entityName);
            var entitiesDto = await Service.CreateAsync(entities, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Created {name} entities. {@entitiesDbo}", _entityName, entitiesDto);

            var dictionary = entitiesDto.ToDictionary(entity => GetEntityId(entity), entity => entity);

            Logger.LogDebug("Creating {name} entities in cache...", _entityName);
            await Cache.AddOrUpdateAsync(dictionary).ConfigureAwait(false);
            Logger.LogDebug("Created {name} entities. {@entityDto}", _entityName, entitiesDto);

            return entitiesDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to create new {name} entities: {reason}. {@entities}", _entityName, ex.Message, entities);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.UpdateAsync(TId,TEntityDto,CancellationToken)"/>
    /// </summary>
    public virtual async Task<TEntityDto> UpdateAsync(TId id, TEntityDto entity, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(entity, nameof(entity));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} id: {id}, entity: {@entity}", _serviceName, nameof(UpdateAsync), id, entity);

            Logger.LogDebug("Updating {name} entity with id '{id}' in db...", _entityName, id);
            var entityDto = await Service.UpdateAsync(id, entity, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Updated {name} entity with id {id}. {@entityDbo}", _entityName, id, entityDto);

            var entityId = GetEntityId(entityDto);

            Logger.LogDebug("Updating {name} entity with id '{id}' in cache...", _entityName, entityId);
            await Cache.AddOrUpdateAsync(entityId, entityDto).ConfigureAwait(false);
            Logger.LogDebug("Updated {name} entity with id '{id}'. {@entityDto}", _entityName, entityId, entityDto);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to update a {name} entity by id '{id}': {reason}. {@entity}", _entityName, id, ex.Message, entity);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.UpdateAsync(List{TEntityDto},CancellationToken)"/>
    /// </summary>
    public virtual async Task<List<TEntityDto>> UpdateAsync(List<TEntityDto> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entities, nameof(entities));
            Guard.Against.NegativeOrZero(entities.Count, nameof(entities));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} entities: {@entities}", _serviceName, nameof(UpdateAsync), entities);

            Logger.LogDebug("Updating entity in db...");
            var entitiesDto = await Service.UpdateAsync(entities, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Updated {name} entities. {@entitiesDbo}", _entityName, entitiesDto);

            var dictionary = entitiesDto.ToDictionary(entity => GetEntityId(entity), entity => entity);

            Logger.LogDebug("Updating {name} entities from cache with ids: {@ids}...", _entityName, dictionary);
            await Cache.AddOrUpdateAsync(dictionary).ConfigureAwait(false);
            Logger.LogDebug("Update {name} entities", _entityName);

            return entitiesDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to update {name} entities: {reason}. {@entities}", _entityName, ex.Message, entities);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.UpdateAsync(TId,Action{TEntityDto},CancellationToken)"/>
    /// </summary>
    public virtual async Task<TEntityDto> UpdateAsync(TId id, Action<TEntityDto> update, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(update, nameof(update));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} id: {id}", _serviceName, nameof(UpdateAsync), id);

            var entityDto = await Cache.GetByIdAsync(id).ConfigureAwait(false);
            update(entityDto);

            Logger.LogDebug("Updating {name} entity with id '{id}' in db...", _entityName, id);
            entityDto = await Service.UpdateAsync(id, entityDto, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Updated {name} entity with id {id}. {@entityDbo}", _entityName, id, entityDto);

            Logger.LogDebug("Updating {name} entity with id '{id}' in cache...", _entityName, id);
            await Cache.AddOrUpdateAsync(id, entityDto).ConfigureAwait(false);
            Logger.LogDebug("Updated {name} entity with id '{id}'. {@entityDto}", _entityName, id, entityDto);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to update a {name} entity by id '{id}': {reason}", _entityName, id, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.DeleteAsync(TId,CancellationToken)"/>
    /// </summary>
    public virtual async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} id: {id}", _serviceName, nameof(DeleteAsync), id);
            Logger.LogDebug("Deleting {name} entity with id '{id}' in db...", _entityName, id);

            var result = await Service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            if (!result)
            {
                Logger.LogDebug("{name} entity with id {id} was not found for deletion", _entityName, id);
                return result;
            }

            Logger.LogDebug("Deleted {name} entity with id {id}", _entityName, id);

            Logger.LogDebug("Deleting {name} entity with id '{id}' from cache...", _entityName, id);
            await Cache.DeleteAsync(id).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entity with id '{id}'", _entityName, id);

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to delete a {name} entity by id '{id}': {reason}", _entityName, id, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.DeleteAsync(List{TId},CancellationToken)"/>
    /// </summary>
    public virtual async Task<bool> DeleteAsync(List<TId> ids, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(ids, nameof(ids));
            Guard.Against.NegativeOrZero(ids.Count, nameof(ids));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} ids: {@ids}", _serviceName, nameof(DeleteAsync), ids);
            Logger.LogDebug("Updating {name} entities in db...", _entityName);

            var result = await Service.DeleteAsync(ids, cancellationToken).ConfigureAwait(false);
            if (!result)
            {
                Logger.LogDebug("Not all {name} entities with ids: {@ids} were found for deletion", _entityName, ids);
                return result;
            }

            Logger.LogDebug("Deleted {name} entities with ids: {@ids}. Result: {result}", _entityName, ids, result);

            Logger.LogDebug("Deleting {name} entities from cache with ids: {@ids}...", _entityName, ids);
            await Cache.DeleteAsync(ids).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entities", _entityName);

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to delete {name} entities by ids '{@ids}': {reason}", _entityName, ids, ex.Message);
            throw CreateProperException(ex);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IModernCrudInMemoryService{TEntityDto,TId}.DeleteAndReturnAsync"/>
    /// </summary>
    public virtual async Task<TEntityDto> DeleteAndReturnAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogTrace("{serviceName}.{method} id: {id}", _serviceName, nameof(DeleteAndReturnAsync), id);

            // Check if entity exists in cache. If not - exception will be thrown.
            Logger.LogDebug("Deleting {name} entity with id '{id}' in db...", _entityName, id);
            var entityDto = await Service.DeleteAndReturnAsync(id, cancellationToken).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entity with id {id}. {@entityDbo}", _entityName, id, entityDto);

            Logger.LogDebug("Deleting {name} entity with id '{id}' from cache...", _entityName, id);
            await Cache.DeleteAsync(id).ConfigureAwait(false);
            Logger.LogDebug("Deleted {name} entity with id '{id}'", _entityName, id);

            return entityDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to delete a {name} entity by id '{id}': {reason}", _entityName, id, ex.Message);
            throw CreateProperException(ex);
        }
    }

    private void LogMethod(string methodName)
    {
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("{serviceName}.{method}", _serviceName, methodName);
        }
    }
}