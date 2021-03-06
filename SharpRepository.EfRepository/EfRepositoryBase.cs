using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using SharpRepository.Repository;
using SharpRepository.Repository.Caching;
using SharpRepository.Repository.FetchStrategies;

namespace SharpRepository.EfRepository
{
    [Obsolete("Please upgrade to EF5 in order to use SharpRepository and Entity Framework")]
    public class EfRepositoryBase<T, TKey> : LinqRepositoryBase<T, TKey> where T : class, new()
    {
        protected IDbSet<T> DbSet { get; private set; }
        protected DbContext Context { get; private set; }

        internal EfRepositoryBase(DbContext dbContext, ICachingStrategy<T, TKey> cachingStrategy = null) : base(cachingStrategy)
        {
            Initialize(dbContext);
        }

        private void Initialize(DbContext dbContext)
        {
            Context = dbContext;
            DbSet = Context.Set<T>();
        }

        protected override void AddItem(T entity)
        {
            if (typeof(TKey) == typeof(Guid) || typeof(TKey) == typeof(string))
            {
                TKey id;
                if (GetPrimaryKey(entity, out id) && Equals(id, default(TKey)))
                {
                    id = GeneratePrimaryKey();
                    SetPrimaryKey(entity, id);
                }
            }
            DbSet.Add(entity);
        }

        protected override void DeleteItem(T entity)
        {
            DbSet.Remove(entity);
        }

        protected override void UpdateItem(T entity)
        {
            // mark this entity as modified, in case it is not currently attached to this context
            Context.Entry(entity).State = EntityState.Modified;
        }

        protected override void SaveChanges()
        {
            Context.SaveChanges();
        }

        protected override IQueryable<T> BaseQuery(IFetchStrategy<T> fetchStrategy = null)
        {
            var query = DbSet.AsQueryable();
            return fetchStrategy == null ? query : fetchStrategy.IncludePaths.Aggregate(query, (current, path) => current.Include(path));
        }

        // we override the implementation fro LinqBaseRepository becausee this is built in and doesn't need to find the key column and do dynamic expressions, etc.
        protected override T GetQuery(TKey key)
        {
            return DbSet.Find(key);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            if (Context == null) return;

            Context.Dispose();
            Context = null;
        }

        private TKey GeneratePrimaryKey()
        {
            if (typeof(TKey) == typeof(Guid))
            {
                return (TKey)Convert.ChangeType(Guid.NewGuid(), typeof(TKey));
            }

            if (typeof(TKey) == typeof(string))
            {
                return (TKey)Convert.ChangeType(Guid.NewGuid().ToString(), typeof(TKey));
            }
            
            throw new InvalidOperationException("Primary key could not be generated. This only works for GUID, Int32 and String.");
        }
    }
}