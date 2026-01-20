using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Session.Loaders;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Minimal fake implementation of IAsyncDocumentSession for unit tests.
/// Only LoadAsync is properly implemented - other methods throw NotImplementedException.
/// This is sufficient for testing StreamingService which only uses LoadAsync.
/// </summary>
public class AsyncDocumentSessionFake : IAsyncDocumentSession
{
    private readonly Dictionary<string, object> _documents = new();

    public void Dispose() { }

    public void StoreDocument<T>(string id, T document) where T : notnull
    {
        _documents[id] = document;
    }

    public void Clear()
    {
        _documents.Clear();
    }

    // Implemented methods - these are the only ones used by StreamingService
    public Task<T?> LoadAsync<T>(string id, CancellationToken token = default)
    {
        if (_documents.TryGetValue(id, out var doc) && doc is T typed)
        {
            return Task.FromResult<T?>(typed);
        }
        return Task.FromResult<T?>(default);
    }

    public Task<Dictionary<string, T>> LoadAsync<T>(IEnumerable<string> ids, CancellationToken token = default)
    {
        var result = new Dictionary<string, T>();
        foreach (var id in ids)
        {
            if (_documents.TryGetValue(id, out var doc) && doc is T typed)
            {
                result[id] = typed;
            }
        }
        return Task.FromResult(result);
    }

    // Basic operations that may be called
    public void Delete<T>(T entity) { }
    public void Delete(string id) { }
    public void Delete(string id, string expectedChangeVector) { }
    public Task SaveChangesAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StoreAsync(object entity, CancellationToken token = default) => Task.CompletedTask;
    public Task StoreAsync(object entity, string? changeVector, string id, CancellationToken token = default) => Task.CompletedTask;
    public Task StoreAsync(object entity, string id, CancellationToken token = default) => Task.CompletedTask;

    // All other interface members throw NotImplementedException
    public IAsyncSessionDocumentCounters CountersFor(string documentId) => throw new NotImplementedException();
    public IAsyncSessionDocumentCounters CountersFor(object entity) => throw new NotImplementedException();
    public IAsyncSessionDocumentTimeSeries TimeSeriesFor(string documentId, string name) => throw new NotImplementedException();
    public IAsyncSessionDocumentTimeSeries TimeSeriesFor(object entity, string name) => throw new NotImplementedException();
    public IAsyncSessionDocumentTypedTimeSeries<TValues> TimeSeriesFor<TValues>(string documentId, string? name = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncSessionDocumentTypedTimeSeries<TValues> TimeSeriesFor<TValues>(object entity, string? name = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncSessionDocumentRollupTypedTimeSeries<TValues> TimeSeriesRollupFor<TValues>(object entity, string policy, string? raw = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncSessionDocumentRollupTypedTimeSeries<TValues> TimeSeriesRollupFor<TValues>(string documentId, string policy, string? raw = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncSessionDocumentIncrementalTimeSeries IncrementalTimeSeriesFor(string documentId, string name) => throw new NotImplementedException();
    public IAsyncSessionDocumentIncrementalTimeSeries IncrementalTimeSeriesFor(object entity, string name) => throw new NotImplementedException();
    public IAsyncSessionDocumentTypedIncrementalTimeSeries<TValues> IncrementalTimeSeriesFor<TValues>(string documentId, string? name = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncSessionDocumentTypedIncrementalTimeSeries<TValues> IncrementalTimeSeriesFor<TValues>(object entity, string? name = null) where TValues : new() => throw new NotImplementedException();
    public IAsyncAdvancedSessionOperations Advanced => throw new NotImplementedException();
    public IRavenQueryable<T> Query<T>(string? indexName = null, string? collectionName = null, bool isMapReduce = false) => throw new NotImplementedException();
    public IRavenQueryable<T> Query<T, TIndexCreator>() where TIndexCreator : AbstractCommonApiForIndexes, new() => throw new NotImplementedException();
    public IAsyncRawDocumentQuery<T> RawQuery<T>(string query) => throw new NotImplementedException();
    public Task<T?> LoadAsync<T>(string id, Action<IIncludeBuilder<T>>? includes, CancellationToken token = default) => throw new NotImplementedException();
    public Task<Dictionary<string, T>> LoadAsync<T>(IEnumerable<string> ids, Action<IIncludeBuilder<T>>? includes, CancellationToken token = default) => throw new NotImplementedException();
    public IAsyncLoaderWithInclude<T> Include<T>(System.Linq.Expressions.Expression<Func<T, string?>>? path) => throw new NotImplementedException();
    public IAsyncLoaderWithInclude<T> Include<T, TInclude>(System.Linq.Expressions.Expression<Func<T, string?>>? path) => throw new NotImplementedException();
    public IAsyncLoaderWithInclude<T> Include<T>(System.Linq.Expressions.Expression<Func<T, IEnumerable<string>?>>? path) => throw new NotImplementedException();
    public IAsyncLoaderWithInclude<T> Include<T, TInclude>(System.Linq.Expressions.Expression<Func<T, IEnumerable<string>?>>? path) => throw new NotImplementedException();
    public IAsyncLoaderWithInclude<object> Include(string path) => throw new NotImplementedException();
}
