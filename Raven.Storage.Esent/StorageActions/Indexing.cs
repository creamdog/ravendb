//-----------------------------------------------------------------------
// <copyright file="Indexing.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Isam.Esent.Interop;
using Raven.Database.Data;
using Raven.Database.Exceptions;
using Raven.Database.Extensions;
using Raven.Database.Storage;

namespace Raven.Storage.Esent.StorageActions
{
	public partial class DocumentStorageActions : IIndexingStorageActions
	{
		public void SetCurrentIndexStatsTo(string index)
		{
			Api.JetSetCurrentIndex(session, IndexesStats, "by_key");
			Api.MakeKey(session, IndexesStats, index, Encoding.Unicode, MakeKeyGrbit.NewKey);
			if (Api.TrySeek(session, IndexesStats, SeekGrbit.SeekEQ) == false)
				throw new IndexDoesNotExistsException("There is no index named: " + index);
		}

		public void IncrementIndexingAttempt()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["attempts"], 1);
		}

		public void IncrementSuccessIndexing()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["successes"], 1);
		}

		public void IncrementIndexingFailure()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["errors"], 1);
		}

		public void DecrementIndexingAttempt()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["attempts"], -1);
		}

		public void DecrementReduceIndexingAttempt()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_attempts"], -1);
		}

		public void IncrementReduceIndexingAttempt()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_attempts"], 1);
		}

		public void IncrementReduceSuccessIndexing()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_successes"], 1);
		}

		public void IncrementReduceIndexingFailure()
		{
			Api.EscrowUpdate(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_errors"], 1);
		}


		public IEnumerable<IndexStats> GetIndexesStats()
		{
			Api.MoveBeforeFirst(session, IndexesStats);
			while (Api.TryMoveNext(session, IndexesStats))
			{
				yield return new IndexStats
				{
					Name = Api.RetrieveColumnAsString(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["key"]),
					IndexingAttempts =
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["attempts"]).Value,
					IndexingSuccesses =
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["successes"]).Value,
					IndexingErrors =
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["errors"]).Value,
					ReduceIndexingAttempts = 
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_attempts"]).Value,
					ReduceIndexingSuccesses =
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_successes"]).Value,
					ReduceIndexingErrors =
						Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_errors"]).Value,
					
					LastIndexedEtag = 
						Api.RetrieveColumn(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["last_indexed_etag"]).TransfromToGuidWithProperSorting(),
					LastIndexedTimestamp= 
						Api.RetrieveColumnAsDateTime(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["last_indexed_timestamp"]).Value,
				};
			}
		}       

		public void AddIndex(string name)
		{
			using (var update = new Update(session, IndexesStats, JET_prep.Insert))
			{
				Api.SetColumn(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["key"], name, Encoding.Unicode);
				Api.SetColumn(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["last_indexed_etag"], Guid.Empty.TransformToValueForEsentSorting());
				Api.SetColumn(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["last_indexed_timestamp"], DateTime.MinValue);
				update.Save();
			}
		}

		public void DeleteIndex(string name)
		{
			Api.JetSetCurrentIndex(session, IndexesStats, "by_key");
			Api.MakeKey(session, IndexesStats, name, Encoding.Unicode, MakeKeyGrbit.NewKey);
			if (Api.TrySeek(session, IndexesStats, SeekGrbit.SeekEQ) == false)
				return;
			Api.JetDelete(session, IndexesStats);
		}

		public IndexFailureInformation GetFailureRate(string index)
		{
			SetCurrentIndexStatsTo(index);
			return new IndexFailureInformation
			{
				Name = index,
				Attempts = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["attempts"]).Value,
				Errors = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["errors"]).Value,
				Successes = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["successes"]).Value,
				ReduceAttempts = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_attempts"]).Value,
				ReduceErrors = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_errors"]).Value,
				ReduceSuccesses = Api.RetrieveColumnAsInt32(session, IndexesStats, tableColumnsCache.IndexesStatsColumns["reduce_successes"]).Value
			};
		}

		public void UpdateLastIndexed(string index, Guid etag, DateTime timestamp)
		{
			SetCurrentIndexStatsTo(index);
			using(var update = new Update(session, IndexesStats, JET_prep.Replace))
			{
				Api.SetColumn(session, IndexesStats,
				              tableColumnsCache.IndexesStatsColumns["last_indexed_etag"],
				              etag.TransformToValueForEsentSorting());
				Api.SetColumn(session, IndexesStats,
							  tableColumnsCache.IndexesStatsColumns["last_indexed_timestamp"],
							  timestamp);
				update.Save();
			}
		}

    }
}
