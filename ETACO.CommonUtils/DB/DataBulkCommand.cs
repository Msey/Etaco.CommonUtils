using System;
using System.Collections.Generic;
using System.Data;

namespace ETACO.CommonUtils
{
    /// <summary> Групповое выполнение sql команд </summary>
    public interface IDataBulkCommand
    {
        IDataBulkCommand Add(DataCommand command);
        List<DataCommand> ExecuteNonQuery(DataAccess oda, int batchSize = 0);
        List<DataCommand> ExecuteNonQuery(DataConnection ot, int batchSize = 0);
        int Count { get; }
    }
    public abstract class DataBulkCommand<T> : IDataBulkCommand where T: DataCommand<T>
    {
        public readonly List<DataCommand> CommandList = new List<DataCommand>();
        public int LastExecutingCommandIndex { get; private set; }
        public event Action<DataCommand> OnCommandExecute;
        public DataBulkCommand<T> Add(string command, params object[] param)
        {
            CommandList.Add(DataCommand<T>.CreateCommand(command, param));
            return this;
        }
        
        public IDataBulkCommand Add(DataCommand command)
        {
            CommandList.Add(command);
            return this;
        }

        public DataBulkCommand<T> Add(DataBulkCommand<T> command)
        {
            return Add(command.CommandList);
        }

        public DataBulkCommand<T> Add(List<DataCommand> commands)
        {
            if (commands != null) commands.ForEach(CommandList.Add);
            return this;
        }
        
        public int Count { get { return CommandList.Count; } }
        
        /// <summary> Добавить команду сохранения изменений DataRow </summary>
        public DataBulkCommand<T> AddSaveCommands(DataRow row, string command, DataRowVersion version, string outKey = "")
        {
            if (row != null && !command.IsEmpty())
            {
                var dataCommand = DataCommand<T>.CreateCommand(command);
                var commandParams = dataCommand.DeriveParameters();
                PropertyCollection prop = null;
                foreach (DataColumn column in row.Table.Columns)
                {
                    if (Array.Exists(commandParams, cmd => column.Caption.Equals(cmd, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        prop = column.ExtendedProperties;
                        if (prop.ContainsKey(outKey) && "out".Equals(prop[outKey]+"", StringComparison.InvariantCultureIgnoreCase))
                            dataCommand.AddOut(column.DataType, column.Caption);
                        else
                            dataCommand.AddIn(row[column, version], column.Caption); 
                    }
                }
                Add(dataCommand);
            }
            return this;
        }

        /// <summary> Добавить команды сохранения изменений DataTable </summary>
        public DataBulkCommand<T> AddSaveCommands(DataTable dataTable, string insert, string update, string delete)
        {
            DataTable dt = null;

            if (!insert.IsEmpty())
            {
                dt = dataTable.GetChanges(DataRowState.Added);
                if (dt != null) foreach (DataRow row in dt.Rows) AddSaveCommands(row, insert, DataRowVersion.Current, "insertdir");
            }

            if (!update.IsEmpty())
            {
                dt = dataTable.GetChanges(DataRowState.Modified);
                if (dt != null) foreach (DataRow row in dt.Rows) AddSaveCommands(row, update, DataRowVersion.Current, "updatedir");
            }

            if (!delete.IsEmpty())
            {
                dt = dataTable.GetChanges(DataRowState.Deleted);
                if (dt != null) foreach (DataRow row in dt.Rows) AddSaveCommands(row, delete, DataRowVersion.Original);
            }
            
            return this;
        }

        /// <summary> Добавить команды сохранения изменений DataTable с учётом иерархической структуры данных </summary>
        public DataBulkCommand<T> AddSaveCommands(DataTable dataTable, string columnId, string columnParentId, string insert, string update, string delete, object startWith = null)
        {
            Action<DataRow> saveDeleted = null;
            Action<DataRow> saveModified = null;
            saveDeleted = (row) =>
            {
                var rows = dataTable.Select("{0} = '{1}'".FormatStr(columnParentId, row[columnId, DataRowVersion.Original]), "", DataViewRowState.ModifiedCurrent | DataViewRowState.ModifiedOriginal | DataViewRowState.Added | DataViewRowState.OriginalRows);
                foreach (var _row in rows) saveDeleted(_row);//вызываем сохранение для дочерних записей в иерархии
                AddSaveCommands(row, delete, DataRowVersion.Original);
            };

            saveModified = (row) => 
            {
                if (row.RowState == DataRowState.Added)         AddSaveCommands(row, insert, DataRowVersion.Current);
                else if (row.RowState == DataRowState.Modified) AddSaveCommands(row, update, DataRowVersion.Current);
                
                foreach (var _row in dataTable.Select("{0} = '{1}'".FormatStr(columnParentId, row[columnId])))                                  saveModified(_row);
                foreach (var _row in dataTable.Select("{0} = '{1}'".FormatStr(columnParentId, row[columnId]), "", DataViewRowState.Deleted))    saveDeleted(_row);
            };
            
            var start = ((startWith == null) || (startWith == DBNull.Value)) ? "{0} is null".FormatStr(columnParentId) : "{0} = '{1}'".FormatStr(columnParentId, startWith);
          
            foreach (var row in dataTable.Select(start))                                saveModified(row);
            foreach (var row in dataTable.Select(start, "", DataViewRowState.Deleted))  saveDeleted(row);

            return this;
        }

        /// <summary> Очистить список sql команд </summary>
        public DataBulkCommand<T> Clear()
        {
            CommandList.Clear();
            LastExecutingCommandIndex = -1;
            return this;
        }

        /// <summary> Выполнить список команд (в одной транзакции) </summary>
        public List<DataCommand> ExecuteNonQuery(DataAccess oda, int batchSize = 0)
        {
            oda.UseTransaction(ot=>ExecuteNonQuery(ot, batchSize));
            return CommandList;
        }

        /// <summary> Выполнить список команд (в одной транзакции) </summary>
        public List<DataCommand> ExecuteNonQuery(DataConnection ot, int batchSize = 0)
        {
            var i = 0;
            LastExecutingCommandIndex = -1;
            foreach (var cmd in CommandList)
            {
                LastExecutingCommandIndex++;
                cmd.ExecuteNonQuery(ot);
                OnCommandExecute?.Invoke(cmd);
                if(batchSize > 0 && i++ > batchSize) { ot.Commit(true); i = 0; }
            }
            return CommandList;
        }
    }
}
