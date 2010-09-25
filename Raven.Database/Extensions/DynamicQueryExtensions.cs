﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Raven.Database;
using Raven.Database.Data;
using System.Threading;
using System.Diagnostics;
using Microsoft.Isam.Esent.Interop;
using Raven.Database.Indexing;
using Raven.Database.Plugins;
using Raven.Database.Json;
using Raven.Database.Extensions;
using System.Security.Cryptography;


namespace Raven.Database.Extensions
{
    public static class DynamicQueryExtensions
    {
        public static QueryResult ExecuteDynamicQuery(this DocumentDatabase database, IndexQuery query)
        {
            // Create the map
            var map = DynamicQueryMapping.Create(query.Query);

            // Get the index name
            string indexName = CreateOrGetDynamicIndexName(database, map);

            // Re-write the query
            string realQuery = query.Query;
            foreach (var mapItem in map.Items)
            {
                realQuery = realQuery.Replace(mapItem.From, mapItem.To);
            }

            // Perform the query until we have some results at least
            QueryResult result = null;
            var sp = Stopwatch.StartNew();
            while (true)
            {
                result = database.Query(indexName,
                   new Raven.Database.Data.IndexQuery()
                   {
                       Cutoff = query.Cutoff,
                       PageSize = query.PageSize,
                       Query = realQuery,
                       Start = query.Start,
                       FieldsToFetch = query.FieldsToFetch,
                       SortedFields = query.SortedFields,
                   });

                if (result.IsStale && result.Results.Count < query.PageSize)
                {
                    if (sp.Elapsed.TotalMilliseconds > 10000)
                    {
                        sp.Stop();
                        break;
                    }

                    Thread.Sleep(100);
                    continue;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private static string CreateOrGetDynamicIndexName(DocumentDatabase database, DynamicQueryMapping map)
        {
            // This isn't sustainable with long dynamic queries
            String combinedFields = String.Join("", 
                map.Items
                .OrderBy(x => x.To)
                .Select(x=>x.To)
                .ToArray());

            // Need to use an appropriate index name based on the fields passed in
            var indexName = combinedFields;

            // Hash the name if it's too long
            if (indexName.Length > 240)
            {
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(indexName));
                    indexName = Encoding.UTF8.GetString(bytes);
                }
            }

            // Using a Temp Prefix so we can identify temporary indexes
            indexName = string.Format("Temp_{0}", indexName);

            var mapping = map.Items
              .Select(x => string.Format("{0} = doc.{1}", x.To, x.From))
              .ToArray();
            
            var indexes = new Dictionary<string, FieldIndexing>();
            foreach(var mapItem in map.Items)
            {
                indexes.Add(mapItem.To, FieldIndexing.NotAnalyzed);
            }

            // Create the definition
            var definition = new IndexDefinition()
            {
                Map = @"from doc in docs select new 
                 { 
                    " + String.Join(",\r\n", mapping) + @"
                 }",
                Indexes =  indexes
            };

            // Store the index - this will check for the present of the index for us
            database.PutIndex(indexName, definition);

            // And return the index name
            return indexName;
        }
    }
}