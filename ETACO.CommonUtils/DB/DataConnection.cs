using System;
using System.Data.Common;
using System.Diagnostics;

namespace ETACO.CommonUtils
{
    /// <summary> Обёртка для DbConnection </summary>
    [DebuggerDisplay("{Connection.ConnectionString}, State={Connection.State}")]
    public class DataConnection : IDisposable
    {
        internal DbConnection Connection { get; private set; }
        internal DbTransaction Transaction { get; private set; }
        
        internal DataConnection(DbConnection connection){ Connection = connection;}
        internal DataConnection(DbTransaction transaction) { Transaction = transaction; Connection = transaction?.Connection;}
        internal void BeginTransaction() { Transaction = Connection?.BeginTransaction();}
        internal void Commit(bool beginTransaction = false) { Transaction?.Commit(); if (beginTransaction) BeginTransaction(); }
        internal void Rollback() { Transaction?.Rollback(); }
        public void Dispose() { Transaction?.Dispose(); Connection?.Dispose(); }
    }
}