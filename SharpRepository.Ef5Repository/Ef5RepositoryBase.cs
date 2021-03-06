using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using SharpRepository.Repository;
using SharpRepository.Repository.Caching;
using SharpRepository.Repository.FetchStrategies;
using SharpRepository.Repository.Helpers;

namespace SharpRepository.Ef5Repository
{
    public class Ef5RepositoryBase<T, TKey> : LinqRepositoryBase<T, TKey> where T : class, new()
    {
        protected IDbSet<T> DbSet { get; private set; }
        protected DbContext Context { get; private set; }

        internal Ef5RepositoryBase(DbContext dbContext, ICachingStrategy<T, TKey> cachingStrategy = null) : base(cachingStrategy)
        {
            Initialize(dbContext, cachingStrategy != null);
        }

        private void Initialize(DbContext dbContext, bool hasCaching)
        {
            Context = dbContext;
            DbSet = Context.Set<T>();

            // this could solve issue #50 where DynamicProxy objects mess up the cache
//            if (hasCaching)
//            {
//                Context.Configuration.ProxyCreationEnabled = false;
//            }
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
        //  this also provides the EF5 first level caching out of the box
        protected override T GetQuery(TKey key)
        {
            return DbSet.Find(key);
        }

        protected override PropertyInfo GetPrimaryKeyPropertyInfo()
        {
            // checks for the Code First KeyAttribute and if not there do the normal checks
            var type = typeof(T);
            var keyType = typeof(TKey);

            return type.GetProperties().FirstOrDefault(x => x.HasAttribute<KeyAttribute>() && x.PropertyType == keyType)
                ?? base.GetPrimaryKeyPropertyInfo();
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