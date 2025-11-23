using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore
{
    public static class EntityFrameworkQueryableExtensions
    {
        public static IQueryable<TEntity> Include<TEntity, TProperty>(
            this IQueryable<TEntity> source, 
            Expression<Func<TEntity, TProperty>> navigationPropertyPath) 
            where TEntity : class
        {
            // Mock behavior: just return source
            return source;
        }

        public static IQueryable<TEntity> ThenInclude<TEntity, TPreviousProperty, TProperty>(
            this IQueryable<TEntity> source, 
            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
            where TEntity : class
        {
            return source;
        }

        public static IQueryable<TEntity> AsSplitQuery<TEntity>(
            this IQueryable<TEntity> source)
            where TEntity : class
        {
            return source;
        }
    }
}
