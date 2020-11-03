﻿using System;
using Smartstore.Domain;

namespace Smartstore.Data.Caching
{
    // TODO: (core) Annotate all cacheable entities with CacheableEntityAttribute.

    /// <summary>
    /// Marks a <see cref="BaseEntity"/> type as cacheable by the database 2nd level caching framework.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CacheableEntityAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether the entity should NEVER be cached, under no circumstances
        /// (event when caching was enabled on specific query level).
        /// Set this to <c>true</c> for 'toxic' entity types that are extremely volatile.
        /// </summary>
        public bool NeverCache { get; set; }

        /// <summary>
        /// Specifies a max rows limit. Query results with more items than the given number will not be cached.
        /// </summary>
        public int MaxRows { get; set; }

        /// <summary>
        /// Gets or sets the expiration timeout in minutes. Default value is 1440 (1 day).
        /// </summary>
        public int Expiry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the query result should be inserted to the request scoped cache also.
        /// Getting items from request cache does not come with the overhead of materializing the cached data reader.
        /// </summary>
        public bool RequestCaching { set; get; }
    }
}